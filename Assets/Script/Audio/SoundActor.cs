using System.Collections.Generic;
using UnityEngine;

namespace Cycle33.Audio
{
    /// <summary>
    /// Dipasang di hewan/objek yang bisa bersuara. Memetakan "aksi" (Eat, Die, Born, ...) ke SFXCue lewat Inspector,
    /// sehingga skrip gameplay lain tidak perlu tahu clip apa pun.
    ///
    /// Cara pakai:
    /// - Animation Event: panggil fungsi PlayFromAnimation dengan parameter string = actionKey.
    ///   (Komponen ini harus berada di GameObject yang SAMA dengan Animator.)
    /// - UnityEvent / skrip lain: Play("Eat") atau PlayCue(cue).
    /// </summary>
    [DisallowMultipleComponent]
    public class SoundActor : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("Nama aksi, mis. Eat, Die, Born, Mate, Lightning. Harus sama dengan string di Animation Event.")]
            public string actionKey;
            public SFXCue cue;
            [Tooltip("Pengali volume khusus entry ini (dikali volume di SFXCue).")]
            [Range(0f, 1f)] public float volumeScale = 1f;
        }

        [SerializeField] private Entry[] entries;

        private Dictionary<string, Entry> _map;
        private readonly HashSet<string> _warned = new HashSet<string>();

        private void Awake()
        {
            BuildMap();
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

        /// <summary>Putar cue yang dipetakan ke actionKey.</summary>
        public void Play(string actionKey)
        {
            if (string.IsNullOrEmpty(actionKey)) return;
            if (_map == null) BuildMap();

            if (!_map.TryGetValue(actionKey, out Entry entry))
            {
                if (_warned.Add(actionKey))
                    Debug.LogWarning($"[SoundActor] actionKey '{actionKey}' belum dipetakan pada {name}.", this);
                return;
            }

            AudioManager am = AudioManager.Instance;
            if (am == null) return;
            am.PlaySFX(entry.cue, transform.position, entry.volumeScale);
        }

        /// <summary>Alias khusus untuk Animation Event (parameter string).</summary>
        public void PlayFromAnimation(string actionKey)
        {
            Play(actionKey);
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
