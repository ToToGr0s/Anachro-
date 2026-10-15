using Anachronism.Core;
using Anachronism.Network;
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
    /// Construit la base réseau dans Boot : prefab joueur, NetworkManager + Multipass (Tugboat + Steam), spawn, lobby Steam.
    /// Rejouable : remplace ce qu'il a créé au lieu de dupliquer, et garde le GUID du prefab.
    /// </summary>
    public static class NetworkBaseBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Boot.unity";
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
            BuildScene(playerPrefab, prefabObjects);
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

        private static void BuildScene(NetworkObject playerPrefab, DefaultPrefabObjects prefabObjects)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            DestroyRoot(scene, NetworkManagerName);
            DestroyRoot(scene, SpawnPointsName);
            DestroyRoot(scene, GroundName);
            DestroyRoot(scene, ServicesName);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = GroundName;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            Transform[] spawns = CreateSpawnPoints();

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

            PlayerSpawner spawner = managerObject.AddComponent<PlayerSpawner>();
            spawner.SetPlayerPrefab(playerPrefab);
            spawner.Spawns = spawns;

            managerObject.AddComponent<SteamLobby>();
            managerObject.AddComponent<DevConnectionHud>();

            new GameObject(ServicesName).AddComponent<SteamService>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
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
