using System.Collections.Generic;
using UnityEngine;
using Cycle33.Core;

namespace Cycle33.Data
{
    [CreateAssetMenu(fileName = "StageConfig", menuName = "Cycle33/StageConfig")]
    public class StageConfigSO : ScriptableObject
    {
        public int stageIndex = 1;
        public int totalCycles = 7;
        public int startMoney = 300;
        public int targetMoney = 1000;
        public int grassBasePerCycle = 5;
        public int caveCapacityPerSpecies = 10;
        public bool hasCave = true; // Stage 7 = false (No Cave)

        [Header("Available Creatures in Stage")]
        public List<Species> availableAnimals = new List<Species>();

        [Header("Random Weather Settings (Muncul setiap N Cycle)")]
        public int eventIntervalCycles = 3;
        public List<Cycle33.Core.EventType> availableEvents = new List<Cycle33.Core.EventType>(); // Isi sesuai tabel: Rn, Fm, Ts, Bs

        [Header("Habitat Layout")]
        public List<ObjectPlacement> staticObjects = new List<ObjectPlacement>();

        // Memilih event secara pure random setiap kelipatan eventIntervalCycles (Cycle 3, 6, 9, dst.)
        public Cycle33.Core.EventType RollEventForCycle(int cycle, System.Random rng)
        {
            if (eventIntervalCycles <= 0 || cycle % eventIntervalCycles != 0)
                return Cycle33.Core.EventType.None;

            if (availableEvents == null || availableEvents.Count == 0)
                return Cycle33.Core.EventType.None;

            // Filter bila ada yang tidak sengaja memasukkan EventType.None ke dalam list
            List<Cycle33.Core.EventType> validPool = new List<Cycle33.Core.EventType>();
            foreach (var evt in availableEvents)
            {
                if (evt != Cycle33.Core.EventType.None) validPool.Add(evt);
            }

            if (validPool.Count == 0) return Cycle33.Core.EventType.None;

            int randIdx = rng.Next(0, validPool.Count);
            return validPool[randIdx];
        }

        // Helper tombol klik kanan di Inspector untuk mengisi otomatis sesuai Tabel Stage 1-7
        [ContextMenu("Auto-Setup From Stage Index (1-7)")]
        public void AutoSetupByStageIndex()
        {
            availableAnimals.Clear();
            availableEvents.Clear();
            eventIntervalCycles = 3;
            hasCave = (stageIndex != 7);

            // Semua stage punya Sheep & Wolf
            availableAnimals.Add(Species.Sheep);
            availableAnimals.Add(Species.Wolf);

            if (stageIndex >= 4)
            {
                availableAnimals.Add(Species.Worm);
                availableAnimals.Add(Species.Chicken);
            }
            if (stageIndex >= 5)
            {
                availableAnimals.Add(Species.Civet);
            }

            // Setup Weather & Total Days sesuai tabel
            switch (stageIndex)
            {
                case 1:
                    totalCycles = 7;
                    break;
                case 2:
                    totalCycles = 14;
                    availableEvents.Add(Cycle33.Core.EventType.Rain);
                    break;
                case 3:
                    totalCycles = 21;
                    availableEvents.Add(Cycle33.Core.EventType.Rain);
                    availableEvents.Add(Cycle33.Core.EventType.FullMoon);
                    break;
                case 4:
                    totalCycles = 28;
                    availableEvents.Add(Cycle33.Core.EventType.Rain);
                    availableEvents.Add(Cycle33.Core.EventType.FullMoon);
                    break;
                case 5:
                    totalCycles = 35;
                    availableEvents.Add(Cycle33.Core.EventType.Rain);
                    availableEvents.Add(Cycle33.Core.EventType.FullMoon);
                    availableEvents.Add(Cycle33.Core.EventType.Thunderstorm);
                    break;
                case 6:
                case 7:
                    totalCycles = 35;
                    availableEvents.Add(Cycle33.Core.EventType.Rain);
                    availableEvents.Add(Cycle33.Core.EventType.FullMoon);
                    availableEvents.Add(Cycle33.Core.EventType.Thunderstorm);
                    availableEvents.Add(Cycle33.Core.EventType.BirthSeason);
                    break;
            }
        }
    }
}