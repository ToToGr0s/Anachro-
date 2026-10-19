using System;
using Anachronism.Npc;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// PNJ en ligne : l'hôte fait tourner l'IA (NpcAgent + NavMeshAgent) et le NetworkTransform diffuse la position.
    /// Les clients n'ont que le visuel : modèle du profil et Animator piloté par la vitesse mesurée localement.
    /// </summary>
    [RequireComponent(typeof(NpcAgent))]
    public sealed class NetworkNpc : NetworkBehaviour
    {
        [Tooltip("Profils que ce prefab peut prendre. Seul l'index est envoyé aux clients.")]
        [SerializeField] private NpcProfile[] _profiles = new NpcProfile[0];

        // Rien de secret : c'est ce que tout le monde voit (modèle, allure).
        private readonly SyncVar<byte> _profileIndex = new SyncVar<byte>();
        private readonly SyncVar<float> _baseSpeed = new SyncVar<float>();

        private NpcAgent _agent;
        private NpcProfile _pendingProfile;
        private Vector3 _lastPosition;
        private bool _isVisualReady;

        private void Awake()
        {
            _agent = GetComponent<NpcAgent>();
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
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // L'hôte a déjà le PNJ complet (NpcAgent.Init côté serveur).
            if (IsServerInitialized)
                return;

            byte index = _profileIndex.Value;
            if (index >= _profiles.Length || _profiles[index] == null)
            {
                Debug.LogError($"[NetworkNpc] {name} : index de profil {index} invalide.", this);
                return;
            }

            _agent.InitVisual(_profiles[index], _baseSpeed.Value);
            _lastPosition = transform.position;
            _isVisualReady = true;
        }

        private void Update()
        {
            if (!_isVisualReady)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            _lastPosition = transform.position;
            _agent.UpdateAnimator(delta.magnitude / dt, dt);
        }
    }
}
