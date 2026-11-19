using UnityEngine;

namespace Cycle33.Core
{
    /// <summary>
    /// Pasang di GameObject mana pun, lalu hubungkan GameQuitter.QuitGame()
    /// ke OnClick tombol Quit. Berfungsi di Editor maupun build.
    /// </summary>
    public class GameQuitter : MonoBehaviour
    {
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
