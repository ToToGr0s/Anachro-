using Anachronism.Network;
using Anachronism.Npc;
using Anachronism.World;
using FishNet.Object;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // COTE SERVEUR SEULEMENT : dit ce que le rayon du serveur a touche. Lit le role des joueurs, que seul l'hote connait
    // (regle reseau 3) : un client n'appelle jamais ce code, il recoit seulement le ShotResult.
    public static class ShotTargetResolver
    {
        public static ShotTargetKind KindOf(Collider hit, Era mapEra, MatchManager match)
        {
            if (hit == null)
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

            // TODO (Theo) : mechant deja capture ou parti -> a decider avec T06 (_neutralizedVillains).
            switch (match.GetRoleServer(networkObject.Owner))
            {
                case PlayerRole.Villain: return ShotTargetKind.Villain;
                case PlayerRole.Guardian: return ShotTargetKind.Guardian;
                default: return ShotTargetKind.Nothing;
            }
        }

        // Ce que la RPC (T06, Theo) appelle : meme validation et meme rayon que le client (ShotRules), puis la regle.
        // eyePosition : position du joueur connue du serveur + hauteur d'oeil, jamais une valeur envoyee par le client.
        // hit : ce qui a ete touche, pour les consequences (detruire l'objet, capturer le mechant) sans second rayon.
        public static ShotResult Resolve(ShotRequest request, Vector3 eyePosition, GameplayTuning tuning, Era mapEra,
            MatchManager match, out ShotError error, out Collider hit)
        {
            hit = null;
            if (!ShotRules.IsValid(request, eyePosition, tuning, out error))
                return ShotResult.Missed;

            if (ShotRules.Raycast(request, tuning, out RaycastHit info))
                hit = info.collider;

            return ShotRules.ResultFor(KindOf(hit, mapEra, match));
        }
    }
}