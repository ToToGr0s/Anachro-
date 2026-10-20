using System.Linq;
using Anachronism.Network;
using Anachronism.Npc;
using Anachronism.UI;
using FishNet.Component.Spawning;
using FishNet.Managing.Object;
using FishNet.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism.Editor
{
    /// <summary>
    /// Branche Boot et la map sur le joueur et les PNJ. Rejouable, et ne touche jamais aux prefabs Player_Base et Npc_v0 :
    /// ils sont faits à la main (un enfant par rôle : Controller, Input, Network, Camera, Model) et seulement enregistrés ici ;
    /// - Map_Greybox reçoit un NavMesh cuit et le spawner de PNJ s'ils manquent (réglages copiés de Pnj_v0) ;
    /// - les joueurs apparaissent au lancement de la partie (MatchPlayerSpawner) et plus à la connexion (PlayerSpawner retiré de Boot) ;
    /// - joueurs et PNJ tirent leur skin dans la même table (NpcSkins, tenue à jour par NpcAssetSync) ;
    /// - Boot reçoit l'overlay : debug F1 et menu Échap.
    /// </summary>
    public static class OnlineGameplayBuilder
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string MapScenePath = "Assets/_Project/Scenes/Maps/Map_Greybox.unity";
        private const string NpcTestScenePath = "Assets/_Project/Scenes/Pnj_v0.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Base.prefab";
        private const string NpcPrefabPath = NpcAssetSync.NpcPrefabPath;
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";
        private const string MapNavMeshFolderParent = "Assets/_Project/Scenes/Maps";
        private const string MapNavMeshFolderName = "Map_Greybox";
        private const string NavMeshName = "NpcNavMesh";
        private const string NpcsName = "Npcs";
        private const string OverlayName = "Overlay";
        private const string OverlayLayoutPath = "Assets/_Project/Prefabs/UI/Overlay/Overlay.uxml";
        private const string PanelSettingsPath = "Assets/_Project/Prefabs/UI/AnachroPanelSettings.asset";
        // Au-dessus du menu du lobby (même PanelSettings, ordre 0).
        private const int OverlaySortingOrder = 10;

        [MenuItem("Anachro/Setup/Brancher joueur et PNJ en réseau")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Le builder ouvre Boot puis la map : on remet ensuite la ou les scènes de l'utilisateur
            // (sinon il se retrouve sur la map et son prochain Play part sans Boot, donc sans réseau ni UI).
            SceneSetup[] userScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                BuildAll();
            }
            finally
            {
                if (userScenes.Length > 0 && userScenes.All(setup => !string.IsNullOrEmpty(setup.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(userScenes);
            }
        }

        private static void BuildAll()
        {
            DefaultPrefabObjects prefabObjects = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);
            if (prefabObjects == null)
            {
                Debug.LogError($"[OnlineGameplayBuilder] {DefaultPrefabObjectsPath} introuvable : lancer d'abord Anachro > Setup > Construire la base réseau.");
                return;
            }

            NpcAssetSync.Sync();
            NetworkObject player = AssetDatabase.LoadAssetAtPath<NetworkObject>(PlayerPrefabPath);
            NetworkObject npc = AssetDatabase.LoadAssetAtPath<NetworkObject>(NpcPrefabPath);
            if (player == null || npc == null)
            {
                Debug.LogError($"[OnlineGameplayBuilder] {PlayerPrefabPath} ou {NpcPrefabPath} introuvable, ou sans NetworkObject à la racine.");
                return;
            }

            prefabObjects.RemoveNull();
            prefabObjects.AddObject(player, true);
            prefabObjects.AddObject(npc, true);
            EditorUtility.SetDirty(prefabObjects);
            AssetDatabase.SaveAssets();

            BuildBoot();
            BuildMap();
            ApplyWindowSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[OnlineGameplayBuilder] Joueur et PNJ branchés sur le réseau.");
        }

        // Le PlayerSpawner de FishNet fait apparaître le joueur dès la connexion : au lobby, il verrouillerait la souris
        // au-dessus du menu. MatchPlayerSpawner (dans la map) le remplace.
        private static void BuildBoot()
        {
            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (PlayerSpawner spawner in scene.GetRootGameObjects().SelectMany(rootObject => rootObject.GetComponentsInChildren<PlayerSpawner>(true)))
                Object.DestroyImmediate(spawner);

            EnsureOverlay(scene);
            GameObject managerObject = scene.GetRootGameObjects()
                .Select(rootObject => rootObject.GetComponentInChildren<FishNet.Managing.NetworkManager>(true))
                .Where(component => component != null)
                .Select(component => component.gameObject)
                .FirstOrDefault();
            if (managerObject != null)
                NetworkBaseBuilder.EnsureLocalTransport(managerObject);
            else
                Debug.LogError("[OnlineGameplayBuilder] Pas de NetworkManager dans Boot : lancer d'abord Anachro > Setup > Construire la base réseau.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // Debug F1 et menu Échap : un UIDocument à part, par-dessus le menu du lobby.
        private static void EnsureOverlay(Scene scene)
        {
            GameObject overlay = FindRoot(scene, OverlayName) ?? new GameObject(OverlayName);
            if (!overlay.TryGetComponent(out UIDocument document))
                document = overlay.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(OverlayLayoutPath);
            document.sortingOrder = OverlaySortingOrder;

            if (!overlay.TryGetComponent(out DebugOverlay _))
                overlay.AddComponent<DebugOverlay>();
            if (!overlay.TryGetComponent(out PauseMenu _))
                overlay.AddComponent<PauseMenu>();
            if (!overlay.TryGetComponent(out DisplayController _))
                overlay.AddComponent<DisplayController>();
        }

        // Fenêtré par défaut pour lancer deux instances côte à côte ; F11 passe en plein écran (DisplayController).
        private static void ApplyWindowSettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            // Indispensable à deux instances : celle qui n'a pas le focus doit continuer à tourner.
            PlayerSettings.runInBackground = true;
        }

        private static void BuildMap()
        {
            Scene map = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);

            GameObject navMeshObject = FindRoot(map, NavMeshName) ?? new GameObject(NavMeshName);
            if (!navMeshObject.TryGetComponent(out NavMeshSurface surface))
                surface = navMeshObject.AddComponent<NavMeshSurface>();

            // Cuit seulement s'il manque : ensuite, le LD le recuit depuis l'inspecteur (bouton Bake).
            if (surface.navMeshData == null)
            {
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
                surface.BuildNavMesh();

                string folder = $"{MapNavMeshFolderParent}/{MapNavMeshFolderName}";
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(MapNavMeshFolderParent, MapNavMeshFolderName);
                AssetDatabase.CreateAsset(surface.navMeshData, $"{folder}/NavMesh-{NavMeshName}.asset");
            }

            GameObject npcsObject = FindRoot(map, NpcsName) ?? new GameObject(NpcsName);
            if (!npcsObject.TryGetComponent(out NpcDestinationManager _))
            {
                NpcDestinationManager manager = npcsObject.AddComponent<NpcDestinationManager>();
                CopyNpcSettingsFromTestScene(manager);
            }

            SetupMatchPlayerSpawner(map);

            EditorSceneManager.MarkSceneDirty(map);
            EditorSceneManager.SaveScene(map);
        }

        private static void SetupMatchPlayerSpawner(Scene map)
        {
            MatchManager matchManager = map.GetRootGameObjects()
                .Select(rootObject => rootObject.GetComponentInChildren<MatchManager>(true))
                .FirstOrDefault(component => component != null);
            MapSpawnPoints spawnPoints = map.GetRootGameObjects()
                .Select(rootObject => rootObject.GetComponentInChildren<MapSpawnPoints>(true))
                .FirstOrDefault(component => component != null);
            if (matchManager == null || spawnPoints == null)
            {
                Debug.LogError("[OnlineGameplayBuilder] MatchManager ou MapSpawnPoints absent de la map : lancer d'abord Anachro > Setup > Construire la base réseau.");
                return;
            }

            if (!matchManager.TryGetComponent(out MatchPlayerSpawner playerSpawner))
                playerSpawner = matchManager.gameObject.AddComponent<MatchPlayerSpawner>();

            // Rechargé après l'ouverture de la scène, qui décharge les assets non référencés.
            SerializedObject spawnerSettings = new SerializedObject(playerSpawner);
            spawnerSettings.FindProperty("_playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(PlayerPrefabPath);
            spawnerSettings.FindProperty("_spawnPoints").objectReferenceValue = spawnPoints;
            spawnerSettings.FindProperty("_npcs").objectReferenceValue = map.GetRootGameObjects()
                .Select(rootObject => rootObject.GetComponentInChildren<NpcDestinationManager>(true))
                .FirstOrDefault(component => component != null);
            spawnerSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        // Mêmes profils et quantités que la scène de test d'Olivier : aucune valeur d'équilibrage inventée ici.
        private static void CopyNpcSettingsFromTestScene(NpcDestinationManager target)
        {
            Scene testScene = EditorSceneManager.OpenScene(NpcTestScenePath, OpenSceneMode.Additive);
            try
            {
                NpcDestinationManager source = testScene.GetRootGameObjects()
                    .Select(rootObject => rootObject.GetComponentInChildren<NpcDestinationManager>(true))
                    .FirstOrDefault(component => component != null);
                if (source == null)
                {
                    Debug.LogWarning($"[OnlineGameplayBuilder] Pas de NpcDestinationManager dans {NpcTestScenePath} : liste de PNJ de la map à remplir à la main.");
                    return;
                }

                SerializedObject from = new SerializedObject(source);
                SerializedObject to = new SerializedObject(target);
                to.CopyFromSerializedProperty(from.FindProperty("_spawnEntries"));
                to.CopyFromSerializedProperty(from.FindProperty("_maxAttempts"));
                to.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                EditorSceneManager.CloseScene(testScene, true);
            }
        }

        private static GameObject FindRoot(Scene scene, string objectName) =>
            scene.GetRootGameObjects().FirstOrDefault(rootObject => rootObject.name == objectName);
    }
}
