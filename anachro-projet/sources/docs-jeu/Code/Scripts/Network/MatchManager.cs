using System;
using System.Collections.Generic;
using System.Linq;
using Anachronism.Gameplay;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace Anachronism.Network
{
    /// Objet réseau de scène. Seul l'hôte décide : phases, rôles, tirs, victoire.
    /// Les SyncVar ne contiennent RIEN de secret (ni rôles, ni quels objets sont des anachronismes).
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("Tir (validation serveur)")]
        [SerializeField] private float _maxShootDistance = 30f;
        [SerializeField] private float _shootCooldown = 0.75f;
        [SerializeField] private float _maxOriginOffset = 3f;   // origine du tir vs position serveur du tireur
        [SerializeField] private float _hitTolerance = 1.2f;    // distance max entre le rayon et la cible déclarée
        [SerializeField] private float _targetCenterHeight = 1f;
        [SerializeField] private LayerMask _staticPropMask;     // objets d'époque non réseau

        // ---------- État public synchronisé ----------
        private readonly SyncVar<GamePhase> _phase = new SyncVar<GamePhase>();
        private readonly SyncVar<uint> _phaseEndTick = new SyncVar<uint>();   // 0 = pas de chrono
        private readonly SyncVar<float> _degradation = new SyncVar<float>();  // 0..1
        private readonly SyncVar<int> _ammo = new SyncVar<int>();             // munitions partagées des gentils
        private readonly SyncVar<int> _objectsRemaining = new SyncVar<int>();
        private readonly SyncVar<Winner> _winner = new SyncVar<Winner>();

        public GamePhase Phase => _phase.Value;
        public float Degradation => _degradation.Value;
        public int Ammo => _ammo.Value;
        public int ObjectsRemaining => _objectsRemaining.Value;
        public Winner Winner => _winner.Value;

        /// Rôle du joueur local (reçu par TargetRpc, jamais diffusé aux autres).
        public PlayerRole LocalRole { get; private set; }

        public event Action<GamePhase> PhaseChanged;          // client + serveur
        public event Action<PlayerRole> LocalRoleAssigned;    // client
        public event Action<NetworkConnection> VillainCaptured; // serveur : figer / téléporter le joueur
        public event Action PortalOpened;                     // serveur : spawn du portail à un point aléatoire

        // ---------- État serveur uniquement ----------
        private readonly Dictionary<NetworkConnection, PlayerRole> _roles = new Dictionary<NetworkConnection, PlayerRole>();
        private readonly HashSet<NetworkConnection> _captured = new HashSet<NetworkConnection>();
        private readonly HashSet<NetworkConnection> _escaped = new HashSet<NetworkConnection>();
        private readonly Dictionary<NetworkConnection, float> _lastShot = new Dictionary<NetworkConnection, float>();
        private MatchSettings _settings;

        private void Awake()
        {
            Instance = this;
            _phase.OnChange += (prev, next, asServer) => PhaseChanged?.Invoke(next);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            TimeManager.OnTick += ServerTick;
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnTick -= ServerTick;
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        /// Temps restant de la phase, valable côté client comme serveur.
        public float RemainingTime
        {
            get
            {
                uint end = _phaseEndTick.Value, now = TimeManager.Tick;
                return end == 0 || end <= now ? 0f : (float)TimeManager.TicksToTime(end - now);
            }
        }

        [Server]
        public PlayerRole GetRoleServer(NetworkConnection conn) =>
            conn != null && _roles.TryGetValue(conn, out var r) ? r : PlayerRole.None;

        // ================= Déroulé de la partie =================

        [Server]
        public void StartMatch(MatchSettings settings)
        {
            if (_phase.Value != GamePhase.Lobby && _phase.Value != GamePhase.End) return;

            var players = ServerManager.Clients.Values.Where(c => c.IsActive)
                                       .OrderBy(_ => UnityEngine.Random.value).ToList();
            if (players.Count < 2) return;

            _settings = settings;
            int villains = Mathf.Clamp(settings.VillainCount, 1, players.Count - 1);
            _roles.Clear(); _captured.Clear(); _escaped.Clear(); _lastShot.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                var role = i < villains ? PlayerRole.Villain : PlayerRole.Guardian;
                _roles[players[i]] = role;
                TargetAssignRole(players[i], role);
            }

            _ammo.Value = 0;
            _degradation.Value = 0f;
            _objectsRemaining.Value = 0;
            _winner.Value = Winner.None;
            SetPhase(GamePhase.Briefing, settings.BriefingDuration);
        }

        [TargetRpc]
        private void TargetAssignRole(NetworkConnection conn, PlayerRole role)
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
            // Réévalue les ObserverCondition (ex : méchants/objets invisibles aux gentils pendant la cache).
            ServerManager.Objects.RebuildObservers();
        }

        private void ServerTick()
        {
            var p = _phase.Value;
            if (p == GamePhase.Lobby || p == GamePhase.End) return;

            if (p == GamePhase.Searching || p == GamePhase.Portal)
            {
                AddDegradation((float)TimeManager.TickDelta / _settings.DegradationDuration);
                if (_phase.Value == GamePhase.End) return;
            }

            if (_phaseEndTick.Value != 0 && TimeManager.Tick >= _phaseEndTick.Value)
            {
                if (p == GamePhase.Briefing) SetPhase(GamePhase.Hiding, _settings.HidingDuration);
                // TODO : auto-placer les objets que les méchants n'ont pas posés à temps.
                else if (p == GamePhase.Hiding) SetPhase(GamePhase.Searching, 0f);
            }
        }

        [Server]
        private void AddDegradation(float amount)
        {
            _degradation.Value = Mathf.Min(1f, _degradation.Value + amount);
            if (_degradation.Value >= 1f) EndMatch(Winner.Villains);
        }

        [Server]
        private void EndMatch(Winner w)
        {
            if (_phase.Value == GamePhase.End) return;
            _winner.Value = w;
            SetPhase(GamePhase.End, 0f);
        }

        // ================= Anachronismes =================

        [Server]
        public void RegisterAnachronism() => _objectsRemaining.Value++;

        [Server]
        private void OpenPortal()
        {
            SetPhase(GamePhase.Portal, 0f);
            PortalOpened?.Invoke();
        }

        // ================= Tir =================

        /// Le client envoie son rayon + la cible réseau qu'il pense toucher (null si aucune).
        /// Le serveur valide tout. NB : la tolérance sur la cible déclarée est trichable
        /// (aimbot) — acceptable pour un party game entre amis, pas pour du compétitif.
        [ServerRpc(RequireOwnership = false)]
        public void ShootServerRpc(Vector3 origin, Vector3 direction, NetworkObject claimedTarget,
                                   NetworkConnection caller = null)
        {
            if (GetRoleServer(caller) != PlayerRole.Guardian) return;
            if (_phase.Value != GamePhase.Searching && _phase.Value != GamePhase.Portal) return;

            float now = Time.time;
            if (_lastShot.TryGetValue(caller, out float last) && now - last < _shootCooldown) return;
            _lastShot[caller] = now;

            var shooter = caller.FirstObject;
            if (shooter == null || Vector3.Distance(shooter.transform.position, origin) > _maxOriginOffset) return;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();

            if (claimedTarget != null)
            {
                if (IsOnRay(origin, direction, claimedTarget.transform.position + Vector3.up * _targetCenterHeight))
                    ResolveHit(claimedTarget);
            }
            else if (Physics.Raycast(origin, direction, _maxShootDistance, _staticPropMask))
            {
                // Objet d'époque innocent : pas de munition consommée, mais pénalité.
                AddDegradation(_settings.WrongShotPenalty);
            }
        }

        private bool IsOnRay(Vector3 origin, Vector3 dir, Vector3 point)
        {
            float t = Vector3.Dot(point - origin, dir);
            if (t < 0f || t > _maxShootDistance) return false;
            return Vector3.Distance(origin + dir * t, point) <= _hitTolerance;
        }

        [Server]
        private void ResolveHit(NetworkObject target)
        {
            // 1) Anachronisme : gratuit, rapporte une munition.
            if (target.TryGetComponent(out PlacedAnachronism anachronism))
            {
                if (anachronism.Found) return;
                anachronism.MarkFound();
                _ammo.Value++;
                _objectsRemaining.Value--;
                if (_objectsRemaining.Value <= 0 && _phase.Value == GamePhase.Searching) OpenPortal();
                return;
            }

            // Tir sur un autre gentil (skin distinct) : ignoré.
            var owner = target.Owner;
            if (owner.IsValid && GetRoleServer(owner) == PlayerRole.Guardian) return;

            // 2) PNJ ou méchant : la munition est vérifiée et consommée AVANT de savoir qui c'est.
            //    Sinon, à 0 munition, "tir refusé" vs "tir accepté" révélerait le méchant.
            if (_ammo.Value <= 0) return;
            _ammo.Value--;

            if (owner.IsValid && GetRoleServer(owner) == PlayerRole.Villain) Capture(owner);
            else AddDegradation(_settings.WrongShotPenalty);
        }

        // ================= Capture / portail =================

        [Server]
        private void Capture(NetworkConnection villain)
        {
            if (_captured.Contains(villain) || _escaped.Contains(villain)) return;
            _captured.Add(villain);
            VillainCaptured?.Invoke(villain);
            CheckVillainsResolved();
        }

        /// À appeler par le trigger du portail (côté serveur).
        [Server]
        public void NotifyEnteredPortal(NetworkConnection conn)
        {
            if (_phase.Value != GamePhase.Portal) return;
            if (GetRoleServer(conn) != PlayerRole.Villain || _captured.Contains(conn)) return;
            _escaped.Add(conn);
            CheckVillainsResolved();
        }

        /// Règle actuelle (à confirmer) : les gentils gagnent s'ils capturent TOUS les méchants ;
        /// un seul méchant qui s'échappe suffit aux méchants.
        [Server]
        private void CheckVillainsResolved()
        {
            int villains = _roles.Count(kv => kv.Value == PlayerRole.Villain);
            if (_captured.Count + _escaped.Count < villains) return;
            EndMatch(_escaped.Count > 0 ? Winner.Villains : Winner.Guardians);
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;
            if (GetRoleServer(conn) == PlayerRole.Villain) Capture(conn); // méchant déconnecté = neutralisé
            _roles.Remove(conn);
            // TODO : si plus aucun gentil, fin de partie.
        }
    }
}
