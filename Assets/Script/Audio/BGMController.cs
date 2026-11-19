using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Cycle33.Audio
{
    /// <summary>Urutan enum ini HARUS sama dengan urutan array track di BGMController.</summary>
    public enum BGMSlot { MainMenu = 0, Day = 1, Night = 2, Rain = 3 }

    /// <summary>
    /// Mengatur BGM per slot. Satu AudioSource per slot. Transisi berurutan:
    /// fade out semua yang terdengar ke 0 (-80 dB) -> ganti -> fade in dari 0 ke volume default slot.
    /// Permintaan slot dikumpulkan per frame (yang terakhir menang) agar beberapa event dalam satu frame tidak memicu transisi ganda.
    /// Slot dengan clip kosong dianggap hening.
    /// </summary>
    public class BGMController : MonoBehaviour
    {
        [System.Serializable]
        public class Track
        {
            [Tooltip("Kosong = hening.")]
            public AudioClip clip;
            [Tooltip("Volume default (0-1) yang dituju setelah fade in.")]
            [Range(0f, 1f)] public float volume = 0.8f;
        }

        [Header("Tracks")]
        [SerializeField] private Track mainMenu = new Track();
        [SerializeField] private Track day = new Track();
        [Tooltip("Dikosongkan untuk saat ini (malam hening). Isi clip di sini jika kelak ada BGM malam.")]
        [SerializeField] private Track night = new Track();
        [SerializeField] private Track rain = new Track();

        [Header("Mixer")]
        [Tooltip("Group tempat semua source BGM diputar (disarankan child 'BGM_Duck' dari group BGM).")]
        [SerializeField] private AudioMixerGroup bgmGroup;

        [Header("Fade (detik, real-time)")]
        [SerializeField, Min(0f)] private float fadeOutSec = 1.5f;
        [SerializeField, Min(0f)] private float fadeInSec = 1.5f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>Pengali percepatan fade (isi dengan kecepatan fast-forward). 1 = normal.</summary>
        public float FadeSpeedScale { get; set; } = 1f;

        public bool IsTransitioning => _busy;

        /// <summary>Slot tujuan saat ini; null = hening.</summary>
        public BGMSlot? TargetSlot => _target >= 0 ? (BGMSlot?)_target : null;

        private Track[] _tracks;
        private AudioSource[] _sources;
        private bool[] _active;

        private int _desired = -1;   // -1 = hening
        private int _target = -2;    // -2 = belum pernah diproses
        private bool _dirty;
        private bool _exiting;
        private Coroutine _routine;
        private bool _busy;

        private void Awake()
        {
            _tracks = new[] { mainMenu, day, night, rain };
            string[] names = System.Enum.GetNames(typeof(BGMSlot));
            _sources = new AudioSource[_tracks.Length];
            _active = new bool[_tracks.Length];

            for (int i = 0; i < _tracks.Length; i++)
            {
                var go = new GameObject("BGM_" + (i < names.Length ? names[i] : i.ToString()));
                go.transform.SetParent(transform, false);

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                src.volume = 0f;
                src.outputAudioMixerGroup = bgmGroup;
                _sources[i] = src;
            }
        }

        // ---------------- API publik ----------------

        /// <summary>Minta slot tertentu aktif. Slot yang sama dengan tujuan saat ini diabaikan (tidak restart).</summary>
        public void SetSlot(BGMSlot slot)
        {
            _desired = (int)slot; // tetap dicatat saat exit; diterapkan setelah ResumeRequests()
            _dirty = true;
        }

        /// <summary>Minta hening (fade out semua).</summary>
        public void SetSilence()
        {
            _desired = -1;
            _dirty = true;
        }

        /// <summary>
        /// Fade out semua BGM lalu berhenti. Permintaan slot yang masuk selama exit dicatat tapi baru diterapkan setelah ResumeRequests().
        /// Dipakai sebelum pindah scene. Bisa di-yield: yield return FadeOutAll(sec).
        /// </summary>
        public Coroutine FadeOutAll(float seconds)
        {
            if (_routine != null) StopCoroutine(_routine);
            _exiting = true;
            _dirty = false;
            _desired = -1;
            _target = -2;
            _routine = StartCoroutine(FadeOutAllRoutine(seconds));
            return _routine;
        }

        /// <summary>Buka kunci setelah FadeOutAll (panggil setelah scene baru dimuat).</summary>
        public void ResumeRequests()
        {
            _exiting = false;
        }

        // ---------------- Internal ----------------

        private void LateUpdate()
        {
            if (!_dirty || _exiting) return;
            _dirty = false;
            Apply(_desired);
        }

        private void Apply(int desired)
        {
            if (desired >= 0 && _tracks[desired].clip == null) desired = -1; // slot kosong = hening
            if (desired == _target) return;

            _target = desired;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(TransitionRoutine(desired));
        }

        private IEnumerator TransitionRoutine(int target)
        {
            _busy = true;
            yield return FadeOutActive(AudioMath.ScaledFadeSec(fadeOutSec, FadeSpeedScale));

            if (target >= 0)
            {
                AudioSource src = _sources[target];
                Track track = _tracks[target];

                src.clip = track.clip;
                src.volume = 0f;
                src.time = 0f;      // selalu mulai dari awal
                src.Play();
                _active[target] = true;

                float d = AudioMath.ScaledFadeSec(fadeInSec, FadeSpeedScale);
                for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
                {
                    src.volume = Mathf.Lerp(0f, track.volume, fadeCurve.Evaluate(t / d));
                    yield return null;
                }
                src.volume = track.volume;
            }

            _busy = false;
        }

        private IEnumerator FadeOutAllRoutine(float seconds)
        {
            _busy = true;
            yield return FadeOutActive(seconds);
            _busy = false;
        }

        /// <summary>Fade semua source yang aktif dari volume saat ini ke 0, lalu Stop().</summary>
        private IEnumerator FadeOutActive(float duration)
        {
            float[] from = new float[_sources.Length];
            bool any = false;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_active[i]) continue;
                from[i] = _sources[i].volume;
                any = true;
            }
            if (!any) yield break;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = fadeCurve.Evaluate(t / duration);
                for (int i = 0; i < _sources.Length; i++)
                    if (_active[i]) _sources[i].volume = Mathf.Lerp(from[i], 0f, k);
                yield return null;
            }

            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_active[i]) continue;
                _sources[i].volume = 0f;
                _sources[i].Stop();
                _active[i] = false;
            }
        }
    }
}
