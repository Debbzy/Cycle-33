using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Cycle33.Audio
{
    /// <summary>
    /// Pool AudioSource untuk SFX sekali-putar. Dua pool: World (ikut pause) dan UI (tetap bunyi saat pause).
    /// Tiap pemutaran memakai Play() pada source sendiri, sehingga pitch acak tidak mengubah suara lain.
    /// </summary>
    public class SFXPlayer : MonoBehaviour
    {
        [Header("Pool")]
        [SerializeField, Min(1)] private int worldPoolSize = 16;
        [SerializeField, Min(1)] private int uiPoolSize = 4;

        [Header("Default Mixer Group per Kategori")]
        [SerializeField] private AudioMixerGroup worldGroup;
        [SerializeField] private AudioMixerGroup uiGroup;

        private class Voice
        {
            public AudioSource source;
            public SFXCue cue;
            public float startTime;
        }

        private class Pool
        {
            public Voice[] voices;
            public int next;
            public AudioMixerGroup defaultGroup;
        }

        private Pool _world;
        private Pool _ui;
        private readonly Dictionary<SFXCue, float> _lastPlayTime = new Dictionary<SFXCue, float>();
        private readonly Dictionary<SFXCue, int> _lastClipIndex = new Dictionary<SFXCue, int>();

        private void Awake()
        {
            _world = BuildPool("World", worldPoolSize, worldGroup, false);
            _ui = BuildPool("UI", uiPoolSize, uiGroup, true);
        }

        private Pool BuildPool(string label, int size, AudioMixerGroup group, bool ignoreListenerPause)
        {
            Transform root = new GameObject("SFXPool_" + label).transform;
            root.SetParent(transform, false);

            var pool = new Pool { voices = new Voice[size], defaultGroup = group };
            for (int i = 0; i < size; i++)
            {
                var go = new GameObject(label + "_" + i);
                go.transform.SetParent(root, false);

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f; // 2D. Spatial 3D ditambahkan di tahap A4.
                src.outputAudioMixerGroup = group;
                src.ignoreListenerPause = ignoreListenerPause;

                pool.voices[i] = new Voice { source = src };
            }
            return pool;
        }

        /// <summary>
        /// Putar cue. Mengembalikan AudioSource yang dipakai, atau null bila ditolak
        /// (cooldown, maxVoices, pool penuh, atau clip kosong).
        /// <paramref name="position"/> disiapkan untuk tahap 3D (A4), saat ini belum dipakai.
        /// </summary>
        public AudioSource Play(SFXCue cue, Vector3? position = null, float volumeScale = 1f)
        {
            if (cue == null || cue.clips == null || cue.clips.Length == 0) return null;
            if (_world == null || _ui == null) return null;
            if (cue.category == SFXCategory.World && AudioListener.pause) return null; // sedang pause

            float now = Time.unscaledTime;
            if (cue.cooldownSec > 0f && _lastPlayTime.TryGetValue(cue, out float last) && now - last < cue.cooldownSec)
                return null;

            Pool pool = cue.category == SFXCategory.UI ? _ui : _world;
            if (cue.maxVoices > 0 && CountActive(pool, cue) >= cue.maxVoices) return null;

            AudioClip clip = PickClip(cue);
            if (clip == null) return null;

            Voice voice = AcquireVoice(pool, cue.priority);
            if (voice == null) return null;

            AudioSource src = voice.source;
            src.clip = clip;
            src.volume = Mathf.Clamp01(cue.volume * volumeScale);
            src.pitch = Mathf.Clamp(cue.basePitch + Random.Range(-cue.pitchRandomRange, cue.pitchRandomRange), 0.1f, 3f);
            src.priority = cue.priority;
            src.outputAudioMixerGroup = cue.mixerGroup != null ? cue.mixerGroup : pool.defaultGroup;
            src.Play();

            voice.cue = cue;
            voice.startTime = now;
            _lastPlayTime[cue] = now;
            return src;
        }

        public void StopAll()
        {
            StopPool(_world);
            StopPool(_ui);
        }

        public void StopAll(SFXCategory category)
        {
            StopPool(category == SFXCategory.UI ? _ui : _world);
        }

        // ---------------------------------------------------------------

        private static void StopPool(Pool pool)
        {
            if (pool == null) return;
            foreach (Voice v in pool.voices)
            {
                v.source.Stop();
                v.cue = null;
            }
        }

        private static int CountActive(Pool pool, SFXCue cue)
        {
            int count = 0;
            foreach (Voice v in pool.voices)
                if (v.cue == cue && v.source.isPlaying) count++;
            return count;
        }

        private static Voice AcquireVoice(Pool pool, int priority)
        {
            int n = pool.voices.Length;

            // 1) Cari slot kosong, mulai dari giliran berikutnya (round-robin).
            for (int i = 0; i < n; i++)
            {
                int idx = (pool.next + i) % n;
                Voice v = pool.voices[idx];
                if (!v.source.isPlaying)
                {
                    pool.next = (idx + 1) % n;
                    return v;
                }
            }

            // 2) Pool penuh: curi voice tertua yang prioritasnya sama atau lebih rendah.
            Voice oldest = null;
            foreach (Voice v in pool.voices)
            {
                if (v.source.priority < priority) continue; // angka lebih kecil = lebih penting
                if (oldest == null || v.startTime < oldest.startTime) oldest = v;
            }
            if (oldest != null) oldest.source.Stop();
            return oldest;
        }

        private AudioClip PickClip(SFXCue cue)
        {
            int n = cue.clips.Length;
            int idx;
            if (n == 1)
            {
                idx = 0;
            }
            else if (_lastClipIndex.TryGetValue(cue, out int lastIdx))
            {
                idx = (lastIdx + 1 + Random.Range(0, n - 1)) % n; // acak, selain lastIdx
            }
            else
            {
                idx = Random.Range(0, n);
            }
            _lastClipIndex[cue] = idx;
            return cue.clips[idx];
        }
    }
}
