using System.Collections.Generic;
using UnityEngine;

namespace Cycle33.Audio
{
    /// <summary>
    /// Pintu masuk audio yang persisten (DontDestroyOnLoad). Menyatukan SFXPlayer, BGMController, DuckController.
    /// Pasang di GameObject ROOT (bukan child) di scene pertama, mis. main menu. Duplikat di scene lain otomatis dihancurkan.
    /// </summary>
    [RequireComponent(typeof(SFXPlayer), typeof(BGMController), typeof(DuckController))]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public SFXPlayer SFX { get; private set; }
        public BGMController BGM { get; private set; }
        public DuckController Duck { get; private set; }

        private readonly List<int> _heldDucks = new List<int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SFX = GetComponent<SFXPlayer>();
            BGM = GetComponent<BGMController>();
            Duck = GetComponent<DuckController>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------- SFX ----------------

        /// <summary>
        /// Putar SFX. Jika cue.duckBGM aktif, BGM diredam sesuai pengaturan cue
        /// (selama durasi clip, atau ditahan sampai ReleaseHeldDucks/ReleaseAllDucks).
        /// </summary>
        public AudioSource PlaySFX(SFXCue cue, Vector3? position = null, float volumeScale = 1f)
        {
            AudioSource src = SFX.Play(cue, position, volumeScale);
            if (src == null || !cue.duckBGM) return src;

            if (cue.duckHold == DuckHold.ByClipDuration)
            {
                float length = src.clip.length / Mathf.Max(0.1f, Mathf.Abs(src.pitch));
                Duck.DuckForDuration(cue.duckPercent, length, cue.duckAttackSec, cue.duckReleaseSec);
            }
            else
            {
                _heldDucks.Add(Duck.Acquire(cue.duckPercent, cue.duckAttackSec));
            }
            return src;
        }

        public void StopAllSFX()
        {
            SFX.StopAll();
        }

        // ---------------- BGM ----------------

        public void SetBGM(BGMSlot slot) { BGM.SetSlot(slot); }
        public void SetBGMSilence() { BGM.SetSilence(); }

        // ---------------- Duck ----------------

        /// <summary>Lepas peredam bertipe UntilReleased (Win/Lose).</summary>
        public void ReleaseHeldDucks(float releaseSec = 0.5f)
        {
            foreach (int token in _heldDucks) Duck.Release(token, releaseSec);
            _heldDucks.Clear();
        }

        /// <summary>Lepas semua peredam, termasuk yang berbasis durasi.</summary>
        public void ReleaseAllDucks(float releaseSec = 0.5f)
        {
            _heldDucks.Clear();
            Duck.ReleaseAll(releaseSec);
        }

        // ---------------- Fast-forward ----------------

        /// <summary>Teruskan kecepatan fast-forward agar durasi fade ikut dipercepat. Lagu tidak ikut dipercepat.</summary>
        public void SetFadeSpeedScale(float scale)
        {
            BGM.FadeSpeedScale = scale;
            Duck.FadeSpeedScale = scale;
        }
    }
}
