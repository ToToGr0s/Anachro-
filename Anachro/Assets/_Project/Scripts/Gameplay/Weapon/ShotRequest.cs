using UnityEngine;

namespace Anachronism.Gameplay
{
    // Intention du client : le serveur refait son propre rayon, il ne fait jamais confiance au resultat du client.
    public readonly struct ShotRequest
    {
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }

        public ShotRequest(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
        }
    }
}
