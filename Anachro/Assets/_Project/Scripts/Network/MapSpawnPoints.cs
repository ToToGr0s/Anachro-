using FishNet;
using FishNet.Component.Spawning;
using FishNet.Managing;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Points d'apparition d'une map. Le PlayerSpawner vit sur le NetworkManager (Boot, persistant) et ne peut
    /// pas référencer une autre scène : la map lui donne ses points à son chargement, avant le spawn des joueurs.
    /// </summary>
    public sealed class MapSpawnPoints : MonoBehaviour
    {
        [SerializeField] private Transform[] _spawns = new Transform[0];

        private PlayerSpawner _spawner;

        private void Awake()
        {
            NetworkManager networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                Debug.LogWarning("[MapSpawnPoints] Pas de NetworkManager : lancer le jeu depuis Boot.");
                return;
            }

            _spawner = networkManager.GetComponent<PlayerSpawner>();
            if (_spawner != null)
                _spawner.Spawns = _spawns;
        }

        private void OnDestroy()
        {
            // La map déchargée, ses points n'existent plus : on ne laisse pas de références mortes au spawner.
            if (_spawner != null && _spawner.Spawns == _spawns)
                _spawner.Spawns = new Transform[0];
        }
    }
}
