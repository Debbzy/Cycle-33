using UnityEngine;

namespace Cycle33.Audio
{
    /// <summary>Helper kecil bersama: konversi dB dan durasi fade yang ikut fast-forward.</summary>
    internal static class AudioMath
    {
        public const float SilenceDb = -80f;
        public const float MinScaledFadeSec = 0.3f;

        public static float LinearToDb(float linear)
        {
            if (linear <= 0.0001f) return SilenceDb;
            return Mathf.Max(SilenceDb, 20f * Mathf.Log10(linear));
        }

        /// <summary>
        /// Durasi fade = baseSec / speedScale, tapi tidak kurang dari MinScaledFadeSec
        /// (dan tidak pernah lebih panjang dari baseSec). baseSec = 0 tetap 0.
        /// </summary>
        public static float ScaledFadeSec(float baseSec, float speedScale)
        {
            if (baseSec <= 0f) return 0f;
            float scale = Mathf.Max(1f, speedScale);
            return Mathf.Min(baseSec, Mathf.Max(MinScaledFadeSec, baseSec / scale));
        }
    }
}
