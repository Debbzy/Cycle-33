using UnityEngine;
using UnityEngine.SceneManagement;
using Cycle33.Core;
using Cycle33.Systems;
using GameEventType = Cycle33.Core.EventType; // hindari bentrok dengan UnityEngine.EventType

namespace Cycle33.Audio
{
    /// <summary>
    /// Satu-satunya skrip audio yang tahu soal gameplay. Menerjemahkan EventBus + scene menjadi perintah ke AudioManager.
    /// Pasang di GameObject yang sama dengan AudioManager.
    ///
    /// ASUMSI NAMESPACE: CycleManager dan SimulationClock berada di Cycle33.Core atau Cycle33.Systems.
    /// Bila tidak, sesuaikan baris using di atas.
    /// </summary>
    [RequireComponent(typeof(AudioManager))]
    public class AudioEventAdapter : MonoBehaviour
    {
        private enum Phase { None, Day, Night }

        [Header("Scene")]
        [Tooltip("Nama scene main menu (memakai BGM MainMenu). Scene lain dianggap gameplay.")]
        [SerializeField] private string[] mainMenuScenes = { "MainMenu" };

        [Header("Override SFX (opsional, kosong = dilewati)")]
        [SerializeField] private SFXCue fullMoonCue;
        [SerializeField] private SFXCue winCue;
        [SerializeField] private SFXCue loseCue;

        private AudioManager _manager;
        private bool _initialized;
        private bool _inMenu;
        private Phase _phase;
        private bool _rain;
        private bool _fullMoon;
        private bool _evaluated;
        private bool _pausePending;
        private bool _pauseValue;
        private float _lastSpeed = 1f;

        private bool Active => _manager != null && AudioManager.Instance == _manager;

        // Status baca-saja untuk skrip audio lain (mis. CreatureCallScheduler).
        public bool IsGameplay => _initialized && !_inMenu;
        public bool IsDay => _phase == Phase.Day;
        public bool IsNight => _phase == Phase.Night;
        public bool IsRain => _rain;
        public bool IsFullMoon => _fullMoon;
        public bool IsEvaluated => _evaluated;

        private void Awake()
        {
            _manager = GetComponent<AudioManager>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EventBus.OnCycleStart += HandleCycleStart;
            EventBus.OnEventStarted += HandleEventStarted;
            EventBus.OnPhaseChanged += HandlePhaseChanged;
            EventBus.OnGameEvaluated += HandleGameEvaluated;
            EventBus.OnPauseChanged += HandlePauseChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EventBus.OnCycleStart -= HandleCycleStart;
            EventBus.OnEventStarted -= HandleEventStarted;
            EventBus.OnPhaseChanged -= HandlePhaseChanged;
            EventBus.OnGameEvaluated -= HandleGameEvaluated;
            EventBus.OnPauseChanged -= HandlePauseChanged;
        }

        private void Start()
        {
            // Jaga-jaga bila sceneLoaded untuk scene pertama terlewat (mis. tes langsung dari scene gameplay).
            if (!_initialized && Active) EnterScene(SceneManager.GetActiveScene());
        }

        private void Update()
        {
            if (!Active) return;

            // Tidak ada event perubahan kecepatan, jadi dibaca tiap frame (murah).
            float speed = 1f;
            SimulationClock clock = SimulationClock.Instance;
            if (clock != null) speed = clock.CurrentSpeedMultiplier;

            if (!Mathf.Approximately(speed, _lastSpeed))
            {
                _lastSpeed = speed;
                _manager.SetSpeedScale(speed);
            }
        }

        private void LateUpdate()
        {
            // Pause ditunda sampai akhir frame. Saat Win/Lose, HUD memanggil SetPaused(true) di dalam handler
            // OnGameEvaluated, mungkin SEBELUM handler adapter berjalan (urutan subscriber). Dengan menunda,
            // _evaluated pasti sudah terisi dan pause itu bisa diabaikan.
            if (!_pausePending) return;
            _pausePending = false;
            if (!Active || _evaluated) return;
            _manager.SetPaused(_pauseValue);
        }

        // ---------------- Scene ----------------

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || !Active) return;
            EnterScene(scene);
        }

        private void EnterScene(Scene scene)
        {
            _initialized = true;
            _inMenu = IsMenuScene(scene.name);

            _phase = Phase.None;
            _rain = false;
            _fullMoon = false;
            _evaluated = false;
            _pausePending = false;
            _lastSpeed = 1f;

            _manager.SetPaused(false);
            _manager.SetSpeedScale(1f);

            if (!_inMenu) SyncFromGame();
            Resolve();
        }

        private bool IsMenuScene(string sceneName)
        {
            if (mainMenuScenes == null) return false;
            foreach (string n in mainMenuScenes)
                if (n == sceneName) return true;
            return false;
        }

        /// <summary>Baca keadaan siklus saat ini (bila bootstrapper sudah berjalan lebih dulu dari adapter).</summary>
        private void SyncFromGame()
        {
            CycleManager cm = CycleManager.Instance;
            if (cm == null) return;
            ApplyEvent(cm.CurrentEvent);
            ApplyPhase(cm.CurrentPhase, false);
        }

        // ---------------- EventBus ----------------

        private void HandleCycleStart(int cycle)
        {
            if (!Active || _inMenu) return;
            _rain = false;
            _fullMoon = false;
            Resolve();
        }

        private void HandleEventStarted(GameEventType evt)
        {
            if (!Active || _inMenu) return;
            ApplyEvent(evt);
            Resolve();
        }

        private void HandlePhaseChanged(PhaseType phase)
        {
            if (!Active || _inMenu) return;
            ApplyPhase(phase, true);
        }

        private void HandleGameEvaluated(bool isWin)
        {
            if (!Active || _inMenu) return;

            _evaluated = true;
            if (AudioListener.pause) _manager.SetPaused(false); // Win/Lose tidak boleh membisukan audio
            Resolve();
            _manager.PlaySFX(isWin ? winCue : loseCue);
        }

        private void HandlePauseChanged(bool paused)
        {
            _pauseValue = paused;
            _pausePending = true;
        }

        // ---------------- Logika ----------------

        private void ApplyEvent(GameEventType evt)
        {
            _rain = evt == GameEventType.Rain;
            _fullMoon = evt == GameEventType.FullMoon;
        }

        private void ApplyPhase(PhaseType phase, bool allowStinger)
        {
            switch (phase)
            {
                case PhaseType.DayPhase:
                    _phase = Phase.Day;
                    break;
                case PhaseType.NightPhase:
                    _phase = Phase.Night;
                    if (allowStinger && _fullMoon && !_evaluated) _manager.PlaySFX(fullMoonCue);
                    break;
                default:
                    return; // fase lain tidak relevan untuk audio
            }
            Resolve();
        }

        /// <summary>Prioritas: Menu > Win/Lose (BGM siang) > Hujan > Fase (Siang/Malam). Permintaan digabung per frame oleh BGMController.</summary>
        private void Resolve()
        {
            if (!Active) return;
            BGMController bgm = _manager.BGM;

            if (_inMenu) { bgm.SetSlot(BGMSlot.MainMenu); return; }
            if (_evaluated) { bgm.SetSlot(BGMSlot.Day); return; }
            if (_rain) { bgm.SetSlot(BGMSlot.Rain); return; }

            switch (_phase)
            {
                case Phase.Day: bgm.SetSlot(BGMSlot.Day); break;
                case Phase.Night: bgm.SetSlot(BGMSlot.Night); break; // clip kosong = hening
                default: bgm.SetSilence(); break;
            }
        }
    }
}
