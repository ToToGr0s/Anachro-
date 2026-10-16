using Anachronism.Network;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    /// <summary>
    /// Menu principal et lobby : créer une partie, rejoindre avec un code, lancer, quitter.
    /// Passe par SteamLobby et MatchManager, ne modifie jamais l'état réseau lui-même (règle de code 5).
    /// </summary>
    // TODO (Olivier) : version provisoire posée par Théo pour tester le code de partie, à reprendre (UXML/USS).
    [RequireComponent(typeof(UIDocument))]
    public sealed class LobbyMenu : MonoBehaviour
    {
        private enum MenuScreen
        {
            Home,
            Connecting,
            Lobby,
            Match
        }

        private UIDocument _document;
        private NetworkManager _networkManager;
        private SteamLobby _steamLobby;

        private VisualElement _homePanel;
        private VisualElement _connectingPanel;
        private VisualElement _lobbyPanel;
        private VisualElement _matchBar;
        private TextField _codeField;
        private Button _createButton;
        private Button _joinButton;
        private Button _startButton;
        private Button _copyButton;
        private Button _leaveButton;
        private Button _cancelButton;
        private Button _quitButton;
        private Label _steamWarning;
        private Label _lobbyCode;
        private Label _playerCount;
        private Label _resultLabel;
        private Label _waitingLabel;
        private Label _matchPhase;
        private Label _matchRole;
        private Label _statusLabel;

        private bool _isLeaving;
        private MenuScreen _currentScreen = (MenuScreen)(-1);
        // Dernières valeurs affichées : on ne reconstruit les textes que quand elles changent (pas d'allocation par frame).
        private int _shownPlayers = -1;
        private int _shownSeconds = -1;
        private GamePhase _shownPhase;
        private PlayerRole _shownRole;

        private void Awake()
        {
            // Le menu vit dans Boot et doit survivre au chargement du menu puis de la map.
            DontDestroyOnLoad(gameObject);
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // UIDocument reconstruit son arbre à chaque activation : on rebranche tout ici.
            VisualElement root = _document.rootVisualElement;
            _homePanel = root.Q<VisualElement>("home-panel");
            _connectingPanel = root.Q<VisualElement>("connecting-panel");
            _lobbyPanel = root.Q<VisualElement>("lobby-panel");
            _matchBar = root.Q<VisualElement>("match-bar");
            _codeField = root.Q<TextField>("code-field");
            _createButton = root.Q<Button>("create-button");
            _joinButton = root.Q<Button>("join-button");
            _startButton = root.Q<Button>("start-button");
            _steamWarning = root.Q<Label>("steam-warning");
            _lobbyCode = root.Q<Label>("lobby-code");
            _playerCount = root.Q<Label>("player-count");
            _resultLabel = root.Q<Label>("result-label");
            _waitingLabel = root.Q<Label>("waiting-label");
            _matchPhase = root.Q<Label>("match-phase");
            _matchRole = root.Q<Label>("match-role");
            _statusLabel = root.Q<Label>("status-label");

            _createButton.clicked += OnCreateClicked;
            _joinButton.clicked += OnJoinClicked;
            _startButton.clicked += OnStartClicked;
            _copyButton = root.Q<Button>("copy-button");
            _leaveButton = root.Q<Button>("leave-button");
            _cancelButton = root.Q<Button>("cancel-button");
            _quitButton = root.Q<Button>("quit-button");

            _copyButton.clicked += OnCopyClicked;
            _leaveButton.clicked += OnLeaveClicked;
            _cancelButton.clicked += OnLeaveClicked;
            _quitButton.clicked += OnQuitClicked;
            _codeField.RegisterCallback<KeyDownEvent>(OnCodeFieldKeyDown);

            _currentScreen = (MenuScreen)(-1);
        }

        private void OnDisable()
        {
            // Mêmes références qu'à l'abonnement : l'arbre a pu être reconstruit entre-temps.
            _createButton.clicked -= OnCreateClicked;
            _joinButton.clicked -= OnJoinClicked;
            _startButton.clicked -= OnStartClicked;
            _copyButton.clicked -= OnCopyClicked;
            _leaveButton.clicked -= OnLeaveClicked;
            _cancelButton.clicked -= OnLeaveClicked;
            _quitButton.clicked -= OnQuitClicked;
            _codeField.UnregisterCallback<KeyDownEvent>(OnCodeFieldKeyDown);
        }

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                Debug.LogError("[LobbyMenu] Pas de NetworkManager : lancer le jeu depuis Boot.");
                enabled = false;
                return;
            }

            _steamLobby = _networkManager.GetComponent<SteamLobby>();
            _steamLobby.Failed += OnLobbyFailed;
            _steamLobby.LobbyChanged += OnLobbyChanged;
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDestroy()
        {
            if (_steamLobby != null)
            {
                _steamLobby.Failed -= OnLobbyFailed;
                _steamLobby.LobbyChanged -= OnLobbyChanged;
            }

            if (_networkManager != null)
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        private void Update()
        {
            MenuScreen screen = GetScreen();
            if (screen != _currentScreen)
                ShowScreen(screen);

            // Textes qui bougent en continu (joueurs, chrono) : mis à jour seulement sur l'écran visible.
            if (screen == MenuScreen.Home)
                RefreshHome();
            else if (screen == MenuScreen.Lobby)
                RefreshLobby();
            else if (screen == MenuScreen.Match)
                RefreshMatchBar();
        }

        private MenuScreen GetScreen()
        {
            if (_networkManager.IsClientStarted)
            {
                MatchManager match = MatchManager.Instance;
                bool isPlaying = match != null && match.Phase != GamePhase.Lobby && match.Phase != GamePhase.End;
                return isPlaying ? MenuScreen.Match : MenuScreen.Lobby;
            }

            bool isConnecting = _steamLobby.IsBusy || _networkManager.ClientManager.Started;
            return isConnecting ? MenuScreen.Connecting : MenuScreen.Home;
        }

        private void ShowScreen(MenuScreen screen)
        {
            _currentScreen = screen;
            _shownPlayers = -1;
            _shownSeconds = -1;
            _shownRole = (PlayerRole)255;
            SetVisible(_homePanel, screen == MenuScreen.Home);
            SetVisible(_connectingPanel, screen == MenuScreen.Connecting);
            SetVisible(_lobbyPanel, screen == MenuScreen.Lobby);
            SetVisible(_matchBar, screen == MenuScreen.Match);

            if (screen == MenuScreen.Lobby)
            {
                bool isHost = _networkManager.IsServerStarted;
                SetVisible(_startButton, isHost);
                SetVisible(_waitingLabel, !isHost);
                _lobbyCode.text = LobbyCode.Format(_steamLobby.Code);
                SetStatus(string.Empty);
            }
        }

        private void RefreshHome()
        {
            bool isSteamReady = _steamLobby.IsSteamReady;
            SetVisible(_steamWarning, !isSteamReady);
            _createButton.SetEnabled(isSteamReady);
            _joinButton.SetEnabled(isSteamReady && !string.IsNullOrWhiteSpace(_codeField.value));
        }

        private void RefreshLobby()
        {
            int players = _steamLobby.MemberCount;
            if (players != _shownPlayers)
            {
                _shownPlayers = players;
                _playerCount.text = players > 0 ? $"{players} / {_steamLobby.MaxPlayers} joueurs" : string.Empty;
            }

            MatchManager match = MatchManager.Instance;
            bool hasEnded = match != null && match.Phase == GamePhase.End;
            SetVisible(_resultLabel, hasEnded);
            if (hasEnded)
                _resultLabel.text = match.Winner == Winner.Guardians ? "Victoire des gentils" : "Victoire des méchants";

            _startButton.text = hasEnded ? "Rejouer" : "Lancer la partie";
            _startButton.SetEnabled(match != null);
        }

        private void RefreshMatchBar()
        {
            MatchManager match = MatchManager.Instance;
            int seconds = Mathf.CeilToInt(match.RemainingTime);
            if (seconds != _shownSeconds || match.Phase != _shownPhase)
            {
                _shownSeconds = seconds;
                _shownPhase = match.Phase;
                _matchPhase.text = $"{DescribePhase(match.Phase)}  {FormatTime(seconds)}";
            }

            if (match.LocalRole != _shownRole)
            {
                _shownRole = match.LocalRole;
                _matchRole.text = match.LocalRole == PlayerRole.Villain ? "Méchant" : match.LocalRole == PlayerRole.Guardian ? "Gentil" : string.Empty;
            }
        }

        private void OnCreateClicked()
        {
            SetStatus(string.Empty);
            _steamLobby.HostLobby();
        }

        private void OnJoinClicked()
        {
            SetStatus(string.Empty);
            _steamLobby.JoinByCode(_codeField.value);
        }

        private void OnCodeFieldKeyDown(KeyDownEvent keyEvent)
        {
            if ((keyEvent.keyCode == KeyCode.Return || keyEvent.keyCode == KeyCode.KeypadEnter) && _joinButton.enabledSelf)
                OnJoinClicked();
        }

        private void OnStartClicked()
        {
            // StartMatch est réservé au serveur : le bouton n'est montré qu'à l'hôte, on revérifie quand même.
            MatchManager match = MatchManager.Instance;
            if (match == null || !_networkManager.IsServerStarted)
                return;

            SetStatus(match.StartMatch() switch
            {
                MatchStartResult.NotEnoughPlayers => "Pas assez de joueurs pour lancer la partie.",
                MatchStartResult.PlayersLoading => "Un joueur charge encore la map, réessaie dans un instant.",
                MatchStartResult.InvalidSettings => "Réglages de partie invalides (voir la console).",
                MatchStartResult.WrongPhase => "Une partie est déjà en cours.",
                _ => string.Empty
            });
        }

        private void OnCopyClicked() => GUIUtility.systemCopyBuffer = _steamLobby.Code;

        private void OnLeaveClicked()
        {
            // Sans client démarré (annulation pendant « Connexion… »), aucun Stopped n'arrivera pour remettre le drapeau.
            _isLeaving = _networkManager.ClientManager.Started;
            _steamLobby.LeaveGame();
        }

        private void OnQuitClicked() => Application.Quit();

        private void OnLobbyFailed(string message) => SetStatus(message);

        private void OnLobbyChanged() => _shownPlayers = -1;

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped)
                return;

            if (!_isLeaving)
                SetStatus("Déconnecté de la partie.");

            _isLeaving = false;
        }

        private void SetStatus(string message) => _statusLabel.text = message;

        private static void SetVisible(VisualElement element, bool isVisible) =>
            element.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

        private static string FormatTime(int seconds) =>
            seconds <= 0 ? string.Empty : $"{seconds / 60}:{seconds % 60:00}";

        private static string DescribePhase(GamePhase phase) => phase switch
        {
            GamePhase.Briefing => "Préparation",
            GamePhase.Hiding => "Les méchants cachent les objets",
            GamePhase.Searching => "Recherche",
            GamePhase.Portal => "Portail ouvert",
            _ => string.Empty
        };
    }
}
