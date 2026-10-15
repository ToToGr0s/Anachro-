using FishNet.Managing;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Boutons Hôte / Client / Stop pour tester en local (Tugboat) en attendant le lobby Steam (T08).
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class DevConnectionHud : MonoBehaviour
    {
        [SerializeField] private string _address = "localhost";

        private NetworkManager _networkManager;

        private void Awake()
        {
            // Outil de dev : absent des builds de release, sans dépendre d'un #if qui changerait la sérialisation.
            if (!Debug.isDebugBuild)
            {
                Destroy(this);
                return;
            }

            _networkManager = GetComponent<NetworkManager>();
        }

        private void OnGUI()
        {
            bool isServerStarted = _networkManager.ServerManager.Started;
            bool isClientStarted = _networkManager.ClientManager.Started;

            GUILayout.BeginArea(new Rect(10f, 10f, 220f, 140f), GUI.skin.box);

            if (!isServerStarted && !isClientStarted)
            {
                _address = GUILayout.TextField(_address);

                if (GUILayout.Button("Hôte"))
                    StartHost();

                if (GUILayout.Button("Client"))
                    _networkManager.ClientManager.StartConnection(_address);
            }
            else
            {
                GUILayout.Label(isServerStarted ? "Hôte" : "Client");

                if (GUILayout.Button("Stop"))
                    Stop(isServerStarted, isClientStarted);
            }

            GUILayout.EndArea();
        }

        private void StartHost()
        {
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
        }

        private void Stop(bool isServerStarted, bool isClientStarted)
        {
            if (isClientStarted)
                _networkManager.ClientManager.StopConnection();

            if (isServerStarted)
                _networkManager.ServerManager.StopConnection(true);
        }
    }
}
