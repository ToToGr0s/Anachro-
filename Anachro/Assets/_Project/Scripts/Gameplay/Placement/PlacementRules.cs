using UnityEngine;

namespace Anachronism.Gameplay
{
    // Regle partagee client/serveur (regle de code 6) : fonction pure, aucune dependance a la scene ni au reseau.
    // L'apercu (client) et la revalidation (serveur) appellent exactement cette fonction.
    // La phase et la recharge dependent de l'etat de la partie : c'est NetworkPlacement (Theo) qui les verifie.
    public static class PlacementRules
    {

        // viewerPosition : la camera du joueur (client) ou sa position connue du serveur.
        public static bool IsValid(PlacementRequest request, PlacementSurfaceType allowedSurfaces,
            Vector3 viewerPosition, GameplayTuning tuning, out PlacementError error)
        {
            if (!IsFinite(request.Position) || !IsFinite(request.Normal))
            {
                error = PlacementError.Invalid;
                return false;
            }

            if (Vector3.Distance(viewerPosition, request.Position) > tuning.InteractionDistance)
            {
                error = PlacementError.TooFar;
                return false;
            }

            if (!IsSurfaceValid(request, allowedSurfaces, tuning))
            {
                error = PlacementError.InvalidSurface;
                return false;
            }

            error = PlacementError.None;
            return true;
        }

        // Sol, mur, ou aucun des deux (plafond, pente trop raide).
        public static PlacementSurfaceType ClassifySurface(Vector3 normal, GameplayTuning tuning)
        {
            if (normal.y >= tuning.FloorMinNormalY)
                return PlacementSurfaceType.Floor;

            if (Mathf.Abs(normal.y) <= tuning.WallMaxAbsNormalY)
                return PlacementSurfaceType.Wall;

            return PlacementSurfaceType.None;
        }

        // Un client modifie peut envoyer NaN : toute comparaison avec NaN vaut false, donc la validation passerait.
        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        }

        // TODO (Celian) : completer avec PlacementSurface (C05) quand l'outil de surfaces existera.
        private static bool IsSurfaceValid(PlacementRequest request, PlacementSurfaceType allowedSurfaces, GameplayTuning tuning)
        {
            // La normale vient du client (donc hostile) : une normale pas unitaire est refusee.
            if (Mathf.Abs(request.Normal.sqrMagnitude - 1f) > 0.05f)
                return false;

            PlacementSurfaceType kind = ClassifySurface(request.Normal, tuning);
            return kind != PlacementSurfaceType.None && (allowedSurfaces & kind) != 0;
        }
        // Centre de l'objet pose : le point vise est le centre de la face posee (pivot du prefab au centre),
        // l'objet depasse donc de la moitie de son epaisseur (hauteur au sol, profondeur au mur).
        public static Vector3 PositionFor(Vector3 contactPoint, Vector3 normal, Vector3 dimensions, GameplayTuning tuning)
        {
            bool isWall = ClassifySurface(normal, tuning) == PlacementSurfaceType.Wall;
            float thickness = isWall ? dimensions.z : dimensions.y;
            return contactPoint + normal * (thickness * 0.5f);
        }


        // Orientation de l'objet pose : debout au sol, face a la piece contre un mur. Le serveur fera apparaitre l'objet avec la meme.
        public static Quaternion RotationFor(Vector3 normal, GameplayTuning tuning)
        {
            return ClassifySurface(normal, tuning) == PlacementSurfaceType.Wall
                ? Quaternion.LookRotation(normal, Vector3.up)
                : Quaternion.FromToRotation(Vector3.up, normal);
        }
    }
}