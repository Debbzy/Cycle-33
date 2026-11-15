using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cycle33.Core;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class CaveManager : MonoBehaviour
    {
        public static CaveManager Instance { get; private set; }

        private StageConfigSO _stage;
        private readonly Dictionary<Species, int> _occupantCounts = new Dictionary<Species, int>();
        public Species? RainMajoritySpecies { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize(StageConfigSO stage)
        {
            _stage = stage;
            _occupantCounts[Species.Wolf] = 0;
            _occupantCounts[Species.Civet] = 0;
            RainMajoritySpecies = null;
        }

        // CAVE-02: Saat Rain, kepemilikan goa dihitung sekali di pagi hari. Seri -> Wolf menang.
        public void EvaluateRainMorningOwnership()
        {
            if (_stage == null || !_stage.hasCave)
            {
                RainMajoritySpecies = null;
                return;
            }

            int wolfCount = CycleManager.Instance.ActiveCreatures.Count(c => c != null && c.Data.species == Species.Wolf && c.State != CreatureState.Dead);
            int civetCount = CycleManager.Instance.ActiveCreatures.Count(c => c != null && c.Data.species == Species.Civet && c.State != CreatureState.Dead);

            RainMajoritySpecies = (wolfCount >= civetCount) ? Species.Wolf : Species.Civet;
        }

        // CAVE-01: Siapa cepat tiba di entry goa masuk sesuai kapasitas per spesies
        public bool TryEnterCave(Creature c)
        {
            if (_stage == null || !_stage.hasCave) return false; // CAVE-04

            if (!_occupantCounts.ContainsKey(c.Data.species))
                _occupantCounts[c.Data.species] = 0;

            if (_occupantCounts[c.Data.species] < _stage.caveCapacityPerSpecies)
            {
                _occupantCounts[c.Data.species]++;
                return true;
            }
            return false;
        }

        public void WakeUpSpeciesFromCave(Species species)
        {
            _occupantCounts[species] = 0;
            foreach (var c in CycleManager.Instance.ActiveCreatures)
            {
                if (c != null && c.Data.species == species && c.isUndercover)
                {
                    c.ExitCaveAndWakeUp();
                }
            }
        }
    }
}