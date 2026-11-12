using System.Linq;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Points d'apparition des joueurs d'une map, lus par MatchPlayerSpawner au lancement de la partie.
    /// Bureau (T19) : salle fermée hors de la carte où attendent les chasseurs pendant le repérage, et tout le monde à la fin.
    /// Sans point de bureau, tout le monde apparaît sur la carte, comme avant.
    /// </summary>
    public sealed class MapSpawnPoints : MonoBehaviour
    {
        [SerializeField] private Transform[] _spawns = new Transform[0];
        [Tooltip("Points dans le bureau (salle des chasseurs, hors de la carte). Vide = pas de bureau sur cette map.")]
        [SerializeField] private Transform[] _officeSpawns = new Transform[0];

        private Transform[] _validSpawns = new Transform[0];
        private Transform[] _validOfficeSpawns = new Transform[0];

        public int Count => _validSpawns.Length;
        public bool HasOffice => _validOfficeSpawns.Length > 0;

        private void Awake()
        {
            // Un point supprimé en LD laisse une case vide : on l'ignore au lieu de faire apparaître un joueur en (0, 0, 0).
            _validSpawns = _spawns.Where(spawn => spawn != null).ToArray();
            _validOfficeSpawns = _officeSpawns.Where(spawn => spawn != null).ToArray();
        }

        /// <summary>Point n° index, en boucle s'il y a plus de joueurs que de points.</summary>
        public Transform GetSpawn(int index) =>
            _validSpawns.Length == 0 ? null : _validSpawns[index % _validSpawns.Length];

        /// <summary>Point n° index du bureau, en boucle ; null s'il n'y a pas de bureau.</summary>
        public Transform GetOfficeSpawn(int index) =>
            _validOfficeSpawns.Length == 0 ? null : _validOfficeSpawns[index % _validOfficeSpawns.Length];

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_spawns.Any(spawn => spawn == null) || _officeSpawns.Any(spawn => spawn == null))
                Debug.LogWarning($"[MapSpawnPoints] {name} : point d'apparition vide dans la liste, il sera ignoré.", this);
        }
#endif
    }
}
