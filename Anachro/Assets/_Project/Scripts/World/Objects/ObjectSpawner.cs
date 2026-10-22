using System.Collections.Generic;
using Anachronism.Gameplay;
using Anachronism.Network;
using FishNet.Object;
using UnityEngine;

namespace Anachronism.World
{
    /// <summary>
    /// Répartit au hasard les objets de l'époque de la map dans les zones, au début de chaque partie (côté serveur).
    /// Ils restent visibles sur l'écran de fin et disparaissent au retour au lobby ou au lancement suivant.
    /// Nouvelle map : poser le prefab ObjectSpawner et des prefabs ObjectSpawnZone, choisir l'époque et les réglages,
    /// puis clic droit sur le composant > « Référencer toutes les zones de la scène ».
    /// </summary>
    public sealed class ObjectSpawner : NetworkBehaviour
    {
        // TODO (Théo) : V0 = époque fixée dans la scène. À lire depuis la carte choisie au lobby (T09).
        [SerializeField] private EraDefinition _era;
        [SerializeField] private ObjectSpawnSettings _settings;
        // Seuil « sol » partagé avec la pose des fugitifs.
        [SerializeField] private GameplayTuning _tuning;
        [SerializeField] private WorldObject _prefab;
        [SerializeField] private ObjectSpawnZone[] _zones = new ObjectSpawnZone[0];

        // L'objet est posé un peu au-dessus de la surface pour le test de chevauchement : sinon il toucherait son propre support.
        private const float SurfaceClearance = 0.02f;

        private readonly List<WorldObject> _spawned = new List<WorldObject>();
        private readonly List<AnachronismDefinition> _valid = new List<AnachronismDefinition>();
        private MatchManager _match;

        public override void OnStartServer()
        {
            base.OnStartServer();
            // MatchManager est dans la même scène et se déclare dans Awake : pas de référence à régler sur chaque map.
            _match = MatchManager.Instance;
            if (_match == null)
            {
                Debug.LogError("[ObjectSpawner] Pas de MatchManager dans la scène : aucun objet ne sera généré.", this);
                return;
            }
            _match.PhaseChanged += OnPhaseChanged;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_match != null)
                _match.PhaseChanged -= OnPhaseChanged;
            _match = null;
            _spawned.Clear();
        }

        [ContextMenu("Référencer toutes les zones de la scène")]
        private void CollectZonesInScene()
        {
            var zones = new List<ObjectSpawnZone>();
            foreach (ObjectSpawnZone zone in FindObjectsByType<ObjectSpawnZone>(FindObjectsInactive.Include))
            {
                if (zone.gameObject.scene == gameObject.scene)
                    zones.Add(zone);
            }
            _zones = zones.ToArray();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[ObjectSpawner] {_zones.Length} zone(s) référencée(s).", this);
        }

        // Ne réagit que sur le serveur : l'hôte, qui est aussi client, reçoit l'event côté client.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (!IsServerInitialized)
                return;

            if (phase == GamePhase.Lobby)
                DespawnAll();
            else if (phase == GamePhase.Scouting)
            {
                // « Rejouer » peut relancer sans repasser par le lobby : on repart d'une map vide.
                DespawnAll();
                SpawnAll();
            }
        }

        [Server]
        private void SpawnAll()
        {
            if (_era == null || _settings == null || _tuning == null || _prefab == null || _era.SpawnedObjects.Count == 0)
            {
                Debug.LogError("[ObjectSpawner] Époque, réglages, GameplayTuning, prefab ou objets de l'époque manquants : aucun objet généré.", this);
                return;
            }

            float totalArea = 0f;
            foreach (ObjectSpawnZone zone in _zones)
            {
                if (IsUsable(zone))
                    totalArea += zone.Area;
            }
            if (totalArea <= 0f)
            {
                // Avertissement et pas erreur : désactiver les zones est la façon voulue de couper le spawn d'une map.
                Debug.LogWarning("[ObjectSpawner] Aucune zone de spawn active : aucun objet généré.", this);
                return;
            }

            // Tri une seule fois par partie : un objet mal configuré donne une erreur, pas une par tirage.
            CollectValidDefinitions();
            if (_valid.Count == 0)
            {
                Debug.LogError("[ObjectSpawner] Aucun objet valide pour cette époque : aucun objet généré.", this);
                return;
            }

            // TODO (Olivier) : avec T09, calculer le nombre selon la densité choisie par l'hôte au lobby (GDD 2.6)
            // et la surface des zones, la plage min / max servant de garde-fou.
            int target = Random.Range(_settings.MinCount, _settings.MaxCount + 1);
            for (int i = 0; i < target; i++)
                TrySpawnOne(totalArea);

            if (_spawned.Count < target)
                Debug.LogWarning($"[ObjectSpawner] {_spawned.Count} objets posés sur {target} visés : zones trop petites ou écart trop grand.", this);
            else
                Debug.Log($"[ObjectSpawner] {_spawned.Count} objets posés ({_era.DisplayName}).", this);
        }

        [Server]
        private bool TrySpawnOne(float totalArea)
        {
            for (int attempt = 0; attempt < _settings.AttemptsPerObject; attempt++)
            {
                ObjectSpawnZone zone = PickZone(totalArea);
                AnachronismDefinition definition = _valid[Random.Range(0, _valid.Count)];
                if (!TryFindPlace(zone, definition.Mesh.bounds, out Vector3 position, out Quaternion rotation))
                    continue;

                WorldObject worldObject = Instantiate(_prefab, position, rotation);
                worldObject.SetSpawnDefinition(definition);
                ServerManager.Spawn(worldObject.NetworkObject, null, gameObject.scene);
                _spawned.Add(worldObject);
                // Le collider vient d'être calé sur le mesh : le test de chevauchement suivant doit le voir.
                Physics.SyncTransforms();
                return true;
            }
            return false;
        }

        // Objets de l'époque que les clients sauront afficher : sans mesh ou hors registre, ils seraient vides ou faux.
        private void CollectValidDefinitions()
        {
            _valid.Clear();
            for (int i = 0; i < _era.SpawnedObjects.Count; i++)
            {
                AnachronismDefinition definition = _era.SpawnedObjects[i];
                if (definition == null)
                    Debug.LogError($"[ObjectSpawner] {_era.name} : case {i} vide, ignorée.", this);
                else if (definition.Mesh == null)
                    Debug.LogError($"[ObjectSpawner] {definition.name} n'a pas de mesh, ignoré.", this);
                else if (!_prefab.CanDisplay(definition))
                    Debug.LogError($"[ObjectSpawner] {definition.name} absent du registre des anachronismes, ignoré.", this);
                else
                    _valid.Add(definition);
            }
        }

        // Plus une zone est grande, plus elle reçoit d'objets.
        private ObjectSpawnZone PickZone(float totalArea)
        {
            float pick = Random.Range(0f, totalArea);
            ObjectSpawnZone last = null;
            foreach (ObjectSpawnZone zone in _zones)
            {
                // Une zone de surface nulle pourrait sortir quand le tirage vaut 0 : son point de départ serait dégénéré.
                if (!IsUsable(zone))
                    continue;
                last = zone;
                pick -= zone.Area;
                if (pick <= 0f)
                    return zone;
            }
            return last;
        }

        // Les zones restent référencées même désactivées (le menu contextuel inclut les inactives) :
        // une zone éteinte dans la hiérarchie ne doit plus recevoir d'objets.
        private static bool IsUsable(ObjectSpawnZone zone) => zone != null && zone.isActiveAndEnabled && zone.Area > 0f;

        private bool TryFindPlace(ObjectSpawnZone zone, Bounds bounds, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Vector3 top = zone.RandomTopPoint();
            if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, zone.Height, _settings.SurfaceLayers, QueryTriggerInteraction.Ignore))
                return false;
            // Seulement sur le décor : pas sur un objet déjà posé, un PNJ ou un joueur (tous des objets réseau),
            // ni sur une pente ou l'arête d'un mur.
            // Même règle « sol » que la pose des fugitifs.
            if (PlacementRules.ClassifySurface(hit.normal, _tuning) != PlacementSurfaceType.Floor
                || hit.collider.GetComponentInParent<NetworkObject>() != null)
                return false;

            // Le bas du mesh sur la surface, quel que soit son pivot.
            position = hit.point - Vector3.up * bounds.min.y;
            if (!zone.ContainsFootprint(position + rotation * new Vector3(bounds.center.x, 0f, bounds.center.z), rotation,
                    new Vector2(bounds.extents.x, bounds.extents.z)))
                return false;

            float spacing = _settings.MinSpacing;
            Vector3 center = position + rotation * bounds.center + Vector3.up * SurfaceClearance;
            // Mesh très fin (bague, timbre) : la demi-hauteur ne doit pas devenir négative une fois la marge retirée.
            var halfExtents = new Vector3(bounds.extents.x + spacing, Mathf.Max(0.001f, bounds.extents.y - SurfaceClearance), bounds.extents.z + spacing);
            return !Physics.CheckBox(center, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Objet posé par un fugitif (T07), déjà validé par NetworkPlacement. Suivi comme les objets de la map :
        /// il disparaît au retour au lobby ou au lancement suivant. Faux si les clients ne sauraient pas l'afficher.
        /// </summary>
        [Server]
        public bool SpawnPlaced(AnachronismDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (_prefab == null || !_prefab.CanDisplay(definition))
                return false;

            WorldObject worldObject = Instantiate(_prefab, position, rotation);
            worldObject.SetSpawnDefinition(definition);
            ServerManager.Spawn(worldObject.NetworkObject, null, gameObject.scene);
            _spawned.Add(worldObject);
            // La pose suivante, dans la même frame, doit voir son collider.
            Physics.SyncTransforms();
            return true;
        }

        [Server]
        private void DespawnAll()
        {
            // TODO (Olivier) : pooling FishNet (DespawnType.Pool + GetPooledInstantiated) une fois la pose des fugitifs
            // et le tir des chasseurs faits : ils créeront et détruiront aussi des WorldObject pendant la partie.
            foreach (WorldObject worldObject in _spawned)
            {
                if (worldObject != null && worldObject.IsSpawned)
                {
                    worldObject.DisableCollision();
                    ServerManager.Despawn(worldObject.NetworkObject);
                }
            }
            _spawned.Clear();
        }
    }
}
