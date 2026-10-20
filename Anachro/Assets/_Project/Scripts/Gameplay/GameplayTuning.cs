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
        [SerializeField, Min(0f)] private float _finalCountdownSeconds = 60f;
        [SerializeField, Min(1)] private int _maxPhotos = 5;
        [SerializeField, Min(0f)] private float _smallReloadSeconds = 15f;
        [SerializeField, Min(0f)] private float _mediumReloadSeconds = 30f;
        [SerializeField, Min(0f)] private float _largeReloadSeconds = 45f;


        public float CarrierRatio => _carrierRatio;
        public float PlacementDuration => _placementDuration;
        public float WrongShotPenalty => _wrongShotPenalty;
        public float InteractionDistance => _interactionDistance;
        public float ShootDistance => _shootDistance;
        public float FinalCountdownSeconds => _finalCountdownSeconds;
        public int MaxPhotos => _maxPhotos;
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
