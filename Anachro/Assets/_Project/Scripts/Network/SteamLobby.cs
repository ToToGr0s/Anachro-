using System;
using Anachronism.Core;
using FishNet.Managing;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Lobby Steam : l'hôte crée un lobby et partage son code (LobbyCode), les autres le rejoignent avec ce code
    /// et se connectent en P2P (FishySteamworks, seul transport du jeu) sur le SteamID du propriétaire du lobby.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class SteamLobby : MonoBehaviour
    {
        // Argument ajouté par Steam quand on accepte une invitation alors que le jeu est fermé.
        private const string ConnectLobbyArgument = "+connect_lobby";

        [SerializeField] private int _maxPlayers = 8;
        [Tooltip("Délai avant d'abandonner si Steam ne répond pas à une demande pour rejoindre.")]
        [SerializeField, Min(1f)] private float _joinTimeout = 15f;

        private NetworkManager _networkManager;
        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdated;
        private CSteamID _lobbyId = CSteamID.Nil;
        // IsInLobby reste faux tant que Steam n'a pas répondu : sans ces verrous, un double clic crée deux lobbies.
        private bool _isCreatingLobby;
        // Lobby attendu : une réponse tardive d'une demande annulée ne doit pas passer pour la nouvelle.
        private CSteamID _joiningLobbyId = CSteamID.Nil;
        private float _joinDeadline;
        private bool _isLobbyClosedForMatch;

        public bool IsSteamReady => SteamService.Instance != null && SteamService.Instance.IsInitialized;
        public bool IsInLobby => _lobbyId != CSteamID.Nil;
        public bool IsBusy => _isCreatingLobby || IsJoining;
        private bool IsJoining => _joiningLobbyId != CSteamID.Nil;

        /// <summary>Code à partager (7 caractères), vide hors lobby.</summary>
        public string Code { get; private set; } = string.Empty;

        public int MemberCount => IsInLobby && IsSteamReady ? SteamMatchmaking.GetNumLobbyMembers(_lobbyId) : 0;
        public int MaxPlayers => _maxPlayers;

        /// <summary>Entrée, sortie ou changement de membres du lobby.</summary>
        public event Action LobbyChanged;

        /// <summary>Échec de création ou de connexion, avec un message à afficher au joueur.</summary>
        public event Action<string> Failed;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
        }

        private void Start()
        {
            // Start et pas Awake : SteamService doit avoir initialisé l'API avant de créer les callbacks.
            // Steam lancé après le jeu n'est pas rattrapé : le menu demande de relancer.
            if (!IsSteamReady)
                return;

            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _lobbyChatUpdated = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdated);
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;

            TryJoinFromCommandLine();
        }

        private void Update()
        {
            if (IsJoining && Time.unscaledTime > _joinDeadline)
            {
                _joiningLobbyId = CSteamID.Nil;
                Failed?.Invoke("Steam ne répond pas. Réessaie dans un instant.");
            }

            UpdateLobbyJoinable();
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
            _lobbyChatUpdated?.Dispose();
        }

        public void HostLobby()
        {
            if (!IsSteamReady || IsInLobby || IsBusy || !_networkManager.IsOffline)
                return;

            _isCreatingLobby = true;
            // Public : seul type qu'on peut rejoindre avec l'identifiant (donc le code) sans être ami ni invité.
            // TODO (Théo) : Authenticator FishNet (T11) pour refuser les connexions sans le code : quelqu'un qui liste
            // les lobbies publics ou connaît le SteamID de l'hôte peut aujourd'hui entrer sans passer par le code.
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, _maxPlayers);
        }

        /// <summary>Rejoint la partie dont on a reçu le code. Faux si la demande n'a pas pu partir.</summary>
        public bool JoinByCode(string code)
        {
            if (!LobbyCode.TryDecode(code, out ulong lobbyId))
            {
                Failed?.Invoke("Code invalide : 7 caractères, lettres et chiffres.");
                return false;
            }

            if (!JoinLobby(new CSteamID(lobbyId)))
            {
                Failed?.Invoke("Impossible de rejoindre maintenant : quitte d'abord la partie en cours.");
                return false;
            }

            return true;
        }

        public void InviteFriends()
        {
            if (IsInLobby)
                SteamFriends.ActivateGameOverlayInviteDialog(_lobbyId);
        }

        /// <summary>Quitte la partie (client ou hôte) et le lobby, ou annule une demande en cours.</summary>
        public void LeaveGame()
        {
            _joiningLobbyId = CSteamID.Nil;
            LeaveLobby();

            if (_networkManager.ClientManager.Started)
                _networkManager.ClientManager.StopConnection();

            // true : prévient les clients pour qu'ils se déconnectent proprement au lieu d'attendre le timeout.
            if (_networkManager.ServerManager.Started)
                _networkManager.ServerManager.StopConnection(true);
        }

        public void LeaveLobby()
        {
            bool wasInLobby = IsInLobby;

            // À la fermeture, SteamService peut avoir déjà coupé l'API : l'appeler planterait.
            if (wasInLobby && IsSteamReady)
                SteamMatchmaking.LeaveLobby(_lobbyId);

            _lobbyId = CSteamID.Nil;
            _isLobbyClosedForMatch = false;
            Code = string.Empty;

            if (wasInLobby)
                LobbyChanged?.Invoke();
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            _isCreatingLobby = false;

            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Failed?.Invoke($"Steam n'a pas pu créer la partie ({callback.m_eResult}).");
                return;
            }

            _lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            if (!LobbyCode.TryEncode(_lobbyId.m_SteamID, out string code))
            {
                Failed?.Invoke("Identifiant de lobby inattendu : impossible de générer un code.");
                LeaveLobby();
                return;
            }

            if (!_networkManager.ServerManager.StartConnection())
            {
                Failed?.Invoke("Le serveur n'a pas démarré.");
                LeaveLobby();
                return;
            }

            Code = code;
            LobbyChanged?.Invoke();

            // Sans adresse : FishySteamworks voit le serveur local et connecte l'hôte sans passer par le réseau.
            _networkManager.ClientManager.StartConnection();
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t callback)
        {
            if (!JoinLobby(callback.m_steamIDLobby))
                Failed?.Invoke("Invitation ignorée : quitte d'abord la partie en cours.");
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            // Le propriétaire du lobby fait serveur. Seul Steam peut le désigner, aucun membre ne peut le falsifier.
            CSteamID hostId = SteamMatchmaking.GetLobbyOwner(lobbyId);

            // L'hôte reçoit aussi LobbyEnter pour son propre lobby : il s'est déjà connecté dans OnLobbyCreated.
            if (hostId == SteamUser.GetSteamID())
                return;

            // Demande annulée, abandonnée (délai dépassé) ou remplacée par une autre : Steam répond quand même,
            // on ressort sans se connecter.
            if (lobbyId != _joiningLobbyId)
            {
                if (callback.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                    SteamMatchmaking.LeaveLobby(lobbyId);
                return;
            }

            _joiningLobbyId = CSteamID.Nil;

            EChatRoomEnterResponse response = (EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse;
            if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Failed?.Invoke(DescribeJoinFailure(response));
                return;
            }

            // Déjà en partie (entrée arrivée en retard) ou lobby sans hôte : on ne relance pas une connexion par-dessus.
            if (!_networkManager.IsOffline || hostId == CSteamID.Nil)
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                Failed?.Invoke(hostId == CSteamID.Nil ? "L'hôte a quitté la partie." : "Tu es déjà dans une partie.");
                return;
            }

            if (!LobbyCode.TryEncode(lobbyId.m_SteamID, out string code))
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                Failed?.Invoke("Identifiant de lobby inattendu.");
                return;
            }

            _lobbyId = lobbyId;
            Code = code;
            LobbyChanged?.Invoke();

            _networkManager.ClientManager.StartConnection(hostId.m_SteamID.ToString());
        }

        private void OnLobbyChatUpdated(LobbyChatUpdate_t callback)
        {
            if (callback.m_ulSteamIDLobby == _lobbyId.m_SteamID)
                LobbyChanged?.Invoke();
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            // Hôte parti ou déconnexion volontaire : on quitte le lobby pour ne pas y rester fantôme.
            // Côté hôte, c'est l'arrêt du serveur qui ferme le lobby (OnServerConnectionState).
            if (args.ConnectionState == LocalConnectionState.Stopped && !_networkManager.IsServerStarted)
                LeaveLobby();
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            // Serveur arrêté seul : le lobby resterait ouvert et joignable vers un hôte qui ne répond plus.
            if (args.ConnectionState == LocalConnectionState.Stopped)
                LeaveLobby();
        }

        /// <summary>
        /// L'hôte ferme le lobby pendant une manche : personne n'arrive en cours de partie sans rôle (T11),
        /// et il le rouvre au retour au salon pour la revanche.
        /// </summary>
        private void UpdateLobbyJoinable()
        {
            if (!IsInLobby || !_networkManager.IsServerStarted)
                return;

            MatchManager match = MatchManager.Instance;
            bool isPlaying = match != null && match.Phase != GamePhase.Lobby && match.Phase != GamePhase.End;
            if (isPlaying == _isLobbyClosedForMatch)
                return;

            _isLobbyClosedForMatch = isPlaying;
            SteamMatchmaking.SetLobbyJoinable(_lobbyId, !isPlaying);
        }

        private bool JoinLobby(CSteamID lobbyId)
        {
            if (!IsSteamReady || !_networkManager.IsOffline || IsInLobby || IsBusy)
                return false;

            _joiningLobbyId = lobbyId;
            _joinDeadline = Time.unscaledTime + _joinTimeout;
            SteamMatchmaking.JoinLobby(lobbyId);
            return true;
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

        private static string DescribeJoinFailure(EChatRoomEnterResponse response) => response switch
        {
            EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist => "Partie introuvable : vérifie le code.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseFull => "La partie est complète.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed => "La partie a déjà commencé.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned => "Tu as été exclu de cette partie.",
            _ => $"Impossible de rejoindre la partie ({response})."
        };
    }
}
