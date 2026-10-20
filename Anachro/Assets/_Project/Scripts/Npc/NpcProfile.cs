using Anachronism.Gameplay;
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
        [SerializeField] private string _speedParameter = "Speed";                // float de l'Animator
        [SerializeField] private float _animPlaybackSpeed = 1f;

        [Header("Vitesse (marche et sprint du joueur)")]
        [SerializeField] private PlayerControllerSettings _playerMovement;
        [SerializeField] private float _acceleration = 28f;
        [SerializeField] private float _speedChangeRate = 12f;

        [Header("Sprint")]
        [SerializeField, Range(0f, 1f)] private float _sprintChance = 0.1f;        // chance de faire tout un trajet en sprint
        [SerializeField] private float _sprintBurstChancePerSecond = 0.02f;         // chance de partir en sprint en plein trajet
        [SerializeField] private Vector2 _sprintBurstDuration = new Vector2(1f, 3f);

        [Header("Orientation")]
        [SerializeField] private float _angularSpeed = 360f;
        [SerializeField] private float _turnSmoothing = 10f;

        [Header("Imperfection")]
        [SerializeField, Range(0f, 0.6f)] private float _wanderAmount = 0.25f;
        [SerializeField] private float _wanderFrequency = 0.6f;
        [SerializeField] private float _headingNoiseDegrees = 8f;

        [Header("Corps / évitement")]
        [SerializeField] private float _radius = 0.4f;
        [SerializeField] private int _avoidancePriority = 50;

        [Header("Départ et arrivée")]
        [SerializeField] private Vector2 _reactionDelayRange = new Vector2(0.2f, 0.6f);
        [SerializeField, Range(0f, 1f)] private float _pauseChance = 0.5f;
        [SerializeField] private Vector2 _pauseDurationRange = new Vector2(0.5f, 2f);
        [SerializeField] private float _stoppingDistance = 0.8f;

        [Header("Destinations")]
        [SerializeField] private Vector2 _walkDistanceRange = new Vector2(8f, 25f);

        [Header("Points intermédiaires")]
        [SerializeField] private Vector2 _waypointSpacingRange = new Vector2(2.5f, 5f);
        [SerializeField, Range(0f, 90f)] private float _waypointMaxAngle = 55f;
        [SerializeField, Min(0.1f)] private float _waypointReachDistance = 1.2f;

        [Header("Blocage")]
        [SerializeField, Min(0.1f)] private float _stuckDuration = 1.5f;

        public NpcAgent Prefab => _prefab;
        public GameObject ModelPrefab => _modelPrefab;
        public RuntimeAnimatorController AnimatorController => _animatorController;
        public float ModelScale => _modelScale;
        public string SpeedParameter => _speedParameter;
        public float AnimPlaybackSpeed => _animPlaybackSpeed;
        public PlayerControllerSettings PlayerMovement => _playerMovement;
        public float WalkSpeed => _playerMovement != null ? _playerMovement.WalkSpeed : 0f;
        public float SprintSpeed => _playerMovement != null ? _playerMovement.WalkSpeed * _playerMovement.SprintMultiplier : 0f;
        public float Acceleration => _acceleration;
        public float SpeedChangeRate => _speedChangeRate;
        public float SprintChance => _sprintChance;
        public float SprintBurstChancePerSecond => _sprintBurstChancePerSecond;
        public Vector2 SprintBurstDuration => _sprintBurstDuration;
        public float AngularSpeed => _angularSpeed;
        public float TurnSmoothing => _turnSmoothing;
        public float WanderAmount => _wanderAmount;
        public float WanderFrequency => _wanderFrequency;
        public float HeadingNoiseDegrees => _headingNoiseDegrees;
        public float Radius => _radius;
        public int AvoidancePriority => _avoidancePriority;
        public Vector2 ReactionDelayRange => _reactionDelayRange;
        public float PauseChance => _pauseChance;
        public Vector2 PauseDurationRange => _pauseDurationRange;
        public float StoppingDistance => _stoppingDistance;
        public Vector2 WalkDistanceRange => _walkDistanceRange;
        public Vector2 WaypointSpacingRange => _waypointSpacingRange;
        public float WaypointMaxAngle => _waypointMaxAngle;
        public float WaypointReachDistance => _waypointReachDistance;
        public float StuckDuration => _stuckDuration;
    }
}
