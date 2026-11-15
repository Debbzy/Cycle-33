using UnityEngine;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class SimulationClock : MonoBehaviour
    {
        public static SimulationClock Instance { get; private set; }

        public System.Random SeededRng { get; private set; }
        [SerializeField] private int defaultSeed = 33;

        private GlobalRulesSO _rules;
        private int _currentSpeedIndex = 0;

        // TIME-02: Ekosistem memakai SimDeltaTime, UI memakai UnscaledDeltaTime
        public float SimDeltaTime => Time.deltaTime;
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SeededRng = new System.Random(defaultSeed);
        }

        public void Initialize(GlobalRulesSO rules, int seed = 33)
        {
            _rules = rules;
            SeededRng = new System.Random(seed);
            SetSpeedIndex(0);
        }

        public void SetSpeedIndex(int index)
        {
            if (_rules == null || _rules.speedMultipliers == null || _rules.speedMultipliers.Length == 0) return;
            _currentSpeedIndex = Mathf.Clamp(index, 0, _rules.speedMultipliers.Length - 1);
            Time.timeScale = _rules.speedMultipliers[_currentSpeedIndex];
        }
    }
}