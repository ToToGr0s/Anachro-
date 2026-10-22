using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Choix du transport quand le NetworkManager en a plusieurs (Multipass : Steam + Tugboat local).
    /// </summary>
    public static class TransportSelection
    {
        /// <summary>
        /// Démarre le serveur sur le seul transport T. ServerManager.StartConnection ouvrirait tous les transports :
        /// une partie Steam écouterait aussi en UDP et contournerait le code du lobby.
        /// </summary>
        public static bool StartServerOn<T>(this NetworkManager networkManager) where T : Transport
        {
            if (networkManager.TransportManager.Transport is not Multipass multipass)
                return networkManager.ServerManager.StartConnection();

            IReadOnlyList<Transport> transports = multipass.Transports;
            for (int i = 0; i < transports.Count; i++)
            {
                if (transports[i] is T)
                    return multipass.StartConnection(true, i);
            }

            Debug.LogError($"[TransportSelection] Aucun transport {typeof(T).Name} dans le Multipass.");
            return false;
        }

        /// <summary>Transport utilisé par le prochain ClientManager.StartConnection.</summary>
        public static void UseClientTransport<T>(this NetworkManager networkManager) where T : Transport
        {
            if (networkManager.TransportManager.Transport is Multipass multipass)
                multipass.SetClientTransport<T>();
        }
    }
}
