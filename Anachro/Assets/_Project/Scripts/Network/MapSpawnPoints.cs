using System.Linq;
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
        private Transform[] _validSpawns = new Transform[0];

        private void Awake()
        {
            NetworkManager networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                Debug.LogWarning("[MapSpawnPoints] Pas de NetworkManager : lancer le jeu depuis Boot.");
                return;
            }

            _spawner = networkManager.GetComponent<PlayerSpawner>();
            if (_spawner == null)
                return;

            // Un point supprimé en LD laisse une case vide : le spawner planterait au spawn suivant.
            _validSpawns = _spawns.Where(spawn => spawn != null).ToArray();
            // TODO (Théo) : PlayerSpawner garde son index de point entre deux maps ; une map avec moins de points que la
            // précédente le ferait sortir du tableau. Sans risque tant qu'il n'y a qu'une map, à revoir avec la map 2 (L08).
            _spawner.Spawns = _validSpawns;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_spawns.Any(spawn => spawn == null))
                Debug.LogWarning($"[MapSpawnPoints] {name} : point d'apparition vide dans la liste, il sera ignoré.", this);
        }
#endif

        private void OnDestroy()
        {
            // La map déchargée, ses points n'existent plus : on ne laisse pas de références mortes au spawner.
            if (_spawner != null && _spawner.Spawns == _validSpawns)
                _spawner.Spawns = new Transform[0];
        }
    }
}
