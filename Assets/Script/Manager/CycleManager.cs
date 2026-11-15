using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Cycle33.Core;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class CycleManager : MonoBehaviour
    {
        public static CycleManager Instance { get; private set; }

        public int CurrentCycle { get; private set; } = 0;
        public PhaseType CurrentPhase { get; private set; }
        public Cycle33.Core.EventType CurrentEvent { get; private set; }
        public bool IsSessionADone { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool HasCaveInStage => _stage != null && _stage.hasCave;

        public float PhaseRemainingSec => _rules != null
            ? Mathf.Max(0f, (CurrentPhase == PhaseType.DayPhase ? _rules.dayDurationSec : _rules.nightDurationSec) - _phaseTimer)
            : 0f;

        public List<Creature> ActiveCreatures { get; private set; } = new List<Creature>();

        private GlobalRulesSO _rules;
        private StageConfigSO _stage;
        private float _phaseTimer;
        private int _nextCreatureId = 1;

        private static readonly Species[] SessionASpecies = { Species.Sheep, Species.Chicken };
        private static readonly Species[] SessionBSpecies = { Species.Wolf };
        private static readonly Species[] SessionCSpecies = { Species.Civet };
        private static readonly Species[] SessionCDSpecies = { Species.Civet, Species.Wolf };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetupConfig(GlobalRulesSO rules, StageConfigSO stage)
        {
            _rules = rules;
            _stage = stage;
        }

        public void StartStage(GlobalRulesSO rules, StageConfigSO stage)
        {
            _rules = rules;
            _stage = stage;
            CurrentCycle = 0;
            IsGameOver = false;
            BeginNextCycle();
        }

        public Creature SpawnCreature(AnimalDataSO data, Vector2Int gridPos, bool isPurchased)
        {
            Vector3 worldPos = GridService.Instance.GridToWorld(gridPos);
            GameObject go = data.prefab != null
                ? Instantiate(data.prefab, worldPos, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);

            go.name = $"{data.species}_{_nextCreatureId}";
            go.transform.position = worldPos;

            Creature c = go.GetComponent<Creature>();
            if (c == null) c = go.AddComponent<Creature>();

            c.Initialize(_nextCreatureId++, data, _rules, isPurchased);
            ActiveCreatures.Add(c);

            // Jika di-spawn saat siklus sudah berjalan (baik beli maupun lahir)
            if (CurrentCycle > 0 && !IsGameOver)
            {
                ApplyMidCycleSpawnState(c, isPurchased);
            }

            return c;
        }

        private void ApplyMidCycleSpawnState(Creature c, bool isPurchased)
        {
            // 1. Jika sedang Rain dan hewan ini predator -> langsung Frozen (WX-RN-02)
            if (CurrentEvent == Cycle33.Core.EventType.Rain && c.Data.isPredator)
            {
                c.SetFrozen(true);
                return;
            }

            // 2. Cek apakah hewan ini nocturnal atau diurnal terhadap fase saat ini
            if (CurrentPhase == PhaseType.DayPhase || CurrentPhase == PhaseType.NightPhase)
            {
                bool isActiveNow = c.IsActiveInPhase(CurrentPhase, CurrentEvent);
                if (!isActiveNow)
                {
                    // Bila di-spawn di waktu tidurnya (mis. Sheep saat malam, atau Civet saat siang) -> langsung tidur!
                    bool canUseCave = c.Data.isPredator && HasCaveInStage;
                    c.StartSleeping(canUseCave);
                    return;
                }
            }

            // 3. Jika hewan baru dibeli (Adult & Hungry) pada fase aktifnya -> langsung bisa makan!
            if (isPurchased && c.isAdult)
            {
                if (CurrentPhase == PhaseType.DayPhase)
                {
                    if (SessionASpecies.Contains(c.Data.species))
                    {
                        // Buka kembali Session A bila sebelumnya sudah Done, supaya Sheep/Chicken baru ini makan & kawin dulu sebelum dimakan Wolf
                        IsSessionADone = false;
                        TargetRegistry.Instance.TryAssignFoodTarget(c);
                    }
                    else if (c.Data.species == Species.Wolf)
                    {
                        // Wolf baru dibeli di siang hari: jika Session A sudah Done, langsung makan; jika belum, tunggu Session A Done di Update()
                        if (IsSessionADone)
                        {
                            TargetRegistry.Instance.TryAssignFoodTarget(c);
                        }
                    }
                }
                else if (CurrentPhase == PhaseType.NightPhase)
                {
                    // Civet (atau Wolf saat Full Moon) yang dibeli malam hari langsung berburu
                    TargetRegistry.Instance.TryAssignFoodTarget(c);
                }
            }
        }

        public void KillCreature(Creature c)
        {
            if (c == null || c.State == CreatureState.Dead) return;
            c.MarkDead();
            ActiveCreatures.Remove(c);
            EventBus.RaiseCreatureDied(c.RuntimeId);
            Destroy(c.gameObject);
        }

        // CYC-01: Urutan 1 -> 2 -> 3 -> 4 berjalan terurut dan atomik sebelum Day dimulai
        private void BeginNextCycle()
        {
            if (CurrentCycle >= _stage.totalCycles)
            {
                IsGameOver = true;
                EconomyService.Instance.EvaluateWinLose();
                return;
            }

            // 1. CycleStart
            CurrentCycle++;
            CurrentPhase = PhaseType.CycleStart;
            TargetRegistry.Instance.ClearAllReservations();
            Assert.AreEqual(0, TargetRegistry.Instance.ReservedWorms, "[INV-08] Reservasi harus kosong di awal siklus!");

            CurrentEvent = _stage.GetEventForCycle(CurrentCycle);
            EventBus.RaiseCycleStart(CurrentCycle);

            // 2. EventStart
            CurrentPhase = PhaseType.EventStart;
            EventBus.RaiseEventStarted(CurrentEvent);
            ApplyEventStartEffects();

            // 3. HabitatStep
            CurrentPhase = PhaseType.HabitatStep;
            HabitatManager.Instance.ExecuteHabitatStep(CurrentEvent);

            // 4. AnimalUpkeep
            CurrentPhase = PhaseType.AnimalUpkeep;
            List<Creature> deadList = new List<Creature>();
            foreach (var c in ActiveCreatures)
            {
                if (c.PerformUpkeepAndCheckDeath()) deadList.Add(c);
            }
            foreach (var dead in deadList) KillCreature(dead);

            // 5. Mulai DayPhase
            StartDayPhase();
        }

        private void ApplyEventStartEffects()
        {
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.ResetTimer();
            }

            bool isRain = (CurrentEvent == Cycle33.Core.EventType.Rain);
            if (isRain && CaveManager.Instance != null)
            {
                CaveManager.Instance.EvaluateRainMorningOwnership(); // CAVE-02
            }

            foreach (var c in ActiveCreatures)
            {
                // WX-RN-02: Saat Rain, semua predator Frozen seharian
                if (c.Data.isPredator)
                {
                    c.SetFrozen(isRain);
                }

                // MAT-06: Birth Season mengisi penuh birthBar semua adult non-Frozen
                if (CurrentEvent == Cycle33.Core.EventType.BirthSeason && c.isAdult && !c.isFrozen)
                {
                    c.birthBar = c.Data.birthBarMax;
                }
            }
        }

        private void StartDayPhase()
        {
            CurrentPhase = PhaseType.DayPhase;
            _phaseTimer = 0f;
            IsSessionADone = false;
            EventBus.RaisePhaseChanged(PhaseType.DayPhase);

            if (CurrentEvent != Cycle33.Core.EventType.Rain && CaveManager.Instance != null)
            {
                CaveManager.Instance.WakeUpSpeciesFromCave(Species.Wolf);
            }

            foreach (var c in ActiveCreatures)
            {
                if (c.isFrozen) continue;

                c.ateThisPeriod = false;
                c.matedThisPeriod = false;

                if (!c.IsActiveInPhase(PhaseType.DayPhase, CurrentEvent))
                {
                    // Hewan nocturnal (Civet) tidur di siang hari
                    c.StartSleeping(c.Data.isPredator && HasCaveInStage);
                }
                else
                {
                    c.ExitCaveAndWakeUp();
                }
            }

            // Mulai Session A (Sheep & Chicken makan dan kawin dulu)
            AssignSessionTargets(SessionASpecies, isBonusFullMoonWolf: false);
        }

        private void StartSessionB_WolfDay()
        {
            if (CurrentEvent == Cycle33.Core.EventType.Rain) return;
            AssignSessionTargets(SessionBSpecies, isBonusFullMoonWolf: false);
        }

        private void StartNightPhase()
        {
            RegisterPeriodMisses(isDayEnd: true);
            TargetRegistry.Instance.ClearAllReservations();

            CurrentPhase = PhaseType.NightPhase;
            _phaseTimer = 0f;
            EventBus.RaisePhaseChanged(PhaseType.NightPhase);

            // ANI-04: Cek kematian lapar untuk hewan malam (Civet) di awal active period-nya (missedMeals >= hungryTolerance)
            var starvingCivets = ActiveCreatures.Where(c => c.CheckNightStarvationDeath()).ToList();
            foreach (var dead in starvingCivets) KillCreature(dead);

            bool isFullMoon = (CurrentEvent == Cycle33.Core.EventType.FullMoon);

            if (CurrentEvent != Cycle33.Core.EventType.Rain && CaveManager.Instance != null)
            {
                CaveManager.Instance.WakeUpSpeciesFromCave(Species.Civet);
                if (isFullMoon) CaveManager.Instance.WakeUpSpeciesFromCave(Species.Wolf);
            }

            foreach (var c in ActiveCreatures)
            {
                if (c.isFrozen) continue;

                if (c.IsActiveInPhase(PhaseType.NightPhase, CurrentEvent))
                {
                    c.ateThisPeriod = false;
                    c.matedThisPeriod = false;
                    c.ExitCaveAndWakeUp();
                }
                else
                {
                    // Hewan diurnal (Sheep, Chicken, dan Wolf saat bukan Full Moon) tidur di malam hari
                    c.StartSleeping(c.Data.isPredator && HasCaveInStage);
                }
            }

            if (CurrentEvent != Cycle33.Core.EventType.Rain)
            {
                AssignSessionTargets(isFullMoon ? SessionCDSpecies : SessionCSpecies, isBonusFullMoonWolf: isFullMoon);
            }
        }

        private void AssignSessionTargets(Species[] sessionSpecies, bool isBonusFullMoonWolf)
        {
            var members = ActiveCreatures
                .Where(c => c != null && sessionSpecies.Contains(c.Data.species) && c.isAdult && !c.isFrozen && c.State != CreatureState.Dead)
                .OrderBy(_ => SimulationClock.Instance.SeededRng.Next())
                .ToList();

            foreach (var m in members)
            {
                bool wantsToEat = m.isHungry || (isBonusFullMoonWolf && m.Data.species == Species.Wolf);
                if (wantsToEat && m.State == CreatureState.Idle)
                {
                    TargetRegistry.Instance.TryAssignFoodTarget(m);
                }
            }
        }

        public void NotifyCreatureFinishedEating(Creature c)
        {
            if (c != null && c.isReadyToMate && !c.matedThisPeriod)
            {
                TargetRegistry.Instance.TryPairForMating(c);
            }
        }

        private void Update()
        {
            if (IsGameOver || _rules == null) return;

            float dt = SimulationClock.Instance.SimDeltaTime;
            _phaseTimer += dt;

            if (CurrentEvent == Cycle33.Core.EventType.Thunderstorm && WeatherManager.Instance != null)
            {
                WeatherManager.Instance.TickThunderstorm(dt);
            }

            if (CurrentPhase == PhaseType.DayPhase)
            {
                // Pastikan hewan Session A yang masih Hungry & Idle mendapat target bila rumput/cacing tersedia
                AssignHungryIdleCreatures(SessionASpecies, isBonusFullMoonWolf: false);
                TryMatchIdleReadyMates(SessionASpecies);

                // CYC-04 & CYC-06: Wolf (Session B) HANYA boleh mulai berburu setelah seluruh Sheep & Chicken selesai makan & selesai kawin (Idle)
                if (!IsSessionADone && CheckIsSessionDone(SessionASpecies))
                {
                    IsSessionADone = true;
                    StartSessionB_WolfDay();
                }

                if (IsSessionADone && CurrentEvent != Cycle33.Core.EventType.Rain)
                {
                    AssignHungryIdleCreatures(SessionBSpecies, isBonusFullMoonWolf: false);
                    TryMatchIdleReadyMates(SessionBSpecies);
                }

                if (_phaseTimer >= _rules.dayDurationSec)
                {
                    StartNightPhase();
                }
            }
            else if (CurrentPhase == PhaseType.NightPhase)
            {
                if (CurrentEvent != Cycle33.Core.EventType.Rain)
                {
                    bool isFullMoon = (CurrentEvent == Cycle33.Core.EventType.FullMoon);
                    Species[] nightSpecies = isFullMoon ? SessionCDSpecies : SessionCSpecies;
                    AssignHungryIdleCreatures(nightSpecies, isBonusFullMoonWolf: isFullMoon);
                    TryMatchIdleReadyMates(nightSpecies);
                }

                if (_phaseTimer >= _rules.nightDurationSec)
                {
                    ExecuteEndOfCycle();
                }
            }
        }

        // Memastikan hewan yang baru di-spawn atau yang menunggu mangsa Idle langsung bergerak makan begitu target tersedia
        private void AssignHungryIdleCreatures(Species[] activeSpecies, bool isBonusFullMoonWolf)
        {
            foreach (var c in ActiveCreatures)
            {
                if (c == null || !activeSpecies.Contains(c.Data.species) || !c.isAdult || c.isFrozen) continue;
                if (c.State != CreatureState.Idle || c.ateThisPeriod) continue;

                bool wantsToEat = c.isHungry || (isBonusFullMoonWolf && c.Data.species == Species.Wolf);
                if (wantsToEat && TargetRegistry.Instance.HasAvailableFoodTargetFor(c))
                {
                    TargetRegistry.Instance.TryAssignFoodTarget(c);
                }
            }
        }

        private void TryMatchIdleReadyMates(Species[] activeSpecies)
        {
            foreach (var c in ActiveCreatures)
            {
                if (c != null && activeSpecies.Contains(c.Data.species) &&
                    c.isReadyToMate && !c.matedThisPeriod && c.State == CreatureState.Idle)
                {
                    TargetRegistry.Instance.TryPairForMating(c);
                }
            }
        }

        // CYC-06: Evaluasi apakah semua anggota session sudah selesai makan & selesai kawin (kembali Idle)
        private bool CheckIsSessionDone(Species[] sessionSpecies)
        {
            var adults = ActiveCreatures
                .Where(c => c != null && sessionSpecies.Contains(c.Data.species) && c.isAdult && !c.isFrozen && c.State != CreatureState.Dead)
                .ToList();

            foreach (var a in adults)
            {
                if (a.State == CreatureState.MovingToFood || a.State == CreatureState.Eating ||
                    a.State == CreatureState.MovingToMate || a.State == CreatureState.Mating)
                {
                    return false;
                }

                bool eatDone = a.ateThisPeriod || !a.isHungry || !TargetRegistry.Instance.HasAvailableFoodTargetFor(a);
                bool mateDone = a.matedThisPeriod || !a.isReadyToMate || !TargetRegistry.Instance.HasAvailableReadyMateFor(a);

                if (!eatDone || !mateDone) return false;
            }
            return true;
        }

        // ANI-03 & WX-FM-02: Registrasi Miss di akhir active period
        private void RegisterPeriodMisses(bool isDayEnd)
        {
            foreach (var c in ActiveCreatures)
            {
                if (c == null || !c.isAdult || c.isFrozen || c.State == CreatureState.Dead) continue;

                if (isDayEnd && c.Data.activeInDay)
                {
                    if (c.isHungry && !c.ateThisPeriod) c.missedMeals++;
                }
                else if (!isDayEnd && c.Data.species == Species.Civet)
                {
                    // WX-FM-02: Wolf gagal makan di malam Full Moon TIDAK dihitung Miss
                    if (c.isHungry && !c.ateThisPeriod) c.missedMeals++;
                }
            }
        }

        private void ExecuteEndOfCycle()
        {
            CurrentPhase = PhaseType.EndOfCycle;
            RegisterPeriodMisses(isDayEnd: false);
            TargetRegistry.Instance.ClearAllReservations();
            BeginNextCycle();
        }
    }
}