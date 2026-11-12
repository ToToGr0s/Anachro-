using System.Collections.Generic;
using System.Linq;
using Anachronism.Network;
using Anachronism.UI;
using FishNet.Component.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism.Editor
{
    /// <summary>
    /// Pose les écrans d'Hanaé : écran titre (scène TitleScreen, scène hors-ligne de FishNet), menu principal (MainMenu),
    /// scène Lobby où « commencer » mène à l'accueil du lobby, et dans Boot l'écran de jeu, l'écran de fin et l'écran
    /// de chargement, chacun avec son pont vers le réseau. Rejouable : les objets posés ici sont recréés, rien d'autre
    /// n'est touché dans les scènes.
    /// </summary>
    public static class MenusBuilder
    {
        public const string TitleScenePath = "Assets/_Project/Scenes/TitleScreen.unity";
        public const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";
        private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string BootScenePath = "Assets/_Project/Scenes/Maps/Boot.unity";

        private const string MenusFolder = "Assets/_Project/Prefabs/UI/menus/";
        private const string PanelSettingsPath = "Assets/_Project/Prefabs/UI/AnachroPanelSettings.asset";
        private const string TipsPath = "Assets/_Project/Data/Tips/LoadingTips.txt";

        private const string TitleName = "UI_TitleScreen";
        private const string MainMenuName = "UI_MainMenu";
        private const string PlayMenuName = "UI_PlayMenu";
        private const string GameEndName = "UI_GameEnd";
        private const string LoadingName = "UI_Loading";
        private const string OverlayName = "Overlay";
        private const string OldHudName = "UI_Hud";

        // Au-dessus du lobby (0) ; le chargement couvre tout, y compris l'overlay de debug (10).
        private const int PlayMenuSortingOrder = 5;
        private const int GameEndSortingOrder = 6;
        private const int LoadingSortingOrder = 20;

        [MenuItem("Anachro/Setup/Poser les menus d'Hanaé")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildMenuScene(TitleScenePath, TitleName, "TitleScreen.uxml", typeof(TitleScreenScript), "scene", "MainMenu");
            BuildMenuScene(MainMenuScenePath, MainMenuName, "MainMenu.uxml", typeof(MainMenuScript), "playScene", "Lobby");
            BuildEmptyScene(LobbyScenePath);
            BuildBootMenus();
            AddBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[MenusBuilder] Menus d'Hanaé posés.");
        }

        /// <summary>Écrans de Boot ; appelé aussi par NetworkBaseBuilder quand il reconstruit Boot.</summary>
        public static void AddBootMenus(Scene boot)
        {
            DestroyRoot(boot, OldHudName);
            // Prefab supprimé : l'instance s'appelle alors « UI_Hud (Missing Prefab…) » et échappe à DestroyRoot.
            foreach (GameObject rootObject in boot.GetRootGameObjects().Where(PrefabUtility.IsPrefabAssetMissing))
                Object.DestroyImmediate(rootObject);

            // L'ancien menu pause vivait sur l'Overlay : son script supprimé y laisse un composant manquant.
            GameObject overlay = boot.GetRootGameObjects().FirstOrDefault(rootObject => rootObject.name == OverlayName);
            if (overlay != null)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(overlay);

            GameObject playMenu = CreateDocument(boot, PlayMenuName, "PlayMenu.uxml", PlayMenuSortingOrder);
            SetString(playMenu.AddComponent<InGameUI>(), "mainMenuScene", "TitleScreen");
            playMenu.AddComponent<InGameUIBridge>();

            GameObject gameEnd = CreateDocument(boot, GameEndName, "GameEndMenu.uxml", GameEndSortingOrder);
            SetString(gameEnd.AddComponent<VictoryMenu>(), "mainMenuScene", "TitleScreen");
            gameEnd.AddComponent<VictoryMenuBridge>();

            GameObject loading = CreateDocument(boot, LoadingName, "LoadingScreen.uxml", LoadingSortingOrder);
            LoadScreenScript loadScreen = loading.AddComponent<LoadScreenScript>();
            SerializedObject loadSettings = new SerializedObject(loadScreen);
            loadSettings.FindProperty("tipsFile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(TipsPath);
            loadSettings.ApplyModifiedPropertiesWithoutUndo();
            loading.AddComponent<LoadingScreenBridge>();

            // Écran titre au lancement ; à la déconnexion, OfflineSceneRouter choisit titre (après une partie) ou lobby.
            foreach (DefaultScene defaultScene in boot.GetRootGameObjects().SelectMany(rootObject => rootObject.GetComponentsInChildren<DefaultScene>(true)))
            {
                defaultScene.SetOfflineScene(TitleScenePath);
                EditorUtility.SetDirty(defaultScene);
                if (!defaultScene.TryGetComponent(out OfflineSceneRouter _))
                    defaultScene.gameObject.AddComponent<OfflineSceneRouter>();
            }
        }

        private static void BuildBootMenus()
        {
            Scene boot = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            AddBootMenus(boot);
            EditorSceneManager.MarkSceneDirty(boot);
            EditorSceneManager.SaveScene(boot);
        }

        private static void BuildMenuScene(string path, string objectName, string layout, System.Type script, string sceneField, string sceneName)
        {
            Scene scene = OpenOrCreateScene(path);
            GameObject document = CreateDocument(scene, objectName, layout, 0);
            SetString((MonoBehaviour)document.AddComponent(script), sceneField, sceneName);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void BuildEmptyScene(string path)
        {
            Scene scene = OpenOrCreateScene(path);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static Scene OpenOrCreateScene(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            // Caméra et lumière par défaut, comme MainMenu.
            return EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        private static GameObject CreateDocument(Scene scene, string objectName, string layout, int sortingOrder)
        {
            DestroyRoot(scene, objectName);
            GameObject documentObject = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(documentObject, scene);

            UIDocument document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MenusFolder + layout);
            document.sortingOrder = sortingOrder;
            if (document.visualTreeAsset == null)
                Debug.LogError($"[MenusBuilder] {MenusFolder + layout} introuvable.");
            return documentObject;
        }

        // Les scripts d'Hanaé ont des champs publics : on les remplit par SerializedObject pour que la scène les garde.
        private static void SetString(MonoBehaviour component, string field, string value)
        {
            SerializedObject settings = new SerializedObject(component);
            settings.FindProperty(field).stringValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // Titre et Lobby juste après Boot ; les autres scènes gardent leur ordre.
        private static void AddBuildScenes()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != TitleScenePath && scene.path != LobbyScenePath)
                .ToList();
            int bootIndex = scenes.FindIndex(scene => scene.path == BootScenePath);
            if (bootIndex < 0)
            {
                Debug.LogError($"[MenusBuilder] {BootScenePath} absent des Build Settings : lancer d'abord Anachro > Setup > Construire la base réseau.");
                return;
            }
            scenes.InsertRange(bootIndex + 1, new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(LobbyScenePath, true)
            });
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void DestroyRoot(Scene scene, string objectName)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == objectName)
                    Object.DestroyImmediate(rootObject);
            }
        }
    }
}
