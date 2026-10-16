using UnityEngine;

[CreateAssetMenu(menuName = "NPC/Profile", fileName = "NpcProfile")]
public class NpcProfile : ScriptableObject
{
    [Header("Prefab racine (NavMeshAgent + NpcAgent)")]
    public NpcAgent prefab;

    [Header("Visuel")]
    public GameObject modelPrefab;
    public RuntimeAnimatorController animatorController;   // optionnel : remplace celui du modèle
    public float modelScale = 1f;
    public string speedParameter = "Speed";                // float de l'Animator
    public float animPlaybackSpeed = 1f;
    
    [Header("Vitesse")]
    public Vector2 walkSpeedRange = new Vector2(7.5f, 8.5f);
    public float acceleration = 28f;
    [Range(0f, 0.8f)] public float cornerSlowdown = 0.2f;
    public float speedChangeRate = 12f;

    [Header("Sprint")]
    [Range(0f, 1f)] public float sprintChance = 0.1f;        // chance de faire tout un trajet en sprint
    public float sprintSpeedMultiplier = 1.6f;
    public float sprintBurstChancePerSecond = 0.02f;         // chance de partir en sprint en plein trajet
    public Vector2 sprintBurstDuration = new Vector2(1f, 3f);

    [Header("Orientation")]
    public float angularSpeed = 360f;
    public float turnSmoothing = 10f;

    [Header("Imperfection")]
    [Range(0f, 0.6f)] public float wanderAmount = 0.25f;
    public float wanderFrequency = 0.6f;
    public float headingNoiseDegrees = 8f;
    [Range(0f, 0.5f)] public float speedNoiseAmount = 0.12f;
    public float speedNoiseFrequency = 0.4f;

    [Header("Corps / évitement")]
    public float radius = 0.4f;
    public int avoidancePriority = 50;

    [Header("Départ et arrivée")]
    public Vector2 reactionDelayRange = new Vector2(0.2f, 0.6f);
    public Vector2 pauseDurationRange = new Vector2(0.5f, 2f);
    public float stoppingDistance = 0.8f;

    [Header("Destinations")]
    public Vector2 walkDistanceRange = new Vector2(8f, 25f);
}