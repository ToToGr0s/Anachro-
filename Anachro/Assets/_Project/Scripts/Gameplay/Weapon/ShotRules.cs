using System;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // Regles partagees client/serveur (regle de code 6). IsValid et ResultFor sont pures ; Raycast depend de la physique
    // de la scene (et d'un tampon statique), mais reste le meme rayon partout.
    public static class ShotRules
    {
        // Pas de controle de distance : cote serveur, l'origine est l'oeil connu de l'hote (NetworkShooting), jamais
        // celle du client. Une origine du client, meme bornee, laisserait tirer depuis l'autre cote d'un mur fin.
        public static bool IsValid(ShotRequest request, out ShotError error)
        {
            if (!IsFinite(request.Origin) || !IsFinite(request.Direction)
                || Mathf.Abs(request.Direction.sqrMagnitude - 1f) > 0.01f)
            {
                error = ShotError.Invalid;
                return false;
            }

            error = ShotError.None;
            return true;
        }

        // Regle unique "cible -> resultat" : le serveur (T06) et le test hors ligne l'utilisent tous les deux.
        public static ShotResult ResultFor(ShotTargetKind kind)
        {
            switch (kind)
            {
                case ShotTargetKind.Anachronism: return ShotResult.RightObjectFound;
                case ShotTargetKind.PeriodObject: return ShotResult.WrongObjectFound;
                case ShotTargetKind.Npc: return ShotResult.Innocent;
                case ShotTargetKind.Guardian: return ShotResult.Teammate;
                case ShotTargetKind.Villain: return ShotResult.Villain;
                default: return ShotResult.Missed;
            }
        }

        // Resolue au premier tir : NameToLayer renvoie -1 sans erreur si la couche est renommee, on le signale alors.
        private static int _characterLayer = int.MinValue;
        // Tampon reutilise : pas d'allocation a chaque tir. Triggers compris, une zone dense peut renvoyer beaucoup de touches.
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        private static int CharacterLayer
        {
            get
            {
                if (_characterLayer == int.MinValue)
                {
                    _characterLayer = LayerMask.NameToLayer("Character");
                    if (_characterLayer < 0)
                        Debug.LogError("ShotRules : couche Character introuvable, les capsules en trigger ne seront pas touchees.");
                }

                return _characterLayer;
            }
        }

        // Meme rayon partout (client, test hors ligne, serveur) : sinon l'impact affiche et le resultat pourraient diverger.
        // shooter : racine du tireur, ignoree. La camera est au-dessus de la capsule : un tir vers le bas la toucherait.
        // passThrough (optionnel) : ce que le rayon traverse sans s'arreter (cote serveur, les fugitifs captures).
        public static bool Raycast(ShotRequest request, GameplayTuning tuning, Transform shooter,
            Func<Collider, bool> passThrough, out RaycastHit hit)
        {
            // Triggers inclus : la capsule d'un joueur est en trigger chez son proprietaire (CharacterMovement), donc sur
            // l'hote pour le joueur de l'hote. Sans eux, un tir sur un fugitif heberge traverserait son corps.
            int count = Physics.RaycastNonAlloc(request.Origin, request.Direction, Hits, tuning.ShootDistance,
                tuning.ShootMask, QueryTriggerInteraction.Collide);

            hit = default;
            bool hasHit = false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                // Les autres triggers (zones, etc.) ne bloquent pas le tir.
                if (collider.isTrigger && collider.gameObject.layer != CharacterLayer)
                    continue;
                if (shooter != null && collider.transform.IsChildOf(shooter))
                    continue;
                if (passThrough != null && passThrough(collider))
                    continue;

                // RaycastNonAlloc ne trie pas ses resultats : on garde le plus proche.
                if (!hasHit || Hits[i].distance < hit.distance)
                {
                    hit = Hits[i];
                    hasHit = true;
                }
            }

            return hasHit;
        }

        // Un client modifie peut envoyer NaN : toute comparaison avec NaN vaut false, donc la validation passerait.
        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        }
    }
}
