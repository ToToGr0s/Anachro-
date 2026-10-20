using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

namespace Anachronism.Gameplay
{
    [CreateAssetMenu(fileName = "AnachronismDefinition", menuName = "Anachronism/AnachronismDefinition")]
    public class AnachronismDefinition : ScriptableObject
    {
        [SerializeField] private AnachronismSize _size;
        [SerializeField] private NetworkObject _prefab;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;
        [Tooltip("% de la barre de degradation retire quand l'objet est trouve (0-100)")]
        [SerializeField][Range(0, 100)] private float _disruptionPercent;
        [SerializeField] private EraDefinition[] _allowedEras;
        public AnachronismSize Size => _size;
        public NetworkObject Prefab => _prefab;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public float DisruptionPercent => _disruptionPercent;
        public IReadOnlyList<EraDefinition> AllowedEras => _allowedEras;

        public bool IsAllowedIn(EraDefinition era)
        {
            if (era == null)
                return false;   // pas d'epoque connue : on ne devine pas

            if (_allowedEras == null || _allowedEras.Length == 0)
                return true;   // liste vide = permis dans toutes les epoques

            return System.Array.IndexOf(_allowedEras, era) >= 0;
        }

        private void OnValidate()
        {
            if (_prefab == null)
                Debug.LogWarning($"{name} : prefab manquant, a assigner avant la pose en ligne.", this);
        }
    }
}
