using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Cycle33.Core;

namespace Cycle33.Systems
{
    public class TargetRegistry : MonoBehaviour
    {
        public static TargetRegistry Instance { get; private set; }

        private readonly HashSet<GrassInstance> _reservedGrass = new HashSet<GrassInstance>();
        private readonly HashSet<Creature> _reservedPrey = new HashSet<Creature>();
        private readonly HashSet<Creature> _reservedMates = new HashSet<Creature>();
        public int ReservedWorms { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ClearAllReservations()
        {
            foreach (var prey in _reservedPrey)
            {
                if (prey != null) prey.isLocked = false;
            }
            foreach (var g in _reservedGrass)
            {
                if (g != null) g.IsReserved = false;
            }
            _reservedGrass.Clear();
            _reservedPrey.Clear();
            _reservedMates.Clear();
            ReservedWorms = 0;
            if (HabitatManager.Instance != null)
            {
                HabitatManager.Instance.ClearAllGrassReservations();
            }
        }

        public void ReleaseAllForCreature(Creature c)
        {
            if (c == null) return;
            ReleaseFoodReservation(c);
            ReleaseWormReservation(c);
            ReleaseMateReservation(c);

            if (_reservedPrey.Contains(c))
            {
                c.isLocked = false;
                _reservedPrey.Remove(c);
            }
        }

        public void ReleaseFoodReservation(Creature hunter)
        {
            if (hunter.TargetGrass != null)
            {
                hunter.TargetGrass.IsReserved = false;
                _reservedGrass.Remove(hunter.TargetGrass);
                hunter.SetTargetGrass(null);
            }
            if (hunter.TargetPrey != null)
            {
                hunter.TargetPrey.isLocked = false;
                _reservedPrey.Remove(hunter.TargetPrey);
                hunter.SetTargetPrey(null);
            }
        }

        public void ReleaseWormReservation(Creature chicken)
        {
            if (chicken.HasWormTarget)
            {
                ReservedWorms = Mathf.Max(0, ReservedWorms - 1);
                chicken.SetWormTarget(false);
            }
            if (chicken.TargetWormCreature != null)
            {
                chicken.TargetWormCreature.isLocked = false;
                _reservedPrey.Remove(chicken.TargetWormCreature);
                chicken.SetTargetWormCreature(null);
            }
        }

        public void ReleaseMateReservation(Creature c)
        {
            if (c.TargetMate != null)
            {
                Creature partner = c.TargetMate;
                _reservedMates.Remove(partner);
                partner.ClearMateTarget();
            }
            _reservedMates.Remove(c);
            c.ClearMateTarget();
        }

        public bool IsPreyEligibleToBeHunted(Creature prey, Vector2Int hunterPos)
        {
            if (prey == null || prey.Data == null || prey.Data.isPredator) return false;
            if (prey.Data.species == Species.Worm) return false; // Worm ditangani sebagai target Worm oleh Chicken
            if (!prey.isAdult) return false;
            if (prey.State == CreatureState.Dead || prey.isUndercover) return false;
            if (_reservedPrey.Contains(prey) || _reservedMates.Contains(prey)) return false;

            if (CycleManager.Instance.CurrentPhase == PhaseType.DayPhase)
            {
                bool isIdleState = (prey.State == CreatureState.Idle || prey.State == CreatureState.Drinking);
                if (!isIdleState) return false;

                if (prey.isHungry && !prey.ateThisPeriod && HasAvailableFoodTargetFor(prey))
                    return false;

                if (prey.isReadyToMate && !prey.matedThisPeriod && HasAvailableReadyMateFor(prey))
                    return false;
            }
            else if (CycleManager.Instance.CurrentPhase == PhaseType.NightPhase)
            {
                bool isValidNightPreyState = (prey.State == CreatureState.Sleeping || prey.State == CreatureState.Idle || prey.State == CreatureState.Drinking);
                if (!isValidNightPreyState) return false;
            }

            return GridService.Instance.FindPath(hunterPos, prey.CurrentGridPos) != null;
        }

        public bool TryAssignFoodTarget(Creature seeker)
        {
            if (seeker == null || seeker.isFrozen || seeker.State == CreatureState.Dead)
                return false;

            // Worm tidak punya hungerInterval/isAdult wajib, langsung boleh mencari Grass
            if (seeker.Data.species != Species.Worm && !seeker.isAdult)
                return false;

            ReleaseFoodReservation(seeker);
            ReleaseWormReservation(seeker);

            // 1. Chicken prioritas mengejar Worm yang ada di grid terlebih dahulu
            if (seeker.Data.eatsWorm)
            {
                var wormCandidates = CycleManager.Instance.ActiveCreatures
                    .Where(w => w != null &&
                                w.Data != null &&
                                w.Data.species == Species.Worm &&
                                w.State != CreatureState.Dead &&
                                !_reservedPrey.Contains(w) &&
                                GridService.Instance.FindPath(seeker.CurrentGridPos, w.CurrentGridPos) != null)
                    .OrderBy(w => Vector2Int.Distance(seeker.CurrentGridPos, w.CurrentGridPos))
                    .ThenBy(w => w.RuntimeId)
                    .ToList();

                if (wormCandidates.Count > 0)
                {
                    Creature chosenWorm = wormCandidates[0];
                    bool added = _reservedPrey.Add(chosenWorm);
                    Assert.IsTrue(added, "[INV-01] Dua hewan menarget Worm yang sama!");

                    seeker.SetTargetWormCreature(chosenWorm);
                    chosenWorm.LockAsPrey();
                    seeker.StartMovingToFood();
                    return true;
                }

                // Fallback counter Worm jika ada wormCount tambahan di HabitatManager
                if (HabitatManager.Instance != null && HabitatManager.Instance.WormCount - ReservedWorms > 0)
                {
                    ReservedWorms++;
                    seeker.SetWormTarget(true);
                    seeker.StartMovingToFood();
                    return true;
                }
            }

            // 2. Predator (Wolf / Civet)
            if (seeker.Data.isPredator)
            {
                foreach (Species preySpecies in seeker.Data.preyDiet)
                {
                    var candidates = CycleManager.Instance.ActiveCreatures
                        .Where(c => c != null &&
                                    c.Data.species == preySpecies &&
                                    IsPreyEligibleToBeHunted(c, seeker.CurrentGridPos))
                        .OrderBy(c => (c.Data.ageLimit - c.age))
                        .ThenBy(c => Vector2Int.Distance(seeker.CurrentGridPos, c.CurrentGridPos))
                        .ThenBy(c => c.RuntimeId)
                        .ToList();

                    if (candidates.Count > 0)
                    {
                        Creature chosenPrey = candidates[0];
                        bool added = _reservedPrey.Add(chosenPrey);
                        Assert.IsTrue(added, "[INV-01] Dua predator menarget mangsa yang sama!");

                        seeker.SetTargetPrey(chosenPrey);
                        chosenPrey.LockAsPrey();
                        seeker.StartMovingToFood();
                        return true;
                    }
                }
                return false;
            }

            // 3. Herbivora, Worm, atau fallback Chicken ke Rumput terdekat
            if (seeker.Data.eatsGrass)
            {
                var grassCandidates = HabitatManager.Instance.ActiveGrass
                    .Where(g => g != null && !g.IsReserved && !_reservedGrass.Contains(g) &&
                                GridService.Instance.FindPath(seeker.CurrentGridPos, g.Coords) != null)
                    .OrderBy(g => Vector2Int.Distance(seeker.CurrentGridPos, g.Coords))
                    .ToList();

                if (grassCandidates.Count > 0)
                {
                    GrassInstance chosenGrass = grassCandidates[0];
                    chosenGrass.IsReserved = true;
                    bool added = _reservedGrass.Add(chosenGrass);
                    Assert.IsTrue(added, "[INV-01] Dua hewan menarget rumput yang sama!");

                    seeker.SetTargetGrass(chosenGrass);
                    seeker.StartMovingToFood();
                    return true;
                }
            }

            return false;
        }

        public bool TryPairForMating(Creature seeker)
        {
            if (seeker == null || seeker.Data.species == Species.Worm || !seeker.isReadyToMate || seeker.matedThisPeriod || seeker.isFrozen)
                return false;
            if (seeker.State != CreatureState.Idle || _reservedMates.Contains(seeker) || _reservedPrey.Contains(seeker))
                return false;

            var partner = CycleManager.Instance.ActiveCreatures
                .Where(c => c != null && c != seeker &&
                            c.Data.species == seeker.Data.species &&
                            c.isReadyToMate &&
                            !c.matedThisPeriod &&
                            !c.isFrozen &&
                            c.State == CreatureState.Idle &&
                            !_reservedMates.Contains(c) &&
                            !_reservedPrey.Contains(c) &&
                            GridService.Instance.FindPath(seeker.CurrentGridPos, c.CurrentGridPos) != null)
                .OrderBy(c => Vector2Int.Distance(seeker.CurrentGridPos, c.CurrentGridPos))
                .ThenBy(c => c.RuntimeId)
                .FirstOrDefault();

            if (partner != null)
            {
                _reservedMates.Add(seeker);
                _reservedMates.Add(partner);
                partner.LockForMatingAsReceiver(seeker);
                seeker.StartMatingAsInitiator(partner);
                return true;
            }
            return false;
        }

        public bool HasAvailableFoodTargetFor(Creature seeker)
        {
            if (seeker.Data.eatsWorm)
            {
                bool hasGridWorm = CycleManager.Instance.ActiveCreatures.Any(w =>
                    w != null && w.Data != null && w.Data.species == Species.Worm &&
                    w.State != CreatureState.Dead && !_reservedPrey.Contains(w));
                if (hasGridWorm) return true;

                if (HabitatManager.Instance != null && (HabitatManager.Instance.WormCount - ReservedWorms > 0))
                    return true;
            }

            if (seeker.Data.eatsGrass && HabitatManager.Instance.ActiveGrass.Any(g => !g.IsReserved && !_reservedGrass.Contains(g)))
                return true;

            if (seeker.Data.isPredator)
            {
                return CycleManager.Instance.ActiveCreatures.Any(prey =>
                    prey != null &&
                    seeker.Data.preyDiet.Contains(prey.Data.species) &&
                    IsPreyEligibleToBeHunted(prey, seeker.CurrentGridPos));
            }
            return false;
        }

        public bool HasAvailableReadyMateFor(Creature seeker)
        {
            return CycleManager.Instance.ActiveCreatures.Any(other =>
                other != null && other != seeker &&
                other.Data.species == seeker.Data.species &&
                other.isReadyToMate &&
                !other.matedThisPeriod &&
                !other.isFrozen &&
                other.State == CreatureState.Idle &&
                !_reservedMates.Contains(other) &&
                !_reservedPrey.Contains(other));
        }
    }
}