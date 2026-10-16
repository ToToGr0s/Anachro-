using UnityEngine;

[CreateAssetMenu(menuName = "NPC/Profile", fileName = "NpcProfile")]
public class NpcProfile : ScriptableObject
{
    [Header("Prefab")]
    public NpcAgent prefab;

    [Header("Vitesse")]
    public Vector2 walkSpeedRange = new Vector2(1.2f, 1.6f);   // tirée une fois par PNJ
    public float acceleration = 8f;
    [Range(0f, 0.8f)] public float cornerSlowdown = 0.4f;     // ralentissement dans les virages
    public float speedChangeRate = 3f;                         // lissage de la vitesse

    [Header("Orientation")]
    public float angularSpeed = 240f;
    public float turnSmoothing = 8f;

    [Header("Corps / évitement")]
    public float radius = 0.4f;
    public int avoidancePriority = 50;

    [Header("Départ et arrivée")]
    public Vector2 reactionDelayRange = new Vector2(0.2f, 0.7f);
    public Vector2 pauseDurationRange = new Vector2(1f, 5f);
    public float stoppingDistance = 0.5f;

    [Header("Destinations")]
    public Vector2 walkDistanceRange = new Vector2(5f, 25f);   // distance à vol d'oiseau
    
    [Header("Imperfection")]
    [Range(0f, 0.6f)] public float wanderAmount = 0.25f;        // dérive latérale (fraction de la vitesse)
    public float wanderFrequency = 0.6f;                         // vitesse d'oscillation (Hz environ)
    public float headingNoiseDegrees = 8f;                       // écart max du cap
    [Range(0f, 0.5f)] public float speedNoiseAmount = 0.12f;     // variation de vitesse
    public float speedNoiseFrequency = 0.4f;
}