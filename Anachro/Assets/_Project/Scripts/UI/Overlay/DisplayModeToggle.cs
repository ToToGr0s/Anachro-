using UnityEngine;
using UnityEngine.InputSystem;

namespace Anachronism.UI
{
    /// <summary>
    /// F11 : plein écran ↔ fenêtre. Le jeu démarre fenêtré (Player Settings) pour lancer deux instances côte à côte.
    /// </summary>
    public sealed class DisplayModeToggle : MonoBehaviour
    {
        [SerializeField] private Vector2Int _windowSize = new Vector2Int(1280, 720);

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f11Key.wasPressedThisFrame)
                return;

            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                Resolution native = Screen.currentResolution;
                Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(_windowSize.x, _windowSize.y, FullScreenMode.Windowed);
            }
        }
    }
}
