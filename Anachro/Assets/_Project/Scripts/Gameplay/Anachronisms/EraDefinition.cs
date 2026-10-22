using Anachronism.Npc;
using System.Collections.Generic;
using UnityEngine;

namespace Anachronism.Gameplay
{
    [CreateAssetMenu(fileName = "EraDefinition", menuName = "Anachronism/EraDefinition")]
    public class EraDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Era _era;
        [SerializeField] private NpcSkinTable _npcs;
        // Listes figées par le game design plutôt que recalculées à chaque partie : on choisit ce qui apparaît et ce qui se pose.
        // Objets répartis sur la map au lancement : de cette époque.
        [SerializeField] private AnachronismDefinition[] _spawnedObjects = new AnachronismDefinition[0];
        // Objets que les fugitifs peuvent poser : d'une autre époque.
        [SerializeField] private AnachronismDefinition[] _placeableObjects = new AnachronismDefinition[0];
        [SerializeField] private AudioClip _music;
        [SerializeField] private string _mapScene;

        public string DisplayName => _displayName;
        public Era Era => _era;
        public NpcSkinTable Npcs => _npcs;
        public IReadOnlyList<AnachronismDefinition> SpawnedObjects => _spawnedObjects;
        public IReadOnlyList<AnachronismDefinition> PlaceableObjects => _placeableObjects;
        public AudioClip Music => _music;
        public string MapScene => _mapScene;

        /// <summary>Objet posable de cette taille, au hasard (barre d'items du fugitif). Null si aucun.</summary>
        public AnachronismDefinition RandomPlaceable(AnachronismSize size)
        {
            var candidates = new List<AnachronismDefinition>();
            foreach (AnachronismDefinition definition in _placeableObjects)
            {
                if (definition != null && definition.Size == size)
                    candidates.Add(definition);
            }
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        // Les listes sont remplies à la main : on signale tout de suite un objet rangé du mauvais côté de la règle.
        private void OnValidate()
        {
            CheckList(_spawnedObjects, "à faire apparaître", shouldBeAnachronism: false);
            CheckList(_placeableObjects, "posables", shouldBeAnachronism: true);
        }

        private void CheckList(AnachronismDefinition[] list, string label, bool shouldBeAnachronism)
        {
            if (list == null)
                return;

            for (int i = 0; i < list.Length; i++)
            {
                AnachronismDefinition definition = list[i];
                if (definition == null)
                    Debug.LogError($"{name} : objets {label}, case {i} vide.", this);
                else if (EraRules.IsAnachronism(definition.Era, _era) != shouldBeAnachronism)
                    Debug.LogError($"{name} ({_era}) : {definition.name} ({definition.Era}) ne peut pas être dans les objets {label}.", this);
            }
        }
    }
}
