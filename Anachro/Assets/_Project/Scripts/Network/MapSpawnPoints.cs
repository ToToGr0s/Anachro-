using System.Linq;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Points d'apparition des joueurs d'une map, lus par MatchPlayerSpawner au lancement de la partie.
    /// </summary>
    public sealed class MapSpawnPoints : MonoBehaviour
    {
        [SerializeField] private Transform[] _spawns = new Transform[0];

        private Transform[] _validSpawns = new Transform[0];

        public int Count => _validSpawns.Length;

        private void Awake()
        {
            // Un point supprimé en LD laisse une case vide : on l'ignore au lieu de faire apparaître un joueur en (0, 0, 0).
            _validSpawns = _spawns.Where(spawn => spawn != null).ToArray();
        }

        /// <summary>Point n° index, en boucle s'il y a plus de joueurs que de points.</summary>
        public Transform GetSpawn(int index) =>
            _validSpawns.Length == 0 ? null : _validSpawns[index % _validSpawns.Length];

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_spawns.Any(spawn => spawn == null))
                Debug.LogWarning($"[MapSpawnPoints] {name} : point d'apparition vide dans la liste, il sera ignoré.", this);
        }
#endif
    }
}
