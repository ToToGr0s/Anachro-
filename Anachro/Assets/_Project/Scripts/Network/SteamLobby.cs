using System;
using Anachronism.Core;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using Steamworks;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Lobby Steam : l'hôte crée un lobby « amis », les invités le rejoignent par l'overlay
    /// et se connectent en P2P (FishySteamworks) sur le SteamID du propriétaire du lobby.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class SteamLobby : MonoBehaviour
    {
        // Argument ajouté par Steam quand on accepte une invitation alors que le jeu est fermé.
        private const string ConnectLobbyArgument = "+connect_lobby";

        [SerializeField] private int _maxPlayers = 8;

        private NetworkManager _networkManager;
        private Multipass _multipass;
        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private CSteamID _lobbyId = CSteamID.Nil;

        public bool IsSteamReady => SteamService.Instance != null && SteamService.Instance.IsInitialized;
        public bool IsInLobby => _lobbyId != CSteamID.Nil;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _multipass = GetComponent<Multipass>();
        }

        private void Start()
        {
            // Start et pas Awake : SteamService doit avoir initialisé l'API avant de créer les callbacks.
            if (!IsSteamReady)
                return;

            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;

            TryJoinFromCommandLine();
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;

            LeaveLobby();
            _lobbyCreated?.Dispose();
            _joinRequested?.Dispose();
            _lobbyEntered?.Dispose();
        }

        public void HostLobby()
        {
            if (!IsSteamReady || IsInLobby || !_networkManager.IsOffline)
                return;

            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _maxPlayers);
        }

        public void InviteFriends()
        {
            if (IsInLobby)
                SteamFriends.ActivateGameOverlayInviteDialog(_lobbyId);
        }

        public void LeaveLobby()
        {
            // À la fermeture, SteamService peut avoir déjà coupé l'API : l'appeler planterait.
            if (!IsInLobby || !IsSteamReady)
                return;

            SteamMatchmaking.LeaveLobby(_lobbyId);
            _lobbyId = CSteamID.Nil;
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"[SteamLobby] Création du lobby refusée : {callback.m_eResult}.");
                return;
            }

            _lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            // Le client de l'hôte passe aussi par FishySteamworks : il détecte le serveur local et ne sort pas sur le réseau.
            _multipass.SetClientTransport<FishySteamworks.FishySteamworks>();
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t callback)
        {
            if (!_networkManager.IsOffline)
                return;

            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Debug.LogWarning($"[SteamLobby] Entrée refusée : {(EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse}.");
                return;
            }

            CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            // Le propriétaire du lobby fait serveur. Seul Steam peut le désigner, aucun membre ne peut le falsifier.
            CSteamID hostId = SteamMatchmaking.GetLobbyOwner(lobbyId);

            // L'hôte reçoit aussi LobbyEnter pour son propre lobby : il s'est déjà connecté dans OnLobbyCreated.
            if (hostId == SteamUser.GetSteamID())
                return;

            _lobbyId = lobbyId;
            _multipass.SetClientTransport<FishySteamworks.FishySteamworks>();
            _networkManager.ClientManager.StartConnection(hostId.m_SteamID.ToString());
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            // Hôte parti ou déconnexion volontaire : on quitte le lobby pour ne pas y rester fantôme.
            if (args.ConnectionState == LocalConnectionState.Stopped)
                LeaveLobby();
        }

        private static void TryJoinFromCommandLine()
        {
            string[] arguments = Environment.GetCommandLineArgs();

            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == ConnectLobbyArgument && ulong.TryParse(arguments[i + 1], out ulong lobbyId))
                {
                    SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
                    return;
                }
            }
        }
    }
}
