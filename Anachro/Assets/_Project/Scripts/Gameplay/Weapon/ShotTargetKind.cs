namespace Anachronism.Gameplay
{
    // Ce que le rayon a touche. Le serveur le determine avec ses propres infos (role, objet pose) : le client ne l'envoie jamais.
    public enum ShotTargetKind
    {
        Nothing,
        Npc,
        Guardian,
        Villain,
        PeriodObject,
        Anachronism
    }
}