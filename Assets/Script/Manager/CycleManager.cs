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
        public bool IsPredatorShelterWeather => CurrentEvent == Cycle33.Core.EventType.Rain || CurrentEvent == Cycle33.Core.EventType.Thunderstorm;

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

        public float PhaseProgress01
        {
            get
            {
                if (_rules == null) return 0f;
                float duration = CurrentPhase == PhaseType.DayPhase ? _rules.dayDurationSec : _rules.nightDurationSec;
                return duration > 0f ? Mathf.Clamp01(_phaseTimer / duration) : 0f;
            }
        }

        public float CycleProgress01
        {
            get
            {
                if (_rules == null) return 0f;
                float dayRatio = _rules.dayDurationSec / Mathf.Max(0.01f, _rules.dayDurationSec + _rules.nightDurationSec);
                float nightRatio = 1f - dayRatio;

                if (CurrentPhase == PhaseType.DayPhase)
                    return PhaseProgress01 * dayRatio;
                if (CurrentPhase == PhaseType.NightPhase)
                    return dayRatio + (PhaseProgress01 * nightRatio);
                return 0f;
            }
        }

        public Creature SpawnCreature(AnimalDataSO data, Vector2Int gridPos, bool isPurchased)
        {
            Vector3 worldPos = GridService.Instance.GridToWorld(gridPos);
            int initialAge = isPurchased ? 1 : 0;
            GameObject chosenPrefab = data.GetPrefabForAge(initialAge);

            Quaternion prefabRot = chosenPrefab != null ? chosenPrefab.transform.rotation : Quaternion.identity;
            GameObject go = chosenPrefab != null
                ? Instantiate(chosenPrefab, worldPos, prefabRot)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);

            go.name = $"{data.species}_{_nextCreatureId}";
            go.transform.position = worldPos;
            go.transform.rotation = prefabRot;

            Creature c = go.GetComponent<Creature>();
            if (c == null) c = go.AddComponent<Creature>();

            c.Initialize(_nextCreatureId++, data, _rules, isPurchased);
            ActiveCreatures.Add(c);

            if (CurrentCycle > 0 && !IsGameOver)
            {
                ApplyMidCycleSpawnState(c, isPurchased);
            }

            return c;
        }

        public void RefreshCreatureVisualForAge(Creature c)
        {
            if (c == null || c.Data == null) return;
            if (c.Data.babyPrefab == null || c.Data.prefab == null || c.Data.babyPrefab == c.Data.prefab) return;

            GameObject targetPrefab = c.Data.GetPrefabForAge(c.age);
            if (targetPrefab == null) return;

            Vector3 oldPos = c.transform.position;
            Quaternion targetRot = targetPrefab.transform.rotation;

            GameObject newVisual = Instantiate(targetPrefab, oldPos, targetRot);
            newVisual.name = c.gameObject.name;
            newVisual.transform.rotation = targetRot;

            Creature newCreature = newVisual.GetComponent<Creature>();
            if (newCreature == null) newCreature = newVisual.AddComponent<Creature>();

            newCreature.CopyRuntimeStateFrom(c);
            newCreature.SyncBaseScaleFromCurrentTransform();

            int idx = ActiveCreatures.IndexOf(c);
            if (idx >= 0) ActiveCreatures[idx] = newCreature;

            Destroy(c.gameObject);
        }

        private void ApplyMidCycleSpawnState(Creature c, bool isPurchased)
        {
            // 1. Jika sedang Rain atau Thunderstorm, predator (Wolf & Civet) langsung masuk Cave & Frozen!
            if (IsPredatorShelterWeather && c.Data.isPredator)
            {
                c.EvacuateToCaveAndFreeze(HasCaveInStage);
                return;
            }

            // 2. Cek apakah hewan ini nocturnal atau diurnal terhadap fase saat ini
            if (CurrentPhase == PhaseType.DayPhase || CurrentPhase == PhaseType.NightPhase)
            {
                bool isActiveNow = c.IsActiveInPhase(CurrentPhase, CurrentEvent);
                if (!isActiveNow)
                {
                    bool canUseCave = c.Data.isPredator && HasCaveInStage;
                    c.StartSleeping(canUseCave);
                    return;
                }
            }

            // 3. Jika hewan baru dibeli pada fase aktifnya -> langsung bisa makan!
            if (isPurchased && c.isAdult)
            {
                if (CurrentPhase == PhaseType.DayPhase)
                {
                    if (SessionASpecies.Contains(c.Data.species))
                    {
                        IsSessionADone = false;
                        TargetRegistry.Instance.TryAssignFoodTarget(c);
                    }
                    else if (c.Data.species == Species.Wolf && IsSessionADone && !IsPredatorShelterWeather)
                    {
                        TargetRegistry.Instance.TryAssignFoodTarget(c);
                    }
                }
                else if (CurrentPhase == PhaseType.NightPhase && !IsPredatorShelterWeather)
                {
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

        private void BeginNextCycle()
        {
            if (CurrentCycle >= _stage.totalCycles)
            {
                IsGameOver = true;
                EconomyService.Instance.EvaluateWinLose();
                return;
            }

            // 1. CycleStart: Roll event secara pure random setiap 3 cycle
            CurrentCycle++;
            CurrentPhase = PhaseType.CycleStart;
            TargetRegistry.Instance.ClearAllReservations();
            Assert.AreEqual(0, TargetRegistry.Instance.ReservedWorms, "[INV-08] Reservasi harus kosong di awal siklus!");

            CurrentEvent = _stage.RollEventForCycle(CurrentCycle, SimulationClock.Instance.SeededRng);
            if (CurrentEvent != Cycle33.Core.EventType.None)
            {
                Debug.Log($"🌦️ [Cycle {CurrentCycle}] Random Weather Triggered: {CurrentEvent}!");
            }
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
            foreach (var c in ActiveCreatures.ToList())   // snapshot, aman bila list berubah
            {
                if (c == null) continue;
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

            if (IsPredatorShelterWeather && CaveManager.Instance != null)
            {
                CaveManager.Instance.EvaluateRainMorningOwnership();
            }

            foreach (var c in ActiveCreatures)
            {
                if (c.Data.isPredator)
                {
                    if (IsPredatorShelterWeather)
                    {
                        // Saat Rain maupun Thunderstorm, predator masuk ke Cave dan ter-Frozen seharian (Siang & Malam)
                        c.EvacuateToCaveAndFreeze(HasCaveInStage);
                    }
                    else
                    {
                        c.SetFrozen(false);
                    }
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

            // Hanya bangunkan Wolf jika TIDAK sedang Rain / Thunderstorm
            if (!IsPredatorShelterWeather && CaveManager.Instance != null)
            {
                CaveManager.Instance.WakeUpSpeciesFromCave(Species.Wolf);
            }

            foreach (var c in ActiveCreatures)
            {
                if (c.Data.isPredator && IsPredatorShelterWeather)
                {
                    // Pastikan predator tetap di Cave / menuju Cave lalu Frozen
                    if (!c.isFrozen) c.EvacuateToCaveAndFreeze(HasCaveInStage);
                    continue;
                }

                if (c.isFrozen) continue;

                c.ateThisPeriod = false;
                c.matedThisPeriod = false;

                if (!c.IsActiveInPhase(PhaseType.DayPhase, CurrentEvent))
                {
                    c.StartSleeping(c.Data.isPredator && HasCaveInStage);
                }
                else
                {
                    c.ExitCaveAndWakeUp();
                }
            }

            AssignSessionTargets(SessionASpecies, isBonusFullMoonWolf: false);
        }

        private void StartSessionB_WolfDay()
        {
            if (IsPredatorShelterWeather) return;
            AssignSessionTargets(SessionBSpecies, isBonusFullMoonWolf: false);
        }

        private void StartNightPhase()
        {
            RegisterPeriodMisses(isDayEnd: true);
            TargetRegistry.Instance.ClearAllReservations();

            CurrentPhase = PhaseType.NightPhase;
            _phaseTimer = 0f;
            EventBus.RaisePhaseChanged(PhaseType.NightPhase);

            var starvingCivets = ActiveCreatures.Where(c => c.CheckNightStarvationDeath()).ToList();
            foreach (var dead in starvingCivets) KillCreature(dead);

            bool isFullMoon = (CurrentEvent == Cycle33.Core.EventType.FullMoon);

            if (!IsPredatorShelterWeather && CaveManager.Instance != null)
            {
                CaveManager.Instance.WakeUpSpeciesFromCave(Species.Civet);
                if (isFullMoon) CaveManager.Instance.WakeUpSpeciesFromCave(Species.Wolf);
            }

            foreach (var c in ActiveCreatures)
            {
                if (c.Data.isPredator && IsPredatorShelterWeather)
                {
                    // Malam saat Rain / Thunderstorm: Wolf dan Civet tetap di Cave & Frozen
                    if (!c.isFrozen) c.EvacuateToCaveAndFreeze(HasCaveInStage);
                    continue;
                }

                if (c.isFrozen) continue;

                if (c.IsActiveInPhase(PhaseType.NightPhase, CurrentEvent))
                {
                    c.ateThisPeriod = false;
                    c.matedThisPeriod = false;
                    c.ExitCaveAndWakeUp();
                }
                else
                {
                    c.StartSleeping(c.Data.isPredator && HasCaveInStage);
                }
            }

            if (!IsPredatorShelterWeather)
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
                AssignHungryIdleCreatures(SessionASpecies, isBonusFullMoonWolf: false);
                TryMatchIdleReadyMates(SessionASpecies);

                if (!IsSessionADone && CheckIsSessionDone(SessionASpecies))
                {
                    IsSessionADone = true;
                    StartSessionB_WolfDay();
                }

                if (IsSessionADone && !IsPredatorShelterWeather)
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
                if (!IsPredatorShelterWeather)
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

        private void AssignHungryIdleCreatures(Species[] activeSpecies, bool isBonusFullMoonWolf)
        {
            foreach (var c in ActiveCreatures)
            {
                if (c == null || !activeSpecies.Contains(c.Data.species) || !c.isAdult || c.isFrozen) continue;
                if (c.State != CreatureState.Idle || c.ateThisPeriod) continue;

                bool wantsToEat = (c.Data.species == Species.Worm) ||
                                  c.isHungry ||
                                  (isBonusFullMoonWolf && c.Data.species == Species.Wolf);
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

        private void RegisterPeriodMisses(bool isDayEnd)
        {
            foreach (var c in ActiveCreatures)
            {
                if (c == null || !c.isAdult || c.isFrozen || c.State == CreatureState.Dead) continue;

                // Predator tidak dihitung Miss saat Rain / Thunderstorm karena ter-Frozen di Cave
                if (c.Data.isPredator && IsPredatorShelterWeather) continue;

                if (isDayEnd && c.Data.activeInDay)
                {
                    if (c.isHungry && !c.ateThisPeriod) c.missedMeals++;
                }
                else if (!isDayEnd && c.Data.species == Species.Civet)
                {
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