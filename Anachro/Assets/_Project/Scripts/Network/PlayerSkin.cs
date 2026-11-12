using Anachronism.Gameplay;
using Anachronism.Npc;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Habille le joueur d'un skin de PNJ tiré par le serveur. Vu des autres, il s'anime comme un PNJ :
    /// même NpcSkin, piloté par la vitesse mesurée sur le transform synchronisé (règle réseau 5).
    /// </summary>
    // Sur l'enfant « Network » du joueur ; le mesh va sous l'enfant « Model » (son Y règle la hauteur du mesh).
    public sealed class PlayerSkin : NetworkBehaviour
    {
        [SerializeField] private NpcSkinTable _skins;
        [SerializeField] private PlayerControllerSettings _settings;
        [Tooltip("NpcSkin de l'enfant « Model ».")]
        [SerializeField] private NpcSkin _skin;
        [Tooltip("Teinte de la stase (C06) : fugitif capturé, figé sur place.")]
        [SerializeField] private Color _stasisTint = new Color(0.45f, 0.8f, 1f, 1f);

        // URP Lit (_BaseColor) et anciens shaders (_Color) : la teinte passe par un MaterialPropertyBlock, sans copier les matériaux.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Mêmes données publiques que NetworkNpc (index de skin + vitesse de base) : rien de secret, c'est ce que tout le monde voit.
        private readonly SyncVar<byte> _skinIndex = new SyncVar<byte>();
        private readonly SyncVar<float> _baseSpeed = new SyncVar<float>();

        private Vector3 _lastPosition;
        private bool _isFrozen;
        private MaterialPropertyBlock _stasisBlock;

        /// <summary>
        /// Stase du fugitif capturé (C06), vue par tous : pose figée et teinte. L'owner, passé sur la caméra de la scène,
        /// voit alors son propre corps.
        /// </summary>
        public void SetFrozen(bool isFrozen)
        {
            _isFrozen = isFrozen;
            ApplyFrozen();
        }

        private void Awake()
        {
            _skinIndex.OnChange += OnSkinIndexChange;
        }

        private void OnDestroy()
        {
            _skinIndex.OnChange -= OnSkinIndexChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Tiré par le serveur : un client ne choisit pas son apparence (il pourrait prendre la moins visible).
            if (_skins != null && _skins.Count > 0)
                _skinIndex.Value = (byte)Mathf.Min(_skins.RandomIndex(), byte.MaxValue);

            // Même vitesse de base que les PNJ (NpcProfile.WalkSpeed) : l'Animator passe marche/course aux mêmes seuils.
            if (_settings != null)
                _baseSpeed.Value = _settings.WalkSpeed;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _lastPosition = NetworkObject.transform.position;
            ApplySkin();
        }

        private void OnSkinIndexChange(byte previous, byte next, bool asServer)
        {
            if (asServer || !IsClientInitialized)
                return;

            ApplySkin();
        }

        private void ApplySkin()
        {
            if (_skins == null || _settings == null)
            {
                Debug.LogError($"[PlayerSkin] {name} : table de skins ou PlayerControllerSettings manquant, le joueur reste sans modèle.", this);
                return;
            }

            NpcProfile skin = _skins.Get(_skinIndex.Value);
            if (skin == null)
                return;

            _skin.Apply(skin, _baseSpeed.Value, _settings.SprintMultiplier);

            // Vue à la première personne : son propre corps remplirait l'écran (comme la capsule, cachée par MouseLook).
            if (IsOwner && _skin.Model != null)
            {
                foreach (Renderer modelRenderer in _skin.Model.GetComponentsInChildren<Renderer>(true))
                    modelRenderer.enabled = false;
            }

            // Nouveau modèle : la stase en cours doit s'y appliquer.
            ApplyFrozen();
        }

        private void ApplyFrozen()
        {
            GameObject model = _skin != null ? _skin.Model : null;
            if (model == null)
                return;

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.speed = _isFrozen ? 0f : _skin.AppliedSkin.AnimPlaybackSpeed;

            if (_stasisBlock == null)
            {
                _stasisBlock = new MaterialPropertyBlock();
                _stasisBlock.SetColor(BaseColorId, _stasisTint);
                _stasisBlock.SetColor(ColorId, _stasisTint);
            }

            foreach (Renderer modelRenderer in model.GetComponentsInChildren<Renderer>(true))
            {
                modelRenderer.SetPropertyBlock(_isFrozen ? _stasisBlock : null);
                if (IsOwner)
                    modelRenderer.enabled = _isFrozen;
            }
        }

        private void Update()
        {
            // L'owner ne voit pas son corps : inutile d'animer. Figé : la pose ne bouge plus.
            if (!IsClientInitialized || IsOwner || _isFrozen)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            Vector3 position = NetworkObject.transform.position;
            Vector3 delta = position - _lastPosition;
            delta.y = 0f;
            _lastPosition = position;
            _skin.SetSpeed(delta.magnitude / dt, dt);
        }
    }
}
