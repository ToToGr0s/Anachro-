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
        // IsInLobby reste faux tant que Steam n'a pas répondu : sans ce verrou, un double clic crée deux lobbies.
        private bool _isCreatingLobby;

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
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;

            TryJoinFromCommandLine();
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            }

            LeaveLobby();
            _lobbyCreated?.Dispose();
            _joinRequested?.Dispose();
            _lobbyEntered?.Dispose();
        }

        public void HostLobby()
        {
            if (!IsSteamReady || IsInLobby || _isCreatingLobby || !_networkManager.IsOffline)
                return;

            _isCreatingLobby = true;
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
            if (IsInLobby && IsSteamReady)
                SteamMatchmaking.LeaveLobby(_lobbyId);

            _lobbyId = CSteamID.Nil;
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            _isCreatingLobby = false;

            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"[SteamLobby] Création du lobby refusée : {callback.m_eResult}.");
                return;
            }

            _lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            // Steam seulement : Tugboat ouvert permettrait d'entrer par l'IP sans passer par le lobby.
            if (!_multipass.StartServer<FishySteamworks.FishySteamworks>())
            {
                Debug.LogError("[SteamLobby] Le serveur Steam n'a pas démarré, lobby fermé.");
                LeaveLobby();
                return;
            }

            // Sans adresse : FishySteamworks voit le serveur local et connecte l'hôte sans passer par le réseau.
            _multipass.SetClientTransport<FishySteamworks.FishySteamworks>();
            _networkManager.ClientManager.StartConnection();
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t callback) => JoinLobby(callback.m_steamIDLobby);

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

            // Déjà en partie (ou entrée arrivée en retard) : on ne relance pas une connexion par-dessus.
            if (!_networkManager.IsOffline || hostId == CSteamID.Nil)
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                return;
            }

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

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            // Serveur arrêté seul : le lobby resterait ouvert et joignable vers un hôte qui ne répond plus.
            if (args.ConnectionState == LocalConnectionState.Stopped)
                LeaveLobby();
        }

        private void JoinLobby(CSteamID lobbyId)
        {
            if (!_networkManager.IsOffline || IsInLobby || _isCreatingLobby)
                return;

            SteamMatchmaking.JoinLobby(lobbyId);
        }

        private void TryJoinFromCommandLine()
        {
            string[] arguments = Environment.GetCommandLineArgs();

            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == ConnectLobbyArgument && ulong.TryParse(arguments[i + 1], out ulong lobbyId))
                {
                    JoinLobby(new CSteamID(lobbyId));
                    return;
                }
            }
        }
    }
}
