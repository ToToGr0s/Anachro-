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
    /// Tir et anachronismes arrivent avec T06 / T07.
    /// </summary>
    public sealed class MatchManager : NetworkBehaviour
    {
        // Singleton autorisé par la règle de code 2.
        public static MatchManager Instance { get; private set; }

        [SerializeField] private MatchPresetTable _presets;

        // ---------- État public synchronisé ----------
        private readonly SyncVar<GamePhase> _phase = new SyncVar<GamePhase>();
        private readonly SyncVar<uint> _phaseEndTick = new SyncVar<uint>();   // 0 = pas de chrono
        // Dégradation calculée par chacun à partir d'un point de départ et d'une vitesse : synchroniser la valeur
        // à chaque tick enverrait un float à tous les joueurs à chaque tick (règles réseau 4 et 6).
        private readonly SyncVar<float> _degradationBase = new SyncVar<float>();      // 0..1 au tick de référence
        private readonly SyncVar<uint> _degradationBaseTick = new SyncVar<uint>();
        private readonly SyncVar<float> _degradationPerSecond = new SyncVar<float>(); // 0 = ne monte pas
        private readonly SyncVar<Winner> _winner = new SyncVar<Winner>();

        // ---------- État serveur uniquement ----------
        private readonly Dictionary<NetworkConnection, PlayerRole> _roles = new Dictionary<NetworkConnection, PlayerRole>();
        // Méchants capturés (T06) ou déconnectés : ils restent dans _roles pour compter les méchants de la partie.
        private readonly HashSet<NetworkConnection> _neutralizedVillains = new HashSet<NetworkConnection>();
        // Adresse de chaque joueur de la partie, lue au lancement : à la déconnexion, le transport ne la donne plus.
        private readonly Dictionary<NetworkConnection, string> _addresses = new Dictionary<NetworkConnection, string>();
        // Joueurs partis en pleine partie, par adresse : ils retrouvent leur rôle s'ils reviennent avant la fin.
        private readonly Dictionary<string, AwayPlayer> _awayPlayers = new Dictionary<string, AwayPlayer>();
        private MatchSettings _settings;
        // Fin de la chasse, gardée côté serveur : en SyncVar, les clients connaîtraient le timer caché (T18-7).
        private uint _searchingEndTick;

        private readonly struct AwayPlayer
        {
            public readonly NetworkConnection Connection;
            public readonly PlayerRole Role;
            // Faux pour un méchant déjà capturé avant de partir : revenir ne le libère pas.
            public readonly bool WasActive;

            public AwayPlayer(NetworkConnection connection, PlayerRole role, bool wasActive)
            {
                Connection = connection;
                Role = role;
                WasActive = wasActive;
            }
        }

        public GamePhase Phase => _phase.Value;
        public float Degradation => IsSpawned ? ComputeDegradation(TimeManager.Tick) : 0f;
        public Winner Winner => _winner.Value;

        /// <summary>Rôle du joueur local, reçu par TargetRpc et jamais diffusé aux autres.</summary>
        public PlayerRole LocalRole { get; private set; }

        /// <summary>Temps restant de la phase, en ticks réseau (valable côté client comme serveur).</summary>
        public float RemainingTime
        {
            get
            {
                // TimeManager n'existe qu'une fois l'objet réseau initialisé (map ouverte hors partie, chargement).
                if (!IsSpawned)
                    return 0f;

                uint endTick = _phaseEndTick.Value;
                uint nowTick = TimeManager.Tick;
                return endTick == 0 || endTick <= nowTick ? 0f : (float)TimeManager.TicksToTime(endTick - nowTick);
            }
        }

        public event Action<GamePhase> PhaseChanged;
        public event Action<PlayerRole> LocalRoleAssigned;
        /// <summary>Côté serveur : un joueur parti en pleine partie est revenu et a retrouvé son rôle.</summary>
        public event Action<NetworkConnection> PlayerRejoined;

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
            SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnTick -= OnServerTick;
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            LocalRole = PlayerRole.None;
        }

        [Server]
        public PlayerRole GetRoleServer(NetworkConnection connection) =>
            connection != null && _roles.TryGetValue(connection, out PlayerRole role) ? role : PlayerRole.None;

        /// <summary>Partie de test : un joueur seul, sans minimum de joueurs, qui choisit son rôle. Même règle pour l'UI et le lancement.</summary>
        public static bool IsSoloTest(int activePlayerCount) => activePlayerCount == 1;

        [Server]
        public int CountActivePlayers()
        {
            int count = 0;
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                if (connection.IsActive)
                    count++;
            }

            return count;
        }

        // ================= Déroulé de la partie =================

        /// <summary>
        /// Démarre avec le préréglage correspondant au nombre de joueurs connectés.
        /// Seul, c'est une partie de test : le minimum de joueurs ne s'applique pas et soloRole est son rôle.
        /// </summary>
        [Server]
        public MatchStartResult StartMatch(PlayerRole soloRole = PlayerRole.Guardian)
        {
            if (_presets == null)
            {
                Debug.LogError("[MatchManager] Aucun MatchPresetTable assigné dans la map.");
                return MatchStartResult.InvalidSettings;
            }

            return StartMatch(_presets.GetFor(GetActivePlayers().Count), soloRole);
        }

        [Server]
        public MatchStartResult StartMatch(MatchSettings settings, PlayerRole soloRole = PlayerRole.Guardian)
        {
            if (_phase.Value != GamePhase.Lobby && _phase.Value != GamePhase.End)
                return MatchStartResult.WrongPhase;

            string error = "aucun MatchPresetTable";
            if (_presets == null || !settings.IsValid(out error))
            {
                Debug.LogError($"[MatchManager] Réglages invalides : {error}.");
                return MatchStartResult.InvalidSettings;
            }

            List<NetworkConnection> players = GetActivePlayers();
            bool isSolo = IsSoloTest(players.Count);
            if (players.Count < _presets.MinPlayers && !isSolo)
                return MatchStartResult.NotEnoughPlayers;

            foreach (NetworkConnection player in players)
            {
                if (!player.LoadedStartScenes(true))
                    return MatchStartResult.PlayersLoading;
            }

            Shuffle(players);
            _settings = settings;
            _roles.Clear();
            _neutralizedVillains.Clear();
            _addresses.Clear();
            _awayPlayers.Clear();

            // Seul : pas d'adversaire, la partie ne se gagne que par la dégradation. Sert à tester un rôle.
            // Sinon au moins un gentil : la partie serait gagnée d'avance par les méchants.
            int villainCount = isSolo
                ? (soloRole == PlayerRole.Villain ? 1 : 0)
                : Mathf.Clamp(settings.VillainCount, 1, players.Count - 1);
            for (int i = 0; i < players.Count; i++)
            {
                PlayerRole role = i < villainCount ? PlayerRole.Villain : PlayerRole.Guardian;
                _roles[players[i]] = role;
                _addresses[players[i]] = players[i].GetAddress();
                TargetAssignRole(players[i], role);
            }

            _degradationBase.Value = 0f;
            _degradationBaseTick.Value = TimeManager.Tick;
            _degradationPerSecond.Value = 0f;
            _searchingEndTick = 0;
            _winner.Value = Winner.None;
            EnterScouting();

            return MatchStartResult.Started;
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

            // La barre ne monte seule qu'en dernière chance : de sa valeur actuelle à 100 % pendant la durée du chrono.
            float degradationRate = phase == GamePhase.LastChance
                ? (1f - ComputeDegradation(TimeManager.Tick)) / _settings.LastChanceDuration
                : 0f;
            if (!Mathf.Approximately(degradationRate, _degradationPerSecond.Value))
                SetDegradationRate(degradationRate);

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

            if (_degradationPerSecond.Value > 0f && ComputeDegradation(TimeManager.Tick) >= 1f)
            {
                EndMatch(Winner.Villains);
                return;
            }

            // Pendant la chasse, _phaseEndTick vaut 0 (chrono caché) : son échéance est testée à part.
            if (phase == GamePhase.Searching && TimeManager.Tick >= _searchingEndTick)
            {
                SetPhase(GamePhase.LastChance, _settings.LastChanceDuration);
                return;
            }

            if (_phaseEndTick.Value == 0 || TimeManager.Tick < _phaseEndTick.Value)
                return;

            // TODO (Théo) : avec T07, placer automatiquement les objets que les fugitifs n'ont pas posés à temps.
            if (phase == GamePhase.Scouting)
                EnterSearching();
            // Filet de sécurité : la barre est recalée sur le chrono, mais un arrondi de tick peut la laisser juste sous 100 %.
            else if (phase == GamePhase.LastChance)
                EndMatch(Winner.Villains);
        }

        /// <summary>Seule entrée dans le repérage : T07 n'aura qu'un endroit à compléter.</summary>
        [Server]
        private void EnterScouting()
        {
            SetPhase(GamePhase.Scouting, _settings.ScoutingDuration);
        }

        /// <summary>Seule entrée dans la chasse : son timer reste côté serveur, aucun chrono n'est envoyé aux clients.</summary>
        [Server]
        private void EnterSearching()
        {
            _searchingEndTick = TimeManager.Tick + TimeManager.TimeToTicks(_settings.SearchingDuration, TickRounding.RoundUp);
            SetPhase(GamePhase.Searching, 0f);
        }

        /// <summary>Ajoute (ou retire si négatif) une part de barre : pose, destruction, tir sur un innocent. La montée continue repart de la nouvelle valeur.</summary>
        [Server]
        private void AddDegradation(float amount)
        {
            // N'a de sens qu'en jeu : hors manche, elle modifierait l'état de la partie suivante.
            if (_phase.Value != GamePhase.Scouting && _phase.Value != GamePhase.Searching && _phase.Value != GamePhase.LastChance)
                return;

            uint now = TimeManager.Tick;
            _degradationBase.Value = Mathf.Clamp01(ComputeDegradation(now) + amount);
            _degradationBaseTick.Value = now;

            // La barre doit rester calée sur le chrono : après un ajout ou un retrait, elle repart pour finir pile à 0:00.
            if (_phase.Value == GamePhase.LastChance)
                _degradationPerSecond.Value = (1f - _degradationBase.Value) / Mathf.Max(RemainingTime, (float)TimeManager.TickDelta);

            if (_degradationBase.Value >= 1f)
                EndMatch(Winner.Villains);
        }

        [Server]
        private void SetDegradationRate(float perSecond)
        {
            uint now = TimeManager.Tick;
            _degradationBase.Value = ComputeDegradation(now);
            _degradationBaseTick.Value = now;
            _degradationPerSecond.Value = perSecond;
        }

        private float ComputeDegradation(uint tick)
        {
            float degradation = _degradationBase.Value;
            uint baseTick = _degradationBaseTick.Value;

            if (_degradationPerSecond.Value > 0f && tick > baseTick)
                degradation += (float)TimeManager.TicksToTime(tick - baseTick) * _degradationPerSecond.Value;

            return Mathf.Min(1f, degradation);
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
                _addresses.Remove(connection);
                return;
            }

            // Gardé pour son retour : sans adresse (transport qui n'en donne pas), il ne pourra pas être reconnu.
            // Tugboat (partie locale) donne la même adresse à toutes les instances du PC : le premier qui revient prend le rôle.
            if (_addresses.Remove(connection, out string address) && !string.IsNullOrEmpty(address))
                _awayPlayers[address] = new AwayPlayer(connection, role, !_neutralizedVillains.Contains(connection));

            // TODO (Théo) : règle de déconnexion voulue (T18-6), à appliquer ici et au retour (OnClientLoadedStartScenes) :
            // - repérage et chasse : un joueur parti, chasseur ou fugitif, peut revenir ; son départ ne fait gagner personne ;
            // - dernière chance : s'il était le dernier de son camp encore connecté, l'autre camp gagne ; sinon il peut revenir.
            // Aujourd'hui un fugitif parti est neutralisé dans toutes les phases, et un chasseur ne fait perdre que s'il était le dernier.
            // Un joueur qui plante n'est vu parti qu'après le délai d'expiration du ServerManager (environ 1 min).
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

        // Après le chargement de la map par le client : son avatar peut apparaître tout de suite.
        private void OnClientLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            if (!asServer || _phase.Value == GamePhase.Lobby || _phase.Value == GamePhase.End)
                return;

            // Adresse donnée par le transport (SteamID avec Steam), jamais par le client : il ne peut pas prendre le rôle d'un autre.
            string address = connection.GetAddress();
            if (string.IsNullOrEmpty(address) || !_awayPlayers.Remove(address, out AwayPlayer away))
                return;

            // La nouvelle connexion remplace l'ancienne partout, pour que le décompte des méchants reste juste.
            _roles.Remove(away.Connection);
            bool wasNeutralized = _neutralizedVillains.Remove(away.Connection);
            _roles[connection] = away.Role;
            _addresses[connection] = address;
            if (wasNeutralized && !away.WasActive)
                _neutralizedVillains.Add(connection);

            TargetAssignRole(connection, away.Role);
            PlayerRejoined?.Invoke(connection);
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
