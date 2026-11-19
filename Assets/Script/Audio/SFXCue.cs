using UnityEngine;
using UnityEngine.Audio;

namespace Cycle33.Audio
{
    public enum SFXCategory { World, UI }

    public enum DuckHold
    {
        /// <summary>BGM diredam selama durasi clip SFX (mis. Full Moon).</summary>
        ByClipDuration,
        /// <summary>BGM diredam sampai dilepas manual / scene berganti (mis. Win, Lose).</summary>
        UntilReleased
    }

    /// <summary>
    /// Satu "resep" suara: clip, volume, pitch, routing, limiter, dan perilaku peredam BGM.
    /// Dibuat sebagai asset (Create > Cycle33 > Audio > SFX Cue), lalu di-drag ke SoundActor.
    /// </summary>
    [CreateAssetMenu(fileName = "SFXCue_New", menuName = "Cycle33/Audio/SFX Cue")]
    public class SFXCue : ScriptableObject
    {
        [Header("Clip")]
        [Tooltip("Satu atau lebih clip. Jika lebih dari satu, dipilih acak (tidak mengulang clip yang sama berturut-turut).")]
        public AudioClip[] clips;

        [Header("Volume & Pitch")]
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float basePitch = 1f;
        [Tooltip("Variasi acak +/- dari Base Pitch tiap kali diputar. 0 = tanpa variasi. Contoh suara hewan: 0.1")]
        [Range(0f, 1f)] public float pitchRandomRange = 0f;
        [Tooltip("Seberapa kuat pitch/kecepatan putar mengikuti fast-forward. 0 = tidak ikut. 1 = ikut penuh (6x = pitch x6, dibatasi 3). Disarankan sekitar 0.3 untuk suara hewan.")]
        [Range(0f, 1f)] public float pitchFollowSpeed = 0f;

        [Header("Routing")]
        [Tooltip("UI = tetap berbunyi saat pause (AudioListener.pause). World = ikut berhenti.")]
        public SFXCategory category = SFXCategory.World;
        [Tooltip("Opsional. Kosong = pakai group default kategori di SFXPlayer.")]
        public AudioMixerGroup mixerGroup;
        [Tooltip("Prioritas Unity: 0 = paling penting, 256 = paling tidak penting. Dipakai saat pool penuh.")]
        [Range(0, 256)] public int priority = 128;

        [Header("Limiter (cegah penumpukan saat populasi besar / fast-forward)")]
        [Tooltip("Jeda minimum antar pemutaran cue ini, dalam detik real-time. 0 = tanpa batas.")]
        [Min(0f)] public float cooldownSec = 0f;
        [Tooltip("Jumlah maksimum cue ini yang boleh berbunyi bersamaan. 0 = tanpa batas.")]
        [Min(0)] public int maxVoices = 0;

        [Header("BGM Override (SFX penimpa)")]
        public bool duckBGM = false;
        [Tooltip("Persen BGM yang diredam. 50 = volume BGM jadi 50%. 100 = hening total.")]
        [Range(0f, 100f)] public float duckPercent = 50f;
        public DuckHold duckHold = DuckHold.ByClipDuration;
        [Min(0f)] public float duckAttackSec = 0.5f;
        [Min(0f)] public float duckReleaseSec = 0.5f;
    }
}
