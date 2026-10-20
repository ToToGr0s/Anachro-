using Anachronism.Npc;
using FishNet;
using FishNet.Managing;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Réglages de l'hôte (menu Échap). L'UI appelle ces commandes au lieu de toucher elle-même aux objets réseau
    /// (règle de code 5) ; chacune vérifie d'abord que ce poste est bien l'hôte.
    /// </summary>
    public static class HostTools
    {
        public static bool IsHost
        {
            get
            {
                NetworkManager networkManager = NetworkManager.Instances.Count > 0 ? InstanceFinder.NetworkManager : null;
                return networkManager != null && networkManager.IsServerStarted;
            }
        }

        /// <summary>Retire tous les PNJ et en refait apparaître de nouveaux. Faux si ce poste n'est pas l'hôte ou sans PNJ.</summary>
        public static bool TryRespawnNpcs()
        {
            NpcDestinationManager npcs = FindNpcs();
            if (npcs == null)
                return false;

            npcs.RespawnAll();
            return true;
        }

        /// <summary>Nouveau skin pour chaque PNJ, sans changer leur comportement. Faux si ce poste n'est pas l'hôte ou sans PNJ.</summary>
        public static bool TryRerollNpcSkins()
        {
            NpcDestinationManager npcs = FindNpcs();
            if (npcs == null)
                return false;

            npcs.RerollSkins();
            return true;
        }

        // Cherché à l'appel (un clic, jamais dans Update) : les PNJ vivent dans la map, le menu dans Boot.
        private static NpcDestinationManager FindNpcs() =>
            IsHost ? Object.FindAnyObjectByType<NpcDestinationManager>() : null;
    }
}
