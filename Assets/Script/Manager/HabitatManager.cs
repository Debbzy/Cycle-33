using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Cycle33.Core;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class HabitatManager : MonoBehaviour
    {
        public static HabitatManager Instance { get; private set; }

        public int WormCount { get; private set; }
        public List<GrassInstance> ActiveGrass { get; private set; } = new List<GrassInstance>();

        private GlobalRulesSO _rules;
        private StageConfigSO _stage;
        private ObjectDataSO _grassObjectData;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize(GlobalRulesSO rules, StageConfigSO stage, ObjectDataSO grassObjectData)
        {
            _rules = rules;
            _stage = stage;
            _grassObjectData = grassObjectData;
            WormCount = 0;
            ActiveGrass.Clear();
        }

        public void AddWorms(int count) => WormCount = Mathf.Max(0, WormCount + count);
        public void ConsumeOneWorm() => WormCount = Mathf.Max(0, WormCount - 1);

        // Langkah 3 Pipeline: HabitatStep (HAB-01 s/d HAB-05, MAT-07)
        public void ExecuteHabitatStep(Cycle33.Core.EventType activeEvent)
        {
            // MAT-07: Reproduksi cacing di HabitatStep
            int multiplier = (activeEvent == Cycle33.Core.EventType.BirthSeason) ? 2 : 1;
            int newWorms = (WormCount / 2) * multiplier;
            WormCount += newWorms;

            // HAB-04: Cek overpopulasi cacing
            int totalCapable = GridService.Instance.GetTotalGrassCapableCells();
            bool isOverpopulated = WormCount > (_rules.wormOverpopRatio * totalCapable);

            // HAB-02: Pertumbuhan rumput per siklus
            int wormBonus = (_rules.wormsPerGrassBonus > 0) ? (WormCount / _rules.wormsPerGrassBonus) : 0;
            int rainBonus = (activeEvent == Cycle33.Core.EventType.Rain) ? _rules.rainGrassBonus : 0;
            int totalToGrow = _stage.grassBasePerCycle + wormBonus + rainBonus;

            List<GridCell> emptyCells = GridService.Instance.GetEmptyGrassCells();
            int spawnCount = Mathf.Min(totalToGrow, emptyCells.Count);

            int currentCycle = CycleManager.Instance != null ? CycleManager.Instance.CurrentCycle : 1;

            for (int i = 0; i < spawnCount; i++)
            {
                int randIndex = SimulationClock.Instance.SeededRng.Next(emptyCells.Count);
                GridCell chosenCell = emptyCells[randIndex];
                emptyCells.RemoveAt(randIndex);

                // HAB-05: Hanya rumput baru yang berpeluang berstatus parasit
                bool isParasite = isOverpopulated &&
                                  (SimulationClock.Instance.SeededRng.NextDouble() < _rules.parasiteChancePerNewGrass);

                SpawnGrass(chosenCell, isParasite, currentCycle);
            }
        }

        private void SpawnGrass(GridCell cell, bool isParasite, int currentCycle)
        {
            Assert.IsNull(cell.Grass, "[INV-07] Satu sel rumput maksimal 1 rumput!");

            GrassInstance grass = new GrassInstance
            {
                Coords = cell.Coords,
                IsParasite = isParasite,
                IsReserved = false
            };

            if (_grassObjectData != null)
            {
                // Ambil prefab rumput sesuai urutan Cycle (Cycle 1 = Type 1, Cycle 2 = Type 2, dst.)
                GameObject prefabToSpawn = _grassObjectData.GetPrefabForCycle(currentCycle);
                if (prefabToSpawn != null)
                {
                    Vector3 pos = GridService.Instance.GridToWorld(cell.Coords);
                    grass.VisualGO = Instantiate(prefabToSpawn, pos, Quaternion.identity, transform);
                }
            }

            cell.Grass = grass;
            ActiveGrass.Add(grass);
        }

        public void RemoveGrass(GrassInstance grass)
        {
            if (grass == null) return;
            GridCell cell = GridService.Instance.GetCell(grass.Coords);
            if (cell != null && cell.Grass == grass)
            {
                cell.Grass = null;
            }
            if (grass.VisualGO != null)
            {
                Destroy(grass.VisualGO);
            }
            ActiveGrass.Remove(grass);
        }

        public void ClearAllGrassReservations()
        {
            foreach (var g in ActiveGrass)
            {
                if (g != null) g.IsReserved = false;
            }
        }
    }
}