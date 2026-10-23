using System;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Préréglages de partie selon le nombre de joueurs (T09-2). L'hôte part de là, puis ajuste au lobby (T09-3).
    /// </summary>
    [CreateAssetMenu(menuName = "Anachro/Match Presets", fileName = "MatchPresets")]
    public sealed class MatchPresetTable : ScriptableObject
    {
        [Serializable]
        private struct PlayerCountPreset
        {
            [Range(2, 8)] public int PlayerCount;
            public MatchSettings Settings;
        }

        [SerializeField] private MatchSettings _baseSettings = new MatchSettings(
            villainCount: 1,
            objectsPerVillain: 4,
            scoutingDuration: 60f,
            searchingDuration: 300f,
            lastChanceDuration: 60f,
            wrongShotPenalty: 0.05f);

        [Tooltip("Réglages propres à un nombre de joueurs. Un nombre absent de la liste prend les réglages de base.")]
        [SerializeField] private PlayerCountPreset[] _byPlayerCount = new PlayerCountPreset[0];

        [Tooltip("En dessous, la partie ne démarre pas. 2 pour les tests, 4 visé en jeu.")]
        [SerializeField, Min(2)] private int _minPlayers = 2;

        public int MinPlayers => _minPlayers;

        public MatchSettings GetFor(int playerCount)
        {
            foreach (PlayerCountPreset preset in _byPlayerCount)
            {
                if (preset.PlayerCount == playerCount)
                    return preset.Settings;
            }

            return _baseSettings;
        }
    }
}
