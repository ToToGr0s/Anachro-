using Anachronism.Gameplay;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anachronism.Network
{
    /// <summary>
    /// Objet tenu en main (T04-5), même composant et même synchro pour les PNJ et les fugitifs (règle réseau 5) :
    /// tenir un objet ne prouve rien. Une partie des PNJ tient un objet de l'époque (GameplayTuning.CarrierRatio) ;
    /// un fugitif tient l'anachronisme qu'il va poser (FugitivePlacer). Pas un secret : tout le monde voit l'objet.
    /// Seul l'id de l'objet voyage, chaque machine affiche le mesh.
    /// </summary>
    public sealed class HeldItem : NetworkBehaviour
    {
        [SerializeField] private AnachronismRegistry _registry;
        [SerializeField] private GameplayTuning _tuning;
        // TODO (Théo) : l'époque viendra de la carte choisie au lobby (T09), comme pour NetworkPlacement.
        [SerializeField] private EraDefinition _era;
        [Tooltip("Enfant placé à hauteur de main, devant le personnage. Il porte le MeshFilter et le MeshRenderer.")]
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _meshRenderer;
        [Tooltip("Fugitif seulement : l'objet tenu suit sa barre d'objets. Vide sur les PNJ.")]
        [SerializeField] private FugitivePlacer _placer;

        // 0 = rien, sinon id du registre + 1.
        private readonly SyncVar<byte> _itemId = new SyncVar<byte>();

        private void Awake()
        {
            _itemId.OnChange += OnItemIdChange;
        }

        private void OnDestroy()
        {
            _itemId.OnChange -= OnItemIdChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Avec le pool, l'instance peut resservir : elle repart les mains vides.
            _itemId.Value = 0;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Show(_itemId.Value);

            if (IsOwner && _placer != null)
            {
                _placer.HeldItemChanged += OnPlacerHeldItemChanged;
                OnPlacerHeldItemChanged(_placer.CurrentItem);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_placer != null)
                _placer.HeldItemChanged -= OnPlacerHeldItemChanged;
        }

        /// <summary>PNJ : appelé par NetworkNpc au spawn. Une partie d'entre eux tient un objet de l'époque de la map.</summary>
        [Server]
        public void PickRandomPeriodItemServer()
        {
            if (_tuning == null || _era == null || _era.SpawnedObjects.Count == 0 || Random.value >= _tuning.CarrierRatio)
                return;

            AnachronismDefinition definition = _era.SpawnedObjects[Random.Range(0, _era.SpawnedObjects.Count)];
            if (definition != null && _registry != null && _registry.TryGetId(definition, out byte id) && id < byte.MaxValue)
                _itemId.Value = (byte)(id + 1);
        }

        private void OnPlacerHeldItemChanged(AnachronismDefinition definition)
        {
            byte value = 0;
            if (definition != null && _registry != null && _registry.TryGetId(definition, out byte id) && id < byte.MaxValue)
                value = (byte)(id + 1);

            HoldServerRpc(value);
        }

        // Toute RPC client → serveur est hostile (règle réseau 2) : seul un fugitif en jeu tient un objet posable de l'époque.
        [ServerRpc]
        private void HoldServerRpc(byte value)
        {
            MatchManager match = MatchManager.Instance;
            if (value != 0 && (match == null || !match.IsActiveVillainServer(Owner) || !IsPlaceable((byte)(value - 1))))
            {
                RefusedRequests.Report("Objet tenu", Owner.ClientId, $"objet {value - 1} non autorisé", this);
                return;
            }

            _itemId.Value = value;
        }

        private bool IsPlaceable(byte id)
        {
            AnachronismDefinition definition = _registry != null ? _registry.Get(id) : null;
            if (definition == null || _era == null)
                return false;

            foreach (AnachronismDefinition placeable in _era.PlaceableObjects)
            {
                if (placeable == definition)
                    return true;
            }

            return false;
        }

        private void OnItemIdChange(byte previous, byte next, bool asServer)
        {
            // Sur l'hôte, OnChange passe côté serveur puis côté client : l'affichage ne se fait qu'une fois.
            if (asServer && IsClientInitialized)
                return;

            Show(next);
        }

        private void Show(byte value)
        {
            if (_meshFilter == null || _meshRenderer == null)
                return;

            AnachronismDefinition definition = value != 0 && _registry != null ? _registry.Get((byte)(value - 1)) : null;
            bool isVisible = definition != null && definition.Mesh != null;
            _meshRenderer.enabled = isVisible;
            if (!isVisible)
                return;

            _meshFilter.sharedMesh = definition.Mesh;
            _meshRenderer.sharedMaterial = definition.Material;
            // Le joueur local ne voit pas son propre objet (il cacherait la vue), seulement son ombre, comme son corps.
            _meshRenderer.shadowCastingMode = IsOwner ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;

            // Un gros objet tenu à taille réelle cacherait le personnage : on le réduit, centré dans la main.
            Bounds bounds = definition.Mesh.bounds;
            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = largest > 0f && _tuning != null ? Mathf.Min(1f, _tuning.HeldItemMaxSize / largest) : 1f;
            Transform item = _meshFilter.transform;
            item.localScale = Vector3.one * scale;
            item.localPosition = -bounds.center * scale;
        }
    }
}
