using System.Linq;
using Anachronism.Gameplay;
using Anachronism.Network;
using Anachronism.Npc;
using Anachronism.UI;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Observing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anachronism.Editor
{
    /// <summary>
    /// Branche sur les prefabs et les scènes ce qui ne se règle pas dans le code. Rejouable : ne rajoute que ce qui manque.
    /// - T05 : NetworkObserver + HiddenWhileHidingCondition sur le joueur et le WorldObject ;
    /// - T04-5 : HeldItem (et sa main) sur le joueur et le PNJ ;
    /// - HitFlash (clignotement d'un personnage touché) sur le joueur et le PNJ ;
    /// - T09 : MapSelection sur le NetworkManager de Boot (cartes des Build Settings) ;
    /// - panel F5 : DevPanel sur l'overlay de Boot, LiveTuning sur le MatchManager de chaque map.
    /// Le bureau (T19) reste à poser à la main : MapSpawnPoints > Office Spawns, sur chaque map.
    /// </summary>
    public static class MatchFeaturesBuilder
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Base.prefab";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/Npc/Npc_v0.prefab";
        private const string WorldObjectPrefabPath = "Assets/_Project/Prefabs/Props/WorldObject.prefab";
        private const string ConditionFolder = "Assets/_Project/Data/Network";
        private const string ConditionPath = ConditionFolder + "/HiddenWhileHidingCondition.asset";
        private const string BootScenePath = "Assets/_Project/Scenes/Maps/Boot.unity";
        private const string MapsFolder = "Assets/_Project/Scenes/Maps/";
        private const string HandName = "HeldItem";
        private const string OverlayName = "Overlay";

        // Main tenue devant le corps, à 0,8 m du sol. Le pivot du joueur est au centre de sa capsule (1,8 m),
        // celui du PNJ à ses pieds : même hauteur dans le monde, donc même silhouette (règle réseau 5).
        private static readonly Vector3 PlayerHandPosition = new Vector3(0.3f, -0.1f, 0.3f);
        private static readonly Vector3 NpcHandPosition = new Vector3(0.3f, 0.8f, 0.3f);

        [MenuItem("Anachro/Setup/Brancher visibilité, objet tenu, cartes et panel F5")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            SceneSetup[] userScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                HiddenWhileHidingCondition condition = EnsureCondition();
                SetupPlayer(condition);
                SetupNpc();
                SetupWorldObject(condition);
                SetupBoot();
                foreach (string mapPath in MapScenePaths())
                    SetupMap(mapPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[MatchFeaturesBuilder] Visibilité, objet tenu, cartes et panel F5 branchés.");
            }
            finally
            {
                if (userScenes.Length > 0 && userScenes.All(setup => !string.IsNullOrEmpty(setup.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(userScenes);
            }
        }

        private static HiddenWhileHidingCondition EnsureCondition()
        {
            var condition = AssetDatabase.LoadAssetAtPath<HiddenWhileHidingCondition>(ConditionPath);
            if (condition != null)
                return condition;

            if (!AssetDatabase.IsValidFolder(ConditionFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Network");
            condition = ScriptableObject.CreateInstance<HiddenWhileHidingCondition>();
            AssetDatabase.CreateAsset(condition, ConditionPath);
            return condition;
        }

        private static void SetupPlayer(HiddenWhileHidingCondition condition)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                EnsureObserver(root, condition);

                // Mêmes assets que la pose : le registre, les réglages et l'époque y sont déjà réglés.
                NetworkPlacement placement = root.GetComponentInChildren<NetworkPlacement>(true);
                FugitivePlacer placer = root.GetComponentInChildren<FugitivePlacer>(true);
                var placementSettings = new SerializedObject(placement);
                HeldItem held = EnsureHeldItem(root, placement.gameObject, PlayerHandPosition);
                var heldSettings = new SerializedObject(held);
                heldSettings.FindProperty("_registry").objectReferenceValue = placementSettings.FindProperty("_registry").objectReferenceValue;
                heldSettings.FindProperty("_tuning").objectReferenceValue = placementSettings.FindProperty("_tuning").objectReferenceValue;
                heldSettings.FindProperty("_era").objectReferenceValue = placementSettings.FindProperty("_era").objectReferenceValue;
                heldSettings.FindProperty("_placer").objectReferenceValue = placer;
                heldSettings.ApplyModifiedPropertiesWithoutUndo();

                EnsureHitFlash(root, placement.gameObject, placementSettings.FindProperty("_tuning").objectReferenceValue);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetupNpc()
        {
            // Mêmes assets que le joueur : lus sur son HeldItem, réglé juste avant.
            var playerHeld = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath).GetComponentInChildren<HeldItem>(true);
            GameObject root = PrefabUtility.LoadPrefabContents(NpcPrefabPath);
            try
            {
                NetworkNpc npc = root.GetComponentInChildren<NetworkNpc>(true);
                HeldItem held = EnsureHeldItem(root, npc.gameObject, NpcHandPosition);
                var heldSettings = new SerializedObject(held);
                var source = new SerializedObject(playerHeld);
                foreach (string property in new[] { "_registry", "_tuning", "_era" })
                    heldSettings.FindProperty(property).objectReferenceValue = source.FindProperty(property).objectReferenceValue;
                heldSettings.FindProperty("_placer").objectReferenceValue = null;
                heldSettings.ApplyModifiedPropertiesWithoutUndo();

                var npcSettings = new SerializedObject(npc);
                npcSettings.FindProperty("_heldItem").objectReferenceValue = held;
                npcSettings.ApplyModifiedPropertiesWithoutUndo();

                EnsureHitFlash(root, npc.gameObject, source.FindProperty("_tuning").objectReferenceValue);

                PrefabUtility.SaveAsPrefabAsset(root, NpcPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetupWorldObject(HiddenWhileHidingCondition condition)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(WorldObjectPrefabPath);
            try
            {
                EnsureObserver(root, condition);
                PrefabUtility.SaveAsPrefabAsset(root, WorldObjectPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Sur l'objet qui porte le NetworkObject. AddMissing : les conditions générales de l'ObserverManager restent.
        private static void EnsureObserver(GameObject root, ObserverCondition condition)
        {
            GameObject networkRoot = root.GetComponentInChildren<NetworkObject>(true).gameObject;
            if (!networkRoot.TryGetComponent(out NetworkObserver observer))
                observer = networkRoot.AddComponent<NetworkObserver>();

            var settings = new SerializedObject(observer);
            settings.FindProperty("_overrideType").enumValueIndex = (int)NetworkObserver.ConditionOverrideType.AddMissing;
            SerializedProperty conditions = settings.FindProperty("_observerConditions");
            for (int i = 0; i < conditions.arraySize; i++)
            {
                if (conditions.GetArrayElementAtIndex(i).objectReferenceValue == condition)
                {
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }

            conditions.arraySize++;
            conditions.GetArrayElementAtIndex(conditions.arraySize - 1).objectReferenceValue = condition;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // La main : un enfant de la racine à hauteur de main, avec un enfant « Mesh » qui affiche l'objet.
        private static HeldItem EnsureHeldItem(GameObject root, GameObject componentHost, Vector3 handPosition)
        {
            Transform hand = root.transform.Find(HandName);
            if (hand == null)
            {
                hand = new GameObject(HandName).transform;
                hand.SetParent(root.transform, false);
                hand.localPosition = handPosition;

                var mesh = new GameObject("Mesh");
                mesh.transform.SetParent(hand, false);
                mesh.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = mesh.AddComponent<MeshRenderer>();
                meshRenderer.enabled = false;
            }

            if (!componentHost.TryGetComponent(out HeldItem held))
                held = componentHost.AddComponent<HeldItem>();

            Transform meshTransform = hand.Find("Mesh");
            var settings = new SerializedObject(held);
            settings.FindProperty("_meshFilter").objectReferenceValue = meshTransform.GetComponent<MeshFilter>();
            settings.FindProperty("_meshRenderer").objectReferenceValue = meshTransform.GetComponent<MeshRenderer>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            return held;
        }

        // Même composant sur le joueur et le PNJ, à côté de leur script réseau : il fait clignoter le modèle du NpcSkin.
        private static void EnsureHitFlash(GameObject root, GameObject componentHost, Object tuning)
        {
            if (!componentHost.TryGetComponent(out HitFlash flash))
                flash = componentHost.AddComponent<HitFlash>();

            var settings = new SerializedObject(flash);
            settings.FindProperty("_tuning").objectReferenceValue = tuning;
            settings.FindProperty("_skin").objectReferenceValue = root.GetComponentInChildren<NpcSkin>(true);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupBoot()
        {
            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            GameObject overlay = scene.GetRootGameObjects().FirstOrDefault(rootObject => rootObject.name == OverlayName);
            if (overlay == null)
                Debug.LogError("[MatchFeaturesBuilder] Pas d'Overlay dans Boot : lancer d'abord Anachro > Setup > Brancher joueur et PNJ en réseau.");
            else if (!overlay.TryGetComponent(out DevPanel _))
                overlay.AddComponent<DevPanel>();

            NetworkManager networkManager = scene.GetRootGameObjects()
                .Select(rootObject => rootObject.GetComponentInChildren<NetworkManager>(true))
                .FirstOrDefault(component => component != null);
            if (networkManager != null)
            {
                if (!networkManager.TryGetComponent(out MapSelection maps))
                    maps = networkManager.gameObject.AddComponent<MapSelection>();

                // Une entrée par carte des Build Settings : nom affiché = nom de la scène.
                string[] mapPaths = MapScenePaths();
                var settings = new SerializedObject(maps);
                SerializedProperty entries = settings.FindProperty("_maps");
                entries.arraySize = mapPaths.Length;
                for (int i = 0; i < mapPaths.Length; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("DisplayName").stringValue = System.IO.Path.GetFileNameWithoutExtension(mapPaths[i]);
                    entry.FindPropertyRelative("ScenePath").stringValue = mapPaths[i];
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError("[MatchFeaturesBuilder] Pas de NetworkManager dans Boot.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetupMap(string mapPath)
        {
            Scene map = EditorSceneManager.OpenScene(mapPath, OpenSceneMode.Single);
            MatchManager match = map.GetRootGameObjects()
                .Select(rootObject => rootObject.GetComponentInChildren<MatchManager>(true))
                .FirstOrDefault(component => component != null);
            if (match == null)
            {
                Debug.LogWarning($"[MatchFeaturesBuilder] Pas de MatchManager dans {mapPath} : pas de réglages en direct sur cette carte.");
                return;
            }

            if (!match.TryGetComponent(out LiveTuning live))
                live = match.gameObject.AddComponent<LiveTuning>();

            var playerHeld = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath).GetComponentInChildren<HeldItem>(true);
            var settings = new SerializedObject(live);
            settings.FindProperty("_tuning").objectReferenceValue = new SerializedObject(playerHeld).FindProperty("_tuning").objectReferenceValue;
            settings.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(map);
            EditorSceneManager.SaveScene(map);
        }

        private static string[] MapScenePaths() => EditorBuildSettings.scenes
            // Boot est rangé dans le même dossier que les cartes : ce n'en est pas une.
            .Where(scene => scene.enabled && scene.path.StartsWith(MapsFolder) && scene.path != BootScenePath)
            .Select(scene => scene.path)
            .ToArray();
    }
}
