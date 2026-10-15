using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Préréglages de partie selon le nombre de joueurs. L'hôte part de là (T09 : réglages modifiables).
    /// </summary>
    [CreateAssetMenu(menuName = "Anachro/Match Presets", fileName = "MatchPresets")]
    public sealed class MatchPresetTable : ScriptableObject
    {
        [SerializeField] private MatchSettings _baseSettings = new MatchSettings(
            villainCount: 1,
            objectsPerVillain: 4,
            briefingDuration: 10f,
            hidingDuration: 60f,
            degradationDuration: 420f,
            wrongShotPenalty: 0.05f);

        [Tooltip("En dessous, la partie ne démarre pas. 2 pour les tests, 4 visé en jeu.")]
        [SerializeField, Min(2)] private int _minPlayers = 2;

        [Tooltip("À partir de ce nombre de joueurs, deux méchants au lieu d'un.")]
        [SerializeField, Min(2)] private int _twoVillainsFromPlayerCount = 7;

        public int MinPlayers => _minPlayers;

        public MatchSettings GetFor(int playerCount)
        {
            byte villainCount = (byte)(playerCount >= _twoVillainsFromPlayerCount ? 2 : 1);
            return _baseSettings.WithVillainCount(villainCount);
        }
    }
}
