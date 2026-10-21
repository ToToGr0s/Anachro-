using UnityEngine;

namespace Anachronism.Gameplay
{
    // Regles de pose qui interrogent la scene (physique) : l'objet repose-t-il sur la surface, la place est-elle libre ?
    // Meme appel cote client (apercu) et cote serveur (revalidation) ; PlacementRules, lui, reste pur.
    // A appeler APRES PlacementRules.IsValid : la normale est alors garantie unitaire.
    public static class PlacementSpace
    {
        private const int SamplesPerAxis = 7;
        private const float SurfaceTolerance = 0.05f;
        private const float OverlapMargin = 0.01f;

        // obstacleMask : tout ce qui est solide (sert de surface et d'obstacle). placedObjectsMask : objets deja poses.
        // viewerPosition : la camera du joueur (client) ou sa position connue du serveur.
        public static bool Check(PlacementRequest request, Vector3 dimensions, GameplayTuning tuning,
            Vector3 viewerPosition, int obstacleMask, int placedObjectsMask, out PlacementError error)
        {
            // Ligne de vue : sans elle, un client modifie pourrait viser a travers un mur (point a moins de
            // InteractionDistance, mais de l'autre cote). Le bout du segment reste juste devant la surface.
            if (Physics.Linecast(viewerPosition, request.Position + request.Normal * SurfaceTolerance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                error = PlacementError.Obstructed;
                return false;
            }

            Quaternion rotation = PlacementRules.RotationFor(request.Normal, tuning);

            if (SupportRatio(request, dimensions, rotation, tuning, obstacleMask, placedObjectsMask) < tuning.MinSupportRatio)
            {
                error = PlacementError.Floating;
                return false;
            }

            // Boite legerement reduite : un objet qui touche juste un autre n'est pas "dedans".
            Vector3 center = PlacementRules.PositionFor(request.Position, request.Normal, dimensions, tuning);
            Vector3 halfExtents = Vector3.Max(dimensions * 0.5f - Vector3.one * OverlapMargin, Vector3.one * 0.001f);
            if (Physics.CheckBox(center, halfExtents, rotation, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                error = PlacementError.Occupied;
                return false;
            }

            error = PlacementError.None;
            return true;
        }

        // Part (0-1) de la face posee qui touche vraiment la surface : on tire un quadrillage de rayons sous la face.
        private static float SupportRatio(PlacementRequest request, Vector3 dimensions, Quaternion rotation,
            GameplayTuning tuning, int obstacleMask, int placedObjectsMask)
        {
            bool isWall = PlacementRules.ClassifySurface(request.Normal, tuning) == PlacementSurfaceType.Wall;
            // Face posee : sol = largeur x profondeur, mur = largeur x hauteur.
            Vector3 axisA = rotation * Vector3.right;
            Vector3 axisB = rotation * (isWall ? Vector3.up : Vector3.forward);
            float sizeA = dimensions.x;
            float sizeB = isWall ? dimensions.y : dimensions.z;

            int supported = 0;
            for (int i = 0; i < SamplesPerAxis; i++)
            {
                for (int j = 0; j < SamplesPerAxis; j++)
                {
                    // Centre de chaque case du quadrillage.
                    float a = ((i + 0.5f) / SamplesPerAxis - 0.5f) * sizeA;
                    float b = ((j + 0.5f) / SamplesPerAxis - 0.5f) * sizeB;
                    Vector3 point = request.Position + axisA * a + axisB * b;
                    if (IsSupported(point, request.Normal, obstacleMask, placedObjectsMask))
                        supported++;
                }
            }

            return supported / (float)(SamplesPerAxis * SamplesPerAxis);
        }

        // Le rayon part juste devant la surface et la cherche juste derriere : trop loin = vide ou marche, un objet pose ne compte pas.
        private static bool IsSupported(Vector3 point, Vector3 normal, int obstacleMask, int placedObjectsMask)
        {
            Vector3 origin = point + normal * SurfaceTolerance;
            if (!Physics.Raycast(origin, -normal, out RaycastHit hit, SurfaceTolerance * 2f, obstacleMask, QueryTriggerInteraction.Ignore))
                return false;

            bool isPlacedObject = (placedObjectsMask & (1 << hit.collider.gameObject.layer)) != 0;
            return !isPlacedObject && Vector3.Dot(hit.normal, normal) > 0.9f;
        }
    }
}