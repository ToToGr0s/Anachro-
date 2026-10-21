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
        [SerializeField] private WorldObject _prefab;
        [SerializeField] private ObjectSpawnZone[] _zones = new ObjectSpawnZone[0];

        // L'objet est posé un peu au-dessus de la surface pour le test de chevauchement : sinon il toucherait son propre support.
        private const float SurfaceClearance = 0.02f;

        private readonly List<WorldObject> _spawned = new List<WorldObject>();
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

        // PhaseChanged n'est levé qu'une fois sur l'hôte (côté client) : on revérifie qu'on est bien le serveur.
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
            if (_era == null || _settings == null || _prefab == null || _era.SpawnedObjects.Count == 0)
            {
                Debug.LogError("[ObjectSpawner] Époque, réglages, prefab ou objets de l'époque manquants : aucun objet généré.", this);
                return;
            }

            float totalArea = 0f;
            foreach (ObjectSpawnZone zone in _zones)
            {
                if (zone != null)
                    totalArea += zone.Area;
            }
            if (totalArea <= 0f)
            {
                Debug.LogError("[ObjectSpawner] Aucune zone de spawn : aucun objet généré.", this);
                return;
            }

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
                AnachronismDefinition definition = _era.SpawnedObjects[Random.Range(0, _era.SpawnedObjects.Count)];
                if (definition == null || definition.Mesh == null)
                    continue;

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

        // Plus une zone est grande, plus elle reçoit d'objets.
        private ObjectSpawnZone PickZone(float totalArea)
        {
            float pick = Random.Range(0f, totalArea);
            ObjectSpawnZone last = null;
            foreach (ObjectSpawnZone zone in _zones)
            {
                if (zone == null)
                    continue;
                last = zone;
                pick -= zone.Area;
                if (pick <= 0f)
                    return zone;
            }
            return last;
        }

        private bool TryFindPlace(ObjectSpawnZone zone, Bounds bounds, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Vector3 top = zone.RandomTopPoint();
            if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, zone.Height, _settings.SurfaceLayers, QueryTriggerInteraction.Ignore))
                return false;
            // Pas d'empilement : un objet ne se pose pas sur un autre objet généré.
            if (hit.collider.GetComponentInParent<WorldObject>() != null)
                return false;

            // Le bas du mesh sur la surface, quel que soit son pivot.
            position = hit.point - Vector3.up * bounds.min.y;
            if (!zone.ContainsFootprint(position + rotation * new Vector3(bounds.center.x, 0f, bounds.center.z), rotation,
                    new Vector2(bounds.extents.x, bounds.extents.z)))
                return false;

            float spacing = _settings.MinSpacing;
            Vector3 center = position + rotation * bounds.center + Vector3.up * SurfaceClearance;
            var halfExtents = new Vector3(bounds.extents.x + spacing, bounds.extents.y - SurfaceClearance, bounds.extents.z + spacing);
            return !Physics.CheckBox(center, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
        }

        [Server]
        private void DespawnAll()
        {
            // TODO (Olivier) : passer en DespawnType.Pool (et GetPooledInstantiated) si recréer les objets à chaque partie coûte trop.
            foreach (WorldObject worldObject in _spawned)
            {
                if (worldObject != null && worldObject.IsSpawned)
                    ServerManager.Despawn(worldObject.NetworkObject);
            }
            _spawned.Clear();
        }
    }
}
