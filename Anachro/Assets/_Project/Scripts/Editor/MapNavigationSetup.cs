using System;
using System.Collections.Generic;
using System.IO;
using Anachronism.Gameplay;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Anachronism.Editor
{
    /// <summary>
    /// Prépare la scène ouverte pour les collisions du joueur et le NavMesh des PNJ, puis recuit le NavMesh. Rejouable.
    /// - sols sans épaisseur (Plane Unity : une seule face) → BoxCollider de 1 m sous la surface : le contrôleur du joueur
    ///   (CharacterMovement) ne voyait plus rien sous lui dès que ses pieds passaient sous la surface, et tombait ;
    /// - NavMesh cuit sur les colliders (ce qui bloque vraiment) et pas sur les rendus, jusqu'à MaxWalkableY : plafond,
    ///   poutres et dessus des murs n'ont plus de NavMesh, où PNJ et joueurs apparaissaient (tirage sur toute sa surface) ;
    /// - marche des PNJ (agentClimb) = celle du joueur : ils prennent les escaliers, pas les chaises.
    /// Les meubles sont non marchables dans leurs prefabs (NavMeshModifier de Table et Chair).
    /// </summary>
    public static class MapNavigationSetup
    {
        private const float FloorThickness = 1f;
        // Plus haut sol marchable des maps, en Y monde. À monter si une map gagne un étage plus haut.
        private const float MaxWalkableY = 6f;
        private const float VolumeMargin = 1f;
        // Agent « Humanoid » : le seul type d'agent du projet (PNJ).
        private const int HumanoidAgentIndex = 0;
        private const string DefaultBatchScene = "Assets/_Project/Scenes/Maps/Blocking.unity";

        /// <summary>
        /// Batchmode (Unity fermé) : -executeMethod Anachronism.Editor.MapNavigationSetup.RunBatch [-mapScene chemin.unity].
        /// Ouvre la scène, applique tout, cuit et sauvegarde.
        /// </summary>
        public static void RunBatch()
        {
            string scenePath = GetArgument("-mapScene") ?? DefaultBatchScene;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Run();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        [MenuItem("Anachro/Level Design/Sol épais et NavMesh (scène ouverte)")]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            List<NavMeshSurface> surfaces = GetSurfaces(scene);
            if (surfaces.Count == 0)
            {
                Debug.LogWarning($"[MapNavigationSetup] Aucun NavMeshSurface dans {scene.name}.");
                return;
            }

            if (!TrySetAgentClimb(out float climb))
                return;

            int floors = ThickenFlatFloors(scene);
            if (!TryGetCollidersBounds(scene, out Bounds bounds))
            {
                Debug.LogWarning($"[MapNavigationSetup] Aucun collider dans {scene.name}.");
                return;
            }

            foreach (NavMeshSurface surface in surfaces)
                SetupSurface(surface, bounds);

            // Cuisson immédiate (quelques secondes) : marche aussi en batchmode, où une cuisson en tâche de fond n'aurait pas le temps de finir.
            foreach (NavMeshSurface surface in surfaces)
                BakeNow(surface);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MapNavigationSetup] {scene.name} : {floors} sol(s) épaissi(s), marche des PNJ {climb} m, " +
                      $"{surfaces.Count} NavMesh recuit(s). Sauvegarde la scène.");
        }

        // Même emplacement que le bouton Bake du NavMeshSurface : l'ancien asset est remplacé, sinon dossier de la scène.
        private static void BakeNow(NavMeshSurface surface)
        {
            string path = surface.navMeshData != null ? AssetDatabase.GetAssetPath(surface.navMeshData) : null;
            surface.BuildNavMesh();
            if (string.IsNullOrEmpty(path))
            {
                string scenePath = surface.gameObject.scene.path;
                string folder = Path.Combine(Path.GetDirectoryName(scenePath), Path.GetFileNameWithoutExtension(scenePath));
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, $"NavMesh-{surface.name}.asset").Replace(Path.DirectorySeparatorChar, '/');
            }
            else
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(surface.navMeshData, path);
            EditorUtility.SetDirty(surface);
        }

        private static List<NavMeshSurface> GetSurfaces(Scene scene)
        {
            var surfaces = new List<NavMeshSurface>();
            foreach (GameObject root in scene.GetRootGameObjects())
                surfaces.AddRange(root.GetComponentsInChildren<NavMeshSurface>());
            return surfaces;
        }

        // Même hauteur de marche que le joueur (PlayerControllerSettings.MaxStepHeight) : un PNJ ne passe pas où un joueur ne passe pas.
        private static bool TrySetAgentClimb(out float climb)
        {
            climb = 0f;
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(PlayerControllerSettings)}");
            if (guids.Length == 0)
            {
                Debug.LogError("[MapNavigationSetup] Aucun PlayerControllerSettings : hauteur de marche inconnue.");
                return false;
            }

            var playerSettings = AssetDatabase.LoadAssetAtPath<PlayerControllerSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            climb = playerSettings.MaxStepHeight;

            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings"));
            SerializedProperty agent = settings.FindProperty("m_Settings").GetArrayElementAtIndex(HumanoidAgentIndex);
            agent.FindPropertyRelative("agentClimb").floatValue = climb;
            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return true;
        }

        // MeshCollider plat (épaisseur nulle) → BoxCollider de même emprise, sous la surface : le sol ne bouge pas.
        private static int ThickenFlatFloors(Scene scene)
        {
            var flatColliders = new List<MeshCollider>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshCollider meshCollider in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    Mesh mesh = meshCollider.sharedMesh;
                    if (mesh != null && !meshCollider.isTrigger && mesh.bounds.size.y < 0.001f)
                        flatColliders.Add(meshCollider);
                }
            }

            int count = 0;
            foreach (MeshCollider meshCollider in flatColliders)
            {
                // Retirer un composant d'une instance de prefab se fait dans le prefab.
                if (PrefabUtility.IsPartOfPrefabInstance(meshCollider))
                {
                    Debug.LogWarning($"[MapNavigationSetup] {meshCollider.name} : sol plat dans un prefab, à épaissir dans le prefab.", meshCollider);
                    continue;
                }

                GameObject floor = meshCollider.gameObject;
                Bounds meshBounds = meshCollider.sharedMesh.bounds;
                PhysicsMaterial material = meshCollider.sharedMaterial;
                float thickness = FloorThickness / Mathf.Max(Mathf.Abs(floor.transform.lossyScale.y), 0.0001f);

                Undo.DestroyObjectImmediate(meshCollider);
                BoxCollider box = Undo.AddComponent<BoxCollider>(floor);
                box.size = new Vector3(meshBounds.size.x, thickness, meshBounds.size.z);
                box.center = new Vector3(meshBounds.center.x, meshBounds.center.y - thickness / 2f, meshBounds.center.z);
                box.sharedMaterial = material;
                count++;
            }

            return count;
        }

        private static bool TryGetCollidersBounds(Scene scene, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider sceneCollider in root.GetComponentsInChildren<Collider>())
                {
                    // Zones d'apparition et autres triggers : ils ne bloquent personne.
                    if (!sceneCollider.enabled || sceneCollider.isTrigger)
                        continue;

                    if (hasBounds)
                        bounds.Encapsulate(sceneCollider.bounds);
                    else
                        bounds = sceneCollider.bounds;
                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        private static void SetupSurface(NavMeshSurface surface, Bounds collidersBounds)
        {
            Undo.RecordObject(surface, "Sol épais et NavMesh");
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.Volume;

            Vector3 min = collidersBounds.min - new Vector3(VolumeMargin, VolumeMargin, VolumeMargin);
            Vector3 max = new Vector3(collidersBounds.max.x + VolumeMargin, MaxWalkableY, collidersBounds.max.z + VolumeMargin);
            Transform surfaceTransform = surface.transform;
            Vector3 scale = surfaceTransform.lossyScale;
            surface.center = surfaceTransform.InverseTransformPoint((min + max) / 2f);
            surface.size = new Vector3((max.x - min.x) / scale.x, (max.y - min.y) / scale.y, (max.z - min.z) / scale.z);
            EditorUtility.SetDirty(surface);
        }
    }
}
