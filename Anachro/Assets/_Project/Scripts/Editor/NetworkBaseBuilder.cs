using Anachronism.Network;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anachronism.Editor
{
    /// <summary>
    /// Construit la base réseau de T02 : prefab joueur, NetworkManager + Tugboat + spawn dans Boot.
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
        private const int SpawnPointCount = 8;
        private const float SpawnRadius = 4f;

        [MenuItem("Anachro/Setup/Construire la base réseau (T02)")]
        public static void Build()
        {
            NetworkObject playerPrefab = BuildPlayerPrefab();
            DefaultPrefabObjects prefabObjects = RegisterPrefab(playerPrefab);
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

        private static DefaultPrefabObjects RegisterPrefab(NetworkObject playerPrefab)
        {
            DefaultPrefabObjects prefabObjects = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);
            prefabObjects.AddObject(playerPrefab, true);
            EditorUtility.SetDirty(prefabObjects);
            return prefabObjects;
        }

        private static void BuildScene(NetworkObject playerPrefab, DefaultPrefabObjects prefabObjects)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            DestroyRoot(scene, NetworkManagerName);
            DestroyRoot(scene, SpawnPointsName);
            DestroyRoot(scene, GroundName);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = GroundName;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            Transform[] spawns = CreateSpawnPoints();

            GameObject managerObject = new GameObject(NetworkManagerName);
            NetworkManager networkManager = managerObject.AddComponent<NetworkManager>();
            networkManager.SpawnablePrefabs = prefabObjects;

            Tugboat tugboat = managerObject.AddComponent<Tugboat>();
            TransportManager transportManager = managerObject.GetComponent<TransportManager>();
            if (transportManager == null)
                transportManager = managerObject.AddComponent<TransportManager>();
            transportManager.Transport = tugboat;

            PlayerSpawner spawner = managerObject.AddComponent<PlayerSpawner>();
            spawner.SetPlayerPrefab(playerPrefab);
            spawner.Spawns = spawns;

            managerObject.AddComponent<DevConnectionHud>();

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
