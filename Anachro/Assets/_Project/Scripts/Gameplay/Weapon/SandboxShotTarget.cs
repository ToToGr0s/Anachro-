#if UNITY_EDITOR
using UnityEngine;

namespace Anachronism.Gameplay
{
    // TEST LOCAL SEULEMENT (scenes Sandbox) : dit ce qu'est une cible de test (PNJ, mechant, objet...).
    // En ligne, le serveur le deduit lui-meme (role du joueur touche, objet pose ou non) : ce composant n'existe pas en jeu.
    public class SandboxShotTarget : MonoBehaviour
    {
        [SerializeField] private ShotTargetKind _kind = ShotTargetKind.Npc;

        public ShotTargetKind Kind => _kind;
    }
}
#endif