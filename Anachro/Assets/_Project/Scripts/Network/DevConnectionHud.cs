using FishNet.Managing;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Boutons de test : Hôte / Client en local (Tugboat) et lobby Steam (SteamLobby).
    /// Pilote ServerManager / ClientManager en direct : toléré pour un outil de dev, pas pour l'UI du jeu.
    /// </summary>
    // TODO (Théo) : supprimer quand le menu de lobby (Olivier, UI/Lobby) remplace ce HUD.
    [RequireComponent(typeof(NetworkManager))]
    public sealed class DevConnectionHud : MonoBehaviour
    {
        [SerializeField] private string _address = "localhost";

        private NetworkManager _networkManager;
        private Multipass _multipass;
        private SteamLobby _steamLobby;

        private void Awake()
        {
            // Outil de dev : absent des builds de release, sans dépendre d'un #if qui changerait la sérialisation.
            if (!Debug.isDebugBuild)
            {
                // Destroy est différé : enabled = false coupe OnGUI dès maintenant.
                enabled = false;
                Destroy(this);
                return;
            }

            _networkManager = GetComponent<NetworkManager>();
            _multipass = GetComponent<Multipass>();
            _steamLobby = GetComponent<SteamLobby>();
        }

        private void OnGUI()
        {
            bool isServerStarted = _networkManager.ServerManager.Started;
            bool isClientStarted = _networkManager.ClientManager.Started;

            // Zone large et fond ajusté au contenu : les deux sections ne sont jamais coupées.
            GUILayout.BeginArea(new Rect(10f, 10f, 260f, Screen.height - 20f));
            GUILayout.BeginVertical(GUI.skin.box);

            if (!isServerStarted && !isClientStarted)
                DrawOffline();
            else
                DrawConnected(isServerStarted, isClientStarted);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawOffline()
        {
            GUILayout.Label("Local (Tugboat)");
            _address = GUILayout.TextField(_address);

            if (GUILayout.Button("Hôte local"))
                StartLocalHost();

            if (GUILayout.Button("Client local") && !string.IsNullOrWhiteSpace(_address))
                StartLocalClient(_address.Trim());

            GUILayout.Label("Steam");

            if (_steamLobby == null || !_steamLobby.IsSteamReady)
                GUILayout.Label("Steam non lancé");
            else if (GUILayout.Button("Héberger un lobby"))
                _steamLobby.HostLobby();
        }

        private void DrawConnected(bool isServerStarted, bool isClientStarted)
        {
            GUILayout.Label(isServerStarted ? "Hôte" : "Client");

            if (_steamLobby != null && _steamLobby.IsInLobby && GUILayout.Button("Inviter des amis"))
                _steamLobby.InviteFriends();

            if (GUILayout.Button("Stop"))
                Stop(isServerStarted, isClientStarted);
        }

        private void StartLocalHost()
        {
            // Tugboat seul : une partie locale n'a pas à être joignable par Steam.
            if (!_multipass.StartServer<Tugboat>())
                return;

            _multipass.SetClientTransport<Tugboat>();
            _networkManager.ClientManager.StartConnection();
        }

        private void StartLocalClient(string address)
        {
            _multipass.SetClientTransport<Tugboat>();
            _networkManager.ClientManager.StartConnection(address);
        }

        private void Stop(bool isServerStarted, bool isClientStarted)
        {
            if (_steamLobby != null)
                _steamLobby.LeaveLobby();

            if (isClientStarted)
                _networkManager.ClientManager.StopConnection();

            // true : prévient les clients pour qu'ils se déconnectent proprement au lieu d'attendre le timeout.
            if (isServerStarted)
                _networkManager.ServerManager.StopConnection(true);
        }
    }
}
