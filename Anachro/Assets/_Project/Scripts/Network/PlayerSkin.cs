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
    [RequireComponent(typeof(NpcSkin))]
    public sealed class PlayerSkin : NetworkBehaviour
    {
        [SerializeField] private NpcSkinTable _skins;
        [SerializeField] private PlayerControllerSettings _settings;
        [Tooltip("NpcSkin du prefab Npc_v0 : le joueur reprend la hauteur et l'échelle réglées sur son enfant Model, pour qu'aucun réglage ne le distingue d'un PNJ.")]
        [SerializeField] private NpcSkin _npcReference;
        [Tooltip("Capsule provisoire, cachée dès qu'un skin est posé.")]
        [SerializeField] private Renderer _placeholderRenderer;

        private readonly SyncVar<byte> _skinIndex = new SyncVar<byte>();

        private NpcSkin _skin;
        private Vector3 _lastPosition;

        private void Awake()
        {
            _skin = GetComponent<NpcSkin>();
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
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _lastPosition = transform.position;
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
                return;

            NpcProfile skin = _skins.Get(_skinIndex.Value);
            if (skin == null)
                return;

            // Pivot du joueur au centre de la capsule, pieds du modèle au bas de la capsule ; puis le même réglage
            // (hauteur, échelle) que l'enfant Model des PNJ, sinon un joueur se repère à sa taille.
            Transform npcModelRoot = _npcReference != null ? _npcReference.ModelRoot : null;
            Vector3 npcOffset = npcModelRoot != null ? npcModelRoot.localPosition : Vector3.zero;
            Vector3 npcScale = npcModelRoot != null ? npcModelRoot.localScale : Vector3.one;

            // L'échelle de la racine du joueur (réglée à la main dans Player_Base) agit sur sa capsule et sa caméra,
            // jamais sur son apparence : on la retire du mesh, qui garde exactement la taille d'un PNJ.
            Vector3 rootScale = transform.lossyScale;
            Vector3 inverseRootScale = new Vector3(1f / rootScale.x, 1f / rootScale.y, 1f / rootScale.z);
            Vector3 offset = Vector3.down * (_settings.Height * 0.5f) + Vector3.Scale(npcOffset, inverseRootScale);
            _skin.Apply(skin, _settings.WalkSpeed, _settings.SprintMultiplier, offset, Vector3.Scale(npcScale, inverseRootScale));

            if (_placeholderRenderer != null)
                _placeholderRenderer.enabled = false;

            // Vue à la première personne : son propre corps remplirait l'écran (comme la capsule, cachée par MouseLook).
            if (IsOwner && _skin.Model != null)
            {
                foreach (Renderer modelRenderer in _skin.Model.GetComponentsInChildren<Renderer>(true))
                    modelRenderer.enabled = false;
            }
        }

        private void Update()
        {
            // L'owner ne voit pas son corps : inutile d'animer.
            if (!IsClientInitialized || IsOwner)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            _lastPosition = transform.position;
            _skin.SetSpeed(delta.magnitude / dt, dt);
        }
    }
}
