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

        public bool IsPaused { get; private set; }
        public int CurrentSpeedMultiplier => (_rules != null && _rules.speedMultipliers != null && _rules.speedMultipliers.Length > 0)
            ? _rules.speedMultipliers[_currentSpeedIndex]
            : 1;

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
            IsPaused = false;
            SetSpeedIndex(0);
        }

        // Dipanggil berdasarkan index array speedMultipliers (0 = 1x, 1 = 2x, 2 = 3x, 3 = 5x, 4 = 6x)
        public void SetSpeedIndex(int index)
        {
            if (_rules == null || _rules.speedMultipliers == null || _rules.speedMultipliers.Length == 0) return;
            _currentSpeedIndex = Mathf.Clamp(index, 0, _rules.speedMultipliers.Length - 1);
            ApplyTimeScale();
        }

        // Dipanggil berdasarkan nilai multiplier langsung (misal: 1, 2, 3, 5, 6)
        public void SetSpeedByMultiplierValue(int multiplierValue)
        {
            if (_rules == null || _rules.speedMultipliers == null) return;
            for (int i = 0; i < _rules.speedMultipliers.Length; i++)
            {
                if (_rules.speedMultipliers[i] == multiplierValue)
                {
                    SetSpeedIndex(i);
                    return;
                }
            }
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            ApplyTimeScale();
        }

        public void TogglePause()
        {
            SetPaused(!IsPaused);
        }

        private void ApplyTimeScale()
        {
            if (IsPaused)
            {
                Time.timeScale = 0f;
            }
            else if (_rules != null && _rules.speedMultipliers != null && _rules.speedMultipliers.Length > 0)
            {
                Time.timeScale = _rules.speedMultipliers[_currentSpeedIndex];
            }
            else
            {
                Time.timeScale = 1f;
            }
        }
    }
}