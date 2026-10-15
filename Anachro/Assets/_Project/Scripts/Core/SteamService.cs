using Steamworks;
using UnityEngine;

namespace Anachronism.Core
{
    /// <summary>
    /// Initialise l'API Steam et pompe ses callbacks. FishySteamworks ne le fait pas lui-même.
    /// Le jeu tourne sans Steam (IsInitialized = false) pour garder le test local via Tugboat.
    /// </summary>
    public sealed class SteamService : MonoBehaviour
    {
        public static SteamService Instance { get; private set; }

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            IsInitialized = TryInitialize();
        }

        private void Update()
        {
            if (IsInitialized)
                SteamAPI.RunCallbacks();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            if (IsInitialized)
                SteamAPI.Shutdown();

            IsInitialized = false;
            Instance = null;
        }

        private static bool TryInitialize()
        {
            // Mauvaise version de la DLL native : SteamAPI planterait plus loin sans message clair.
            if (!Packsize.Test() || !DllCheck.Test())
            {
                Debug.LogError("[Steam] DLL Steamworks incompatible avec Steamworks.NET.");
                return false;
            }

            try
            {
                ESteamAPIInitResult result = SteamAPI.InitEx(out string errorMessage);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Debug.LogWarning($"[Steam] Non initialisé ({result}) : {errorMessage}. Steam est-il lancé ? Test local via Tugboat seulement.");
                    return false;
                }
            }
            catch (System.DllNotFoundException exception)
            {
                Debug.LogError($"[Steam] steam_api64 introuvable : {exception.Message}");
                return false;
            }

            return true;
        }
    }
}
