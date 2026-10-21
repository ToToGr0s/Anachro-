namespace Anachronism.Gameplay
{
    /// <summary>
    /// Règle partagée client/serveur. Un objet sur la map vient soit de la liste de spawn de l'époque (même époque),
    /// soit d'un fugitif (liste des objets posables, d'une autre époque) : comparer les deux époques suffit.
    /// </summary>
    public static class EraRules
    {
        /// <summary>Vrai si l'objet n'est pas de l'époque de la map : le détruire fait baisser la barre.
        /// Faux s'il en est : tirer dessus est une erreur du chasseur (+4 %, GDD 2.9.1).</summary>
        public static bool IsAnachronism(Era objectEra, Era mapEra) => objectEra != mapEra;
    }
}
