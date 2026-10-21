using System;

namespace Anachronism.Gameplay
{
    // Surfaces ou un objet peut se poser. Drapeaux : Floor | Wall = les deux.
    [Flags]
    public enum PlacementSurfaceType
    {
        None = 0,
        Floor = 1,
        Wall = 2
    }
}