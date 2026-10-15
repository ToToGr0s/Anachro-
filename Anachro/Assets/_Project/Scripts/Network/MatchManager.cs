using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Objet réseau de scène (dans la map). Seul l'hôte décide : phases, rôles, chrono, victoire.
    /// Les SyncVar ne contiennent rien de secret : chaque rôle n'est envoyé qu'à son joueur (TargetRpc).
    /// Tir, anachronismes et portail arrivent avec T06 / T07.
    /// </summary>
    public sealed class MatchManager : NetworkBehaviour
    {
        // Singleton autorisé par la règle de code 2.
        public static MatchManager Instance { get; private set; }

        [SerializeField] private MatchPresetTable _presets;

        // ---------- État public synchronisé ----------
        private readonly SyncVar<GamePhase> _phase = new SyncVar<GamePhase>();
        private readonly SyncVar<uint> _phaseEndTick = new SyncVar<uint>();   // 0 = pas de chrono
        private readonly SyncVar<float> _degradation = new SyncVar<float>();  // 0..1
        private readonly SyncVar<Winner> _winner = new SyncVar<Winner>();

        // ---------- État serveur uniquement ----------
        private readonly Dictionary<NetworkConnection, PlayerRole> _roles = new Dictionary<NetworkConnection, PlayerRole>();
        // Méchants capturés (T06) ou déconnectés : ils restent dans _roles pour compter les méchants de la partie.
        private readonly HashSet<NetworkConnection> _neutralizedVillains = new HashSet<NetworkConnection>();
        private MatchSettings _settings;

        public GamePhase Phase => _phase.Value;
        public float Degradation => _degradation.Value;
        public Winner Winner => _winner.Value;

        /// <summary>Rôle du joueur local, reçu par TargetRpc et jamais diffusé aux autres.</summary>
        public PlayerRole LocalRole { get; private set; }

        /// <summary>Temps restant de la phase, en ticks réseau (valable côté client comme serveur).</summary>
        public float RemainingTime
        {
            get
            {
                uint endTick = _phaseEndTick.Value;
                uint nowTick = TimeManager.Tick;
                return endTick == 0 || endTick <= nowTick ? 0f : (float)TimeManager.TicksToTime(endTick - nowTick);
            }
        }

        public event Action<GamePhase> PhaseChanged;
        public event Action<PlayerRole> LocalRoleAssigned;

        private void Awake()
        {
            Instance = this;
            _phase.OnChange += OnPhaseChange;
        }

        private void OnDestroy()
        {
            _phase.OnChange -= OnPhaseChange;

            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            TimeManager.OnTick += OnServerTick;
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnTick -= OnServerTick;
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            LocalRole = PlayerRole.None;
        }

        [Server]
        public PlayerRole GetRoleServer(NetworkConnection connection) =>
            connection != null && _roles.TryGetValue(connection, out PlayerRole role) ? role : PlayerRole.None;

        // ================= Déroulé de la partie =================

        /// <summary>Démarre avec le préréglage correspondant au nombre de joueurs connectés.</summary>
        [Server]
        public bool StartMatch() => StartMatch(_presets.GetFor(GetActivePlayers().Count));

        [Server]
        public bool StartMatch(MatchSettings settings)
        {
            if (_phase.Value != GamePhase.Lobby && _phase.Value != GamePhase.End)
                return false;

            List<NetworkConnection> players = GetActivePlayers();
            if (players.Count < _presets.MinPlayers)
                return false;

            Shuffle(players);
            _settings = settings;
            _roles.Clear();
            _neutralizedVillains.Clear();

            // Au moins un gentil : sinon la partie serait gagnée d'avance par les méchants.
            int villainCount = Mathf.Clamp(settings.VillainCount, 1, players.Count - 1);
            for (int i = 0; i < players.Count; i++)
            {
                PlayerRole role = i < villainCount ? PlayerRole.Villain : PlayerRole.Guardian;
                _roles[players[i]] = role;
                TargetAssignRole(players[i], role);
            }

            _degradation.Value = 0f;
            _winner.Value = Winner.None;
            SetPhase(GamePhase.Briefing, settings.BriefingDuration);
            return true;
        }

        [TargetRpc]
        private void TargetAssignRole(NetworkConnection connection, PlayerRole role)
        {
            LocalRole = role;
            LocalRoleAssigned?.Invoke(role);
        }

        [Server]
        private void SetPhase(GamePhase phase, float duration)
        {
            _phase.Value = phase;
            _phaseEndTick.Value = duration > 0f
                ? TimeManager.Tick + TimeManager.TimeToTicks(duration, TickRounding.RoundUp)
                : 0;

            // Réévalue les conditions d'observation (T05 : méchants et objets cachés aux gentils pendant la cache).
            ServerManager.Objects.RebuildObservers();
        }

        private void OnServerTick()
        {
            GamePhase phase = _phase.Value;
            if (phase == GamePhase.Lobby || phase == GamePhase.End)
                return;

            if (phase == GamePhase.Searching || phase == GamePhase.Portal)
            {
                AddDegradation((float)TimeManager.TickDelta / _settings.DegradationDuration);
                if (_phase.Value == GamePhase.End)
                    return;
            }

            if (_phaseEndTick.Value == 0 || TimeManager.Tick < _phaseEndTick.Value)
                return;

            if (phase == GamePhase.Briefing)
                SetPhase(GamePhase.Hiding, _settings.HidingDuration);
            // TODO (Théo) : avec T07, placer automatiquement les objets que les méchants n'ont pas posés à temps.
            else if (phase == GamePhase.Hiding)
                SetPhase(GamePhase.Searching, 0f);
        }

        [Server]
        private void AddDegradation(float amount)
        {
            _degradation.Value = Mathf.Min(1f, _degradation.Value + amount);

            if (_degradation.Value >= 1f)
                EndMatch(Winner.Villains);
        }

        [Server]
        private void EndMatch(Winner winner)
        {
            if (_phase.Value == GamePhase.End)
                return;

            _winner.Value = winner;
            SetPhase(GamePhase.End, 0f);
        }

        // ================= Méchants neutralisés =================

        /// <summary>Capture (T06) ou déconnexion. Les gentils gagnent quand tous les méchants sont neutralisés.</summary>
        [Server]
        private void NeutralizeVillain(NetworkConnection villain)
        {
            if (!_neutralizedVillains.Add(villain))
                return;

            int villainCount = 0;
            foreach (PlayerRole role in _roles.Values)
            {
                if (role == PlayerRole.Villain)
                    villainCount++;
            }

            if (_neutralizedVillains.Count >= villainCount)
                EndMatch(Winner.Guardians);
        }

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped || !_roles.TryGetValue(connection, out PlayerRole role))
                return;

            if (_phase.Value == GamePhase.Lobby || _phase.Value == GamePhase.End)
            {
                _roles.Remove(connection);
                return;
            }

            // Un méchant qui quitte est neutralisé : quitter ne doit pas être un moyen de faire durer la partie.
            if (role == PlayerRole.Villain)
            {
                NeutralizeVillain(connection);
                return;
            }

            _roles.Remove(connection);
            if (!_roles.ContainsValue(PlayerRole.Guardian))
                EndMatch(Winner.Villains);
        }

        private void OnPhaseChange(GamePhase previous, GamePhase next, bool asServer)
        {
            // Sur l'hôte, OnChange passe côté serveur puis côté client : on ne lève l'event qu'une fois.
            if (asServer && IsClientInitialized)
                return;

            PhaseChanged?.Invoke(next);
        }

        private List<NetworkConnection> GetActivePlayers()
        {
            List<NetworkConnection> players = new List<NetworkConnection>();
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                if (connection.IsActive)
                    players.Add(connection);
            }

            return players;
        }

        private static void Shuffle(List<NetworkConnection> players)
        {
            for (int i = players.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (players[i], players[j]) = (players[j], players[i]);
            }
        }
    }
}
