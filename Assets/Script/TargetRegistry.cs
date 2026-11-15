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

        // TGT-06 & INV-08: Bersihkan seluruh reservasi di akhir session atau akhir siklus
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
            HabitatManager.Instance.ClearAllGrassReservations();
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

        // TGT-03: Algoritma Assign Target Makan
        public bool TryAssignFoodTarget(Creature seeker)
        {
            if (seeker == null || !seeker.isAdult || seeker.isFrozen || seeker.State == CreatureState.Dead)
                return false;

            ReleaseFoodReservation(seeker);
            ReleaseWormReservation(seeker);

            // 1. Chicken prioritas Worm dulu (TGT-03.3)
            if (seeker.Data.eatsWorm)
            {
                if (HabitatManager.Instance.WormCount - ReservedWorms > 0)
                {
                    ReservedWorms++;
                    seeker.SetWormTarget(true);
                    Assert.IsTrue(ReservedWorms <= HabitatManager.Instance.WormCount, "[INV-02] ReservedWorms > WormCount!");
                    seeker.StartMovingToFood();
                    return true;
                }
            }

            // 2. Predator (Wolf / Civet): Urut spesies di diet -> paling tua (ageLimit - age terkecil) -> terdekat -> ID terkecil (TGT-03.4)
            if (seeker.Data.isPredator)
            {
                foreach (Species preySpecies in seeker.Data.preyDiet)
                {
                    var candidates = CycleManager.Instance.ActiveCreatures
                        .Where(c => c != null &&
                                    c.Data.species == preySpecies &&
                                    c.isAdult && // INV-06: Baby tidak bisa ditarget
                                    !c.Data.isPredator && // INV-05: Predator tidak pernah dimakan
                                    c.State != CreatureState.Dead && // INV-09
                                    !c.isUndercover &&
                                    (c.State == CreatureState.Idle || c.State == CreatureState.Sleeping || c.State == CreatureState.Drinking) &&
                                    !_reservedPrey.Contains(c) &&
                                    GridService.Instance.FindPath(seeker.CurrentGridPos, c.CurrentGridPos) != null)
                        .OrderBy(c => (c.Data.ageLimit - c.age)) // Sisa umur terkecil (paling tua)
                        .ThenBy(c => Vector2Int.Distance(seeker.CurrentGridPos, c.CurrentGridPos))
                        .ThenBy(c => c.RuntimeId)
                        .ToList();

                    if (candidates.Count > 0)
                    {
                        Creature chosenPrey = candidates[0];
                        bool added = _reservedPrey.Add(chosenPrey);
                        Assert.IsTrue(added, "[INV-01] Dua predator menarget mangsa yang sama!");

                        seeker.SetTargetPrey(chosenPrey);
                        chosenPrey.LockAsPrey(); // TGT-05: Mangsa dikunci
                        seeker.StartMovingToFood();
                        return true;
                    }
                }
                return false;
            }

            // 3. Herbivora (Sheep) atau fallback Chicken ke Rumput terdekat (TGT-03.2 & TGT-03.3)
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

        // MAT-02 & MAT-03: Cari pasangan spesies sama, Ready, belum direservasi, terdekat
        public bool TryPairForMating(Creature seeker)
        {
            if (seeker == null || !seeker.isReadyToMate || seeker.matedThisPeriod || seeker.isFrozen) return false;
            if (seeker.State != CreatureState.Idle || _reservedMates.Contains(seeker)) return false;

            var partner = CycleManager.Instance.ActiveCreatures
                .Where(c => c != null && c != seeker &&
                            c.Data.species == seeker.Data.species &&
                            c.isReadyToMate &&
                            !c.matedThisPeriod &&
                            !c.isFrozen &&
                            c.State == CreatureState.Idle &&
                            !_reservedMates.Contains(c) &&
                            GridService.Instance.FindPath(seeker.CurrentGridPos, c.CurrentGridPos) != null)
                .OrderBy(c => Vector2Int.Distance(seeker.CurrentGridPos, c.CurrentGridPos))
                .ThenBy(c => c.RuntimeId)
                .FirstOrDefault();

            if (partner != null)
            {
                _reservedMates.Add(seeker);
                _reservedMates.Add(partner);
                partner.LockForMatingAsReceiver(seeker); // MAT-03: B dikunci
                seeker.StartMatingAsInitiator(partner);  // MAT-03: A berjalan menghampiri B
                return true;
            }
            return false;
        }

        // Helper untuk evaluasi CYC-06 (Apakah masih ada target makanan tersisa untuk hewan ini?)
        public bool HasAvailableFoodTargetFor(Creature seeker)
        {
            if (seeker.Data.eatsWorm && (HabitatManager.Instance.WormCount - ReservedWorms > 0)) return true;
            if (seeker.Data.eatsGrass && HabitatManager.Instance.ActiveGrass.Any(g => !g.IsReserved && !_reservedGrass.Contains(g))) return true;
            if (seeker.Data.isPredator)
            {
                return CycleManager.Instance.ActiveCreatures.Any(prey =>
                    prey != null &&
                    seeker.Data.preyDiet.Contains(prey.Data.species) &&
                    prey.isAdult &&
                    prey.State != CreatureState.Dead &&
                    !prey.isUndercover &&
                    (prey.State == CreatureState.Idle || prey.State == CreatureState.Sleeping || prey.State == CreatureState.Drinking) &&
                    !_reservedPrey.Contains(prey));
            }
            return false;
        }

        // Helper untuk evaluasi CYC-06 (Apakah masih ada pasangan Ready tersisa?)
        public bool HasAvailableReadyMateFor(Creature seeker)
        {
            return CycleManager.Instance.ActiveCreatures.Any(other =>
                other != null && other != seeker &&
                other.Data.species == seeker.Data.species &&
                other.isReadyToMate &&
                !other.matedThisPeriod &&
                !other.isFrozen &&
                other.State == CreatureState.Idle &&
                !_reservedMates.Contains(other));
        }
    }
}