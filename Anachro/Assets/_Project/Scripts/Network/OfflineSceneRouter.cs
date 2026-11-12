using FishNet.Component.Scenes;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

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
        private MatchManager _match;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _defaultScene = GetComponent<DefaultScene>();

            // Un chemin renommé casserait le retour sans erreur : FishNet chargerait une scène absente du build.
            foreach (string path in new[] { _titleScenePath, _lobbyScenePath })
            {
                if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
                    Debug.LogError($"[OfflineSceneRouter] {path} absente des Build Settings.", this);
            }
        }

        private void OnEnable()
        {
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            UnitySceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnitySceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindMatch();
            if (_networkManager == null)
                return;

            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        // MatchManager vit dans la map et se déclare dans son Awake : il est là quand la map a fini de charger.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (MatchManager.Instance == null || MatchManager.Instance == _match)
                return;

            UnbindMatch();
            _match = MatchManager.Instance;
            _match.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(_match.Phase);
        }

        private void UnbindMatch()
        {
            if (_match != null)
                _match.PhaseChanged -= OnPhaseChanged;
            _match = null;
        }

        // Une fois la partie lancée, une déconnexion ramène à l'écran titre (choix de Théo).
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Lobby)
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
        private void RouteToLobby() => _defaultScene.SetOfflineScene(_lobbyScenePath);
    }
}
