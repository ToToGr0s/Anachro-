using Anachronism.Core;
using Anachronism.Network;
using System.Collections.Generic;
using System.Linq;
using FishNet.Component.Scenes;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anachronism.Editor
{
    /// <summary>
    /// Construit la base réseau : prefab joueur, NetworkManager de Boot (Multipass Tugboat + Steam, spawn, lobby Steam,
    /// enchaînement Boot → MainMenu → map) et Build Settings. Rejouable : Boot est reconstruit, le GUID du prefab est gardé,
    /// MainMenu et la map ne sont créés que s'ils n'existent pas (on n'écrase jamais le travail de LD ou d'UI).
    /// </summary>
    public static class NetworkBaseBuilder
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string MapScenePath = "Assets/_Project/Scenes/Maps/Map_Greybox.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Base.prefab";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";
        private const string NetworkManagerName = "NetworkManager";
        private const string SpawnPointsName = "SpawnPoints";
        private const string GroundName = "Ground";
        private const string ServicesName = "Services";
        // Le client local de l'hôte compte comme une connexion Tugboat : 8 = hôte + 7 joueurs.
        private const int MaxPlayers = 8;
        private const int SpawnPointCount = MaxPlayers;
        private const float SpawnRadius = 4f;

        [MenuItem("Anachro/Setup/Construire la base réseau")]
        public static void Build()
        {
            // Le builder ouvre Boot en mode Single : on laisse sauvegarder la scène en cours avant.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            DefaultPrefabObjects prefabObjects = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);
            if (prefabObjects == null)
            {
                Debug.LogError($"[NetworkBaseBuilder] {DefaultPrefabObjectsPath} introuvable : FishNet le recrée à la réimport du projet (Assets > Reimport All).");
                return;
            }

            NetworkObject playerPrefab = BuildPlayerPrefab();
            RegisterPrefab(prefabObjects, playerPrefab);
            CreateSceneIfMissing(MainMenuScenePath, null);
            CreateSceneIfMissing(MapScenePath, BuildMapContent);
            BuildBootScene(playerPrefab, prefabObjects);
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[NetworkBaseBuilder] Base réseau construite.");
        }

        private static NetworkObject BuildPlayerPrefab()
        {
            // Capsule provisoire : Célian la remplace par le vrai joueur (Gameplay/Player).
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Player_Base";
            root.AddComponent<NetworkObject>();
            // Client-autoritaire par défaut : le mouvement joueur est le seul point client-autoritaire assumé.
            root.AddComponent<NetworkTransform>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<NetworkObject>();
        }

        private static void RegisterPrefab(DefaultPrefabObjects prefabObjects, NetworkObject playerPrefab)
        {
            prefabObjects.AddObject(playerPrefab, true);
            EditorUtility.SetDirty(prefabObjects);
        }

        private static void BuildBootScene(NetworkObject playerPrefab, DefaultPrefabObjects prefabObjects)
        {
            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            DestroyRoot(scene, NetworkManagerName);
            // Sol et points d'apparition vivaient dans Boot avant l'enchaînement de scènes : ils sont dans la map.
            DestroyRoot(scene, SpawnPointsName);
            DestroyRoot(scene, GroundName);
            DestroyRoot(scene, ServicesName);

            GameObject managerObject = new GameObject(NetworkManagerName);
            NetworkManager networkManager = managerObject.AddComponent<NetworkManager>();
            networkManager.SpawnablePrefabs = prefabObjects;

            // Multipass : le serveur écoute en local (Tugboat) et sur Steam (FishySteamworks) en même temps,
            // le client choisit son transport au moment de se connecter.
            Tugboat tugboat = managerObject.AddComponent<Tugboat>();
            FishySteamworks.FishySteamworks steamTransport = managerObject.AddComponent<FishySteamworks.FishySteamworks>();
            Multipass multipass = managerObject.AddComponent<Multipass>();

            // Les setters runtime touchent des sockets absents hors Play Mode : on passe par les champs sérialisés.
            SerializedObject tugboatSettings = new SerializedObject(tugboat);
            tugboatSettings.FindProperty("_maximumClients").intValue = MaxPlayers;
            tugboatSettings.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject steamSettings = new SerializedObject(steamTransport);
            steamSettings.FindProperty("_maximumClients").intValue = MaxPlayers;
            // P2P : on se connecte au SteamID de l'hôte via le relais Steam, pas à une IP.
            steamSettings.FindProperty("_peerToPeer").boolValue = true;
            steamSettings.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject multipassSettings = new SerializedObject(multipass);
            SerializedProperty transports = multipassSettings.FindProperty("_transports");
            transports.arraySize = 2;
            transports.GetArrayElementAtIndex(0).objectReferenceValue = tugboat;
            transports.GetArrayElementAtIndex(1).objectReferenceValue = steamTransport;
            multipassSettings.ApplyModifiedPropertiesWithoutUndo();

            TransportManager transportManager = managerObject.GetComponent<TransportManager>();
            if (transportManager == null)
                transportManager = managerObject.AddComponent<TransportManager>();
            transportManager.Transport = multipass;

            // Points d'apparition fournis par la map à son chargement (MapSpawnPoints).
            PlayerSpawner spawner = managerObject.AddComponent<PlayerSpawner>();
            spawner.SetPlayerPrefab(playerPrefab);

            // Boot charge MainMenu au lancement, la map quand le serveur démarre, et revient au menu à la déconnexion.
            DefaultScene defaultScene = managerObject.AddComponent<DefaultScene>();
            defaultScene.SetOfflineScene(MainMenuScenePath);
            defaultScene.SetOnlineScene(MapScenePath);
            SerializedObject defaultSceneSettings = new SerializedObject(defaultScene);
            defaultSceneSettings.FindProperty("_startInOffline").boolValue = true;
            defaultSceneSettings.ApplyModifiedPropertiesWithoutUndo();

            managerObject.AddComponent<SteamLobby>();
            managerObject.AddComponent<DevConnectionHud>();

            new GameObject(ServicesName).AddComponent<SteamService>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateSceneIfMissing(string path, System.Action buildContent)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            buildContent?.Invoke();
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void BuildMapContent()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = GroundName;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            Transform[] spawns = CreateSpawnPoints();
            MapSpawnPoints mapSpawnPoints = spawns[0].parent.gameObject.AddComponent<MapSpawnPoints>();
            SerializedObject spawnSettings = new SerializedObject(mapSpawnPoints);
            SerializedProperty spawnArray = spawnSettings.FindProperty("_spawns");
            spawnArray.arraySize = spawns.Length;
            for (int i = 0; i < spawns.Length; i++)
                spawnArray.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
            spawnSettings.ApplyModifiedPropertiesWithoutUndo();

            // Vue d'ensemble provisoire pour voir tous les joueurs pendant les tests.
            Camera camera = Camera.main;
            if (camera != null)
                camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, -10f), Quaternion.Euler(45f, 0f, 0f));
        }

        private static void SetBuildScenes()
        {
            // Boot en premier (index 0) ; les autres scènes déjà listées sont gardées après.
            string[] required = { BootScenePath, MainMenuScenePath, MapScenePath };
            List<EditorBuildSettingsScene> scenes = required.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            scenes.AddRange(EditorBuildSettings.scenes.Where(scene => !required.Contains(scene.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Transform[] CreateSpawnPoints()
        {
            Transform root = new GameObject(SpawnPointsName).transform;
            Transform[] spawns = new Transform[SpawnPointCount];

            for (int i = 0; i < SpawnPointCount; i++)
            {
                float angle = i * Mathf.PI * 2f / SpawnPointCount;
                Vector3 position = new Vector3(Mathf.Cos(angle) * SpawnRadius, 1f, Mathf.Sin(angle) * SpawnRadius);

                Transform spawn = new GameObject($"Spawn_{i + 1}").transform;
                spawn.SetParent(root, false);
                spawn.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(-position.x, 0f, -position.z)));
                spawns[i] = spawn;
            }

            return spawns;
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
