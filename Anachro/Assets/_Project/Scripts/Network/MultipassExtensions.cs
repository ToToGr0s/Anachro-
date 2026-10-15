using System.Collections.Generic;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using UnityEngine;

namespace Anachronism.Network
{
    public static class MultipassExtensions
    {
        /// <summary>
        /// Démarre le serveur sur le seul transport T. ServerManager.StartConnection ouvrirait tous les
        /// transports : une partie Steam écouterait aussi en UDP et contournerait le lobby « amis ».
        /// </summary>
        public static bool StartServer<T>(this Multipass multipass) where T : Transport
        {
            IReadOnlyList<Transport> transports = multipass.Transports;

            for (int i = 0; i < transports.Count; i++)
            {
                if (transports[i] is T)
                    return multipass.StartConnection(true, i);
            }

            Debug.LogError($"[Multipass] Aucun transport {typeof(T).Name} configuré.");
            return false;
        }
    }
}
