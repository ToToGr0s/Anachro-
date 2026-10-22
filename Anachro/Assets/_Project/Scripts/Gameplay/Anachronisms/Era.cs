namespace Anachronism.Gameplay
{
    /// <summary>
    /// Époques du jeu. Unity enregistre la position dans les assets : ajouter une époque à la fin, ne jamais réordonner ni supprimer.
    /// </summary>
    public enum Era : byte
    {
        Paleolithic,
        Antiquity,
        MiddleAge,
        Victorian,
        Contemporary,
        Futuristic
    }
}
