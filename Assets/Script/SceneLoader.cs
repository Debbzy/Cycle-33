using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cycle33.Core
{
    /// <summary>
    /// Pasang di GameObject mana pun, lalu hubungkan SceneLoader.LoadScene(string)
    /// ke OnClick tombol dan isi nama scene di kolom string pada Inspector.
    /// Scene harus sudah terdaftar di File > Build Settings (Scenes In Build).
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SceneLoader] Nama scene kosong.", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' tidak ditemukan. Cek ejaan dan Build Settings.", this);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
