using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Cycle33.Core;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class Creature : MonoBehaviour
    {
        public int RuntimeId { get; private set; }
        public AnimalDataSO Data { get; private set; }

        [Header("Runtime Status (Bagian 5.1)")]
        public int age;
        public bool isAdult;
        public int birthBar;
        public int cyclesSinceMeal;
        public bool isHungry;
        public int missedMeals;
        public int sickRemaining;
        public bool isSick => sickRemaining > 0;
        public bool isFrozen;     // ANI-06: Timer berhenti
        public bool isLocked;     // TGT-05: Mangsa dikunci saat ditarget predator
        public bool isUndercover; // CAVE-01: Di dalam goa (kebal petir)

        [Header("Session Flags")]
        public bool ateThisPeriod;
        public bool matedThisPeriod;
        public bool isReadyToMate => isAdult && Data != null && birthBar >= Data.birthBarMax;

        public CreatureState State { get; private set; }

        // Targets
        public GrassInstance TargetGrass { get; private set; }
        public Creature TargetPrey { get; private set; }
        public Creature TargetMate { get; private set; }
        public bool HasWormTarget { get; private set; }
        public bool IsMateInitiator { get; private set; }

        public Vector2Int CurrentGridPos => GridService.Instance.WorldToGrid(transform.position);

        private GlobalRulesSO _rules;
        private List<Vector2Int> _currentPath;
        private int _pathIndex;
        private float _stateTimer;
        private bool _hitFrameTriggered;
        private bool _freezeUponReachingCave; // Flag evakuasi predator saat Thunderstorm/Rain
        private Renderer[] _renderers;

        public void Initialize(int id, AnimalDataSO data, GlobalRulesSO rules, bool isPurchased)
        {
            RuntimeId = id;
            Data = data;
            _rules = rules;
            _renderers = GetComponentsInChildren<Renderer>();

            age = isPurchased ? 1 : 0;
            isAdult = age >= Data.adultGrowCycles;
            birthBar = 0;

            cyclesSinceMeal = isPurchased ? Data.hungerInterval : 0;
            isHungry = isAdult && (cyclesSinceMeal >= Data.hungerInterval);

            missedMeals = 0;
            sickRemaining = 0;
            isFrozen = false;
            isLocked = false;
            isUndercover = false;
            ateThisPeriod = false;
            matedThisPeriod = false;
            _freezeUponReachingCave = false;

            UpdateBabyVisualScale();
            TransitionTo(CreatureState.Idle);
        }

        private void UpdateBabyVisualScale()
        {
            isAdult = Data != null && age >= Data.adultGrowCycles;
            transform.localScale = isAdult ? Vector3.one : Vector3.one * 0.6f;
        }

        public void SetTargetGrass(GrassInstance g) => TargetGrass = g;
        public void SetTargetPrey(Creature p) => TargetPrey = p;
        public void SetWormTarget(bool val) => HasWormTarget = val;
        public void ClearMateTarget()
        {
            TargetMate = null;
            IsMateInitiator = false;
        }

        public bool IsActiveInPhase(PhaseType phase, Cycle33.Core.EventType currentEvent)
        {
            if (Data == null) return false;
            // Saat Rain atau Thunderstorm, predator selalu mengungsi ke goa & Frozen baik siang maupun malam
            if (Data.isPredator && (currentEvent == Cycle33.Core.EventType.Rain || currentEvent == Cycle33.Core.EventType.Thunderstorm))
                return false;

            if (phase == PhaseType.DayPhase)
            {
                return Data.activeInDay;
            }
            if (phase == PhaseType.NightPhase)
            {
                if (Data.activeInNight) return true;
                if (Data.activeInFullMoonNight && currentEvent == Cycle33.Core.EventType.FullMoon) return true;
                return false;
            }
            return false;
        }

        // ANI-01, ANI-02, ANI-04, ANI-06, SICK-05: Upkeep di awal siklus
        public bool PerformUpkeepAndCheckDeath()
        {
            if (State == CreatureState.Dead) return true;

            // ANI-06 & INV-03: Hewan Frozen (atau sedang mengungsi untuk Frozen) melewati semua perubahan timer/status
            if (isFrozen || _freezeUponReachingCave) return false;

            age++;
            UpdateBabyVisualScale();

            if (isAdult)
            {
                cyclesSinceMeal++;
                isHungry = cyclesSinceMeal >= Data.hungerInterval;
            }
            else
            {
                isHungry = false;
            }

            if (sickRemaining > 0)
            {
                sickRemaining--;
            }

            if (age > Data.ageLimit) return true;
            if (Data.activeInDay && missedMeals >= Data.hungryTolerance) return true;

            return false;
        }

        public bool CheckNightStarvationDeath()
        {
            if (State == CreatureState.Dead || isFrozen || _freezeUponReachingCave) return false;
            if (Data.activeInNight && !Data.activeInDay && missedMeals >= Data.hungryTolerance) return true;
            return false;
        }

        // Predator masuk Cave lalu ter-Frozen saat Thunderstorm / Rain (baik siang maupun malam)
        public void EvacuateToCaveAndFreeze(bool hasCaveInStage)
        {
            if (TargetRegistry.Instance != null)
                TargetRegistry.Instance.ReleaseAllForCreature(this);
            isLocked = false;

            // Jika sudah di dalam goa dari malam sebelumnya, langsung bekukan di dalam goa!
            if (isUndercover)
            {
                _freezeUponReachingCave = false;
                SetFrozen(true);
                return;
            }

            // Jika ada Goa di stage ini, berjalan masuk ke pintu Goa dulu baru Frozen
            if (hasCaveInStage && GridService.Instance.CaveEntryCell.HasValue)
            {
                _currentPath = GridService.Instance.FindPath(CurrentGridPos, GridService.Instance.CaveEntryCell.Value);
                _pathIndex = 0;
                if (_currentPath != null)
                {
                    isFrozen = false; // Biarkan bergerak sampai pintu goa
                    _freezeUponReachingCave = true;
                    TransitionTo(CreatureState.Sleeping);
                    return;
                }
            }

            // Jika tidak ada Goa (Stage 7) atau rute tertutup, langsung Frozen di tempat
            _freezeUponReachingCave = false;
            SetFrozen(true);
        }

        public void SetFrozen(bool frozen)
        {
            isFrozen = frozen;
            if (frozen)
            {
                _freezeUponReachingCave = false;
                if (TargetRegistry.Instance != null)
                    TargetRegistry.Instance.ReleaseAllForCreature(this);
                TransitionTo(CreatureState.Frozen);
            }
            else
            {
                _freezeUponReachingCave = false;
                if (State == CreatureState.Frozen)
                {
                    TransitionTo(CreatureState.Idle);
                }
            }
        }

        public void LockAsPrey()
        {
            isLocked = true;
            _currentPath = null;
            if (State == CreatureState.Drinking)
            {
                TransitionTo(CreatureState.Idle);
            }
        }

        public void StartMovingToFood()
        {
            _currentPath = null;
            TransitionTo(CreatureState.MovingToFood);
        }

        public void StartMatingAsInitiator(Creature partner)
        {
            TargetMate = partner;
            IsMateInitiator = true;
            _currentPath = GridService.Instance.FindPath(CurrentGridPos, partner.CurrentGridPos);
            _pathIndex = 0;
            TransitionTo(CreatureState.MovingToMate);
        }

        public void LockForMatingAsReceiver(Creature initiator)
        {
            TargetMate = initiator;
            IsMateInitiator = false;
            _currentPath = null;
            TransitionTo(CreatureState.MovingToMate);
        }

        public void StartSleeping(bool tryGoToCave)
        {
            if (TargetRegistry.Instance != null)
                TargetRegistry.Instance.ReleaseAllForCreature(this);
            isLocked = false;
            _freezeUponReachingCave = false;

            if (isUndercover)
            {
                TransitionTo(CreatureState.Sleeping);
                return;
            }

            if (tryGoToCave && GridService.Instance.CaveEntryCell.HasValue)
            {
                _currentPath = GridService.Instance.FindPath(CurrentGridPos, GridService.Instance.CaveEntryCell.Value);
                _pathIndex = 0;
            }
            else
            {
                _currentPath = null;
            }
            TransitionTo(CreatureState.Sleeping);
        }

        public void EnterCaveUndercover()
        {
            isUndercover = true;
            SetVisualVisible(false);
        }

        public void ExitCaveAndWakeUp()
        {
            _freezeUponReachingCave = false;
            isFrozen = false;
            if (isUndercover)
            {
                isUndercover = false;
                SetVisualVisible(true);
            }
            TransitionTo(CreatureState.Idle);
        }

        private void SetVisualVisible(bool visible)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (r != null) r.enabled = visible;
            }
        }

        public void TransitionTo(CreatureState newState)
        {
            if (State == CreatureState.Dead) return;
            State = newState;
            _stateTimer = 0f;
            _hitFrameTriggered = false;

            if (newState == CreatureState.Idle && !isLocked && !isUndercover)
            {
                PickRandomIdlePath();
            }
            else if (newState == CreatureState.Sleeping && _currentPath == null)
            {
                _pathIndex = 0;
            }
        }

        private void Update()
        {
            if (State == CreatureState.Dead || isFrozen || Data == null) return;

            float dt = SimulationClock.Instance.SimDeltaTime;

            switch (State)
            {
                case CreatureState.Idle:
                    UpdateIdle(dt);
                    break;
                case CreatureState.Drinking:
                    UpdateDrinking(dt);
                    break;
                case CreatureState.MovingToFood:
                    UpdateMovingToFood(dt);
                    break;
                case CreatureState.Eating:
                    UpdateEating(dt);
                    break;
                case CreatureState.MovingToMate:
                    UpdateMovingToMate(dt);
                    break;
                case CreatureState.Mating:
                    UpdateMating(dt);
                    break;
                case CreatureState.Sleeping:
                    UpdateSleeping(dt);
                    break;
            }
        }

        private void UpdateIdle(float dt)
        {
            if (isLocked || isUndercover) return;

            if (CycleManager.Instance != null &&
                (CycleManager.Instance.CurrentPhase == PhaseType.DayPhase || CycleManager.Instance.CurrentPhase == PhaseType.NightPhase) &&
                !IsActiveInPhase(CycleManager.Instance.CurrentPhase, CycleManager.Instance.CurrentEvent))
            {
                bool isStormOrRain = CycleManager.Instance.CurrentEvent == Cycle33.Core.EventType.Thunderstorm ||
                                     CycleManager.Instance.CurrentEvent == Cycle33.Core.EventType.Rain;
                if (Data.isPredator && isStormOrRain)
                {
                    EvacuateToCaveAndFreeze(CycleManager.Instance.HasCaveInStage);
                }
                else
                {
                    StartSleeping(Data.isPredator && CycleManager.Instance.HasCaveInStage);
                }
                return;
            }

            if (_currentPath != null && _pathIndex < _currentPath.Count)
            {
                MoveAlongPath(dt);
            }
            else
            {
                _stateTimer += dt;
                if (_stateTimer >= 2f)
                {
                    _stateTimer = 0f;
                    if (GridService.Instance.PondEdgeCells.Count > 0 &&
                        SimulationClock.Instance.SeededRng.NextDouble() < _rules.drinkChancePerIdleCheck)
                    {
                        var edges = GridService.Instance.PondEdgeCells;
                        Vector2Int edge = edges[SimulationClock.Instance.SeededRng.Next(edges.Count)];
                        _currentPath = GridService.Instance.FindPath(CurrentGridPos, edge);
                        _pathIndex = 0;
                        if (_currentPath != null) TransitionTo(CreatureState.Drinking);
                    }
                    else
                    {
                        PickRandomIdlePath();
                    }
                }
            }
        }

        private void UpdateDrinking(float dt)
        {
            if (isLocked) { TransitionTo(CreatureState.Idle); return; }

            if (_currentPath != null && _pathIndex < _currentPath.Count)
            {
                MoveAlongPath(dt);
            }
            else
            {
                Vector3 dir = (GridService.Instance.PondCenterWorld - transform.position).normalized;
                if (dir != Vector3.zero) transform.forward = dir;

                _stateTimer += dt;
                if (_stateTimer >= _rules.drinkDurationSec)
                {
                    TransitionTo(CreatureState.Idle);
                }
            }
        }

        private void UpdateMovingToFood(float dt)
        {
            if (HasWormTarget)
            {
                if (HabitatManager.Instance.WormCount <= 0) { HandleFoodTargetLost(); return; }
                TransitionTo(CreatureState.Eating);
                return;
            }

            Vector2Int targetCoords;
            if (TargetGrass != null)
            {
                if (!HabitatManager.Instance.ActiveGrass.Contains(TargetGrass))
                {
                    HandleFoodTargetLost();
                    return;
                }
                targetCoords = TargetGrass.Coords;
            }
            else if (TargetPrey != null)
            {
                if (TargetPrey.State == CreatureState.Dead || TargetPrey.isUndercover)
                {
                    HandleFoodTargetLost();
                    return;
                }
                targetCoords = TargetPrey.CurrentGridPos;
            }
            else
            {
                HandleFoodTargetLost();
                return;
            }

            if (_currentPath == null)
            {
                _currentPath = GridService.Instance.FindPath(CurrentGridPos, targetCoords);
                _pathIndex = 0;
                if (_currentPath == null)
                {
                    HandleFoodTargetLost();
                    return;
                }
            }

            Vector3 targetWorld = GridService.Instance.GridToWorld(targetCoords);
            if (Vector3.Distance(transform.position, targetWorld) <= 0.6f)
            {
                if (TargetPrey != null)
                {
                    TargetPrey.TransitionTo(CreatureState.BeingEaten);
                }
                TransitionTo(CreatureState.Eating);
            }
            else
            {
                MoveAlongPath(dt);
            }
        }

        private void HandleFoodTargetLost()
        {
            TargetRegistry.Instance.ReleaseFoodReservation(this);
            TargetRegistry.Instance.ReleaseWormReservation(this);
            _currentPath = null;

            bool foundNew = TargetRegistry.Instance.TryAssignFoodTarget(this);
            if (!foundNew)
            {
                TransitionTo(CreatureState.Idle);
            }
        }

        private void UpdateEating(float dt)
        {
            _stateTimer += dt;
            float hitTime = Data.eatAnimDurationSec * Data.eatHitFrameNormalized;

            if (!_hitFrameTriggered && _stateTimer >= hitTime)
            {
                _hitFrameTriggered = true;
                ExecuteEatHitFrame();
            }

            if (_stateTimer >= Data.eatAnimDurationSec)
            {
                TransitionTo(CreatureState.Idle);
                CycleManager.Instance.NotifyCreatureFinishedEating(this);
            }
        }

        private void ExecuteEatHitFrame()
        {
            Assert.IsFalse(isFrozen, "[INV-03] Hewan Frozen tidak boleh makan!");
            Assert.IsTrue(isAdult, "[INV-06] Baby tidak boleh makan!");

            int finalEarn = Data.earn;
            if (isSick)
            {
                finalEarn = Mathf.FloorToInt(Data.earn * (100f - _rules.sickEarnReductionPercent) / 100f);
            }
            EconomyService.Instance.AddMoney(finalEarn);

            bool contractedSickness = false;

            if (HasWormTarget)
            {
                TargetRegistry.Instance.ReleaseWormReservation(this);
                HabitatManager.Instance.ConsumeOneWorm();
            }
            else if (TargetGrass != null)
            {
                if (Data.species == Species.Chicken || TargetGrass.IsParasite)
                    contractedSickness = true;

                GrassInstance eatenGrass = TargetGrass;
                TargetRegistry.Instance.ReleaseFoodReservation(this);
                HabitatManager.Instance.RemoveGrass(eatenGrass);
            }
            else if (TargetPrey != null)
            {
                if (TargetPrey.isSick)
                    contractedSickness = true;

                Creature eatenPrey = TargetPrey;
                TargetRegistry.Instance.ReleaseFoodReservation(this);
                CycleManager.Instance.KillCreature(eatenPrey);
            }

            if (contractedSickness)
            {
                sickRemaining = _rules.sickDurationCycles;
            }

            // ANI-02: Reset status lapar setelah makan
            cyclesSinceMeal = 0;
            isHungry = false;
            missedMeals = 0;
            ateThisPeriod = true;

            birthBar = Mathf.Min(birthBar + 1, Data.birthBarMax);
        }

        private void UpdateMovingToMate(float dt)
        {
            if (TargetMate == null || TargetMate.State == CreatureState.Dead || TargetMate.isFrozen)
            {
                TargetRegistry.Instance.ReleaseMateReservation(this);
                TransitionTo(CreatureState.Idle);
                return;
            }

            if (!IsMateInitiator) return;

            if (Vector3.Distance(transform.position, TargetMate.transform.position) <= 0.65f)
            {
                TransitionTo(CreatureState.Mating);
                TargetMate.TransitionTo(CreatureState.Mating);
            }
            else
            {
                MoveAlongPath(dt);
            }
        }

        private void UpdateMating(float dt)
        {
            if (!IsMateInitiator) return;

            _stateTimer += dt;
            if (_stateTimer >= Data.mateAnimDurationSec)
            {
                birthBar = 0;
                matedThisPeriod = true;

                if (TargetMate != null && TargetMate.State != CreatureState.Dead)
                {
                    Creature partner = TargetMate;
                    partner.birthBar = 0;
                    partner.matedThisPeriod = true;

                    int babyCount = (CycleManager.Instance.CurrentEvent == Cycle33.Core.EventType.BirthSeason) ? 2 : 1;
                    Vector2Int spawnPos = partner.CurrentGridPos;

                    for (int i = 0; i < babyCount; i++)
                    {
                        CycleManager.Instance.SpawnCreature(Data, spawnPos, isPurchased: false);
                    }

                    TargetRegistry.Instance.ReleaseMateReservation(this);
                    partner.TransitionTo(CreatureState.Idle);
                }

                TransitionTo(CreatureState.Idle);
            }
        }

        private void UpdateSleeping(float dt)
        {
            if (isUndercover)
            {
                if (_freezeUponReachingCave && !isFrozen)
                {
                    SetFrozen(true);
                }
                return;
            }

            if (_currentPath != null && _pathIndex < _currentPath.Count)
            {
                MoveAlongPath(dt);

                if (_pathIndex >= _currentPath.Count &&
                    GridService.Instance.CaveEntryCell.HasValue &&
                    CurrentGridPos == GridService.Instance.CaveEntryCell.Value)
                {
                    if (CaveManager.Instance != null && CaveManager.Instance.TryEnterCave(this))
                    {
                        EnterCaveUndercover();
                    }
                    else
                    {
                        _currentPath = null; // Goa penuh, tidur/beku di luar
                    }

                    // Jika sedang mengungsi karena Thunderstorm/Rain, langsung Frozen begitu tiba di Goa!
                    if (_freezeUponReachingCave)
                    {
                        SetFrozen(true);
                    }
                }
            }
            else if (_freezeUponReachingCave)
            {
                SetFrozen(true);
            }
        }

        private void PickRandomIdlePath()
        {
            if (_rules == null || SimulationClock.Instance == null || GridService.Instance == null)
                return;

            for (int i = 0; i < 5; i++)
            {
                Vector2Int randPos = new Vector2Int(
                    SimulationClock.Instance.SeededRng.Next(0, _rules.gridSize.x),
                    SimulationClock.Instance.SeededRng.Next(0, _rules.gridSize.y)
                );
                if (GridService.Instance.IsWalkable(randPos))
                {
                    _currentPath = GridService.Instance.FindPath(CurrentGridPos, randPos);
                    _pathIndex = 0;
                    if (_currentPath != null) break;
                }
            }
        }

        private void MoveAlongPath(float dt)
        {
            if (_currentPath == null || _pathIndex >= _currentPath.Count) return;

            Vector3 targetWorld = GridService.Instance.GridToWorld(_currentPath[_pathIndex]);
            transform.position = Vector3.MoveTowards(transform.position, targetWorld, Data.moveSpeedCellsPerSec * dt);

            Vector3 dir = (targetWorld - transform.position).normalized;
            if (dir != Vector3.zero) transform.forward = dir;

            if (Vector3.Distance(transform.position, targetWorld) < 0.05f)
            {
                _pathIndex++;
            }
        }

        public void MarkDead()
        {
            if (TargetRegistry.Instance != null)
                TargetRegistry.Instance.ReleaseAllForCreature(this);
            State = CreatureState.Dead;
        }
    }
}