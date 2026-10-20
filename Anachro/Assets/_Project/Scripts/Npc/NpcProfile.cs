using UnityEngine;

namespace Anachronism.Npc
{
    [CreateAssetMenu(menuName = "Anachronism/Npc Profile", fileName = "NpcProfile")]
    public class NpcProfile : ScriptableObject
    {
        [Header("Prefab racine (NavMeshAgent + NpcAgent)")]
        [SerializeField] private NpcAgent _prefab;

        [Header("Visuel")]
        [SerializeField] private GameObject _modelPrefab;
        [SerializeField] private RuntimeAnimatorController _animatorController;   // optionnel : remplace celui du modèle
        [SerializeField] private float _modelScale = 1f;
        [Tooltip("Monte (+) ou descend (−) le modèle, en mètres, si ses pieds ne touchent pas le sol. Réglable en Play : le modèle bouge en direct et la valeur reste après l'arrêt.")]
        [SerializeField] private float _modelHeightOffset = 0f;
        [SerializeField] private string _speedParameter = "Speed";                // float de l'Animator
        [SerializeField] private float _animPlaybackSpeed = 1f;

        [Header("Vitesse")]
        [SerializeField] private Vector2 _walkSpeedRange = new Vector2(7.5f, 8.5f);
        [SerializeField] private float _acceleration = 28f;
        [SerializeField, Range(0f, 0.8f)] private float _cornerSlowdown = 0.2f;
        [SerializeField] private float _speedChangeRate = 12f;

        [Header("Sprint")]
        [SerializeField, Range(0f, 1f)] private float _sprintChance = 0.1f;        // chance de faire tout un trajet en sprint
        [SerializeField] private float _sprintSpeedMultiplier = 1.6f;
        [SerializeField] private float _sprintBurstChancePerSecond = 0.02f;         // chance de partir en sprint en plein trajet
        [SerializeField] private Vector2 _sprintBurstDuration = new Vector2(1f, 3f);

        [Header("Orientation")]
        [SerializeField] private float _angularSpeed = 360f;
        [SerializeField] private float _turnSmoothing = 10f;

        [Header("Imperfection")]
        [SerializeField, Range(0f, 0.6f)] private float _wanderAmount = 0.25f;
        [SerializeField] private float _wanderFrequency = 0.6f;
        [SerializeField] private float _headingNoiseDegrees = 8f;
        [SerializeField, Range(0f, 0.5f)] private float _speedNoiseAmount = 0.12f;
        [SerializeField] private float _speedNoiseFrequency = 0.4f;

        [Header("Corps / évitement")]
        [SerializeField] private float _radius = 0.4f;
        [SerializeField] private int _avoidancePriority = 50;

        [Header("Départ et arrivée")]
        [SerializeField] private Vector2 _reactionDelayRange = new Vector2(0.2f, 0.6f);
        [SerializeField] private Vector2 _pauseDurationRange = new Vector2(0.5f, 2f);
        [SerializeField] private float _stoppingDistance = 0.8f;

        [Header("Destinations")]
        [SerializeField] private Vector2 _walkDistanceRange = new Vector2(8f, 25f);

        public NpcAgent Prefab => _prefab;
        public GameObject ModelPrefab => _modelPrefab;
        public RuntimeAnimatorController AnimatorController => _animatorController;
        public float ModelScale => _modelScale;
        public float ModelHeightOffset => _modelHeightOffset;
        public string SpeedParameter => _speedParameter;
        public float AnimPlaybackSpeed => _animPlaybackSpeed;
        public Vector2 WalkSpeedRange => _walkSpeedRange;
        public float Acceleration => _acceleration;
        public float CornerSlowdown => _cornerSlowdown;
        public float SpeedChangeRate => _speedChangeRate;
        public float SprintChance => _sprintChance;
        public float SprintSpeedMultiplier => _sprintSpeedMultiplier;
        public float SprintBurstChancePerSecond => _sprintBurstChancePerSecond;
        public Vector2 SprintBurstDuration => _sprintBurstDuration;
        public float AngularSpeed => _angularSpeed;
        public float TurnSmoothing => _turnSmoothing;
        public float WanderAmount => _wanderAmount;
        public float WanderFrequency => _wanderFrequency;
        public float HeadingNoiseDegrees => _headingNoiseDegrees;
        public float SpeedNoiseAmount => _speedNoiseAmount;
        public float SpeedNoiseFrequency => _speedNoiseFrequency;
        public float Radius => _radius;
        public int AvoidancePriority => _avoidancePriority;
        public Vector2 ReactionDelayRange => _reactionDelayRange;
        public Vector2 PauseDurationRange => _pauseDurationRange;
        public float StoppingDistance => _stoppingDistance;
        public Vector2 WalkDistanceRange => _walkDistanceRange;
    }
}
