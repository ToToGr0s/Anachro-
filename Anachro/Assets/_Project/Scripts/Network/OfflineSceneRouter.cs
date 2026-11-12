using FishNet.Component.Scenes;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Choisit où FishNet ramène le joueur à la déconnexion (scène hors-ligne de DefaultScene) :
    /// l'écran titre après une partie jouée, l'accueil du lobby sinon (connexion ratée, annulée, salon quitté),
    /// pour que le joueur retrouve le message d'erreur et puisse réessayer.
    /// </summary>
    [RequireComponent(typeof(NetworkManager), typeof(DefaultScene))]
    public sealed class OfflineSceneRouter : MonoBehaviour
    {
        [SerializeField] private string _titleScenePath = "Assets/_Project/Scenes/TitleScreen.unity";
        [SerializeField] private string _lobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";

        private NetworkManager _networkManager;
        private DefaultScene _defaultScene;
        private bool _hasMatchStarted;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _defaultScene = GetComponent<DefaultScene>();
        }

        private void OnEnable()
        {
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        private void OnDisable()
        {
            if (_networkManager == null)
                return;

            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        private void Update()
        {
            // Une fois la partie lancée, une déconnexion ramène à l'écran titre (choix de Théo).
            if (_hasMatchStarted)
                return;

            MatchManager match = MatchManager.Instance;
            if (match == null || match.Phase == GamePhase.Lobby)
                return;

            _hasMatchStarted = true;
            _defaultScene.SetOfflineScene(_titleScenePath);
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Starting)
                RouteToLobby();
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Starting)
                RouteToLobby();
        }

        // Nouvelle connexion : tant qu'aucune partie n'est lancée, un échec ramène à l'accueil du lobby.
        private void RouteToLobby()
        {
            _hasMatchStarted = false;
            _defaultScene.SetOfflineScene(_lobbyScenePath);
        }
    }
}
