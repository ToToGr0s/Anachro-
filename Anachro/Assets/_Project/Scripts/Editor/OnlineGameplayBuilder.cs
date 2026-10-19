using System.Linq;
using Anachronism.Gameplay;
using Anachronism.Network;
using Anachronism.Npc;
using Anachronism.UI;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing.Object;
using FishNet.Object;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism.Editor
{
    /// <summary>
    /// Passage en ligne du joueur de Célian et des PNJ d'Olivier. Rejouable :
    /// - Player_Base est regénéré depuis Player_Fugitive : à relancer quand Célian modifie son prefab ;
    /// - Npc_v0 reçoit ses composants réseau s'ils manquent ;
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
        private const string FugitivePrefabPath = "Assets/_Project/Prefabs/Player/Player_Fugitive.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Base.prefab";
        private const string NpcPrefabPath = NpcAssetSync.NpcPrefabPath;
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";
        private const string MapNavMeshFolderParent = "Assets/_Project/Scenes/Maps";
        private const string MapNavMeshFolderName = "Map_Greybox";
        private const string NavMeshName = "NpcNavMesh";
        private const string NpcsName = "Npcs";
        private const string ModelRootName = "Model";
        private const string SkinTablePath = NpcAssetSync.SkinTablePath;
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

            // Table des skins avant le joueur (il la référence) ; listes du prefab PNJ une fois ses composants posés.
            NpcAssetSync.Sync();
            NetworkObject player = BuildPlayerPrefab();
            NetworkObject npc = BuildNpcPrefab();
            NpcAssetSync.Sync();
            if (player == null || npc == null)
                return;

            // Player_Base est regénéré : l'ancienne entrée pointe sur un composant qui n'existe plus.
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

        // ---------- Joueur ----------

        private static NetworkObject BuildPlayerPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(FugitivePrefabPath);
            if (source == null)
            {
                Debug.LogError($"[OnlineGameplayBuilder] {FugitivePrefabPath} introuvable.");
                return null;
            }

            // Copie (pas une instance) : les UnityEvents du PlayerInput sont remappés sur l'InputManager de la copie.
            GameObject root = Object.Instantiate(source);
            root.name = "Player_Base";

            try
            {
                PlayerInput playerInput = root.GetComponent<PlayerInput>();
                InputManager inputManager = root.GetComponent<InputManager>();
                CharacterMovement movement = root.GetComponent<CharacterMovement>();
                CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
                MouseLook mouseLook = root.GetComponentInChildren<MouseLook>(true);
                Camera camera = mouseLook != null ? mouseLook.GetComponent<Camera>() : null;
                AudioListener listener = camera != null ? camera.GetComponent<AudioListener>() : null;
                PlayerControllerSettings settings = movement != null
                    ? new SerializedObject(movement).FindProperty("_settings").objectReferenceValue as PlayerControllerSettings
                    : null;

                if (playerInput == null || inputManager == null || movement == null || capsule == null
                    || mouseLook == null || camera == null || listener == null || settings == null)
                {
                    Debug.LogError($"[OnlineGameplayBuilder] {FugitivePrefabPath} : il manque un composant du contrôleur (PlayerInput, InputManager, CharacterMovement, MouseLook, Camera, AudioListener ou réglages).", source);
                    return null;
                }

                Behaviour[] ownerOnly = { playerInput, inputManager, movement, mouseLook, camera, listener };
                // Éteints dans le prefab : chez un joueur distant, rien ne doit lire les entrées, rendre ou bouger.
                foreach (Behaviour component in ownerOnly)
                    component.enabled = false;

                root.AddComponent<NetworkObject>();
                ConfigureTransform(root.AddComponent<NetworkTransform>());

                // Les PNJ (NavMeshAgent, côté serveur) contournent les joueurs comme ils se contournent entre eux.
                NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.center = Vector3.zero;
                obstacle.radius = settings.Radius;
                obstacle.height = settings.Height;
                obstacle.carving = false;

                // Skin de PNJ tiré par le serveur ; la capsule n'est plus qu'un repli si la table est vide.
                root.AddComponent<NpcSkin>();
                PlayerSkin playerSkin = root.AddComponent<PlayerSkin>();
                SerializedObject skinSettings = new SerializedObject(playerSkin);
                skinSettings.FindProperty("_skins").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NpcSkinTable>(SkinTablePath);
                skinSettings.FindProperty("_settings").objectReferenceValue = settings;
                skinSettings.FindProperty("_placeholderRenderer").objectReferenceValue = root.GetComponent<MeshRenderer>();
                skinSettings.ApplyModifiedPropertiesWithoutUndo();

                PlayerNetworkSetup setup = root.AddComponent<PlayerNetworkSetup>();
                SerializedObject setupSettings = new SerializedObject(setup);
                SerializedProperty ownerOnlyProperty = setupSettings.FindProperty("_ownerOnlyComponents");
                ownerOnlyProperty.arraySize = ownerOnly.Length;
                for (int i = 0; i < ownerOnly.Length; i++)
                    ownerOnlyProperty.GetArrayElementAtIndex(i).objectReferenceValue = ownerOnly[i];
                setupSettings.FindProperty("_capsule").objectReferenceValue = capsule;
                setupSettings.FindProperty("_camera").objectReferenceValue = camera;
                setupSettings.FindProperty("_playerInput").objectReferenceValue = playerInput;
                setupSettings.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                return prefab != null ? prefab.GetComponent<NetworkObject>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Le PlayerSpawner de FishNet fait apparaître le joueur dès la connexion : au lobby, il verrouillerait la souris
        // au-dessus du menu. MatchPlayerSpawner le remplace.
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
            if (!overlay.TryGetComponent(out DisplayModeToggle _))
                overlay.AddComponent<DisplayModeToggle>();
        }

        // Fenêtré par défaut pour lancer deux instances côte à côte ; F11 passe en plein écran (DisplayModeToggle).
        private static void ApplyWindowSettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            // Indispensable à deux instances : celle qui n'a pas le focus doit continuer à tourner.
            PlayerSettings.runInBackground = true;
        }

        // ---------- PNJ ----------

        private static NetworkObject BuildNpcPrefab()
        {
            // Édité en place : les profils d'Olivier gardent leur référence vers le prefab.
            GameObject contents = PrefabUtility.LoadPrefabContents(NpcPrefabPath);
            try
            {
                if (!contents.TryGetComponent(out NetworkObject _))
                    contents.AddComponent<NetworkObject>();
                if (!contents.TryGetComponent(out NetworkTransform networkTransform))
                    networkTransform = contents.AddComponent<NetworkTransform>();
                ConfigureTransform(networkTransform);

                if (!contents.TryGetComponent(out NpcSkin npcSkin))
                    npcSkin = contents.AddComponent<NpcSkin>();
                SetupModelRoot(contents, npcSkin);
                // Ses listes (comportements, skins) sont tenues à jour par NpcAssetSync.
                if (!contents.TryGetComponent(out NetworkNpc _))
                    contents.AddComponent<NetworkNpc>();

                // Seul le serveur (ou la scène de test hors ligne) l'allume, dans NpcDestinationManager.SpawnOne.
                contents.GetComponent<NavMeshAgent>().enabled = false;

                PrefabUtility.SaveAsPrefabAsset(contents, NpcPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<NetworkObject>(NpcPrefabPath);
        }

        // Enfant « Model » : on règle la hauteur du mesh en le déplaçant dans le prefab. L'aperçu (un skin de la table)
        // n'est là que pour voir où tombent les pieds : NpcSkin le supprime en jeu et le tag EditorOnly le retire des builds.
        private static void SetupModelRoot(GameObject npc, NpcSkin npcSkin)
        {
            Transform modelRoot = npc.transform.Find(ModelRootName);
            if (modelRoot == null)
            {
                modelRoot = new GameObject(ModelRootName).transform;
                modelRoot.SetParent(npc.transform, false);
            }

            NpcSkinTable table = AssetDatabase.LoadAssetAtPath<NpcSkinTable>(SkinTablePath);
            NpcProfile previewSkin = table != null ? table.Get(0) : null;
            if (modelRoot.childCount == 0 && previewSkin != null && previewSkin.ModelPrefab != null)
            {
                GameObject preview = (GameObject)PrefabUtility.InstantiatePrefab(previewSkin.ModelPrefab, modelRoot);
                preview.name = "Apercu (supprime en jeu)";
                preview.tag = "EditorOnly";
                preview.transform.localScale = Vector3.one * previewSkin.ModelScale;
            }

            SerializedObject skinSettings = new SerializedObject(npcSkin);
            skinSettings.FindProperty("_modelRoot").objectReferenceValue = modelRoot;
            skinSettings.ApplyModifiedPropertiesWithoutUndo();
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

        // ---------- Commun ----------

        // Joueurs et PNJ ont exactement le même NetworkTransform (règle réseau 5) : un PNJ et un joueur
        // ne doivent pas se distinguer par leur fluidité. Sans owner, un PNJ reste piloté par le serveur.
        private static void ConfigureTransform(NetworkTransform networkTransform)
        {
            SerializedObject transformSettings = new SerializedObject(networkTransform);
            transformSettings.FindProperty("_clientAuthoritative").boolValue = true;
            transformSettings.FindProperty("_sendToOwner").boolValue = true;
            transformSettings.FindProperty("_interval").intValue = 1;
            transformSettings.FindProperty("_interpolation").intValue = 2;
            transformSettings.FindProperty("_extrapolation").intValue = 2;
            transformSettings.FindProperty("_synchronizePosition").boolValue = true;
            transformSettings.FindProperty("_synchronizeRotation").boolValue = true;
            transformSettings.FindProperty("_synchronizeScale").boolValue = false;
            transformSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindRoot(Scene scene, string objectName) =>
            scene.GetRootGameObjects().FirstOrDefault(rootObject => rootObject.name == objectName);
    }
}
