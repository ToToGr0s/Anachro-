namespace Anachronism.Network
{
    public enum MatchStartResult : byte
    {
        Started,
        WrongPhase,
        NotEnoughPlayers,
        // Un joueur charge encore la map : il raterait son rôle (TargetRpc envoyé une seule fois).
        PlayersLoading,
        InvalidSettings
    }
}
