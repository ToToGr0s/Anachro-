using UnityEngine;

namespace Anachronism.Npc
{
    [CreateAssetMenu(menuName = "Anachronism/Npc Group Settings", fileName = "NpcGroupSettings")]
    public class NpcGroupSettings : ScriptableObject
    {
        [Header("Groupe")]
        [SerializeField, Min(2)] private int _maxGroupSize = 4;

        [Header("Conscience des voisins")]
        [SerializeField, Min(0f)] private float _awarenessRadius = 4f;
        [SerializeField, Min(0f)] private float _destinationZoneRadius = 6f;
        [SerializeField, Range(0f, 1f)] private float _joinChance = 0.3f;
        [SerializeField, Min(0f)] private float _retryDelay = 5f;

        [Header("Formation")]
        [SerializeField, Min(0.5f)] private float _formationSpacing = 1.2f;
        [SerializeField, Min(0f)] private float _catchUpDistance = 1.5f;

        [Header("Arrivée")]
        [SerializeField, Range(0f, 1f)] private float _discussChance = 0.5f;
        [SerializeField] private Vector2 _discussDurationRange = new Vector2(3f, 8f);

        public int MaxGroupSize => _maxGroupSize;
        public float AwarenessRadius => _awarenessRadius;
        public float DestinationZoneRadius => _destinationZoneRadius;
        public float JoinChance => _joinChance;
        public float RetryDelay => _retryDelay;
        public float FormationSpacing => _formationSpacing;
        public float CatchUpDistance => _catchUpDistance;
        public float DiscussChance => _discussChance;
        public Vector2 DiscussDurationRange => _discussDurationRange;
    }
}
