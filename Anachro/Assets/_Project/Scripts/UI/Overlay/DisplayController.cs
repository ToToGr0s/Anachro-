using Anachronism.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anachronism.UI
{
    /// <summary>
    /// Affichage : F11 bascule plein écran ↔ fenêtre (le jeu démarre fenêtré pour lancer deux instances côte à côte),
    /// et cadence limitée. Sans limite, une scène légère (lobby) tourne à des centaines d'images par seconde
    /// et sature CPU et GPU, surtout avec deux instances qui tournent même sans le focus (runInBackground).
    /// Fenêtre en arrière-plan : cadence réduite, sauf chez l'hôte.
    /// </summary>
    public sealed class DisplayController : MonoBehaviour
    {
        [SerializeField] private Vector2Int _windowSize = new Vector2Int(1280, 720);
        [Tooltip("Fenêtre au premier plan : synchro verticale (cadence de l'écran).")]
        [SerializeField, Range(0, 4)] private int _focusedVSyncCount = 1;
        [Tooltip("Fenêtre en arrière-plan (autre instance de test, alt-tab) : elle continue de tourner, mais au ralenti.")]
        [SerializeField, Min(10)] private int _backgroundFrameRate = 30;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            ApplyFrameRate(Application.isFocused);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            ApplyFrameRate(hasFocus);
        }

        private void ApplyFrameRate(bool hasFocus)
        {
            // L'hôte n'est jamais ralenti : ses PNJ et ses ticks saccaderaient chez tous les clients.
            bool isThrottled = !hasFocus && !HostTools.IsHost;

            // targetFrameRate n'est pris en compte que synchro verticale coupée.
            QualitySettings.vSyncCount = isThrottled ? 0 : _focusedVSyncCount;
            Application.targetFrameRate = isThrottled ? _backgroundFrameRate : -1;
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
