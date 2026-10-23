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

        /// <summary>Côté serveur : posé par un fugitif (pas généré avec la map). Caché aux chasseurs pendant le repérage (T05).</summary>
        public bool IsPlacedServer { get; private set; }

        /// <summary>Appelé par le serveur juste avant ServerManager.Spawn (l'objet n'est pas encore réseau).</summary>
        public void SetSpawnDefinition(AnachronismDefinition definition, bool isPlaced = false)
        {
            _pendingDefinition = definition;
            // Posé tout de suite : les observateurs sont calculés pendant le spawn.
            IsPlacedServer = isPlaced;
        }

        /// <summary>Vrai si les clients sauront afficher cet objet (présent dans le registre). À tester avant le spawn.</summary>
        public bool CanDisplay(AnachronismDefinition definition)
        {
            if (_registry == null)
            {
                Debug.LogError("[WorldObject] Aucun AnachronismRegistry assigné sur le prefab.", this);
                return false;
            }
            return _registry.TryGetId(definition, out _);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // L'appelant teste CanDisplay avant le spawn (ObjectSpawner, y compris pour les poses) : ce cas signale un oubli de ce test.
            if (!_registry.TryGetId(_pendingDefinition, out byte id))
            {
                Debug.LogError($"[WorldObject] {(_pendingDefinition != null ? _pendingDefinition.name : "null")} absent du registre : les clients afficheront un mauvais objet.", this);
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

        /// <summary>Appelé juste avant ServerManager.Despawn : Unity ne détruit l'objet qu'en fin de frame, et un nouveau
        /// tirage lancé dans la même frame (« Rejouer » depuis l'écran de fin) buterait sur son collider.</summary>
        public void DisableCollision()
        {
            _collider.enabled = false;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            // Un objet remis dans la réserve ne doit pas garder l'identité de sa vie précédente.
            Definition = null;
            IsPlacedServer = false;
        }

        private void Apply(AnachronismDefinition definition)
        {
            Definition = definition;
            ApplyDefinition(definition, _meshFilter, _meshRenderer, _collider, _obstacle, this);
        }

        /// <summary>
        /// Règle unique « définition -> composants du prefab ». Public et statique pour que les tests locaux (scène Sandbox,
        /// sans réseau) appliquent exactement la même règle que le jeu, sans la recopier.
        /// </summary>
        public static void ApplyDefinition(AnachronismDefinition definition, MeshFilter meshFilter, MeshRenderer meshRenderer,
            BoxCollider collider, NavMeshObstacle obstacle, Object context)
        {
            meshFilter.sharedMesh = definition.Mesh;
            meshRenderer.sharedMaterial = definition.Material;

            if (definition.Mesh == null)
            {
                Debug.LogError($"[WorldObject] {definition.name} n'a pas de mesh : collider et obstacle gardent la taille du prefab.", context);
                return;
            }

            // Boîte calée sur le mesh : suffit pour viser et poser. Réactivée ici pour un objet réutilisé (pooling).
            Bounds bounds = definition.Mesh.bounds;
            collider.enabled = true;
            collider.center = bounds.center;
            collider.size = bounds.size;
            obstacle.center = bounds.center;
            obstacle.size = bounds.size;
            // Un petit objet (bague, timbre) ne gêne pas les PNJ : le découper creuserait un trou inutile dans le NavMesh.
            obstacle.enabled = definition.Size != AnachronismSize.Small;
        }
    }
}
