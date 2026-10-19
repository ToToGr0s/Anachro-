using System.Linq;
using Anachronism.Gameplay;
using Anachronism.Network;
using Anachronism.Npc;
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

namespace Anachronism.Editor
{
    /// <summary>
    /// Passage en ligne du joueur de Célian et des PNJ d'Olivier. Rejouable :
    /// - Player_Base est regénéré depuis Player_Fugitive : à relancer quand Célian modifie son prefab ;
    /// - Npc_v0 reçoit ses composants réseau s'ils manquent, sa liste de profils est remise à jour ;
    /// - Map_Greybox reçoit un NavMesh cuit et le spawner de PNJ s'ils manquent (réglages copiés de Pnj_v0).
    /// </summary>
    public static class OnlineGameplayBuilder
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string MapScenePath = "Assets/_Project/Scenes/Maps/Map_Greybox.unity";
        private const string NpcTestScenePath = "Assets/_Project/Scenes/Pnj_v0.unity";
        private const string FugitivePrefabPath = "Assets/_Project/Prefabs/Player/Player_Fugitive.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Base.prefab";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/Npc/Npc_v0.prefab";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";
        private const string MapNavMeshFolderParent = "Assets/_Project/Scenes/Maps";
        private const string MapNavMeshFolderName = "Map_Greybox";
        private const string NavMeshName = "NpcNavMesh";
        private const string NpcsName = "Npcs";

        [MenuItem("Anachro/Setup/Brancher joueur et PNJ en réseau")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            DefaultPrefabObjects prefabObjects = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);
            if (prefabObjects == null)
            {
                Debug.LogError($"[OnlineGameplayBuilder] {DefaultPrefabObjectsPath} introuvable : lancer d'abord Anachro > Setup > Construire la base réseau.");
                return;
            }

            NetworkObject player = BuildPlayerPrefab();
            NetworkObject npc = BuildNpcPrefab();
            if (player == null || npc == null)
                return;

            // Player_Base est regénéré : l'ancienne entrée pointe sur un composant qui n'existe plus.
            prefabObjects.RemoveNull();
            prefabObjects.AddObject(player, true);
            prefabObjects.AddObject(npc, true);
            EditorUtility.SetDirty(prefabObjects);
            AssetDatabase.SaveAssets();

            PointSpawnerToPlayer();
            BuildMapNpcs();
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

                PlayerNetworkSetup setup = root.AddComponent<PlayerNetworkSetup>();
                SerializedObject setupSettings = new SerializedObject(setup);
                SerializedProperty ownerOnlyProperty = setupSettings.FindProperty("_ownerOnlyComponents");
                ownerOnlyProperty.arraySize = ownerOnly.Length;
                for (int i = 0; i < ownerOnly.Length; i++)
                    ownerOnlyProperty.GetArrayElementAtIndex(i).objectReferenceValue = ownerOnly[i];
                setupSettings.FindProperty("_capsule").objectReferenceValue = capsule;
                setupSettings.FindProperty("_camera").objectReferenceValue = camera;
                setupSettings.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                return prefab != null ? prefab.GetComponent<NetworkObject>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void PointSpawnerToPlayer()
        {
            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            PlayerSpawner spawner = scene.GetRootGameObjects()
                .Select(rootObject => rootObject.GetComponentInChildren<PlayerSpawner>(true))
                .FirstOrDefault(component => component != null);
            if (spawner == null)
            {
                Debug.LogError("[OnlineGameplayBuilder] Pas de PlayerSpawner dans Boot : lancer d'abord Anachro > Setup > Construire la base réseau.");
                return;
            }

            // Rechargé après l'ouverture de la scène, qui décharge les assets non référencés.
            spawner.SetPlayerPrefab(AssetDatabase.LoadAssetAtPath<NetworkObject>(PlayerPrefabPath));
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
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

                if (!contents.TryGetComponent(out NetworkNpc networkNpc))
                    networkNpc = contents.AddComponent<NetworkNpc>();

                // Ordre stable : l'index envoyé aux clients doit désigner le même profil partout.
                NpcProfile[] profiles = AssetDatabase.FindAssets("t:" + nameof(NpcProfile))
                    .Select(guid => AssetDatabase.LoadAssetAtPath<NpcProfile>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(profile => profile != null && profile.Prefab != null && AssetDatabase.GetAssetPath(profile.Prefab) == NpcPrefabPath)
                    .OrderBy(profile => profile.name)
                    .ToArray();

                SerializedObject npcSettings = new SerializedObject(networkNpc);
                SerializedProperty profilesProperty = npcSettings.FindProperty("_profiles");
                profilesProperty.arraySize = profiles.Length;
                for (int i = 0; i < profiles.Length; i++)
                    profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
                npcSettings.ApplyModifiedPropertiesWithoutUndo();

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

        private static void BuildMapNpcs()
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

            EditorSceneManager.MarkSceneDirty(map);
            EditorSceneManager.SaveScene(map);
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
