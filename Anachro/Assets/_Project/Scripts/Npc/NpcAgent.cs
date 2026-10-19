using UnityEngine;
using UnityEngine.AI;

namespace Anachronism.Npc
{
    // Sur l'enfant « Controller » du PNJ : il pilote le NavMeshAgent de la racine et le skin de l'enfant « Model ».
    public class NpcAgent : MonoBehaviour
    {
        private enum State { Idle, Reacting, Walking, Pausing }

        public NavMeshAgent Agent { get; private set; }
        /// <summary>Racine du PNJ : l'objet que le NavMeshAgent déplace et que le réseau synchronise.</summary>
        public Transform Body => Agent.transform;
        public NpcProfile Profile { get; private set; }
        public bool NeedsDestination => _state == State.Idle;
        public float BaseSpeed => _baseSpeed;

        private State _state = State.Idle;
        private float _timer;
        private float _baseSpeed;
        private Vector3 _pendingTarget;

        private bool _isSprinting;
        private float _burstTimer;

        private float _seed;
        private float _noiseTime;

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
            InitVisual(profile, Random.Range(profile.WalkSpeedRange.x, profile.WalkSpeedRange.y));

            Agent.speed = _baseSpeed;
            Agent.acceleration = profile.Acceleration;
            Agent.angularSpeed = profile.AngularSpeed;
            Agent.radius = profile.Radius;
            Agent.stoppingDistance = profile.StoppingDistance;
            Agent.avoidancePriority = profile.AvoidancePriority;
            Agent.updateRotation = false;
        }

        // Client en ligne : pas d'IA (le serveur déplace le PNJ), seulement le modèle et l'Animator.
        // La vitesse de base sert à l'échelle de l'Animator, elle doit être celle tirée par le serveur.
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

        public void BeginWalk(Vector3 target)
        {
            _pendingTarget = target;
            _timer = Random.Range(Profile.ReactionDelayRange.x, Profile.ReactionDelayRange.y);
            Agent.isStopped = true;
            _state = State.Reacting;

            // Tout le trajet en sprint ?
            _isSprinting = Random.value < Profile.SprintChance;
            _burstTimer = -1f;
        }

        public void Wait(float seconds)
        {
            _timer = seconds;
            _isSprinting = false;
            _state = State.Pausing;
        }

        public void Tick(float dt)
        {
            _noiseTime += dt;

            switch (_state)
            {
                case State.Reacting:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        Agent.isStopped = false;
                        Agent.SetDestination(_pendingTarget);
                        _state = State.Walking;
                    }
                    break;

                case State.Walking:
                    UpdateSprint(dt);
                    UpdateSpeed(dt);
                    ApplyWander(dt);
                    if (HasArrived())
                        Wait(Random.Range(Profile.PauseDurationRange.x, Profile.PauseDurationRange.y));
                    break;

                case State.Pausing:
                    _timer -= dt;
                    if (_timer <= 0f) _state = State.Idle;
                    break;
            }

            UpdateRotation(dt);

            Vector3 velocity = Agent.velocity; velocity.y = 0f;
            UpdateAnimator(velocity.magnitude, dt);
        }

        private void UpdateSprint(float dt)
        {
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
            float mult = _isSprinting ? Profile.SprintSpeedMultiplier : 1f;

            Vector3 desired = Agent.desiredVelocity; desired.y = 0f;
            float factor = 1f;
            if (desired.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.Angle(Body.forward, desired);
                factor = 1f - Profile.CornerSlowdown * Mathf.Clamp01(angle / 90f);
            }

            factor *= 1f + Noise(50f, Profile.SpeedNoiseFrequency) * Profile.SpeedNoiseAmount;

            Agent.acceleration = Profile.Acceleration * mult;
            Agent.speed = Mathf.MoveTowards(Agent.speed, _baseSpeed * mult * factor, Profile.SpeedChangeRate * mult * dt);
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
    }
}
