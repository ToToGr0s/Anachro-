namespace Anachronism.Network
{
    public enum GamePhase : byte { Lobby, Briefing, Hiding, Searching, Portal, End }
    public enum PlayerRole : byte { None, Guardian, Villain }
    public enum Winner : byte { None, Guardians, Villains }

    /// Réglages choisis par l'hôte (stockés aussi dans les métadonnées du lobby Steam).
    [System.Serializable]
    public struct MatchSettings
    {
        public byte VillainCount;
        public byte ObjectsPerVillain;
        public float BriefingDuration;
        public float HidingDuration;
        /// Durée de base pour que la dégradation atteigne 100 % (hors pénalités).
        public float DegradationDuration;
        /// Part de dégradation ajoutée par un tir sur un innocent / un objet d'époque.
        public float WrongShotPenalty;

        /// Préréglage équilibré par nombre de joueurs. L'hôte part de là.
        public static MatchSettings Preset(int playerCount) => new MatchSettings
        {
            VillainCount = (byte)(playerCount >= 7 ? 2 : 1),
            ObjectsPerVillain = 4,
            BriefingDuration = 10f,
            HidingDuration = 60f,
            DegradationDuration = 420f,
            WrongShotPenalty = 0.05f,
        };
    }
}
