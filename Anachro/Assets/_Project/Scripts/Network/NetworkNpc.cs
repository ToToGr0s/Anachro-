using System;
using Anachronism.Npc;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// PNJ en ligne : l'hôte fait tourner l'IA (NpcAgent + NavMeshAgent) et le NetworkTransform diffuse la position.
    /// Les clients n'ont que le visuel : skin tiré par le serveur et Animator piloté par la vitesse mesurée localement.
    /// </summary>
    // Sur l'enfant « Network » du PNJ.
    public sealed class NetworkNpc : NetworkBehaviour
    {
        [Tooltip("NpcAgent de l'enfant « Controller ».")]
        [SerializeField] private NpcAgent _agent;
        [Tooltip("Profils de comportement que ce prefab peut prendre. Seul l'index est envoyé aux clients.")]
        [SerializeField] private NpcProfile[] _profiles = new NpcProfile[0];
        [SerializeField] private NpcSkinTable _skins;

        /// <summary>PNJ présents chez ce client (affichage debug).</summary>
        public static int ClientCount { get; private set; }

        // Rien de secret : c'est ce que tout le monde voit (apparence, allure).
        private readonly SyncVar<byte> _profileIndex = new SyncVar<byte>();
        private readonly SyncVar<byte> _skinIndex = new SyncVar<byte>();
        private readonly SyncVar<float> _baseSpeed = new SyncVar<float>();

        private NpcProfile _pendingProfile;
        private Vector3 _lastPosition;
        private bool _isRemoteVisual;
        private bool _isCounted;

        private void Awake()
        {
            _skinIndex.OnChange += OnSkinIndexChange;
        }

        private void OnDestroy()
        {
            _skinIndex.OnChange -= OnSkinIndexChange;
        }

        /// <summary>Appelé par le spawner serveur juste avant ServerManager.Spawn (le PNJ n'est pas encore réseau).</summary>
        public void SetSpawnProfile(NpcProfile profile)
        {
            _pendingProfile = profile;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            int index = Array.IndexOf(_profiles, _pendingProfile);
            if (index < 0 || index > byte.MaxValue)
            {
                Debug.LogError($"[NetworkNpc] {name} : profil {(_pendingProfile != null ? _pendingProfile.name : "null")} absent de la liste du prefab, les clients ne verront pas son modèle.", this);
                return;
            }

            // Posées dans OnStartServer : elles partent avec le message de spawn, le client les a dès OnStartClient.
            _profileIndex.Value = (byte)index;
            _baseSpeed.Value = _agent.BaseSpeed;
            RerollSkin();
        }

        /// <summary>Nouveau skin au hasard (réglages de l'hôte). Le comportement du PNJ ne change pas.</summary>
        [Server]
        public void RerollSkin()
        {
            if (_skins == null || _skins.Count == 0)
                return;

            _skinIndex.Value = (byte)Mathf.Min(_skins.RandomIndex(), byte.MaxValue);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ClientCount++;
            _isCounted = true;

            // L'hôte a déjà l'IA et le comportement (NpcAgent.Init côté serveur) : il ne lui manque que le skin tiré.
            if (!IsServerInitialized)
            {
                byte index = _profileIndex.Value;
                if (index >= _profiles.Length || _profiles[index] == null)
                {
                    Debug.LogError($"[NetworkNpc] {name} : index de profil {index} invalide.", this);
                    return;
                }

                _agent.InitVisual(_profiles[index], _baseSpeed.Value);
                _lastPosition = _agent.Body.position;
                _isRemoteVisual = true;
            }

            ApplySkin();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_isCounted)
                ClientCount--;
            _isCounted = false;
        }

        private void OnSkinIndexChange(byte previous, byte next, bool asServer)
        {
            // Côté client seulement (l'hôte reçoit aussi ce passage) ; le skin initial est posé dans OnStartClient.
            if (asServer || !IsClientInitialized)
                return;

            ApplySkin();
        }

        private void ApplySkin()
        {
            if (_skins == null || _agent.Profile == null)
                return;

            NpcProfile skin = _skins.Get(_skinIndex.Value);
            if (skin != null)
                _agent.SetSkin(skin);
        }

        private void Update()
        {
            if (!_isRemoteVisual)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            Vector3 position = _agent.Body.position;
            Vector3 delta = position - _lastPosition;
            delta.y = 0f;
            _lastPosition = position;
            _agent.UpdateAnimator(delta.magnitude / dt, dt);
        }

        // Domain reload désactivé dans ce projet : le compteur survivrait d'un Play à l'autre.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => ClientCount = 0;
    }
}
