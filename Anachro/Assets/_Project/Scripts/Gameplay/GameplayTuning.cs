using UnityEngine;

namespace Anachronism.Gameplay
{
    [CreateAssetMenu(fileName = "GameplayTuning", menuName = "Anachronism/GameplayTuning")]
    public class GameplayTuning : ScriptableObject
    {
        [Tooltip("Part des PNJ qui tiennent un objet de l'époque : tenir un objet ne prouve pas qu'on est fugitif.")]
        [SerializeField, Range(0f, 1f)] private float _carrierRatio = 0.25f;
        [Tooltip("Plus grande dimension d'un objet tenu en main (m) : un gros objet est réduit pour ne pas cacher le personnage.")]
        [SerializeField, Min(0.05f)] private float _heldItemMaxSize = 0.4f;
        [SerializeField, Min(0f)] private float _placementDuration = 0.5f;
        [Tooltip("Part de barre ajoutee par un tir sur un PNJ ou un objet de l'epoque de la map.")]
        [SerializeField, Range(0f, 1f)] private float _wrongShotPenalty = 0.04f;
        [Tooltip("Part de barre ajoutee par un tir sur un autre chasseur.")]
        [SerializeField, Range(0f, 1f)] private float _teammateShotPenalty = 0.02f;
        [SerializeField, Min(0f)] private float _interactionDistance = 2f;
        [SerializeField, Min(0f)] private float _shootDistance = 5f;
        [Tooltip("Marge ajoutee a la distance de pose pour la validation : le mouvement est client-autoritaire (latence) et le serveur ne connait pas la camera.")]
        [SerializeField, Min(0f)] private float _placementDistanceMargin = 0.5f;
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
        [SerializeField, Min(0f)] private float _shootCooldown = 1f;
        [Tooltip("Part des recharges (tir, pose) que l'hôte exige vraiment : les demandes arrivent avec la gigue du réseau, deux tirs envoyés à 1 s d'écart peuvent arriver à 0,9 s.")]
        [SerializeField, Range(0.5f, 1f)] private float _serverCooldownTolerance = 0.8f;
        [SerializeField] private LayerMask _shootMask = 1;
        [Tooltip("Couleur du personnage touché par un tir (PNJ ou joueur, même rendu pour tous).")]
        [SerializeField] private Color _hitFlashColor = new Color(1f, 0.1f, 0.1f, 1f);
        [Tooltip("Nombre de clignotements rouges d'un personnage touché.")]
        [SerializeField, Min(1)] private int _hitFlashCount = 3;
        [Tooltip("Durée totale du clignotement (s).")]
        [SerializeField, Min(0.05f)] private float _hitFlashDuration = 0.6f;
        [SerializeField, Min(0f)] private float _smallReloadSeconds = 15f;
        [SerializeField, Min(0f)] private float _mediumReloadSeconds = 30f;
        [SerializeField, Min(0f)] private float _largeReloadSeconds = 45f;
        // Part de barre d'un objet selon sa taille (GDD 2.11), avant division par le nombre de fugitifs.
        // Ajoutée à la pose, retirée quand un chasseur détruit l'anachronisme.
        [SerializeField, Range(0f, 1f)] private float _smallDisruption = 0.07f;
        [SerializeField, Range(0f, 1f)] private float _mediumDisruption = 0.14f;
        [SerializeField, Range(0f, 1f)] private float _largeDisruption = 0.21f;

        public float CarrierRatio => _carrierRatio;
        public float HeldItemMaxSize => _heldItemMaxSize;
        public float PlacementDuration => _placementDuration;
        public float WrongShotPenalty => _wrongShotPenalty;
        public float TeammateShotPenalty => _teammateShotPenalty;
        public float InteractionDistance => _interactionDistance;
        public float ShootDistance => _shootDistance;
        public float PlacementDistanceMargin => _placementDistanceMargin;
        public int MaxPhotos => _maxPhotos;
        public float FloorMinNormalY => _floorMinNormalY;
        public float WallMaxAbsNormalY => _wallMaxAbsNormalY;
        public float MinSupportRatio => _minSupportRatio;
        public LayerMask PlacementMask => _placementMask;
        public LayerMask PlacedObjectsMask => _placedObjectsMask;
        public float ShootCooldown => _shootCooldown;
        public float ServerCooldownTolerance => _serverCooldownTolerance;
        public LayerMask ShootMask => _shootMask;
        public Color HitFlashColor => _hitFlashColor;
        public int HitFlashCount => _hitFlashCount;
        public float HitFlashDuration => _hitFlashDuration;
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

        /// <summary>Part de barre (0..1) d'un objet de cette taille pour une partie à villainCount fugitifs.</summary>
        public float GetDisruption(AnachronismSize size, int villainCount)
        {
            float disruption;
            switch (size)
            {
                case AnachronismSize.Small: disruption = _smallDisruption; break;
                case AnachronismSize.Medium: disruption = _mediumDisruption; break;
                case AnachronismSize.Large: disruption = _largeDisruption; break;
                default:
                    Debug.LogError($"GameplayTuning : taille inconnue ({size}).", this);
                    disruption = _largeDisruption;
                    break;
            }
            return disruption / Mathf.Max(villainCount, 1);
        }
    }
}
