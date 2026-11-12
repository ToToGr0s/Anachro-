using Steamworks;
using UnityEngine;

namespace Anachronism.Core
{
    /// <summary>
    /// Initialise l'API Steam et pompe ses callbacks. FishySteamworks ne le fait pas lui-même.
    /// Sans Steam (IsInitialized = false), le jeu se lance mais on ne peut ni créer ni rejoindre de partie :
    /// le menu le signale. Steam doit être lancé avant le jeu, les callbacks ne sont pas recréés ensuite.
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

            UseAppIdNextToMacApp();

            try
            {
                ESteamAPIInitResult result = SteamAPI.InitEx(out string errorMessage);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Debug.LogWarning($"[Steam] Non initialisé ({result}) : {errorMessage}. Steam est-il lancé ?");
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

        // Steamworks cherche steam_appid.txt dans le dossier courant, qui vaut « / » pour une app lancée depuis le Finder.
        // Le zip publié le met à côté d'Anachro.app : on passe l'AppID par la variable d'environnement que Steamworks lit aussi.
        // Sans fichier (jeu lancé par Steam avec le vrai AppID), rien ne change.
        private static void UseAppIdNextToMacApp()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            // dataPath = Anachro.app/Contents : le fichier est deux dossiers plus haut.
            string path = System.IO.Path.Combine(Application.dataPath, "..", "..", "steam_appid.txt");
            if (!System.IO.File.Exists(path))
                return;

            string appId = System.IO.File.ReadAllText(path).Trim();
            System.Environment.SetEnvironmentVariable("SteamAppId", appId);
            System.Environment.SetEnvironmentVariable("SteamGameId", appId);
#endif
        }
    }
}
