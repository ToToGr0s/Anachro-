namespace Anachronism.Gameplay
{
    public enum ShotError
    {
        None,
        Invalid,            // NaN, infini ou direction non unitaire (client modifie)
        TooFarFromPlayer    // le tir ne part pas de la position du joueur
    }
}