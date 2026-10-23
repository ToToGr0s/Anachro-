using System.Collections.Generic;
using Anachronism.Npc;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Pas de joueur au lobby : les avatars apparaissent au lancement de la partie.
    /// Avec un bureau sur la map (MapSpawnPoints, T19) : les chasseurs y attendent pendant le repérage, sont envoyés sur
    /// la carte à la première pose, et tout le monde y revient à la fin (souris libre pour le menu du lobby).
    /// Sans bureau : les chasseurs n'ont pas d'avatar pendant le repérage et apparaissent sur la carte à la première pose ;
    /// plus d'avatar à la fin (vue d'ensemble de la map).
    /// Un joueur qui revient en pleine partie avec son rôle (MatchManager.PlayerRejoined) réapparaît aussitôt.
    /// </summary>
    // TODO (Théo) : un client modifié distingue encore un avatar de joueur d'un PNJ (owner dans le message de spawn,
    // prefab et composants différents). PR suivante : avatar sans owner, même prefab que les PNJ, déplacé par l'hôte
    // d'après la position envoyée par le client.
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
        // Compté sur toute la partie : un joueur qui réapparaît ne reprend pas le point d'un autre.
        private int _spawnCount;

        private void Awake()
        {
            _match = GetComponent<MatchManager>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _match.PhaseChanged += OnPhaseChanged;
            _match.PlayerRejoined += OnPlayerRejoined;
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _match.PhaseChanged -= OnPhaseChanged;
            _match.PlayerRejoined -= OnPlayerRejoined;
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            _players.Clear();
        }

        // PhaseChanged n'est levé qu'une fois sur l'hôte (côté client) : on revérifie qu'on est bien le serveur.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (!IsServerInitialized)
                return;

            switch (phase)
            {
                case GamePhase.Lobby:
                    DespawnPlayers();
                    break;
                case GamePhase.End:
                    if (HasOffice)
                        MoveExistingPlayers();
                    else
                        DespawnPlayers();
                    break;
                case GamePhase.Scouting:
                    // « Rejouer » depuis le bureau : les avatars restent, chacun repart selon son nouveau rôle.
                    _spawnCount = 0;
                    MoveExistingPlayers();
                    SpawnMissingPlayers();
                    break;
                default:
                    // Chasse : les chasseurs quittent le bureau pour la carte.
                    MoveExistingPlayers();
                    SpawnMissingPlayers();
                    break;
            }
        }

        private bool HasOffice => _spawnPoints != null && _spawnPoints.HasOffice;

        // Une fois par changement de phase : la recherche du composant reste ponctuelle.
        [Server]
        private void MoveExistingPlayers()
        {
            foreach (KeyValuePair<NetworkConnection, NetworkObject> pair in _players)
            {
                if (pair.Value == null || !pair.Value.IsSpawned)
                    continue;

                bool isInOffice = IsInOffice(pair.Key);
                PlayerNetworkSetup setup = pair.Value.GetComponentInChildren<PlayerNetworkSetup>(true);
                if (setup == null || setup.IsInOfficeServer == isInOffice)
                    continue;

                GetSpawn(pair.Key, _spawnCount++, out Vector3 position, out Quaternion rotation);
                setup.TeleportServer(position, rotation, isInOffice);
            }
        }

        // Bureau : les chasseurs pendant le repérage, tout le monde à la fin.
        private bool IsInOffice(NetworkConnection connection)
        {
            if (!HasOffice)
                return false;

            GamePhase phase = _match.Phase;
            return phase == GamePhase.End
                || (phase == GamePhase.Scouting && _match.GetRoleServer(connection) == PlayerRole.Guardian);
        }

        private void OnPlayerRejoined(NetworkConnection connection)
        {
            if (connection.IsActive && !HasAvatar(connection) && ShouldHaveAvatar(connection))
                SpawnPlayer(connection);
        }

        // Sans bureau, un chasseur n'a nulle part où attendre le repérage : pas d'avatar avant la première pose.
        private bool ShouldHaveAvatar(NetworkConnection connection) =>
            HasOffice || _match.Phase != GamePhase.Scouting || _match.GetRoleServer(connection) != PlayerRole.Guardian;

        // FishNet despawn déjà l'avatar d'un joueur qui part (il en est l'owner) : on oublie juste l'entrée.
        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
                _players.Remove(connection);
        }

        [Server]
        private void SpawnMissingPlayers()
        {
            // Seuls les joueurs qui ont reçu un rôle jouent : quelqu'un qui rejoint en cours de partie reste au lobby.
            _toSpawn.Clear();
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                if (connection.IsActive && _match.GetRoleServer(connection) != PlayerRole.None && !HasAvatar(connection)
                    && ShouldHaveAvatar(connection))
                    _toSpawn.Add(connection);
            }

            foreach (NetworkConnection connection in _toSpawn)
                SpawnPlayer(connection);
        }

        [Server]
        private void SpawnPlayer(NetworkConnection connection)
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[MatchPlayerSpawner] Prefab joueur manquant : lancer Anachro > Setup > Brancher joueur et PNJ en réseau.", this);
                return;
            }

            GetSpawn(connection, _spawnCount++, out Vector3 position, out Quaternion rotation);
            NetworkObject player = NetworkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);
            PlayerNetworkSetup setup = player.GetComponentInChildren<PlayerNetworkSetup>(true);
            if (setup != null)
                setup.SetSpawnedInOffice(IsInOffice(connection));
            ServerManager.Spawn(player, connection, gameObject.scene);
            _players[connection] = player;
        }

        private bool HasAvatar(NetworkConnection connection) =>
            _players.TryGetValue(connection, out NetworkObject player) && player != null && player.IsSpawned;

        private void GetSpawn(NetworkConnection connection, int index, out Vector3 position, out Quaternion rotation)
        {
            Transform office = IsInOffice(connection) ? _spawnPoints.GetOfficeSpawn(index) : null;
            if (office != null)
            {
                position = office.position;
                rotation = office.rotation;
                return;
            }

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
            foreach (NetworkObject player in _players.Values)
            {
                if (player != null && player.IsSpawned)
                    ServerManager.Despawn(player);
            }

            _players.Clear();
            _spawnCount = 0;
        }
    }
}
