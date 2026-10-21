using UnityEngine;

namespace Anachronism.Gameplay
{
    [CreateAssetMenu(fileName = "GameplayTuning", menuName = "Anachronism/GameplayTuning")]
    public class GameplayTuning : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float _carrierRatio = 0.25f;
        [SerializeField, Min(0f)] private float _placementDuration = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _wrongShotPenalty = 0.04f;
        [SerializeField, Min(0f)] private float _interactionDistance = 2f;
        [SerializeField, Min(0f)] private float _shootDistance = 5f;
        [SerializeField, Min(1)] private int _maxPhotos = 5;
        [Tooltip("Une surface est un sol si sa normale.y est au moins cette valeur (1 = sol parfaitement plat).")]
        [SerializeField, Range(0f, 1f)] private float _floorMinNormalY = 0.7f;
        [Tooltip("Une surface est un mur si |normale.y| est au plus cette valeur (0 = mur parfaitement vertical).")]
        [SerializeField, Range(0f, 1f)] private float _wallMaxAbsNormalY = 0.3f;
        [Tooltip("Part minimale (0-1) de la face posee qui doit reposer sur la surface ; en dessous l'objet flotte et la pose est refusee.")]
        [SerializeField, Range(0f, 1f)] private float _minSupportRatio = 0.7f;
        [Tooltip("Geometrie statique (Default) + objets poses : surfaces visees et obstacles. Les personnages (couche Character) n'en font pas partie.")]
        [SerializeField] private LayerMask _placementMask = 1;
        [Tooltip("Couche des objets deja poses : ils ne sont ni une surface valide ni un appui.")]
        [SerializeField] private LayerMask _placedObjectsMask;
        [SerializeField, Min(0f)] private float _smallReloadSeconds = 15f;
        [SerializeField, Min(0f)] private float _mediumReloadSeconds = 30f;
        [SerializeField, Min(0f)] private float _largeReloadSeconds = 45f;

        public float CarrierRatio => _carrierRatio;
        public float PlacementDuration => _placementDuration;
        public float WrongShotPenalty => _wrongShotPenalty;
        public float InteractionDistance => _interactionDistance;
        public float ShootDistance => _shootDistance;
        public int MaxPhotos => _maxPhotos;
        public float FloorMinNormalY => _floorMinNormalY;
        public float WallMaxAbsNormalY => _wallMaxAbsNormalY;
        public float MinSupportRatio => _minSupportRatio;
        public LayerMask PlacementMask => _placementMask;
        public LayerMask PlacedObjectsMask => _placedObjectsMask;
        public float GetReloadSeconds(AnachronismSize size)
        {
            switch (size)
            {
                case AnachronismSize.Small: return _smallReloadSeconds;
                case AnachronismSize.Medium: return _mediumReloadSeconds;
                case AnachronismSize.Large: return _largeReloadSeconds;
                default:
                    Debug.LogError($"GameplayTuning : taille inconnue ({size}).", this);
                    return _largeReloadSeconds;
            }
        }
    }
}
