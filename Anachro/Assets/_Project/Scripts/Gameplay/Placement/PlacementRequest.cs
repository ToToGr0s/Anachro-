using UnityEngine;

namespace Anachronism.Gameplay
{
    // Intention du client : le serveur la revalide avec PlacementRules, il ne fait jamais confiance au resultat du client.
    public struct PlacementRequest
    {
        public AnachronismSize Size { get; }
        public Vector3 Position { get; }
        public Vector3 Normal { get; }

        public PlacementRequest(AnachronismSize size, Vector3 position, Vector3 normal)
        {
            Size = size;
            Position = position;
            Normal = normal;
        }
    }
}