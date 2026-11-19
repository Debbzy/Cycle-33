using System.Collections.Generic;
using UnityEngine;
using Cycle33.Core;

namespace Cycle33.Data
{
    /// <summary>
    /// Satu entri kalender cuaca: pada cycle ke-N terjadi event X.
    /// Dipisah dari CalendarEntry (StageConfig.cs) karena memakai Cycle33.Core.EventType.
    /// </summary>
    [System.Serializable]
    public struct WeatherCalendarEntry
    {
        public int cycleIndex; // mulai dari 1
        public Cycle33.Core.EventType eventType;
    }

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

        [Header("Weather Pool (event yang boleh dipakai di stage ini)")]
        public List<Cycle33.Core.EventType> availableEvents = new List<Cycle33.Core.EventType>(); // Rn, Fm, Ts, Bs

        [Header("Weather Calendar (atur manual: cycle ke berapa -> event apa)")]
        public List<WeatherCalendarEntry> eventCalendar = new List<WeatherCalendarEntry>();

        [Header("Auto-Generate Calendar (hanya dipakai oleh tombol Auto-Setup / Regenerate)")]
        public int eventIntervalCycles = 3;

        [Header("Habitat Layout")]
        public List<ObjectPlacement> staticObjects = new List<ObjectPlacement>();

        // ------------------------------------------------------------------
        // Runtime API
        // ------------------------------------------------------------------

        /// <summary>
        /// Mengembalikan event yang dijadwalkan pada cycle tersebut (None bila tidak ada).
        /// Bila ada beberapa entri di cycle yang sama, entri pertama yang dipakai.
        /// </summary>
        public Cycle33.Core.EventType GetEventForCycle(int cycle)
        {
            if (eventCalendar == null) return Cycle33.Core.EventType.None;

            for (int i = 0; i < eventCalendar.Count; i++)
            {
                if (eventCalendar[i].cycleIndex == cycle)
                    return eventCalendar[i].eventType;
            }
            return Cycle33.Core.EventType.None;
        }

        /// <summary>
        /// Kompatibilitas dengan kode lama. Parameter rng sudah tidak dipakai
        /// karena event sekarang ditentukan oleh eventCalendar (deterministik).
        /// </summary>
        public Cycle33.Core.EventType RollEventForCycle(int cycle, System.Random rng)
        {
            return GetEventForCycle(cycle);
        }

        // ------------------------------------------------------------------
        // Editor helpers (klik kanan pada header komponen di Inspector)
        // ------------------------------------------------------------------

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

            // Setup Weather Pool & Total Cycles sesuai tabel
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

            RegenerateCalendarFromPool();
        }

        /// <summary>
        /// Membuat kalender awal: setiap eventIntervalCycles (3, 6, 9, ...) diisi event dari
        /// availableEvents secara bergiliran. Hasilnya hanya titik awal; edit manual di Inspector.
        /// PERINGATAN: menimpa seluruh isi eventCalendar.
        /// </summary>
        [ContextMenu("Regenerate Calendar From Weather Pool")]
        public void RegenerateCalendarFromPool()
        {
            eventCalendar.Clear();

            if (eventIntervalCycles <= 0 || availableEvents == null) return;

            // Abaikan None yang tidak sengaja masuk ke pool
            List<Cycle33.Core.EventType> pool = new List<Cycle33.Core.EventType>();
            foreach (var evt in availableEvents)
            {
                if (evt != Cycle33.Core.EventType.None) pool.Add(evt);
            }
            if (pool.Count == 0) return;

            int poolIdx = 0;
            for (int cycle = eventIntervalCycles; cycle <= totalCycles; cycle += eventIntervalCycles)
            {
                eventCalendar.Add(new WeatherCalendarEntry
                {
                    cycleIndex = cycle,
                    eventType = pool[poolIdx % pool.Count]
                });
                poolIdx++;
            }
        }

        /// <summary>Mengurutkan kalender berdasarkan cycle dan melaporkan masalah di Console.</summary>
        [ContextMenu("Sort & Validate Calendar")]
        public void SortAndValidateCalendar()
        {
            eventCalendar.Sort((a, b) => a.cycleIndex.CompareTo(b.cycleIndex));

            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < eventCalendar.Count; i++)
            {
                var entry = eventCalendar[i];

                if (entry.cycleIndex < 1 || entry.cycleIndex > totalCycles)
                    Debug.LogWarning($"[{name}] Entri kalender cycle {entry.cycleIndex} di luar rentang 1-{totalCycles}.", this);

                if (entry.eventType == Cycle33.Core.EventType.None)
                    Debug.LogWarning($"[{name}] Entri kalender cycle {entry.cycleIndex} bertipe None (tidak berefek).", this);
                else if (availableEvents != null && !availableEvents.Contains(entry.eventType))
                    Debug.LogWarning($"[{name}] Event {entry.eventType} di cycle {entry.cycleIndex} tidak ada di Weather Pool.", this);

                if (!seen.Add(entry.cycleIndex))
                    Debug.LogWarning($"[{name}] Cycle {entry.cycleIndex} punya lebih dari satu entri; hanya yang pertama dipakai.", this);
            }
        }
    }
}