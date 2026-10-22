using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Anachronism.Npc
{
    // Sur l'enfant « Controller » du PNJ : il pilote le NavMeshAgent de la racine et le skin de l'enfant « Model ».
    public class NpcAgent : MonoBehaviour
    {
        private enum State { Idle, Reacting, Walking, Pausing }

        private const float StuckSpeedRatio = 0.2f;
        private const float SlotRefreshDistance = 0.3f;

        public NavMeshAgent Agent { get; private set; }
        /// <summary>Racine du PNJ : l'objet que le NavMeshAgent déplace et que le réseau synchronise.</summary>
        public Transform Body => Agent.transform;
        public NpcProfile Profile { get; private set; }
        public float BaseSpeed => _baseSpeed;
        public bool NeedsDestination => _state == State.Idle;
        public bool NeedsNextWaypoint => _state == State.Walking && !_isFinalWaypoint && !IsFollower
                                         && (_needsWaypointNow || (!Agent.pathPending && Agent.remainingDistance <= Profile.WaypointReachDistance));
        public Vector3 Destination => _destination;
        public bool IsWalking => _state == State.Walking;
        public bool IsInGroup => Group != null;
        public bool CanFormGroup => IsWalking && Group == null && _groupRetryTimer <= 0f;
        public bool IsLeaderAtGroupPoint => Group != null && Group.Leader == this && IsWalking && _isFinalWaypoint
                                            && !Agent.pathPending && Agent.remainingDistance <= Profile.WaypointReachDistance;

        internal NpcGroup Group { get; private set; }
        private bool IsFollower => Group != null && Group.Leader != this;

        private bool _hasFaceTarget;
        private Vector3 _faceTarget;
        private float _pauseOverride;
        private float _groupRetryTimer;
        private bool _needsWaypointNow;
        private float _stuckTimer;

        private State _state = State.Idle;
        private float _timer;
        private Vector3 _pendingTarget;
        private Vector3 _destination;
        private bool _isFinalWaypoint;
        private bool _willPauseAtDestination;
        private bool _skipNextReaction;

        private bool _isSprinting;
        private float _burstTimer;

        private float _seed;
        private float _noiseTime;

        private float _baseSpeed;
        private NpcSkin _skin;

        private void Awake()
        {
            Agent = GetComponentInParent<NavMeshAgent>();
            _skin = Agent.GetComponentInChildren<NpcSkin>(true);
            _seed = Random.value * 1000f;
        }

        // Hors ligne ou côté serveur : IA complète + visuel.
        public void Init(NpcProfile profile)
        {
            InitVisual(profile, profile.WalkSpeed);

            Agent.speed = profile.WalkSpeed;
            Agent.acceleration = profile.Acceleration;
            Agent.angularSpeed = profile.AngularSpeed;
            Agent.radius = profile.Radius;
            Agent.stoppingDistance = profile.StoppingDistance;
            Agent.avoidancePriority = profile.AvoidancePriority;
            Agent.updateRotation = false;
        }

        // Client en ligne : pas d'IA (le serveur déplace le PNJ), seulement le modèle et l'Animator.
        // La vitesse de base sert à l'échelle de l'Animator, elle doit être celle envoyée par le serveur.
        public void InitVisual(NpcProfile profile, float baseSpeed)
        {
            Profile = profile;
            _baseSpeed = baseSpeed;
            SetSkin(profile);
        }

        // L'apparence peut venir d'un autre profil que le comportement (skins re-tirés par l'hôte).
        public void SetSkin(NpcProfile skin)
        {
            _skin.Apply(skin, _baseSpeed, Profile.SprintSpeedMultiplier);
        }

        public void BeginWalk(Vector3 destination, Vector3 firstWaypoint, bool isFinalWaypoint)
        {
            _destination = destination;
            _pendingTarget = firstWaypoint;
            _isFinalWaypoint = isFinalWaypoint;
            _willPauseAtDestination = Random.value < Profile.PauseChance;

            // Tout le trajet en sprint ?
            _isSprinting = Random.value < Profile.SprintChance;
            _stuckTimer = 0f;
            _burstTimer = -1f;
            _hasFaceTarget = false;
            _pauseOverride = 0f;

            if (_skipNextReaction)
            {
                _skipNextReaction = false;
                ApplyWaypoint();
                _state = State.Walking;
                return;
            }

            _timer = Random.Range(Profile.ReactionDelayRange.x, Profile.ReactionDelayRange.y);
            Agent.isStopped = true;
            _state = State.Reacting;
        }

        public void SetWaypoint(Vector3 waypoint, bool isFinalWaypoint)
        {
            _pendingTarget = waypoint;
            _isFinalWaypoint = isFinalWaypoint;
            _needsWaypointNow = false;
            ApplyWaypoint();
        }

        public void DelayNextGroupAttempt(float seconds) => _groupRetryTimer = seconds;

        internal void LeadGroup(NpcGroup group)
        {
            Group = group;
            _destination = group.CommonPoint;
            _willPauseAtDestination = false;
            _isSprinting = false;
            _isFinalWaypoint = false;
            _needsWaypointNow = true;
        }

        internal void FollowGroup(NpcGroup group)
        {
            Group = group;
            _destination = group.CommonPoint;
            _isFinalWaypoint = false;
            _needsWaypointNow = false;
        }

        internal void FollowSlot(Vector3 slot, bool isSprinting)
        {
            _isSprinting = isSprinting;
            if (Agent.hasPath && (slot - _pendingTarget).sqrMagnitude < SlotRefreshDistance * SlotRefreshDistance)
                return;

            _pendingTarget = slot;
            Agent.autoBraking = false;
            Agent.SetDestination(slot);
        }

        internal void ContinueAlone()
        {
            Group = null;
            _skipNextReaction = true;
            _state = State.Idle;
        }

        internal void StartDiscussion(Vector3 spot, Vector3 centre, float duration)
        {
            Group = null;
            _destination = spot;
            _pendingTarget = spot;
            _isFinalWaypoint = true;
            _willPauseAtDestination = true;
            _isSprinting = false;
            _faceTarget = centre;
            _hasFaceTarget = true;
            _pauseOverride = duration;
            ApplyWaypoint();
            _state = State.Walking;
        }

        private void LeaveGroup()
        {
            Group?.Remove(this);
            Group = null;
        }

        private void OnDestroy()
        {
            LeaveGroup();
        }

        private void ApplyWaypoint()
        {
            Agent.autoBraking = _isFinalWaypoint && _willPauseAtDestination;
            Agent.SetDestination(_pendingTarget);
        }

        public void Wait(float seconds)
        {
            LeaveGroup();
            _timer = seconds;
            _isSprinting = false;
            _state = State.Pausing;
        }

        public void Tick(float dt)
        {
            _noiseTime += dt;
            if (_groupRetryTimer > 0f)
                _groupRetryTimer -= dt;

            switch (_state)
            {
                case State.Reacting:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        Agent.isStopped = false;
                        ApplyWaypoint();
                        _state = State.Walking;
                    }
                    break;

                case State.Walking:
                    UpdateSprint(dt);
                    UpdateSpeed(dt);
                    ApplyWander(dt);
                    UpdateStuck(dt);
                    if (_isFinalWaypoint)
                        UpdateArrival();
                    break;

                case State.Pausing:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _hasFaceTarget = false;
                        _state = State.Idle;
                    }
                    break;
            }

            UpdateRotation(dt);

            Vector3 velocity = Agent.velocity; velocity.y = 0f;
            UpdateAnimator(velocity.magnitude, dt);
        }

        private void UpdateStuck(float dt)
        {
            float minSpeed = Profile.WalkSpeed * StuckSpeedRatio;
            if (_state != State.Walking || Agent.pathPending || Agent.velocity.sqrMagnitude > minSpeed * minSpeed)
            {
                _stuckTimer = 0f;
                return;
            }

            _stuckTimer += dt;
            if (_stuckTimer < Profile.StuckDuration)
                return;

            _stuckTimer = 0f;
            LeaveGroup();
            _skipNextReaction = true;
            _state = State.Idle;
        }

        private void UpdateArrival()
        {
            if (Group != null)
                return;

            if (_willPauseAtDestination)
            {
                if (HasArrived())
                {
                    float pause = _pauseOverride > 0f ? _pauseOverride : Random.Range(Profile.PauseDurationRange.x, Profile.PauseDurationRange.y);
                    _pauseOverride = 0f;
                    Wait(pause);
                }
                return;
            }

            if (!Agent.pathPending && Agent.remainingDistance <= Profile.WaypointReachDistance)
            {
                LeaveGroup();
                _skipNextReaction = true;
                _state = State.Idle;
            }
        }

        private void UpdateSprint(float dt)
        {
            if (Group != null)
                return;

            if (!_isSprinting)
            {
                // Sprint ponctuel en plein trajet
                if (Random.value < Profile.SprintBurstChancePerSecond * dt)
                {
                    _isSprinting = true;
                    _burstTimer = Random.Range(Profile.SprintBurstDuration.x, Profile.SprintBurstDuration.y);
                }
            }
            else if (_burstTimer > 0f)
            {
                _burstTimer -= dt;
                if (_burstTimer <= 0f) _isSprinting = false;
            }
        }

        private float Noise(float offset, float frequency)
        {
            return Mathf.PerlinNoise(_seed + offset, _noiseTime * frequency) * 2f - 1f;
        }

        private void UpdateSpeed(float dt)
        {
            float targetSpeed = _isSprinting ? Profile.SprintSpeed : Profile.WalkSpeed;
            Agent.speed = Mathf.MoveTowards(Agent.speed, targetSpeed, Profile.SpeedChangeRate * dt);
        }

        private void ApplyWander(float dt)
        {
            Vector3 vel = Agent.velocity; vel.y = 0f;
            float speed = vel.magnitude;
            if (speed < 0.1f) return;

            // On court plus droit qu'on ne marche
            float amount = Profile.WanderAmount * (_isSprinting ? 0.5f : 1f);

            Vector3 right = Vector3.Cross(Vector3.up, vel / speed);
            float lateral = Noise(0f, Profile.WanderFrequency) * amount * speed;
            Agent.Move(right * (lateral * dt));
        }

        private void UpdateRotation(float dt)
        {
            if (_state == State.Pausing && _hasFaceTarget)
            {
                Vector3 toTarget = _faceTarget - Body.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                    Body.rotation = Quaternion.Slerp(Body.rotation, Quaternion.LookRotation(toTarget), Profile.TurnSmoothing * dt);
                return;
            }

            Vector3 v = Agent.velocity; v.y = 0f;
            if (v.sqrMagnitude < 0.05f) return;

            float yawOffset = Noise(100f, Profile.WanderFrequency) * Profile.HeadingNoiseDegrees;
            Quaternion target = Quaternion.Euler(0f, yawOffset, 0f) * Quaternion.LookRotation(v);
            Body.rotation = Quaternion.Slerp(Body.rotation, target, Profile.TurnSmoothing * dt);
        }

        // Piloté par la seule vitesse horizontale (règle réseau 5) : un client la mesure sur le transform synchronisé.
        public void UpdateAnimator(float horizontalSpeed, float dt)
        {
            _skin.SetSpeed(horizontalSpeed, dt);
        }

        private bool HasArrived()
        {
            if (Agent.pathPending) return false;
            return Agent.remainingDistance <= Agent.stoppingDistance
                   && (!Agent.hasPath || Agent.velocity.sqrMagnitude < 0.01f);
        }

#if UNITY_EDITOR
        private const float GizmoLift = 0.1f;
        private const float GroupGizmoHeight = 2.3f;

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || Profile == null)
                return;

            DrawTripGizmos();
            DrawGroupGizmos();
        }

        private void DrawTripGizmos()
        {
            if (_state != State.Walking && _state != State.Reacting)
                return;

            Vector3 lift = Vector3.up * GizmoLift;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(Body.position + lift, _pendingTarget + lift);
            Gizmos.DrawWireSphere(_pendingTarget + lift, 0.25f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(_destination + lift, Vector3.one * 0.6f);
            if (!_isFinalWaypoint)
            {
                Handles.color = Color.cyan;
                Handles.DrawDottedLine(_pendingTarget + lift, _destination + lift, 4f);
            }
        }

        private void DrawGroupGizmos()
        {
            Vector3 head = Body.position + Vector3.up * GroupGizmoHeight;

            if (Group == null)
            {
                if (_state == State.Pausing && _hasFaceTarget)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(head, _faceTarget + Vector3.up * GroupGizmoHeight);
                    Handles.Label(head + Vector3.up * 0.3f, "discute");
                }
                return;
            }

            NpcAgent leader = Group.Leader;
            if (leader == null)
                return;

            Color groupColor = Color.HSVToRGB(Mathf.Repeat(leader.GetEntityId().GetHashCode() * 0.618f, 1f), 0.85f, 1f);
            Gizmos.color = groupColor;
            Handles.color = groupColor;

            if (leader == this)
            {
                Gizmos.DrawSphere(head, 0.3f);
                Handles.Label(head + Vector3.up * 0.4f, $"Groupe : {Group.Count}{(Group.WillDiscuss ? " (discussion)" : string.Empty)}");
                Vector3 point = Group.CommonPoint + Vector3.up * GizmoLift;
                Handles.DrawWireDisc(point, Vector3.up, 0.8f);
                Handles.DrawDottedLine(Body.position + Vector3.up * GizmoLift, point, 4f);
                return;
            }

            Gizmos.DrawSphere(head, 0.15f);
            Gizmos.DrawLine(head, leader.Body.position + Vector3.up * GroupGizmoHeight);
            Gizmos.DrawWireSphere(_pendingTarget + Vector3.up * GizmoLift, 0.3f);
        }
#endif
    }
}
