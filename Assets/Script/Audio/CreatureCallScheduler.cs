using System.Collections.Generic;
using UnityEngine;

namespace Cycle33.Audio
{
    /// <summary>
    /// Memutar suara hewan secara acak: tiap 5-10 detik (real-time) pilih SATU spesies yang masih ada di habitat
    /// dan sedang "bangun", lalu satu individu acak dari spesies itu memutar suaranya sekali.
    ///
    /// Spesies "masih ada" = ada SoundActor terdaftar (joinRandomCalls) dengan speciesId tersebut.
    /// Spesies "bangun" dihitung dari jadwal di bawah (siang/malam/Full Moon/Rain), bukan dari state individu.
    /// Jadwal ini menyalin aturan tidur dan Frozen di GDD; ubah di sini bila aturan GDD berubah.
    ///
    /// Pasang di GameObject yang sama dengan AudioManager dan AudioEventAdapter.
    /// </summary>
    [RequireComponent(typeof(AudioManager), typeof(AudioEventAdapter))]
    public class CreatureCallScheduler : MonoBehaviour
    {
        [System.Serializable]
        public class SpeciesRule
        {
            [Tooltip("Harus sama persis dengan speciesId di SoundActor.")]
            public string speciesId;
            public bool activeDay = true;
            public bool activeNight = false;
            [Tooltip("Aktif di malam hari hanya saat Full Moon (mis. Wolf).")]
            public bool activeNightOnFullMoon = false;
            [Tooltip("Diam saat Rain (predator Frozen).")]
            public bool silentDuringRain = false;
        }

        [Header("Jadwal Spesies")]
        [SerializeField] private SpeciesRule[] rules =
        {
            new SpeciesRule { speciesId = "Sheep",   activeDay = true },
            new SpeciesRule { speciesId = "Chicken", activeDay = true },
            new SpeciesRule { speciesId = "Wolf",    activeDay = true,  activeNightOnFullMoon = true, silentDuringRain = true },
            new SpeciesRule { speciesId = "Civet",   activeDay = false, activeNight = true,           silentDuringRain = true },
        };

        [Header("Interval (detik real-time, tidak ikut fast-forward)")]
        [SerializeField, Min(0.1f)] private float minIntervalSec = 5f;
        [SerializeField, Min(0.1f)] private float maxIntervalSec = 10f;
        [Tooltip("Jeda cek ulang bila belum ada spesies yang layak.")]
        [SerializeField, Min(0.1f)] private float retrySec = 1f;

        [Header("Pemilihan")]
        [Tooltip("Hindari spesies yang sama dua kali berturut-turut bila ada spesies lain yang layak.")]
        [SerializeField] private bool avoidRepeatSpecies = true;

        // ---------------- Registry (statik, diisi SoundActor) ----------------

        private static readonly Dictionary<string, List<SoundActor>> Registry = new Dictionary<string, List<SoundActor>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Registry.Clear();
        }

        internal static void Register(SoundActor actor)
        {
            if (actor == null || string.IsNullOrEmpty(actor.SpeciesId)) return;
            if (!Registry.TryGetValue(actor.SpeciesId, out List<SoundActor> list))
            {
                list = new List<SoundActor>();
                Registry.Add(actor.SpeciesId, list);
            }
            if (!list.Contains(actor)) list.Add(actor);
        }

        internal static void Unregister(SoundActor actor)
        {
            if (actor == null || string.IsNullOrEmpty(actor.SpeciesId)) return;
            if (Registry.TryGetValue(actor.SpeciesId, out List<SoundActor> list)) list.Remove(actor);
        }

        private static int CountOf(string speciesId)
        {
            return Registry.TryGetValue(speciesId, out List<SoundActor> list) ? list.Count : 0;
        }

        // ---------------- Penjadwalan ----------------

        private AudioManager _manager;
        private AudioEventAdapter _adapter;
        private readonly List<SpeciesRule> _eligible = new List<SpeciesRule>();
        private string _lastSpecies;
        private float _timer;

        private void Awake()
        {
            _manager = GetComponent<AudioManager>();
            _adapter = GetComponent<AudioEventAdapter>();
        }

        private void Start()
        {
            _timer = RandomInterval();
        }

        private void Update()
        {
            if (_manager == null || AudioManager.Instance != _manager) return;

            // Di luar gameplay atau saat pindah scene: timer diulang, jadi suara pertama muncul 5-10 dtk setelah mulai.
            if (!_adapter.IsGameplay || _manager.IsSceneChanging)
            {
                _timer = RandomInterval();
                return;
            }

            // Pause dan Win/Lose: timer dibekukan (lanjut dari sisa waktu).
            if (_manager.IsPaused || _adapter.IsEvaluated) return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;

            _timer = TryPlayOne() ? RandomInterval() : retrySec;
        }

        private float RandomInterval()
        {
            float lo = Mathf.Min(minIntervalSec, maxIntervalSec);
            float hi = Mathf.Max(minIntervalSec, maxIntervalSec);
            return Random.Range(lo, hi);
        }

        private bool IsRuleActive(SpeciesRule r)
        {
            if (r.silentDuringRain && _adapter.IsRain) return false;
            if (_adapter.IsDay) return r.activeDay;
            if (_adapter.IsNight) return r.activeNight || (r.activeNightOnFullMoon && _adapter.IsFullMoon);
            return false;
        }

        private bool TryPlayOne()
        {
            _eligible.Clear();
            if (rules != null)
            {
                foreach (SpeciesRule r in rules)
                {
                    if (r == null || string.IsNullOrEmpty(r.speciesId)) continue;
                    if (!IsRuleActive(r)) continue;
                    if (CountOf(r.speciesId) == 0) continue;
                    _eligible.Add(r);
                }
            }
            if (_eligible.Count == 0) return false;

            if (avoidRepeatSpecies && _lastSpecies != null && _eligible.Count > 1)
            {
                for (int i = _eligible.Count - 1; i >= 0; i--)
                    if (_eligible[i].speciesId == _lastSpecies) _eligible.RemoveAt(i);
            }

            SpeciesRule pick = _eligible[Random.Range(0, _eligible.Count)];
            List<SoundActor> actors = Registry[pick.speciesId];
            if (actors.Count == 0) return false;

            SoundActor actor = actors[Random.Range(0, actors.Count)];
            if (actor == null) return false;

            _lastSpecies = pick.speciesId;
            actor.PlayRandomCall(); // bila ditolak limiter cue, giliran ini terlewat; tidak masalah
            return true;
        }
    }
}
