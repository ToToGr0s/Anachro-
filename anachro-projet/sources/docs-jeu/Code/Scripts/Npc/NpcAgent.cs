using UnityEngine;

namespace Anachronism.Npc
{
    /// Marqueur sur les prefabs de PNJ (simulés sur l'hôte, NetworkTransform 10 Hz).
    /// IMPORTANT : le prefab joueur-méchant doit utiliser EXACTEMENT les mêmes réglages
    /// de NetworkTransform (fréquence, interpolation) et le même Animator piloté par la vitesse.
    public class NpcAgent : MonoBehaviour
    {
        // TODO (Olivier) : NavMeshAgent, points d'intérêt, arrêts/idle, Animator piloté par la vitesse.
    }
}
