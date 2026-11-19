using System.Collections.Generic;
using UnityEngine;

namespace Cycle33.Audio
{
    /// <summary>
    /// Dipasang di hewan/objek yang bisa bersuara. Memetakan "aksi" (Voice, Eat, Die, ...) ke SFXCue lewat Inspector,
    /// sehingga skrip gameplay lain tidak perlu tahu clip apa pun.
    ///
    /// Jalur pemanggilan:
    /// - Animation Event: PlayFromAnimation(string actionKey). Komponen ini harus di GameObject yang SAMA dengan Animator.
    /// - UnityEvent / skrip lain: Play("Eat") atau PlayCue(cue).
    /// - Otomatis saat Instantiate / Destroy (opsi di bawah).
    /// - Suara acak spesies lewat CreatureCallScheduler (opsi di bawah).
    /// </summary>
    [DisallowMultipleComponent]
    public class SoundActor : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("Nama aksi, mis. Voice, Eat, Die. Harus sama dengan string di Animation Event / opsi di bawah.")]
            public string actionKey;
            public SFXCue cue;
            [Tooltip("Pengali volume khusus entry ini (dikali volume di SFXCue).")]
            [Range(0f, 1f)] public float volumeScale = 1f;
        }

        [SerializeField] private Entry[] entries;

        [Header("Spawn / Destroy (Instantiate / Destroy)")]
        [SerializeField] private bool playOnSpawn;
        [SerializeField] private string spawnKey = "Voice";
        [Tooltip("Spawn diabaikan selama N detik pertama setelah scene dimuat, agar objek awal scene tidak berbunyi serentak.")]
        [SerializeField, Min(0f)] private float spawnSilenceSec = 0.5f;
        [SerializeField] private bool playOnDestroy;
        [SerializeField] private string destroyKey = "Die";

        [Header("Suara Acak Spesies (CreatureCallScheduler)")]
        [SerializeField] private bool joinRandomCalls;
        [Tooltip("Harus sama persis dengan speciesId pada aturan CreatureCallScheduler, mis. Sheep.")]
        [SerializeField] private string speciesId;
        [Tooltip("Aksi yang diputar saat spesies ini terpilih. Biasanya sama dengan suara spawn.")]
        [SerializeField] private string randomCallKey = "Voice";

        public string SpeciesId => speciesId;

        private Dictionary<string, Entry> _map;
        private readonly HashSet<string> _warned = new HashSet<string>();
        private bool _registered;
        private bool _quitting;

        private void Awake()
        {
            BuildMap();
        }

        private void OnEnable()
        {
            if (joinRandomCalls && !string.IsNullOrEmpty(speciesId))
            {
                CreatureCallScheduler.Register(this);
                _registered = true;
            }

            if (playOnSpawn && Time.timeSinceLevelLoad >= spawnSilenceSec)
                TryPlay(spawnKey);
        }

        private void OnDisable()
        {
            if (!_registered) return;
            CreatureCallScheduler.Unregister(this);
            _registered = false;
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
        }

        private void OnDestroy()
        {
            if (!playOnDestroy || _quitting) return;
            if (!gameObject.scene.isLoaded) return; // scene sedang dibongkar

            AudioManager am = AudioManager.Instance;
            if (am == null || am.IsSceneChanging) return;

            TryPlay(destroyKey);
        }

        private void BuildMap()
        {
            _map = new Dictionary<string, Entry>();
            if (entries == null) return;
            foreach (Entry e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.actionKey) || e.cue == null) continue;
                if (_map.ContainsKey(e.actionKey))
                {
                    Debug.LogWarning($"[SoundActor] actionKey ganda '{e.actionKey}' pada {name}. Entry pertama dipakai.", this);
                    continue;
                }
                _map.Add(e.actionKey, e);
            }
        }

        private bool TryPlay(string actionKey)
        {
            if (string.IsNullOrEmpty(actionKey)) return false;
            if (_map == null) BuildMap();

            if (!_map.TryGetValue(actionKey, out Entry entry))
            {
                if (_warned.Add(actionKey))
                    Debug.LogWarning($"[SoundActor] actionKey '{actionKey}' belum dipetakan pada {name}.", this);
                return false;
            }

            AudioManager am = AudioManager.Instance;
            if (am == null) return false;
            return am.PlaySFX(entry.cue, transform.position, entry.volumeScale) != null;
        }

        /// <summary>Putar cue yang dipetakan ke actionKey.</summary>
        public void Play(string actionKey)
        {
            TryPlay(actionKey);
        }

        /// <summary>Alias khusus untuk Animation Event (parameter string).</summary>
        public void PlayFromAnimation(string actionKey)
        {
            TryPlay(actionKey);
        }

        /// <summary>Dipanggil CreatureCallScheduler. True bila suara benar-benar mulai diputar.</summary>
        public bool PlayRandomCall()
        {
            return TryPlay(randomCallKey);
        }

        /// <summary>Putar cue langsung tanpa pemetaan (mis. dari UnityEvent).</summary>
        public void PlayCue(SFXCue cue)
        {
            AudioManager am = AudioManager.Instance;
            if (am == null || cue == null) return;
            am.PlaySFX(cue, transform.position);
        }
    }
}
