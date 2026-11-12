using Anachronism.Gameplay;
using FishNet.Object;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Réglages modifiés en direct par l'hôte depuis le panel F5 (builds de dev) : cadences, pénalités, distances,
    /// recharges… L'hôte envoie tout GameplayTuning aux joueurs ; chacun l'écrase dans son asset chargé.
    /// Valable jusqu'à la fin de la partie : à l'écran de fin et au lobby, tout le monde revient aux valeurs de l'asset.
    /// Sur l'objet du MatchManager (map). Les RPC existent dans tous les builds (même code réseau partout) ;
    /// seul le panel qui les déclenche est réservé aux builds de dev.
    /// </summary>
    [RequireComponent(typeof(MatchManager))]
    public sealed class LiveTuning : NetworkBehaviour
    {
        [SerializeField] private GameplayTuning _tuning;

        private MatchManager _match;
        // Valeurs de l'asset au chargement : rétablies en fin de partie, et en quittant (dans l'éditeur, un asset modifié
        // en Play le resterait sur le disque).
        private string _defaults;
        private bool _isModified;

        public GameplayTuning Tuning => _tuning;
        public bool IsModified => _isModified;

        private void Awake()
        {
            _match = GetComponent<MatchManager>();
            if (_tuning != null)
                _defaults = JsonUtility.ToJson(_tuning);
        }

        private void OnDestroy()
        {
            RestoreLocal();
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
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            // Déconnecté : les réglages de cet hôte ne valent plus.
            RestoreLocal();
        }

        /// <summary>Hôte : l'asset vient d'être modifié dans le panel, on envoie ses valeurs à tout le monde.</summary>
        [Server]
        public void BroadcastServer()
        {
            if (_tuning == null)
                return;

            _isModified = true;
            ObserversApply(JsonUtility.ToJson(_tuning));
        }

        /// <summary>Hôte : retour aux valeurs de l'asset pour tout le monde.</summary>
        [Server]
        public void ResetServer()
        {
            if (!_isModified)
                return;

            RestoreLocal();
            // Même RPC que les réglages : un seul message mis en tampon, le dernier reçu fait foi pour un joueur qui revient.
            ObserversApply(_defaults);
        }

        // Fin de partie (écran de fin, lobby) : les réglages de test ne débordent pas sur la suivante.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (IsServerInitialized && (phase == GamePhase.End || phase == GamePhase.Lobby))
                ResetServer();
        }

        // BufferLast : un joueur qui revient en pleine partie reçoit les derniers réglages.
        [ObserversRpc(BufferLast = true, ExcludeServer = true)]
        private void ObserversApply(string json)
        {
            if (_tuning == null)
                return;

            JsonUtility.FromJsonOverwrite(json, _tuning);
            _isModified = true;
        }

        private void RestoreLocal()
        {
            if (!_isModified || _tuning == null || string.IsNullOrEmpty(_defaults))
                return;

            JsonUtility.FromJsonOverwrite(_defaults, _tuning);
            _isModified = false;
        }
    }
}
