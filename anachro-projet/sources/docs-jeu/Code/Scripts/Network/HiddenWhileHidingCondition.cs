using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace Anachronism.Network
{
    /// Pendant la phase Hiding, l'objet n'est visible que par les méchants.
    /// À mettre (via NetworkObserver) sur : le prefab joueur-méchant et les anachronismes.
    /// Les gentils voient donc la map intacte → phase "photos temporelles" sans copie de scène.
    /// Réévaluée par MatchManager.SetPhase() → RebuildObservers().
    [CreateAssetMenu(menuName = "Anachronism/Observers/Hidden While Hiding")]
    public class HiddenWhileHidingCondition : ObserverCondition
    {
        public override bool ConditionMet(NetworkConnection connection, bool currentlyShown, out bool notProcessed)
        {
            notProcessed = false;
            var mm = MatchManager.Instance;
            if (mm == null || mm.Phase != GamePhase.Hiding) return true;
            return mm.GetRoleServer(connection) == PlayerRole.Villain;
        }

        public override ObserverConditionType GetConditionType() => ObserverConditionType.Normal;
    }
}
