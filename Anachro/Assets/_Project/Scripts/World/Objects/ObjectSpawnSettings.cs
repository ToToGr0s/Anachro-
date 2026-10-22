using UnityEngine;

namespace Anachronism.World
{
    /// <summary>
    /// Réglages de la répartition des objets d'époque sur une map (un asset par map), modifiables sans toucher au code.
    /// </summary>
    [CreateAssetMenu(fileName = "ObjectSpawnSettings", menuName = "Anachronism/ObjectSpawnSettings")]
    public sealed class ObjectSpawnSettings : ScriptableObject
    {
        // Nombre d'objets tiré au hasard à chaque partie, doublons compris (un même objet peut apparaître plusieurs fois).
        [SerializeField, Min(0)] private int _minCount = 15;
        [SerializeField, Min(0)] private int _maxCount = 30;
        // Écart minimum entre deux objets, et entre un objet et tout ce qui est déjà posé (PNJ, décor).
        [SerializeField, Min(0f)] private float _minSpacing = 0.5f;
        // Une zone pleine ne doit pas faire tourner la boucle sans fin : au-delà, l'objet est abandonné.
        [SerializeField, Min(1)] private int _attemptsPerObject = 20;
        // Surfaces sur lesquelles un objet peut être posé (sol, tables, chaises).
        [SerializeField] private LayerMask _surfaceLayers = 1;

        public int MinCount => _minCount;
        public int MaxCount => _maxCount;
        public float MinSpacing => _minSpacing;
        public int AttemptsPerObject => _attemptsPerObject;
        public LayerMask SurfaceLayers => _surfaceLayers;

        private void OnValidate()
        {
            if (_maxCount < _minCount)
                _maxCount = _minCount;
        }
    }
}
