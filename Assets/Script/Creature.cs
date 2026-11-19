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
        public bool isFrozen;     // ANI-06
        public bool isLocked;     // TGT-05: Mangsa / Worm dikunci
        public bool isUndercover; // CAVE-01

        [Header("Session Flags")]
        public bool ateThisPeriod;
        public bool matedThisPeriod;
        public bool isReadyToMate => isAdult && Data != null && Data.species != Species.Worm && birthBar >= Data.birthBarMax;

        public CreatureState State { get; private set; }

        // Targets
        public GrassInstance TargetGrass { get; private set; }
        public Creature TargetPrey { get; private set; }
        public Creature TargetWormCreature { get; private set; }
        public Creature TargetMate { get; private set; }
        public bool HasWormTarget { get; private set; }
        public bool IsMateInitiator { get; private set; }

        public Vector2Int CurrentGridPos => GridService.Instance.WorldToGrid(transform.position);

        private GlobalRulesSO _rules;
        private List<Vector2Int> _currentPath;
        private int _pathIndex;
        private float _stateTimer;
        private float _walkCycleTimer;
        private bool _hitFrameTriggered;
        private bool _freezeUponReachingCave;
        private bool _isCurrentlyMoving;
        private Vector3 _baseScale;
        private Renderer[] _renderers;
        private Vector3 _prefabEulerAngles;

        public void Initialize(int id, AnimalDataSO data, GlobalRulesSO rules, bool isPurchased)
        {
            RuntimeId = id;
            Data = data;
            _rules = rules;
            _renderers = GetComponentsInChildren<Renderer>();

            // Ambil scale asli dari prefab yang sedang dipakai
            _baseScale = transform.localScale.sqrMagnitude > 0.0001f ? transform.localScale : Vector3.one;
            _prefabEulerAngles = transform.eulerAngles;

            age = isPurchased ? 1 : 0;
            isAdult = age >= Data.adultGrowCycles;
            birthBar = 0;

            cyclesSinceMeal = isPurchased ? Data.hungerInterval : 0;
            isHungry = (Data.species == Species.Worm) || (isAdult && (cyclesSinceMeal >= Data.hungerInterval));

            missedMeals = 0;
            sickRemaining = 0;
            isFrozen = false;
            isLocked = false;
            isUndercover = false;
            ateThisPeriod = false;
            matedThisPeriod = false;
            _freezeUponReachingCave = false;
            _isCurrentlyMoving = false;
            _walkCycleTimer = 0f;

            TransitionTo(CreatureState.Idle);
        }

        public void CopyRuntimeStateFrom(Creature old)
        {
            RuntimeId = old.RuntimeId;
            Data = old.Data;
            _rules = old._rules;
            age = old.age;
            isAdult = Data != null && age >= Data.adultGrowCycles;
            birthBar = old.birthBar;
            cyclesSinceMeal = old.cyclesSinceMeal;
            isHungry = old.isHungry;
            missedMeals = old.missedMeals;
            sickRemaining = old.sickRemaining;
            isFrozen = old.isFrozen;
            isLocked = old.isLocked;
            isUndercover = old.isUndercover;
            ateThisPeriod = old.ateThisPeriod;
            matedThisPeriod = old.matedThisPeriod;
            _freezeUponReachingCave = old._freezeUponReachingCave;

            // Pertahankan rotasi asli prefab adult, jangan timpa pakai rotasi prefab baby
            _prefabEulerAngles = transform.eulerAngles;

            TransitionTo(old.State);
        }

        public void SyncBaseScaleFromCurrentTransform()
        {
            _baseScale = transform.localScale.sqrMagnitude > 0.0001f ? transform.localScale : Vector3.one;
            _prefabEulerAngles = transform.eulerAngles;
            _renderers = GetComponentsInChildren<Renderer>();

            if (isUndercover)
            {
                SetVisualVisible(false);
            }
        }

        public void SetTargetGrass(GrassInstance g) => TargetGrass = g;
        public void SetTargetPrey(Creature p) => TargetPrey = p;
        public void SetTargetWormCreature(Creature w) => TargetWormCreature = w;
        public void SetWormTarget(bool val) => HasWormTarget = val;
        public void ClearMateTarget()
        {
            TargetMate = null;
            IsMateInitiator = false;
        }

        public bool IsActiveInPhase(PhaseType phase, Cycle33.Core.EventType currentEvent)
        {
            if (Data == null) return false;
            if (Data.species == Species.Worm) return true;

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

        public bool PerformUpkeepAndCheckDeath()
        {
            if (State == CreatureState.Dead) return true;
            if (isFrozen || _freezeUponReachingCave) return false;

            age++;
            isAdult = Data != null && age >= Data.adultGrowCycles;

            if (Data.species == Species.Worm)
            {
                isHungry = true;
                return false;
            }

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
            if (Data.species == Species.Worm) return false;
            if (Data.activeInNight && !Data.activeInDay && missedMeals >= Data.hungryTolerance) return true;
            return false;
        }

        public void EvacuateToCaveAndFreeze(bool hasCaveInStage)
        {
            if (Data != null && Data.species == Species.Worm) return;

            if (TargetRegistry.Instance != null)
                TargetRegistry.Instance.ReleaseAllForCreature(this);
            isLocked = false;

            if (isUndercover)
            {
                _freezeUponReachingCave = false;
                SetFrozen(true);
                return;
            }

            if (hasCaveInStage && GridService.Instance.CaveEntryCell.HasValue)
            {
                _currentPath = GridService.Instance.FindPath(CurrentGridPos, GridService.Instance.CaveEntryCell.Value);
                _pathIndex = 0;
                if (_currentPath != null)
                {
                    isFrozen = false;
                    _freezeUponReachingCave = true;
                    TransitionTo(CreatureState.Sleeping);
                    return;
                }
            }

            _freezeUponReachingCave = false;
            SetFrozen(true);
        }

        public void SetFrozen(bool frozen)
        {
            isFrozen = frozen;
            ResetSquashImmediate();
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
            if (Data != null && Data.species == Species.Worm) return;

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
            _isCurrentlyMoving = false;

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

            // Terapkan efek kenyat-kenyot hanya ketika sedang bergerak
            UpdateSquashAndStretch(dt);
        }

        private void UpdateSquashAndStretch(float dt)
        {
            float amt = Data != null ? Data.squashAmount : 0.14f;
            float spd = Data != null ? Data.squashSpeed : 14f;

            Vector3 targetScale = _baseScale;
            if (_isCurrentlyMoving && amt > 0f && dt > 0f)
            {
                _walkCycleTimer += dt * spd;
                float wave = Mathf.Sin(_walkCycleTimer); // -1 .. +1
                float squashFactor = wave * amt;

                // Kenyot vertikal / stretch ke atas dan menyempit ke samping secara bergantian
                targetScale = new Vector3(
                    _baseScale.x * (1f - squashFactor * 0.6f),
                    _baseScale.y * (1f + squashFactor),
                    _baseScale.z * (1f - squashFactor * 0.6f)
                );
            }
            else
            {
                _walkCycleTimer = 0f;
            }

            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Mathf.Clamp01(dt * 20f));
        }

        private void ResetSquashImmediate()
        {
            _isCurrentlyMoving = false;
            _walkCycleTimer = 0f;
            transform.localScale = _baseScale;
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
                    if (Data.species != Species.Worm &&
                        GridService.Instance.PondEdgeCells.Count > 0 &&
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
                FaceDirectionForward(dir);

                _stateTimer += dt;
                if (_stateTimer >= _rules.drinkDurationSec)
                {
                    TransitionTo(CreatureState.Idle);
                }
            }
        }

        private void UpdateMovingToFood(float dt)
        {
            // Target objek Worm di grid (untuk Chicken)
            if (TargetWormCreature != null)
            {
                if (TargetWormCreature.State == CreatureState.Dead)
                {
                    HandleFoodTargetLost();
                    return;
                }

                Vector2Int wormCoords = TargetWormCreature.CurrentGridPos;
                if (_currentPath == null)
                {
                    _currentPath = GridService.Instance.FindPath(CurrentGridPos, wormCoords);
                    _pathIndex = 0;
                    if (_currentPath == null)
                    {
                        HandleFoodTargetLost();
                        return;
                    }
                }

                Vector3 wormWorld = GridService.Instance.GridToWorld(wormCoords);
                if (Vector3.Distance(transform.position, wormWorld) <= 0.6f)
                {
                    TargetWormCreature.TransitionTo(CreatureState.BeingEaten);
                    TransitionTo(CreatureState.Eating);
                }
                else
                {
                    MoveAlongPath(dt);
                }
                return;
            }

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

            if (Data.species != Species.Worm)
            {
                Assert.IsTrue(isAdult, "[INV-06] Baby tidak boleh makan!");

                int finalEarn = Data.earn;
                if (isSick)
                {
                    finalEarn = Mathf.FloorToInt(Data.earn * (100f - _rules.sickEarnReductionPercent) / 100f);
                }
                EconomyService.Instance.AddMoney(finalEarn);
            }

            bool contractedSickness = false;

            if (TargetWormCreature != null)
            {
                Creature eatenWorm = TargetWormCreature;
                TargetRegistry.Instance.ReleaseWormReservation(this);
                CycleManager.Instance.KillCreature(eatenWorm);
            }
            else if (HasWormTarget)
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

            cyclesSinceMeal = 0;
            isHungry = (Data.species == Species.Worm);
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
                        _currentPath = null;
                    }

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
            Vector3 toTarget = targetWorld - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                _isCurrentlyMoving = true;
                FaceDirectionForward(toTarget.normalized);
            }

            transform.position = Vector3.MoveTowards(transform.position, targetWorld, Data.moveSpeedCellsPerSec * dt);

            if (Vector3.Distance(transform.position, targetWorld) < 0.05f)
            {
                _pathIndex++;
            }
        }

        // Mengarahkan hadap maju hewan sesuai modelForwardOffsetY (0 = normal maju, 180 = jika prefab menghadap belakang)
        private void FaceDirectionForward(Vector3 worldDir)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f) return;

            float moveYaw = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;
            float offsetY = Data != null ? Data.modelForwardOffsetY : 0f;
            float finalYaw = moveYaw + offsetY;

            transform.eulerAngles = new Vector3(_prefabEulerAngles.x, finalYaw, _prefabEulerAngles.z);
        }

        public void MarkDead()
        {
            if (TargetRegistry.Instance != null)
                TargetRegistry.Instance.ReleaseAllForCreature(this);
            State = CreatureState.Dead;
        }
    }
}