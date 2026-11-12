using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anachronism.Editor
{
    /// <summary>
    /// Met le pivot des prefabs de level design en bas, au centre : lâché sur la map, l'objet se pose au ras du sol,
    /// centré sous la souris. Unity ne déplace pas un pivot : on décale le contenu sous la racine.
    /// - enfants : décalés ;
    /// - mesh porté par la racine (prefab d'un seul cube) : copié dans un mesh décalé (dossier Meshes à côté du prefab),
    ///   pour garder les composants sur la racine et donc les matériaux changés sur les instances ;
    /// - instances déjà posées (scènes ouvertes, prefabs qui les contiennent, comme Door avec ses Wall) : recalées, rien ne bouge.
    /// Prefabs sélectionnés dans le Project, sinon tout le dossier LevelDesign. Rejouable : un pivot déjà en place est ignoré.
    /// </summary>
    public static class BlockingPivotTool
    {
        private const string LevelDesignFolder = "Assets/_Project/LevelDesign";
        private const string MeshFolderName = "Meshes";
        // En dessous, le pivot est déjà en place : pas de réécriture du prefab pour un arrondi.
        private const float Tolerance = 0.001f;

        [MenuItem("Anachro/Level Design/Recentrer les pivots (bas, centre)")]
        public static void RecenterPivots()
        {
            List<string> targets = SortDependenciesFirst(GetTargetPrefabPaths());
            if (targets.Count == 0)
            {
                Debug.LogWarning($"[BlockingPivotTool] Aucun prefab sélectionné ni dans {LevelDesignFolder}.");
                return;
            }

            // Une scène fermée garderait l'ancien décalage et ses instances bougeraient : il faut l'ouvrir avant.
            List<string> closedScenes = GetClosedScenesUsing(targets);
            string closedWarning = closedScenes.Count == 0
                ? string.Empty
                : $"\n\nATTENTION, scènes fermées qui les utilisent (leurs objets vont bouger, ouvre-les d'abord) :\n{string.Join("\n", closedScenes)}";
            if (!EditorUtility.DisplayDialog("Recentrer les pivots",
                    $"{targets.Count} prefab(s) : pivot en bas, au centre.\nLes instances des scènes ouvertes sont recalées. Les prefabs ne passent pas par Ctrl+Z.{closedWarning}",
                    "Recentrer", "Annuler"))
                return;

            int changed = 0;
            foreach (string path in targets)
            {
                if (!TryRecenter(path, out Vector3 offset))
                    continue;

                CompensateNestedInstances(path, offset);
                CompensateSceneInstances(path, offset);
                changed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[BlockingPivotTool] {changed} prefab(s) recentré(s) sur {targets.Count}. Pense à sauvegarder la scène.");
        }

        private static List<string> GetTargetPrefabPaths()
        {
            List<string> selected = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => path.EndsWith(".prefab"))
                .Distinct()
                .ToList();
            if (selected.Count > 0)
                return selected;

            return AssetDatabase.FindAssets("t:Prefab", new[] { LevelDesignFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();
        }

        // Wall avant Door : Door est recalé sur ses Wall déjà recentrés, puis recentré à son tour.
        private static List<string> SortDependenciesFirst(List<string> paths)
        {
            var sorted = new List<string>();
            var remaining = new List<string>(paths);
            while (remaining.Count > 0)
            {
                string next = remaining.FirstOrDefault(path =>
                    !AssetDatabase.GetDependencies(path, true).Any(dependency => dependency != path && remaining.Contains(dependency)));
                // Dépendance circulaire (impossible entre prefabs, mais sans risque de boucle infinie).
                next ??= remaining[0];
                sorted.Add(next);
                remaining.Remove(next);
            }
            return sorted;
        }

        private static bool TryRecenter(string path, out Vector3 offset)
        {
            offset = Vector3.zero;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!TryGetLocalBounds(root.transform, out Bounds bounds))
                {
                    Debug.LogWarning($"[BlockingPivotTool] {path} : aucun mesh, ignoré.");
                    return false;
                }

                offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                if (offset.sqrMagnitude < Tolerance * Tolerance)
                    return false;

                if (!TryShiftRootMesh(root, path, offset))
                    return false;

                ShiftRootColliders(root, offset);
                foreach (Transform child in root.transform)
                    child.localPosition -= offset;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Boîte de tous les meshes, dans l'espace de la racine (son échelle comprise, comme les localPosition des enfants).
        private static bool TryGetLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                Vector3 min = filter.sharedMesh.bounds.min;
                Vector3 max = filter.sharedMesh.bounds.max;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                    Vector3 local = root.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                        bounds.Encapsulate(local);
                }
            }
            return hasBounds;
        }

        // Le mesh de la racine est dans l'espace de la racine : on décale ses sommets d'autant.
        private static bool TryShiftRootMesh(GameObject root, string prefabPath, Vector3 offset)
        {
            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return true;

            Mesh source = filter.sharedMesh;
            string meshPath = GetMeshPath(prefabPath);
            // Déjà notre copie (outil relancé) : on la modifie sur place.
            bool isOwnCopy = AssetDatabase.GetAssetPath(source) == meshPath;
            Mesh mesh = isOwnCopy ? source : Object.Instantiate(source);
            if (!mesh.isReadable)
            {
                Debug.LogError($"[BlockingPivotTool] {prefabPath} : le mesh {source.name} n'est pas lisible (Read/Write désactivé), prefab ignoré.");
                return false;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] -= offset;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();

            if (isOwnCopy)
                EditorUtility.SetDirty(mesh);
            else
            {
                mesh.name = Path.GetFileNameWithoutExtension(meshPath);
                Directory.CreateDirectory(Path.GetDirectoryName(meshPath));
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            filter.sharedMesh = mesh;
            if (root.TryGetComponent(out MeshCollider meshCollider) && meshCollider.sharedMesh == source)
                meshCollider.sharedMesh = mesh;
            return true;
        }

        private static string GetMeshPath(string prefabPath) =>
            $"{Path.GetDirectoryName(prefabPath).Replace('\\', '/')}/{MeshFolderName}/{Path.GetFileNameWithoutExtension(prefabPath)}_Mesh.asset";

        private static void ShiftRootColliders(GameObject root, Vector3 offset)
        {
            foreach (Collider collider in root.GetComponents<Collider>())
            {
                switch (collider)
                {
                    case BoxCollider box: box.center -= offset; break;
                    case SphereCollider sphere: sphere.center -= offset; break;
                    case CapsuleCollider capsule: capsule.center -= offset; break;
                }
            }
        }

        // Une instance posée doit rester où elle est : sa racine avance d'autant que le contenu a reculé.
        private static void Compensate(Transform instance, Vector3 offset) =>
            instance.position += instance.TransformVector(offset);

        private static bool IsInstanceOf(GameObject gameObject, string prefabPath) =>
            PrefabUtility.IsAnyPrefabInstanceRoot(gameObject)
            && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject) == prefabPath;

        private static void CompensateNestedInstances(string prefabPath, Vector3 offset)
        {
            foreach (string otherPath in AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath))
            {
                if (otherPath == prefabPath || !AssetDatabase.GetDependencies(otherPath, false).Contains(prefabPath))
                    continue;

                GameObject root = PrefabUtility.LoadPrefabContents(otherPath);
                try
                {
                    foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        // Seulement les instances posées directement dans ce prefab : une instance au fond d'un autre
                        // prefab imbriqué est recalée dans celui-là.
                        if (PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject) && IsInstanceOf(transform.gameObject, prefabPath))
                            Compensate(transform, offset);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, otherPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void CompensateSceneInstances(string prefabPath, Vector3 offset)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                bool isChanged = false;
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    foreach (Transform transform in rootObject.GetComponentsInChildren<Transform>(true))
                    {
                        // Les instances imbriquées (Wall dans Door) ont déjà été recalées dans leur prefab.
                        if (!PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject) || !IsInstanceOf(transform.gameObject, prefabPath))
                            continue;

                        Undo.RecordObject(transform, "Recentrer les pivots");
                        Compensate(transform, offset);
                        isChanged = true;
                    }
                }

                if (isChanged)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static List<string> GetClosedScenesUsing(List<string> prefabPaths) =>
            AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(scenePath => !SceneManager.GetSceneByPath(scenePath).isLoaded
                    && AssetDatabase.GetDependencies(scenePath, true).Any(prefabPaths.Contains))
                .ToList();
    }
}
