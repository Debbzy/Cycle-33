using System.Collections.Generic;
using UnityEngine;
using Cycle33.Core;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class EconomyService : MonoBehaviour
    {
        public static EconomyService Instance { get; private set; }

        public int Money { get; private set; }
        private StageConfigSO _stage;
        private readonly Dictionary<Species, AnimalDataSO> _animalDb = new Dictionary<Species, AnimalDataSO>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize(StageConfigSO stage, List<AnimalDataSO> allAnimalData)
        {
            _stage = stage;
            _animalDb.Clear();
            if (allAnimalData != null)
            {
                foreach (var a in allAnimalData)
                {
                    if (a != null) _animalDb[a.species] = a;
                }
            }

            Money = stage.startMoney;
            EventBus.RaiseMoneyChanged(Money);
        }

        public bool IsSpeciesAvailableInStage(Species species)
        {
            return _stage != null && _stage.availableAnimals.Contains(species) && _animalDb.ContainsKey(species);
        }

        public AnimalDataSO GetAnimalData(Species species)
        {
            return _animalDb.TryGetValue(species, out var data) ? data : null;
        }

        // ECON-01 & ECON-02: Money bertipe integer, bertambah di hit frame makan
        public void AddMoney(int amount)
        {
            Money += amount;
            EventBus.RaiseMoneyChanged(Money);
        }

        // ECON-04: Worm dibeli lewat tombol UI (tanpa posisi), menambah wormCount
        public bool TryBuyWorm()
        {
            if (CycleManager.Instance.IsGameOver) return false;
            if (!IsSpeciesAvailableInStage(Species.Worm)) return false;

            int price = _animalDb[Species.Worm].price;
            Money -= price; // ECON-01: Saldo negatif tidak menghalangi pembelian
            HabitatManager.Instance.AddWorms(1);
            EventBus.RaiseMoneyChanged(Money);
            return true;
        }

        // ECON-03: Membeli hewan kapan saja dan di mana saja sesuai posisi klik pada sel walkable
        public bool TryBuyAnimalAtCell(Species species, Vector2Int gridPos)
        {
            if (CycleManager.Instance.IsGameOver) return false;
            if (species == Species.Worm) return TryBuyWorm();
            if (!IsSpeciesAvailableInStage(species)) return false;
            if (!GridService.Instance.IsWalkable(gridPos)) return false;

            AnimalDataSO data = _animalDb[species];
            Money -= data.price; // ECON-01 & ECON-03: Bisa utang negatif tanpa batas
            EventBus.RaiseMoneyChanged(Money);

            CycleManager.Instance.SpawnCreature(data, gridPos, isPurchased: true);
            return true;
        }

        // ECON-05: Evaluasi menang/kalah hanya di akhir siklus terakhir
        public void EvaluateWinLose()
        {
            bool isWin = Money >= _stage.targetMoney;
            Debug.Log($"🏁 [STAGE SELESAI] Saldo Akhir: {Money} | Target: {_stage.targetMoney} => {(isWin ? "MENANG (WIN)" : "KALAH (LOSE)")}");
            EventBus.RaiseGameEvaluated(isWin);
        }
    }
}