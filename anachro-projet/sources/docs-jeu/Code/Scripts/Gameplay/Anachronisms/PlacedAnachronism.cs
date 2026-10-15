using Anachronism.Network;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace Anachronism.Gameplay
{
    /// Sur les prefabs d'anachronismes posés par les méchants (spawnés par le serveur, sans owner).
    /// Ajouter aussi un NetworkObserver avec HiddenWhileHidingCondition.
    public class PlacedAnachronism : NetworkBehaviour
    {
        private readonly SyncVar<bool> _found = new SyncVar<bool>();
        public bool Found => _found.Value;

        public override void OnStartServer()
        {
            base.OnStartServer();
            MatchManager.Instance.RegisterAnachronism();
        }

        [Server]
        public void MarkFound() => _found.Value = true; // VFX / UI côté client via _found.OnChange
    }
}
