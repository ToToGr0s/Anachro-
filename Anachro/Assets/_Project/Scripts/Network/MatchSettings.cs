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
        // Bornes partagées par l'inspecteur et IsValid, pour ne les écrire qu'une fois.
        public const byte MaxVillains = 7;
        public const byte MaxObjectsPerVillain = 12;

        [SerializeField, Range(1f, MaxVillains)] private byte _villainCount;
        [SerializeField, Range(1f, MaxObjectsPerVillain)] private byte _objectsPerVillain;
        [SerializeField, Min(1f)] private float _scoutingDuration;
        [SerializeField, Min(1f)] private float _searchingDuration;
        // Durée de la dernière chance : chrono visible, la barre va seule de sa valeur actuelle à 100 % pendant ce temps.
        [SerializeField, Min(1f)] private float _lastChanceDuration;
        // Part de dégradation ajoutée par un tir sur un innocent ou un objet d'époque (T06).
        [SerializeField, Range(0f, 1f)] private float _wrongShotPenalty;

        public MatchSettings(byte villainCount, byte objectsPerVillain, float scoutingDuration, float searchingDuration,
                             float lastChanceDuration, float wrongShotPenalty)
        {
            _villainCount = villainCount;
            _objectsPerVillain = objectsPerVillain;
            _scoutingDuration = scoutingDuration;
            _searchingDuration = searchingDuration;
            _lastChanceDuration = lastChanceDuration;
            _wrongShotPenalty = wrongShotPenalty;
        }

        public byte VillainCount => _villainCount;
        public byte ObjectsPerVillain => _objectsPerVillain;
        public float ScoutingDuration => _scoutingDuration;
        public float SearchingDuration => _searchingDuration;
        public float LastChanceDuration => _lastChanceDuration;
        public float WrongShotPenalty => _wrongShotPenalty;

        /// <summary>
        /// [Range] et [Min] ne protègent que l'inspecteur : les réglages peuvent aussi venir du code ou de l'hôte (T09).
        /// Une durée de chasse nulle passerait directement à la dernière chance.
        /// Une durée de dernière chance nulle ferait gagner les méchants dès son début (division par zéro de la montée).
        /// </summary>
        public bool IsValid(out string error)
        {
            error = _villainCount < 1 ? "au moins un méchant"
                : _villainCount > MaxVillains ? $"au plus {MaxVillains} méchants"
                : _objectsPerVillain < 1 ? "au moins un objet par méchant"
                : _objectsPerVillain > MaxObjectsPerVillain ? $"au plus {MaxObjectsPerVillain} objets par méchant"
                : _scoutingDuration <= 0f ? "durée de repérage nulle"
                : _searchingDuration <= 0f ? "durée de chasse nulle"
                : _lastChanceDuration <= 0f ? "durée de dernière chance nulle"
                : string.Empty;
            return error.Length == 0;
        }
    }
}
