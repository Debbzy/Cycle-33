using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cycle33.Core;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        private GlobalRulesSO _rules;
        private StageConfigSO _stage;
        private readonly Dictionary<ObjectType, ObjectDataSO> _objectDb = new Dictionary<ObjectType, ObjectDataSO>();
        private float _lightningTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize(GlobalRulesSO rules, StageConfigSO stage, List<ObjectDataSO> allObjectData)
        {
            _rules = rules;
            _stage = stage;
            _objectDb.Clear();
            foreach (var obj in allObjectData)
            {
                if (obj != null) _objectDb[obj.objectType] = obj;
            }
            _lightningTimer = 0f;
        }

        public void ResetTimer()
        {
            _lightningTimer = 0f;
        }

        // WX-TS-01 & WX-TS-02: Timer petir berjalan dalam waktu simulasi (ikut fast-forward)
        public void TickThunderstorm(float simDeltaTime)
        {
            if (_rules == null) return;

            _lightningTimer += simDeltaTime;
            if (_lightningTimer >= _rules.lightningIntervalSec)
            {
                _lightningTimer -= _rules.lightningIntervalSec;
                ExecuteWeightedLightningStrike();
            }
        }

        // WX-TS-02: Pilih 1 target secara acak berbobot dari objek/hewan dengan lightningWeight > 0 yang tidak Undercover
        private void ExecuteWeightedLightningStrike()
        {
            List<(Vector2Int cell, int weight, string label)> candidates = new List<(Vector2Int, int, string)>();
            int totalWeight = 0;

            // 1. Kandidat Hewan (tidak mati, tidak Undercover di goa, lightningWeight > 0)
            foreach (var c in CycleManager.Instance.ActiveCreatures)
            {
                if (c != null && c.State != CreatureState.Dead && !c.isUndercover && c.Data.lightningWeight > 0)
                {
                    candidates.Add((c.CurrentGridPos, c.Data.lightningWeight, c.name));
                    totalWeight += c.Data.lightningWeight;
                }
            }

            // 2. Kandidat Rumput (Grass lightningWeight = 1)
            int grassWeight = _objectDb.TryGetValue(ObjectType.Grass, out var grassData) ? grassData.lightningWeight : 1;
            if (grassWeight > 0)
            {
                foreach (var g in HabitatManager.Instance.ActiveGrass)
                {
                    if (g != null)
                    {
                        candidates.Add((g.Coords, grassWeight, $"Grass@{g.Coords}"));
                        totalWeight += grassWeight;
                    }
                }
            }

            // 3. Kandidat Objek Statis (mis. Tree lightningWeight = 2)
            if (_stage != null)
            {
                foreach (var placement in _stage.staticObjects)
                {
                    if (_objectDb.TryGetValue(placement.objectType, out var objData) && objData.lightningWeight > 0)
                    {
                        candidates.Add((placement.bottomLeftGridPos, objData.lightningWeight, $"Obj_{placement.objectType}"));
                        totalWeight += objData.lightningWeight;
                    }
                }
            }

            if (totalWeight <= 0 || candidates.Count == 0) return;

            int roll = SimulationClock.Instance.SeededRng.Next(0, totalWeight);
            int currentSum = 0;
            Vector2Int struckCell = candidates[0].cell;
            string struckLabel = candidates[0].label;

            foreach (var item in candidates)
            {
                currentSum += item.weight;
                if (roll < currentSum)
                {
                    struckCell = item.cell;
                    struckLabel = item.label;
                    break;
                }
            }

            StrikeCell(struckCell, struckLabel);
        }

        // WX-TS-03 & WX-TS-04: Sambaran mengenai sel target. Semua hewan di sel mati, rumput hilang.
        private void StrikeCell(Vector2Int cellCoords, string targetLabel)
        {
            Debug.Log($"⚡ [THUNDERSTORM] Petir menyambar sel {cellCoords} (Target awal: {targetLabel})!");

            // HAB-03: Rumput yang tersambar petir hilang (gosong)
            GridCell cell = GridService.Instance.GetCell(cellCoords);
            if (cell != null && cell.Grass != null)
            {
                HabitatManager.Instance.RemoveGrass(cell.Grass);
            }

            // Semua hewan di sel tersebut (yang tidak Undercover) mati -> otomatis memicu TGT-04 bagi penargetnya
            var victims = CycleManager.Instance.ActiveCreatures
                .Where(c => c != null && c.State != CreatureState.Dead && !c.isUndercover && c.CurrentGridPos == cellCoords)
                .ToList();

            foreach (var v in victims)
            {
                CycleManager.Instance.KillCreature(v);
            }
        }
    }
}