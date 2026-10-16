using UnityEditor;
using UnityEditor.SceneManagement;

namespace Anachronism.Editor
{
    /// <summary>
    /// Play démarre toujours sur Boot, quelle que soit la scène ouverte : sans Boot, pas de NetworkManager
    /// ni de services, et la map ou le menu lancés seuls ne marchent pas. On peut garder la map ouverte pour le LD.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBoot
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        static PlayFromBoot()
        {
            // delayCall : pendant le chargement de l'éditeur, l'AssetDatabase n'est pas encore prête.
            EditorApplication.delayCall += () =>
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
        }
    }
}
