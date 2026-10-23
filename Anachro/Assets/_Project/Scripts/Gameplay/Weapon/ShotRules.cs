using UnityEngine;

namespace Anachronism.Gameplay
{
    // Regle partagee client/serveur (regle de code 6) : fonction pure. Le serveur l'appelle avant de refaire son propre rayon.
    public static class ShotRules
    {
        // viewerPosition : la camera du joueur (client) ; cote serveur, sa position connue + hauteur d'oeil fixe.
        public static bool IsValid(ShotRequest request, Vector3 viewerPosition, GameplayTuning tuning, out ShotError error)
        {
            if (!IsFinite(request.Origin) || !IsFinite(request.Direction)
                || Mathf.Abs(request.Direction.sqrMagnitude - 1f) > 0.01f)
            {
                error = ShotError.Invalid;
                return false;
            }

            // Mouvement client-autoritaire et latence : marge propre au tir (GameplayTuning).
            if (Vector3.Distance(viewerPosition, request.Origin) > tuning.ShootOriginMargin)
            {
                error = ShotError.TooFarFromPlayer;
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
                // TODO (Celian) : tirer sur un coequipier = innocent pour l'instant, a valider avec l'equipe.
                case ShotTargetKind.Guardian: return ShotResult.Innocent;
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
        public static bool Raycast(ShotRequest request, GameplayTuning tuning, out RaycastHit hit)
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