using UnityEngine;

/// <summary>
/// IconPanelToggle — toggle buka/tutup sebuah GameObject panel (misal slider volume)
/// lewat klik icon. Dipisah dari SoundSettingsController karena ini murni UI behavior,
/// bukan audio logic — supaya reusable untuk kasus toggle-panel lain di luar sound settings.
///
/// CARA PAKAI:
/// 1. Attach komponen ini ke GameObject icon (atau GameObject kosong di sebelahnya).
/// 2. Assign 'targetPanel' = GameObject slider yang mau ditampilkan/disembunyikan.
/// 3. Di Button icon, tambahkan OnClick() → drag GameObject ini → pilih TogglePanel().
///
/// PERILAKU "SELALU RESET SAAT HALAMAN SETTINGS DITUTUP":
/// Skrip ini harus berada di dalam hierarchy halaman settings (sebagai child langsung
/// atau tidak langsung dari GameObject halaman settings). Saat halaman settings di-
/// nonaktifkan (SetActive(false)), Unity otomatis memanggil OnDisable() di semua
/// komponen aktif dalam hierarchy tersebut — termasuk skrip ini — sehingga targetPanel
/// otomatis disembunyikan tanpa perlu kode tambahan di skrip pembuka/penutup halaman
/// settings itu sendiri. OnEnable() juga reset state, sebagai jaring pengaman kedua
/// (misal skrip ini di-enable manual tanpa lewat SetActive parent).
/// </summary>
public class IconPanelToggle : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("GameObject yang akan ditampilkan/disembunyikan saat icon diklik (misal slider panel).")]
    public GameObject targetPanel;

    [Header("State")]
    [Tooltip("Kondisi panel saat halaman ini pertama kali aktif. Biasanya false (tertutup).")]
    public bool startClosed = true;

    private bool _isOpen;

    // ─────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────

    private void OnEnable()
    {
        ResetToClosed();
    }

    private void OnDisable()
    {
        // Dipanggil otomatis saat halaman settings (parent) di-nonaktifkan.
        // Memastikan panel tidak "nyangkut" dalam kondisi terbuka untuk kunjungan berikutnya.
        ResetToClosed();
    }

    // ─────────────────────────────────────────────
    //  Public API — dipanggil dari Button OnClick()
    // ─────────────────────────────────────────────

    /// <summary>
    /// Buka jika sedang tertutup, tutup jika sedang terbuka.
    /// </summary>
    public void TogglePanel()
    {
        SetPanelOpen(!_isOpen);
    }

    public void OpenPanel()
    {
        SetPanelOpen(true);
    }

    public void ClosePanel()
    {
        SetPanelOpen(false);
    }

    // ─────────────────────────────────────────────
    //  Internal
    // ─────────────────────────────────────────────

    private void SetPanelOpen(bool open)
    {
        _isOpen = open;
        if (targetPanel != null)
            targetPanel.SetActive(open);
    }

    private void ResetToClosed()
    {
        SetPanelOpen(!startClosed);
    }
}