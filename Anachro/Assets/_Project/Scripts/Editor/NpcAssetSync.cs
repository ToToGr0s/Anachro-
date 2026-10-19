using System.Linq;
using Anachronism.Network;
using Anachronism.Npc;
using UnityEditor;
using UnityEngine;

namespace Anachronism.Editor
{
    /// <summary>
    /// Plus aucune liste de PNJ à tenir à la main : dès qu'un NpcProfile est créé, modifié ou supprimé,
    /// - tous les profils qui ont un modèle deviennent des skins (table NpcSkins, tirée pour les PNJ et les joueurs) ;
    /// - tous les profils branchés sur Npc_v0 deviennent des comportements que le réseau sait transmettre (NetworkNpc).
    /// Ordre alphabétique : l'index envoyé sur le réseau désigne le même profil dans tous les builds.
    /// </summary>
    public sealed class NpcAssetSync : AssetPostprocessor
    {
        public const string SkinTablePath = "Assets/_Project/ScriptableObjects/Npc/NpcSkins.asset";
        public const string NpcPrefabPath = "Assets/_Project/Prefabs/Npc/Npc_v0.prefab";

        private static bool _isSyncQueued;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touchesProfiles = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(path => path.EndsWith(".asset") && !path.EndsWith("NpcSkins.asset"));
            if (!touchesProfiles || _isSyncQueued)
                return;

            // Après l'import en cours : écrire des assets pendant un import en relancerait un autre.
            _isSyncQueued = true;
            EditorApplication.delayCall += () =>
            {
                _isSyncQueued = false;
                Sync();
            };
        }

        [MenuItem("Anachro/PNJ/Resynchroniser les profils et skins")]
        public static void Sync()
        {
            NpcProfile[] profiles = AssetDatabase.FindAssets("t:" + nameof(NpcProfile))
                .Select(guid => AssetDatabase.LoadAssetAtPath<NpcProfile>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(profile => profile != null)
                .OrderBy(profile => profile.name)
                .ToArray();

            SyncSkinTable(profiles.Where(profile => profile.ModelPrefab != null).ToArray());
            SyncNpcPrefab(profiles.Where(profile => profile.Prefab != null && AssetDatabase.GetAssetPath(profile.Prefab) == NpcPrefabPath).ToArray());
        }

        private static void SyncSkinTable(NpcProfile[] skins)
        {
            NpcSkinTable table = AssetDatabase.LoadAssetAtPath<NpcSkinTable>(SkinTablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<NpcSkinTable>();
                AssetDatabase.CreateAsset(table, SkinTablePath);
            }

            SerializedObject tableSettings = new SerializedObject(table);
            if (WriteList(tableSettings.FindProperty("_skins"), skins))
            {
                tableSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(table);
                Debug.Log($"[NpcAssetSync] {skins.Length} skin(s) : {string.Join(", ", skins.Select(skin => skin.name))}.");
            }
        }

        private static void SyncNpcPrefab(NpcProfile[] behaviours)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            NetworkNpc networkNpc = prefab != null ? prefab.GetComponentInChildren<NetworkNpc>(true) : null;
            if (networkNpc == null)
                return;

            SerializedObject npcSettings = new SerializedObject(networkNpc);
            bool hasChanged = WriteList(npcSettings.FindProperty("_profiles"), behaviours);

            SerializedProperty skinsProperty = npcSettings.FindProperty("_skins");
            NpcSkinTable table = AssetDatabase.LoadAssetAtPath<NpcSkinTable>(SkinTablePath);
            if (skinsProperty.objectReferenceValue != table)
            {
                skinsProperty.objectReferenceValue = table;
                hasChanged = true;
            }

            if (!hasChanged)
                return;

            npcSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(prefab);
        }

        // N'écrit que si la liste change : sinon chaque import resauvegarderait les assets (et relancerait un import).
        private static bool WriteList(SerializedProperty list, NpcProfile[] values)
        {
            bool isSame = list.arraySize == values.Length;
            for (int i = 0; isSame && i < values.Length; i++)
                isSame = list.GetArrayElementAtIndex(i).objectReferenceValue == values[i];
            if (isSame)
                return false;

            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            return true;
        }
    }
}
