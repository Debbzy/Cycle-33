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

            // ANI-05: Jika dibeli saat session spesiesnya masih berjalan -> ikut session
            if (isPurchased && CurrentCycle > 0 && !IsGameOver)
            {
                TryJoinOngoingSession(c);
            }

            return c;
        }

        private void TryJoinOngoingSession(Creature c)
        {
            if (CurrentPhase == PhaseType.DayPhase)
            {
                if (!IsSessionADone && SessionASpecies.Contains(c.Data.species))
                {
                    TargetRegistry.Instance.TryAssignFoodTarget(c);
                }
                else if (IsSessionADone && c.Data.species == Species.Wolf && CurrentEvent != Cycle33.Core.EventType.Rain)
                {
                    TargetRegistry.Instance.TryAssignFoodTarget(c);
                }
            }
            else if (CurrentPhase == PhaseType.NightPhase && CurrentEvent != Cycle33.Core.EventType.Rain)
            {
                if (c.Data.species == Species.Civet || (c.Data.species == Species.Wolf && CurrentEvent == Cycle33.Core.EventType.FullMoon))
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

                if (c.Data.species == Species.Civet)
                {
                    c.StartSleeping(_stage.hasCave);
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

            // ANI-04: Cek kematian lapar untuk hewan malam (Civet) di awal active period-nya
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

                if (c.Data.species == Species.Civet)
                {
                    c.ateThisPeriod = false;
                    c.matedThisPeriod = false;
                    c.ExitCaveAndWakeUp();
                }
                else if (c.Data.species == Species.Wolf)
                {
                    if (isFullMoon)
                    {
                        // WX-FM-01: Wolf aktif kedua kalinya di malam Full Moon
                        c.ateThisPeriod = false;
                        c.matedThisPeriod = false;
                        c.ExitCaveAndWakeUp();
                    }
                    else
                    {
                        c.StartSleeping(_stage.hasCave);
                    }
                }
                else
                {
                    c.StartSleeping(tryGoToCave: false);
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
                if (wantsToEat)
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

            // WX-TS-01: Petir menyambar setiap lightningIntervalSec selama Thunderstorm
            if (CurrentEvent == Cycle33.Core.EventType.Thunderstorm && WeatherManager.Instance != null)
            {
                WeatherManager.Instance.TickThunderstorm(dt);
            }

            if (CurrentPhase == PhaseType.DayPhase)
            {
                Species[] currentActive = IsSessionADone ? SessionBSpecies : SessionASpecies;
                TryMatchIdleReadyMates(currentActive);

                if (!IsSessionADone && CheckIsSessionDone(SessionASpecies))
                {
                    IsSessionADone = true;
                    StartSessionB_WolfDay();
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
                    Species[] nightSpecies = (CurrentEvent == Cycle33.Core.EventType.FullMoon) ? SessionCDSpecies : SessionCSpecies;
                    TryMatchIdleReadyMates(nightSpecies);
                }

                if (_phaseTimer >= _rules.nightDurationSec)
                {
                    ExecuteEndOfCycle();
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