using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// SoundSettingsController — mengatur volume BGM & SFX lewat Audio Mixer,
/// dan menyimpan preferensi player supaya persist antar sesi (buka-tutup game).
///
/// KENAPA TIDAK PAKAI SCRIPTABLEOBJECT:
/// ScriptableObject hanya menyimpan value selama aplikasi berjalan (runtime),
/// tidak otomatis tertulis ke disk. Untuk persist antar sesi kita pakai PlayerPrefs.
/// AudioMixer exposed parameter sendiri sudah berfungsi sebagai "live state" saat
/// runtime — begitu di-SetFloat, semua AudioSource yang di-route ke group itu
/// langsung berubah volumenya tanpa perlu AudioManager tahu-menahu soal settings ini.
///
/// SETUP DI INSPECTOR:
/// 1. Assign AudioMixer yang sama dengan yang dipakai AudioManager.
/// 2. Pastikan mixer punya 2 Exposed Parameter:
///    - "BGMVolume" → di-expose dari attenuation parent group BGM
///    - "SFXVolume" → di-expose dari attenuation parent group SFX
///    (Klik kanan attenuation slider di group Mixer window → Expose Parameter)
/// 3. Semua AudioSource BGM (bgmSource1/2 di AudioManager) route ke group BGM,
///    semua SoundEntry SFX route ke child group di bawah SFX.
/// 4. Assign slider BGM & SFX ke field di bawah (range slider: 0.0001 - 1, BUKAN 0 - 1,
///    karena konversi ke desibel pakai Log10 dan Log10(0) tidak terdefinisi).
/// 5. Taruh komponen ini di scene Settings / Main Menu (tidak perlu DontDestroyOnLoad,
///    karena persistensi sudah ditangani PlayerPrefs, bukan lewat objek ini).
/// </summary>
public class SoundSettingsController : MonoBehaviour
{
    [Header("Audio Mixer")]
    [Tooltip("AudioMixer yang sama dengan yang dipakai AudioManager.")]
    public AudioMixer audioMixer;

    [Tooltip("Nama Exposed Parameter untuk volume BGM di mixer.")]
    public string bgmExposedParam = "BGMVolume";

    [Tooltip("Nama Exposed Parameter untuk volume SFX di mixer.")]
    public string sfxExposedParam = "SFXVolume";

    [Header("UI Sliders")]
    [Tooltip("Slider untuk volume BGM. Set Min Value = 0.0001, Max Value = 1 di Inspector Slider.")]
    public Slider bgmSlider;

    [Tooltip("Slider untuk volume SFX. Set Min Value = 0.0001, Max Value = 1 di Inspector Slider.")]
    public Slider sfxSlider;

    [Header("PlayerPrefs Keys")]
    public string bgmPrefKey = "settings_bgm_volume";
    public string sfxPrefKey = "settings_sfx_volume";

    [Header("Default Value (0 - 1, linear)")]
    [Range(0.0001f, 1f)]
    public float defaultVolume = 1f;

    private const float MinDecibel = -80f;

    // ─────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────

    private void Start()
    {
        LoadAndApplySettings();

        if (bgmSlider != null)
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    private void OnDestroy()
    {
        if (bgmSlider != null)
            bgmSlider.onValueChanged.RemoveListener(SetBGMVolume);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(SetSFXVolume);
    }

    // ─────────────────────────────────────────────
    //  Load — dipanggil tiap kali settings screen dibuka / scene start
    // ─────────────────────────────────────────────

    /// <summary>
    /// Ambil value tersimpan dari PlayerPrefs, apply ke mixer, dan sinkronkan ke slider.
    /// Pakai SetValueWithoutNotify supaya assign value ke slider tidak memicu
    /// onValueChanged (yang akan menulis ulang ke PlayerPrefs secara tidak perlu).
    /// </summary>
    public void LoadAndApplySettings()
    {
        float bgmVolume = PlayerPrefs.GetFloat(bgmPrefKey, defaultVolume);
        float sfxVolume = PlayerPrefs.GetFloat(sfxPrefKey, defaultVolume);

        ApplyBGMVolumeToMixer(bgmVolume);
        ApplySFXVolumeToMixer(sfxVolume);

        if (bgmSlider != null)
            bgmSlider.SetValueWithoutNotify(bgmVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(sfxVolume);
    }

    // ─────────────────────────────────────────────
    //  Slider Callbacks — dipanggil otomatis saat player geser slider
    // ─────────────────────────────────────────────

    public void SetBGMVolume(float linearVolume)
    {
        ApplyBGMVolumeToMixer(linearVolume);
        PlayerPrefs.SetFloat(bgmPrefKey, linearVolume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float linearVolume)
    {
        ApplySFXVolumeToMixer(linearVolume);
        PlayerPrefs.SetFloat(sfxPrefKey, linearVolume);
        PlayerPrefs.Save();
    }

    // ─────────────────────────────────────────────
    //  Mixer Application
    // ─────────────────────────────────────────────

    private void ApplyBGMVolumeToMixer(float linearVolume)
    {
        audioMixer.SetFloat(bgmExposedParam, LinearToDecibel(linearVolume));
    }

    private void ApplySFXVolumeToMixer(float linearVolume)
    {
        audioMixer.SetFloat(sfxExposedParam, LinearToDecibel(linearVolume));
    }

    // ─────────────────────────────────────────────
    //  Utility — konversi linear (0-1, cocok untuk slider) ke desibel (mixer)
    // ─────────────────────────────────────────────

    private float LinearToDecibel(float linear)
    {
        if (linear <= 0.0001f)
            return MinDecibel;

        return Mathf.Log10(linear) * 20f;
    }
}