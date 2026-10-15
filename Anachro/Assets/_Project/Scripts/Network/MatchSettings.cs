using System;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Réglages d'une partie. Les valeurs vivent dans un MatchPresetTable (asset) ; l'hôte pourra les ajuster (T09).
    /// </summary>
    [Serializable]
    public struct MatchSettings
    {
        [SerializeField] private byte _villainCount;
        [SerializeField] private byte _objectsPerVillain;
        [SerializeField] private float _briefingDuration;
        [SerializeField] private float _hidingDuration;
        // Durée de base pour que la dégradation atteigne 100 %, hors pénalités.
        [SerializeField] private float _degradationDuration;
        // Part de dégradation ajoutée par un tir sur un innocent ou un objet d'époque (T06).
        [SerializeField] private float _wrongShotPenalty;

        public MatchSettings(byte villainCount, byte objectsPerVillain, float briefingDuration, float hidingDuration,
                             float degradationDuration, float wrongShotPenalty)
        {
            _villainCount = villainCount;
            _objectsPerVillain = objectsPerVillain;
            _briefingDuration = briefingDuration;
            _hidingDuration = hidingDuration;
            _degradationDuration = degradationDuration;
            _wrongShotPenalty = wrongShotPenalty;
        }

        public byte VillainCount => _villainCount;
        public byte ObjectsPerVillain => _objectsPerVillain;
        public float BriefingDuration => _briefingDuration;
        public float HidingDuration => _hidingDuration;
        public float DegradationDuration => _degradationDuration;
        public float WrongShotPenalty => _wrongShotPenalty;

        public MatchSettings WithVillainCount(byte villainCount)
        {
            MatchSettings copy = this;
            copy._villainCount = villainCount;
            return copy;
        }
    }
}
