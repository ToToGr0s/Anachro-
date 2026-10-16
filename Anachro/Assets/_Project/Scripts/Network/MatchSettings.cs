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
        [SerializeField, Min(1)] private byte _villainCount;
        [SerializeField, Min(1)] private byte _objectsPerVillain;
        [SerializeField, Min(0f)] private float _briefingDuration;
        [SerializeField, Min(1f)] private float _hidingDuration;
        // Durée de base pour que la dégradation atteigne 100 %, hors pénalités.
        [SerializeField, Min(1f)] private float _degradationDuration;
        // Part de dégradation ajoutée par un tir sur un innocent ou un objet d'époque (T06).
        [SerializeField, Range(0f, 1f)] private float _wrongShotPenalty;

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

        /// <summary>
        /// [Min] ne protège que l'inspecteur : les réglages peuvent aussi venir du code ou de l'hôte (T09).
        /// Une durée de dégradation nulle ferait gagner les méchants dès la phase de recherche.
        /// </summary>
        public bool IsValid(out string error)
        {
            error = _villainCount < 1 ? "au moins un méchant"
                : _hidingDuration <= 0f ? "durée de cache nulle"
                : _degradationDuration <= 0f ? "durée de dégradation nulle"
                : _briefingDuration < 0f ? "durée de briefing négative"
                : string.Empty;
            return error.Length == 0;
        }

        public MatchSettings WithVillainCount(byte villainCount)
        {
            MatchSettings copy = this;
            copy._villainCount = villainCount;
            return copy;
        }
    }
}
