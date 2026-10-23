using Anachronism.Network;
using Anachronism.Npc;
using Anachronism.World;
using FishNet.Object;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // COTE SERVEUR SEULEMENT : dit ce que le rayon du serveur a touche. Lit le role des joueurs, que seul l'hote connait
    // (regle reseau 3) : un client n'appelle jamais ce code, il recoit seulement le ShotResult (NetworkShooting).
    public static class ShotTargetResolver
    {
        public static ShotTargetKind KindOf(Collider hit, Era mapEra, MatchManager match)
        {
            if (hit == null || match == null)
                return ShotTargetKind.Nothing;

            // Objet de la map ou pose par un fugitif : son epoque, comparee a celle de la map, suffit (EraRules).
            WorldObject worldObject = hit.GetComponentInParent<WorldObject>();
            if (worldObject != null)
            {
                if (worldObject.Definition == null)
                    return ShotTargetKind.Nothing;

                return EraRules.IsAnachronism(worldObject.Definition.Era, mapEra)
                    ? ShotTargetKind.Anachronism
                    : ShotTargetKind.PeriodObject;
            }

            // Mur, sol, decor : pas d'objet reseau.
            NetworkObject networkObject = hit.GetComponentInParent<NetworkObject>();
            if (networkObject == null)
                return ShotTargetKind.Nothing;

            // PNJ et joueurs se ressemblent (regle reseau 5) : seul le proprietaire les distingue.
            // Un PNJ appartient au serveur, un joueur a sa connexion.
            if (!networkObject.Owner.IsValid)
            {
                // Une fois par tir (pas dans Update) : la recherche reste ponctuelle.
                return networkObject.GetComponentInChildren<NpcAgent>() != null
                    ? ShotTargetKind.Npc
                    : ShotTargetKind.Nothing;
            }

            switch (match.GetRoleServer(networkObject.Owner))
            {
                // Un fugitif capture est traverse par le rayon (IsCapturedVillain) : ce cas ne sert que de filet.
                case PlayerRole.Villain:
                    return match.IsActiveVillainServer(networkObject.Owner) ? ShotTargetKind.Villain : ShotTargetKind.Nothing;
                case PlayerRole.Guardian: return ShotTargetKind.Guardian;
                default: return ShotTargetKind.Nothing;
            }
        }

        // Fugitif capture : une statue figee (C06), le tir doit passer a travers (ShotRules.Raycast).
        // Appele pour chaque collider touche par un tir : GetComponentInParent reste ponctuel.
        public static bool IsCapturedVillain(Collider collider, MatchManager match)
        {
            if (match == null)
                return false;

            NetworkObject networkObject = collider.GetComponentInParent<NetworkObject>();
            if (networkObject == null || !networkObject.Owner.IsValid)
                return false;

            return match.GetRoleServer(networkObject.Owner) == PlayerRole.Villain
                && !match.IsActiveVillainServer(networkObject.Owner);
        }
    }
}
