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
        public bool hasCave = true;

        public List<Species> availableAnimals = new List<Species>();
        public List<ObjectPlacement> staticObjects = new List<ObjectPlacement>();
        public List<EventCalendarEntry> eventCalendar = new List<EventCalendarEntry>();

        // CFG-01 (MUST): Validasi saat load, maksimal satu event per siklus
        public void ValidateCalendar()
        {
            HashSet<int> seenCycles = new HashSet<int>();
            foreach (var entry in eventCalendar)
            {
                if (!seenCycles.Add(entry.cycleIndex))
                {
                    Debug.LogError($"[CFG-01 Violation] Stage {stageIndex}: Duplikat event ditemukan pada Cycle {entry.cycleIndex}!");
                    throw new System.Exception($"CFG-01 Duplicate Event on Cycle {entry.cycleIndex}");
                }
            }
        }

        public Cycle33.Core.EventType GetEventForCycle(int cycle)
        {
            foreach (var entry in eventCalendar)
            {
                if (entry.cycleIndex == cycle) return entry.eventType;
            }
            return Cycle33.Core.EventType.None;
        }
    }
}