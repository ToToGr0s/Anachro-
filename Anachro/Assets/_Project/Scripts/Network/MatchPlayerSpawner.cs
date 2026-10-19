using System.Collections.Generic;
using Anachronism.Npc;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Pas de joueur au lobby : les avatars apparaissent au lancement de la partie et disparaissent à la fin,
    /// pour que le menu du lobby (et « Rejouer ») reste cliquable, souris libre, sur la vue d'ensemble de la map.
    /// </summary>
    [RequireComponent(typeof(MatchManager))]
    public sealed class MatchPlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject _playerPrefab;
        [Tooltip("Tirage d'un point au hasard sur le NavMesh, comme les PNJ : un joueur ne se repère pas à son point d'apparition.")]
        [SerializeField] private NpcDestinationManager _npcs;
        [Tooltip("Secours si la map n'a pas de NavMesh.")]
        [SerializeField] private MapSpawnPoints _spawnPoints;

        // Le pivot du joueur est au centre de sa capsule : apparaître au ras du sol l'enfoncerait à moitié dedans.
        private const float SpawnHeightAboveGround = 1f;

        private readonly Dictionary<NetworkConnection, NetworkObject> _players = new Dictionary<NetworkConnection, NetworkObject>();
        private readonly List<NetworkConnection> _toSpawn = new List<NetworkConnection>();
        private MatchManager _match;

        private void Awake()
        {
            _match = GetComponent<MatchManager>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _match.PhaseChanged += OnPhaseChanged;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _match.PhaseChanged -= OnPhaseChanged;
            _players.Clear();
        }

        // PhaseChanged n'est levé qu'une fois sur l'hôte (côté client) : on revérifie qu'on est bien le serveur.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (!IsServerInitialized)
                return;

            if (phase == GamePhase.Lobby || phase == GamePhase.End)
                DespawnPlayers();
            else
                SpawnMissingPlayers();
        }

        [Server]
        private void SpawnMissingPlayers()
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[MatchPlayerSpawner] Prefab joueur manquant : lancer Anachro > Setup > Brancher joueur et PNJ en réseau.", this);
                return;
            }

            // Seuls les joueurs qui ont reçu un rôle jouent : quelqu'un qui rejoint en cours de partie reste au lobby.
            _toSpawn.Clear();
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                if (connection.IsActive && _match.GetRoleServer(connection) != PlayerRole.None
                    && !(_players.TryGetValue(connection, out NetworkObject existing) && existing != null && existing.IsSpawned))
                    _toSpawn.Add(connection);
            }

            for (int i = 0; i < _toSpawn.Count; i++)
            {
                GetSpawn(i, out Vector3 position, out Quaternion rotation);
                NetworkObject player = NetworkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);
                ServerManager.Spawn(player, _toSpawn[i], gameObject.scene);
                _players[_toSpawn[i]] = player;
            }
        }

        private void GetSpawn(int index, out Vector3 position, out Quaternion rotation)
        {
            if (_npcs != null && _npcs.TryGetRandomPoint(out Vector3 point))
            {
                position = point + Vector3.up * SpawnHeightAboveGround;
                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                return;
            }

            Transform spawn = _spawnPoints != null ? _spawnPoints.GetSpawn(index) : null;
            position = spawn != null ? spawn.position : _playerPrefab.transform.position;
            rotation = spawn != null ? spawn.rotation : _playerPrefab.transform.rotation;
        }

        [Server]
        private void DespawnPlayers()
        {
            // Un joueur déconnecté a déjà été retiré par FishNet (objet possédé) : on ne despawn que ce qui existe encore.
            foreach (NetworkObject player in _players.Values)
            {
                if (player != null && player.IsSpawned)
                    ServerManager.Despawn(player);
            }

            _players.Clear();
        }
    }
}
