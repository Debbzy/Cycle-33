using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Cycle33.Audio
{
    /// <summary>
    /// Peredam BGM sebagai layer terpisah lewat exposed parameter mixer (default "BGMDuck", satuan dB),
    /// sehingga tidak bentrok dengan slider player (BGMVolume) maupun fade antar clip (AudioSource.volume).
    /// Beberapa peredam aktif bersamaan: yang terkuat berlaku (tidak dikali).
    /// </summary>
    public class DuckController : MonoBehaviour
    {
        [SerializeField] private AudioMixer mixer;
        [Tooltip("Exposed parameter pada child group BGM (mis. BGM_Duck).")]
        [SerializeField] private string duckParam = "BGMDuck";

        /// <summary>Pengali percepatan fade peredam (isi dengan kecepatan fast-forward).</summary>
        public float FadeSpeedScale { get; set; } = 1f;

        private readonly Dictionary<int, float> _tokens = new Dictionary<int, float>(); // token -> persen
        private int _nextToken = 1;

        private float _current = 1f; // pengali linear 1 = tanpa peredam
        private float _from = 1f;
        private float _to = 1f;
        private float _t;
        private float _dur;
        private bool _fading;

        private void Start()
        {
            ApplyToMixer(1f); // SetFloat di Awake sering diabaikan mixer; pakai Start.
        }

        // ---------------- API publik ----------------

        /// <summary>Mulai peredam dan tahan sampai Release(). Mengembalikan token.</summary>
        public int Acquire(float percent, float attackSec)
        {
            int id = _nextToken++;
            _tokens[id] = Mathf.Clamp(percent, 0f, 100f);
            Retarget(attackSec);
            return id;
        }

        public void Release(int token, float releaseSec)
        {
            if (_tokens.Remove(token)) Retarget(releaseSec);
        }

        public void ReleaseAll(float releaseSec)
        {
            if (_tokens.Count == 0) return;
            _tokens.Clear();
            Retarget(releaseSec);
        }

        /// <summary>Redam selama holdSec (waktu real-time yang tidak berjalan saat AudioListener.pause), lalu lepas.</summary>
        public Coroutine DuckForDuration(float percent, float holdSec, float attackSec, float releaseSec)
        {
            return StartCoroutine(DuckRoutine(percent, holdSec, attackSec, releaseSec));
        }

        // ---------------- Internal ----------------

        private IEnumerator DuckRoutine(float percent, float holdSec, float attackSec, float releaseSec)
        {
            int token = Acquire(percent, attackSec);
            float elapsed = 0f;
            while (elapsed < holdSec)
            {
                if (!AudioListener.pause) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Release(token, releaseSec); // no-op bila sudah dilepas lewat ReleaseAll
        }

        private void Retarget(float durationSec)
        {
            float strongest = 0f;
            foreach (KeyValuePair<int, float> kv in _tokens)
                if (kv.Value > strongest) strongest = kv.Value;

            float targetMul = 1f - strongest / 100f;
            if (Mathf.Approximately(targetMul, _to)) return;

            _from = _current;
            _to = targetMul;
            _t = 0f;
            _dur = AudioMath.ScaledFadeSec(durationSec, FadeSpeedScale);

            if (_dur <= 0f)
            {
                _current = _to;
                _fading = false;
                ApplyToMixer(_current);
            }
            else
            {
                _fading = true;
            }
        }

        private void Update()
        {
            if (!_fading) return;

            _t += Time.unscaledDeltaTime / _dur;
            if (_t >= 1f)
            {
                _current = _to;
                _fading = false;
            }
            else
            {
                _current = Mathf.Lerp(_from, _to, _t);
            }
            ApplyToMixer(_current);
        }

        private void ApplyToMixer(float multiplier)
        {
            if (mixer == null) return;
            mixer.SetFloat(duckParam, AudioMath.LinearToDb(multiplier));
        }
    }
}
