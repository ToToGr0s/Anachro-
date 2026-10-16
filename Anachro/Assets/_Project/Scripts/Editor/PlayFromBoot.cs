using UnityEditor;
using UnityEditor.SceneManagement;

namespace Anachronism.Editor
{
    /// <summary>
    /// Option par poste (EditorPrefs), désactivée par défaut : chacun choisit s'il veut que Play démarre sur Boot.
    /// Activée, Play part de Boot quelle que soit la scène ouverte : sans Boot, pas de NetworkManager ni de services,
    /// donc la map ou le menu lancés seuls ne marchent pas en réseau. On peut garder la map ouverte pour le LD.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBoot
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string MenuPath = "Anachro/Play depuis Boot";
        // Préfixé par le projet : les EditorPrefs sont partagées entre tous les projets Unity du poste.
        private const string PrefKey = "Anachro.PlayFromBoot.IsEnabled";

        private static bool IsEnabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static PlayFromBoot()
        {
            // delayCall : pendant le chargement de l'éditeur, l'AssetDatabase n'est pas encore prête.
            EditorApplication.delayCall += Apply;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            IsEnabled = !IsEnabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, IsEnabled);
            // Changer la scène de départ en plein Play n'aurait d'effet qu'au prochain lancement : on évite la confusion.
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void Apply()
        {
            EditorSceneManager.playModeStartScene = IsEnabled
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath)
                : null;
        }
    }
}
