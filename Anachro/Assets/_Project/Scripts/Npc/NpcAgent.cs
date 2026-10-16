using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcAgent : MonoBehaviour
{
    private enum State { Idle, Reacting, Walking, Pausing }

    public NavMeshAgent Agent { get; private set; }
    public NpcProfile Profile { get; private set; }
    public bool NeedsDestination => state == State.Idle;

    private State state = State.Idle;
    private float timer;
    private float baseSpeed;
    private Vector3 pendingTarget;

    private bool sprinting;
    private float burstTimer;

    private float seed;
    private float noiseTime;

    private Animator animator;
    private int speedHash;
    private bool hasSpeedParam;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        seed = Random.value * 1000f;
    }

    public void Init(NpcProfile profile)
    {
        Profile = profile;
        baseSpeed = Random.Range(profile.walkSpeedRange.x, profile.walkSpeedRange.y);

        Agent.speed = baseSpeed;
        Agent.acceleration = profile.acceleration;
        Agent.angularSpeed = profile.angularSpeed;
        Agent.radius = profile.radius;
        Agent.stoppingDistance = profile.stoppingDistance;
        Agent.avoidancePriority = profile.avoidancePriority;
        Agent.updateRotation = false;

        SpawnModel();
    }

    private void SpawnModel()
    {
        if (Profile.modelPrefab == null) return;

        GameObject model = Instantiate(Profile.modelPrefab, transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * Profile.modelScale;

        animator = model.GetComponentInChildren<Animator>();
        if (animator == null) return;

        animator.speed = Profile.animPlaybackSpeed;
        animator.applyRootMotion = false;   // c'est le NavMeshAgent qui déplace le PNJ
        if (Profile.animatorController != null)
            animator.runtimeAnimatorController = Profile.animatorController;

        speedHash = Animator.StringToHash(Profile.speedParameter);
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == speedHash) { hasSpeedParam = true; break; }
        }

        if (!hasSpeedParam)
            Debug.LogWarning($"Animator de {name} : paramètre '{Profile.speedParameter}' introuvable.", this);
    }

    public void BeginWalk(Vector3 target)
    {
        pendingTarget = target;
        timer = Random.Range(Profile.reactionDelayRange.x, Profile.reactionDelayRange.y);
        Agent.isStopped = true;
        state = State.Reacting;

        // Tout le trajet en sprint ?
        sprinting = Random.value < Profile.sprintChance;
        burstTimer = -1f;
    }

    public void Wait(float seconds)
    {
        timer = seconds;
        sprinting = false;
        state = State.Pausing;
    }

    public void Tick(float dt)
    {
        noiseTime += dt;

        switch (state)
        {
            case State.Reacting:
                timer -= dt;
                if (timer <= 0f)
                {
                    Agent.isStopped = false;
                    Agent.SetDestination(pendingTarget);
                    state = State.Walking;
                }
                break;

            case State.Walking:
                UpdateSprint(dt);
                UpdateSpeed(dt);
                ApplyWander(dt);
                if (HasArrived())
                    Wait(Random.Range(Profile.pauseDurationRange.x, Profile.pauseDurationRange.y));
                break;

            case State.Pausing:
                timer -= dt;
                if (timer <= 0f) state = State.Idle;
                break;
        }

        UpdateRotation(dt);
        UpdateAnimator(dt);
    }

    private void UpdateSprint(float dt)
    {
        if (!sprinting)
        {
            // Sprint ponctuel en plein trajet
            if (Random.value < Profile.sprintBurstChancePerSecond * dt)
            {
                sprinting = true;
                burstTimer = Random.Range(Profile.sprintBurstDuration.x, Profile.sprintBurstDuration.y);
            }
        }
        else if (burstTimer > 0f)
        {
            burstTimer -= dt;
            if (burstTimer <= 0f) sprinting = false;
        }
    }

    private float Noise(float offset, float frequency)
    {
        return Mathf.PerlinNoise(seed + offset, noiseTime * frequency) * 2f - 1f;
    }

    private void UpdateSpeed(float dt)
    {
        float mult = sprinting ? Profile.sprintSpeedMultiplier : 1f;

        Vector3 desired = Agent.desiredVelocity; desired.y = 0f;
        float factor = 1f;
        if (desired.sqrMagnitude > 0.01f)
        {
            float angle = Vector3.Angle(transform.forward, desired);
            factor = 1f - Profile.cornerSlowdown * Mathf.Clamp01(angle / 90f);
        }

        factor *= 1f + Noise(50f, Profile.speedNoiseFrequency) * Profile.speedNoiseAmount;

        Agent.acceleration = Profile.acceleration * mult;
        Agent.speed = Mathf.MoveTowards(Agent.speed, baseSpeed * mult * factor, Profile.speedChangeRate * mult * dt);
    }

    private void ApplyWander(float dt)
    {
        Vector3 vel = Agent.velocity; vel.y = 0f;
        float speed = vel.magnitude;
        if (speed < 0.1f) return;

        // On court plus droit qu'on ne marche
        float amount = Profile.wanderAmount * (sprinting ? 0.5f : 1f);

        Vector3 right = Vector3.Cross(Vector3.up, vel / speed);
        float lateral = Noise(0f, Profile.wanderFrequency) * amount * speed;
        Agent.Move(right * (lateral * dt));
    }

    private void UpdateRotation(float dt)
    {
        Vector3 v = Agent.velocity; v.y = 0f;
        if (v.sqrMagnitude < 0.05f) return;

        float yawOffset = Noise(100f, Profile.wanderFrequency) * Profile.headingNoiseDegrees;
        Quaternion target = Quaternion.Euler(0f, yawOffset, 0f) * Quaternion.LookRotation(v);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Profile.turnSmoothing * dt);
    }

    private float ToAnimValue(float speed)
    {
        float halfWalk = baseSpeed * 0.5f;
        float walkMax = baseSpeed * 1.15f;                       // tolère le bruit de vitesse
        float sprintSpeed = baseSpeed * Profile.sprintSpeedMultiplier;

        if (speed < halfWalk) return speed / halfWalk;           // idle -> marche
        if (speed <= walkMax || sprintSpeed <= walkMax) return 1f;   // plateau de marche
        return 1f + Mathf.Clamp01((speed - walkMax) / (sprintSpeed - walkMax));   // marche -> course
    }

    private void UpdateAnimator(float dt)
    {
        if (animator == null || !hasSpeedParam) return;

        Vector3 v = Agent.velocity; v.y = 0f;
        animator.SetFloat(speedHash, ToAnimValue(v.magnitude), 0.1f, dt);
    }

    private bool HasArrived()
    {
        if (Agent.pathPending) return false;
        return Agent.remainingDistance <= Agent.stoppingDistance
               && (!Agent.hasPath || Agent.velocity.sqrMagnitude < 0.01f);
    }
}