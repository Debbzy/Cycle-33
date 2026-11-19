using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cycle33.Audio
{
    /// <summary>
    /// Pintu masuk audio yang persisten (DontDestroyOnLoad). Menyatukan SFXPlayer, BGMController, DuckController.
    /// Pasang di GameObject ROOT (bukan child). Duplikat di scene lain otomatis dihancurkan,
    /// jadi aman menaruh prefab ini di setiap scene (berguna saat tes langsung dari scene gameplay).
    /// </summary>
    [RequireComponent(typeof(SFXPlayer), typeof(BGMController), typeof(DuckController))]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public SFXPlayer SFX { get; private set; }
        public BGMController BGM { get; private set; }
        public DuckController Duck { get; private set; }

        [Header("Scene Transition")]
        [Tooltip("Lama fade out BGM sebelum pindah scene (detik, real-time).")]
        [SerializeField, Min(0f)] private float sceneFadeSec = 1f;

        /// <summary>True saat pause menu aktif (AudioListener.pause). Tidak aktif saat panel Win/Lose.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>True selama fade out -> load -> sebelum scene baru siap.</summary>
        public bool IsSceneChanging { get; private set; }

        /// <summary>Kecepatan fast-forward terakhir (1 = normal).</summary>
        public float SpeedScale { get; private set; } = 1f;

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
        /// (selama durasi clip, atau ditahan sampai ReleaseHeldDucks/ReleaseAllDucks/pindah scene).
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

        // ---------------- Pause ----------------

        /// <summary>
        /// Pause audio: BGM dan SFX kategori World berhenti, SFX kategori UI tetap berbunyi.
        /// Dipanggil oleh AudioEventAdapter (pause menu), bukan untuk panel Win/Lose.
        /// </summary>
        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            AudioListener.pause = paused;
        }

        // ---------------- Fast-forward ----------------

        /// <summary>
        /// Teruskan kecepatan fast-forward. Saat ini dipakai untuk mempercepat durasi fade BGM dan peredam;
        /// lagu dan SFX tidak ikut dipercepat.
        /// </summary>
        public void SetSpeedScale(float scale)
        {
            SpeedScale = Mathf.Max(1f, scale);
            BGM.FadeSpeedScale = SpeedScale;
            Duck.FadeSpeedScale = SpeedScale;
        }

        // ---------------- Pindah scene ----------------

        /// <summary>
        /// Fade out BGM, lepas semua peredam, lalu load scene. beforeLoad dipanggil tepat sebelum load
        /// (mis. Time.timeScale = 1f). Abaikan bila pindah scene sedang berjalan.
        /// </summary>
        public void LoadSceneWithFade(string sceneName, System.Action beforeLoad = null)
        {
            if (IsSceneChanging) return;
            StartCoroutine(SceneRoutine(() => SceneManager.LoadSceneAsync(sceneName), beforeLoad));
        }

        public void LoadSceneWithFade(int buildIndex, System.Action beforeLoad = null)
        {
            if (IsSceneChanging) return;
            StartCoroutine(SceneRoutine(() => SceneManager.LoadSceneAsync(buildIndex), beforeLoad));
        }

        private IEnumerator SceneRoutine(System.Func<AsyncOperation> loader, System.Action beforeLoad)
        {
            IsSceneChanging = true;

            SFX.StopAll(SFXCategory.World); // UI (bunyi tombol) dibiarkan selesai
            SetPaused(false);               // agar fade BGM terdengar, bukan terjebak di status pause

            yield return BGM.FadeOutAll(sceneFadeSec);
            ReleaseAllDucks(0f);            // BGM sudah hening, jadi pelepasan instan tidak terdengar

            beforeLoad?.Invoke();

            AsyncOperation op = loader();
            if (op != null) yield return op;
            yield return null;              // beri satu frame agar sceneLoaded dan adapter selesai

            BGM.ResumeRequests();
            IsSceneChanging = false;
        }
    }
}
