using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Partie locale pour tester plusieurs instances sur un même PC : Steam ne permet pas de se connecter
    /// à soi-même (même compte). Tugboat n'écoute que sur 127.0.0.1 : rien n'est ouvert sur le réseau.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class LocalSession : MonoBehaviour
    {
        private const string LocalAddress = "127.0.0.1";

        private NetworkManager _networkManager;

        /// <summary>Vrai pendant une partie locale (affichage du lobby à la place du code Steam).</summary>
        public bool IsLocal { get; private set; }

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
        }

        private void OnEnable()
        {
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        private void OnDisable()
        {
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        /// <summary>Héberge en local. Faux si une partie est déjà en cours ou si le port est pris.</summary>
        public bool Host()
        {
            if (!_networkManager.IsOffline)
                return false;

            if (!_networkManager.StartServerOn<Tugboat>())
                return false;

            // Un hôte sans son propre client ne servirait à rien : on referme le serveur.
            _networkManager.UseClientTransport<Tugboat>();
            if (!_networkManager.ClientManager.StartConnection(LocalAddress))
            {
                _networkManager.ServerManager.StopConnection(true);
                return false;
            }

            IsLocal = true;
            return true;
        }

        /// <summary>Rejoint la partie locale hébergée sur ce PC.</summary>
        public bool Join()
        {
            if (!_networkManager.IsOffline)
                return false;

            // Posé seulement si la connexion démarre : sinon aucun « Stopped » ne viendrait le remettre à faux.
            _networkManager.UseClientTransport<Tugboat>();
            if (!_networkManager.ClientManager.StartConnection(LocalAddress))
                return false;

            IsLocal = true;
            return true;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped && !_networkManager.IsServerStarted)
                IsLocal = false;
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped)
                IsLocal = false;
        }
    }
}
