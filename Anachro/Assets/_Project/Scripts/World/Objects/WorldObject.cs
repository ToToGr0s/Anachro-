using Anachronism.Gameplay;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;

namespace Anachronism.World
{
    /// <summary>
    /// Prefab réseau commun à tous les objets (générés avec la map ou posés par un fugitif) : seul l'id de
    /// l'AnachronismDefinition voyage, chaque machine applique le mesh, le matériau et le collider.
    /// Prêt pour le pooling FishNet : tout est réappliqué à chaque spawn, rien ne reste d'une vie à l'autre.
    /// </summary>
    public sealed class WorldObject : NetworkBehaviour
    {
        [SerializeField] private AnachronismRegistry _registry;
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private BoxCollider _collider;
        // Découpe le NavMesh : les PNJ contournent l'objet. « Carve only stationary » : calculé une fois, l'objet ne bouge pas.
        [SerializeField] private NavMeshObstacle _obstacle;

        // Pas un secret : le mesh montre déjà au joueur de quel objet il s'agit, donc son époque.
        // « Posé par un fugitif » ou « généré avec la map » reste côté serveur.
        private readonly SyncVar<byte> _definitionId = new SyncVar<byte>();

        private AnachronismDefinition _pendingDefinition;

        /// <summary>Objet affiché (null tant que l'objet n'est pas réseau ou si l'id est invalide).</summary>
        public AnachronismDefinition Definition { get; private set; }

        /// <summary>Appelé par le serveur juste avant ServerManager.Spawn (l'objet n'est pas encore réseau).</summary>
        public void SetSpawnDefinition(AnachronismDefinition definition)
        {
            _pendingDefinition = definition;
        }

        /// <summary>Vrai si les clients sauront afficher cet objet (présent dans le registre). À tester avant le spawn.</summary>
        public bool CanDisplay(AnachronismDefinition definition) => _registry.TryGetId(definition, out _);

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (!_registry.TryGetId(_pendingDefinition, out byte id))
            {
                // Sans id valide, les clients afficheraient l'objet 0 du registre : on retire l'objet plutôt que de le montrer faux.
                Debug.LogError($"[WorldObject] {(_pendingDefinition != null ? _pendingDefinition.name : "null")} absent du registre : objet retiré.", this);
                ServerManager.Despawn(NetworkObject);
                return;
            }

            // Posé dans OnStartServer : l'id part avec le message de spawn, le client l'a dès OnStartClient.
            _definitionId.Value = id;
            // Le serveur applique aussi : il a besoin du collider pour valider les tirs, même sans affichage.
            Apply(_pendingDefinition);
            _pendingDefinition = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // L'hôte a déjà appliqué côté serveur.
            if (IsServerInitialized)
                return;

            AnachronismDefinition definition = _registry.Get(_definitionId.Value);
            if (definition == null)
            {
                Debug.LogError($"[WorldObject] id {_definitionId.Value} absent du registre : build différent de l'hôte ?", this);
                return;
            }
            Apply(definition);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            // Un objet remis dans la réserve ne doit pas garder l'identité de sa vie précédente.
            Definition = null;
        }

        private void Apply(AnachronismDefinition definition)
        {
            Definition = definition;
            _meshFilter.sharedMesh = definition.Mesh;
            _meshRenderer.sharedMaterial = definition.Material;

            if (definition.Mesh == null)
            {
                Debug.LogError($"[WorldObject] {definition.name} n'a pas de mesh : collider et obstacle gardent la taille du prefab.", this);
                return;
            }

            // Boîte calée sur le mesh : suffit pour viser et poser.
            Bounds bounds = definition.Mesh.bounds;
            _collider.center = bounds.center;
            _collider.size = bounds.size;
            _obstacle.center = bounds.center;
            _obstacle.size = bounds.size;
        }
    }
}
