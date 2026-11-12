using Anachronism.World;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// T05 : pendant le repérage, un chasseur n'observe ni les fugitifs ni les objets qu'ils posent. L'hôte ne lui envoie
    /// rien (ni spawn ni position) : un client modifié ne peut pas les voir à travers les murs du bureau.
    /// Réévaluée à chaque changement de phase (MatchManager.SetPhase → RebuildObservers).
    /// Sur le NetworkObserver du prefab joueur et du WorldObject (Anachro > Setup > Brancher les fonctions de partie).
    /// </summary>
    [CreateAssetMenu(menuName = "Anachro/Observers/Hidden While Hiding", fileName = "HiddenWhileHidingCondition")]
    public sealed class HiddenWhileHidingCondition : ObserverCondition
    {
        private WorldObject _worldObject;

        public override void Initialize(NetworkObject networkObject)
        {
            base.Initialize(networkObject);
            // Une seule recherche par objet : la condition est réévaluée pour chaque joueur à chaque changement de phase.
            _worldObject = networkObject.GetComponentInChildren<WorldObject>(true);
        }

        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            MatchManager match = MatchManager.Instance;
            if (match == null || match.Phase != GamePhase.Scouting)
                return true;

            if (match.GetRoleServer(connection) != PlayerRole.Guardian)
                return true;

            return !IsHiddenFromHunters(match);
        }

        private bool IsHiddenFromHunters(MatchManager match)
        {
            if (_worldObject != null)
                return _worldObject.IsPlacedServer;

            // Avatar de joueur : son owner est un fugitif. Les PNJ (sans owner) restent visibles.
            NetworkConnection owner = NetworkObject.Owner;
            return owner.IsValid && match.GetRoleServer(owner) == PlayerRole.Villain;
        }

        public override ObserverConditionType GetConditionType() => ObserverConditionType.Normal;
    }
}
