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

    private float seed;
    private float noiseTime;

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
    }

    public void BeginWalk(Vector3 target)
    {
        pendingTarget = target;
        timer = Random.Range(Profile.reactionDelayRange.x, Profile.reactionDelayRange.y);
        Agent.isStopped = true;
        state = State.Reacting;
    }

    public void Wait(float seconds)
    {
        timer = seconds;
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
    }

    private float Noise(float offset, float frequency)
    {
        // Perlin ramené entre -1 et 1, unique pour ce PNJ grâce à la graine
        return Mathf.PerlinNoise(seed + offset, noiseTime * frequency) * 2f - 1f;
    }

    private void UpdateSpeed(float dt)
    {
        Vector3 desired = Agent.desiredVelocity; desired.y = 0f;
        float factor = 1f;
        if (desired.sqrMagnitude > 0.01f)
        {
            float angle = Vector3.Angle(transform.forward, desired);
            factor = 1f - Profile.cornerSlowdown * Mathf.Clamp01(angle / 90f);
        }

        factor *= 1f + Noise(50f, Profile.speedNoiseFrequency) * Profile.speedNoiseAmount;

        Agent.speed = Mathf.MoveTowards(Agent.speed, baseSpeed * factor, Profile.speedChangeRate * dt);
    }

    private void ApplyWander(float dt)
    {
        Vector3 vel = Agent.velocity; vel.y = 0f;
        float speed = vel.magnitude;
        if (speed < 0.1f) return;

        Vector3 right = Vector3.Cross(Vector3.up, vel / speed);
        float lateral = Noise(0f, Profile.wanderFrequency) * Profile.wanderAmount * speed;
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

    private bool HasArrived()
    {
        if (Agent.pathPending) return false;
        return Agent.remainingDistance <= Agent.stoppingDistance
               && (!Agent.hasPath || Agent.velocity.sqrMagnitude < 0.01f);
    }
}