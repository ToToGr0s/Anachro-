using System;
using System.Collections.Generic;
using Anachronism.Network;
using FishNet;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

namespace Anachronism.Npc
{
    // Fait apparaître les PNJ et leur choisit des destinations au hasard sur le NavMesh (tirage pondéré par l'aire).
    // En ligne, seul l'hôte fait apparaître les PNJ et pilote leur IA ; les clients les reçoivent par le réseau (NetworkNpc).
    public class NpcDestinationManager : MonoBehaviour
    {
        [Serializable]
        private class SpawnEntry
        {
            [SerializeField] private NpcProfile _profile;
            [SerializeField] private int _count = 5;

            public NpcProfile Profile => _profile;
            public int Count => _count;
        }

        [Header("Spawn")]
        [SerializeField] private List<SpawnEntry> _spawnEntries = new();
        [SerializeField] private Transform _npcParent;

        [Header("Destinations")]
        [SerializeField] private int _maxAttempts = 15;

        [Header("Regroupement")]
        [SerializeField] private NpcGroupSettings _groupSettings;

        private const float WaypointSampleRadius = 2f;
        private const float DestinationSampleRadius = 4f;
        private const float MaxDetourRatio = 1.3f;
        private const float MinProgressRatio = 0.5f;
        private const int MaxPathCorners = 64;

        private readonly List<NpcAgent> _npcs = new();
        private readonly List<NpcGroup> _groups = new();
        private NavMeshPath _path;
        private NavMeshPath _checkPath;
        private readonly Vector3[] _corners = new Vector3[MaxPathCorners];
        private readonly Vector3[] _checkCorners = new Vector3[MaxPathCorners];
        private NavMeshTriangulation _triangulation;
        private float[] _cumulativeAreas;
        private float _totalArea;
        private NetworkManager _networkManager; // null hors ligne (scène de test lancée sans Boot)

        private bool IsOnline => _networkManager != null && _networkManager.IsServerStarted;

        private void Start()
        {
            // Instances d'abord : InstanceFinder écrit un message dans la console quand il n'en trouve aucun (scène de test).
            _networkManager = NetworkManager.Instances.Count > 0 ? InstanceFinder.NetworkManager : null;
            if (_networkManager != null && _networkManager.IsClientStarted && !_networkManager.IsServerStarted)
            {
                enabled = false;
                return;
            }

            _path = new NavMeshPath();
            _checkPath = new NavMeshPath();
            BuildTriangleData();
            SpawnAll();

            if (_groupSettings == null)
                Debug.LogWarning("NpcDestinationManager : aucun NpcGroupSettings assigné, les PNJ ne se regroupent pas.", this);
        }

        public void BuildTriangleData()
        {
            _triangulation = NavMesh.CalculateTriangulation();
            int count = _triangulation.indices.Length / 3;
            _cumulativeAreas = new float[count];
            _totalArea = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 a = _triangulation.vertices[_triangulation.indices[i * 3]];
                Vector3 b = _triangulation.vertices[_triangulation.indices[i * 3 + 1]];
                Vector3 c = _triangulation.vertices[_triangulation.indices[i * 3 + 2]];
                _totalArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                _cumulativeAreas[i] = _totalArea;
            }
        }

        // ---------- Spawn ----------

        private void SpawnAll()
        {
            if (_totalArea <= 0f)
            {
                Debug.LogWarning("NpcDestinationManager : aucun NavMesh dans la scène, aucun PNJ créé.", this);
                return;
            }

            foreach (SpawnEntry entry in _spawnEntries)
                for (int i = 0; i < entry.Count; i++)
                    SpawnOne(entry.Profile);
        }

        public NpcAgent SpawnOne(NpcProfile profile)
        {
            if (profile == null || profile.Prefab == null) return null;
            if (profile.PlayerMovement == null)
            {
                Debug.LogError($"NpcDestinationManager : {profile.name} n'a pas de PlayerControllerSettings, PNJ non créé.", profile);
                return null;
            }

            Vector3 position = RandomPointOnNavMesh();
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            // En ligne, pas de parent : FishNet ne synchronise pas un parent qui n'est pas un objet réseau.
            // Le profil pointe le NpcAgent de l'enfant « Controller » : on instancie tout le prefab (sa racine).
            GameObject prefabRoot = profile.Prefab.transform.root.gameObject;
            NpcAgent npc = IsOnline
                ? Instantiate(prefabRoot, position, rotation).GetComponentInChildren<NpcAgent>()
                : InstantiateOffline(prefabRoot, position, rotation);
            // Désactivé dans le prefab : un client ne doit jamais déplacer un PNJ lui-même.
            npc.Agent.enabled = true;
            npc.Agent.Warp(position);
            npc.Init(profile);

            if (IsOnline)
            {
                NetworkNpc networkNpc = npc.Body.GetComponentInChildren<NetworkNpc>();
                if (networkNpc != null)
                    networkNpc.SetSpawnProfile(profile);
                _networkManager.ServerManager.Spawn(npc.Body.gameObject, null, gameObject.scene);
            }

            _npcs.Add(npc);
            return npc;
        }

        // Hors ligne (scène de test lancée sans Boot) : FishNet signale en erreur puis désactive tout NetworkObject
        // qui n'est pas spawné. On instancie sous un parent inactif (aucun Awake ne tourne) et on retire les composants
        // réseau avant d'activer le PNJ.
        private NpcAgent InstantiateOffline(GameObject prefabRoot, Vector3 position, Quaternion rotation)
        {
            GameObject holder = new GameObject("NpcOfflineSpawn");
            holder.SetActive(false);

            GameObject instance = Instantiate(prefabRoot, position, rotation, holder.transform);
            foreach (NetworkBehaviour networkBehaviour in instance.GetComponentsInChildren<NetworkBehaviour>(true))
                DestroyImmediate(networkBehaviour);
            if (instance.TryGetComponent(out NetworkObject networkObject))
                DestroyImmediate(networkObject);

            instance.transform.SetParent(_npcParent, true);
            Destroy(holder);
            return instance.GetComponentInChildren<NpcAgent>();
        }

        /// <summary>Réglages de l'hôte : retire tous les PNJ et en refait apparaître de nouveaux (positions, skins).</summary>
        public void RespawnAll()
        {
            if (!CanCommandNpcs())
                return;

            for (int i = _npcs.Count - 1; i >= 0; i--)
            {
                if (_npcs[i] != null)
                    Despawn(_npcs[i]);
            }

            _npcs.Clear();
            _groups.Clear();
            SpawnAll();
        }

        /// <summary>Réglages de l'hôte : chaque PNJ reçoit un nouveau skin au hasard, sans changer de comportement.</summary>
        public void RerollSkins()
        {
            if (!IsOnline)
                return;

            foreach (NpcAgent npc in _npcs)
            {
                NetworkNpc networkNpc = npc != null ? npc.Body.GetComponentInChildren<NetworkNpc>() : null;
                if (networkNpc != null)
                    networkNpc.RerollSkin();
            }
        }

        /// <summary>Point au hasard sur le NavMesh, tiré comme les PNJ (sert aussi à faire apparaître les joueurs).</summary>
        public bool TryGetRandomPoint(out Vector3 point)
        {
            if (_totalArea <= 0f)
            {
                point = default;
                return false;
            }

            point = RandomPointOnNavMesh();
            return true;
        }

        // Désactivé chez un client pur (Start) : seuls l'hôte et la scène de test hors ligne pilotent les PNJ.
        private bool CanCommandNpcs() => enabled && _totalArea > 0f;

        public void Despawn(NpcAgent npc)
        {
            _npcs.Remove(npc);
            if (IsOnline)
                _networkManager.ServerManager.Despawn(npc.Body.gameObject);
            else
                Destroy(npc.Body.gameObject);
        }

        // ---------- Boucle ----------

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < _npcs.Count; i++)
            {
                NpcAgent npc = _npcs[i];
                if (npc == null)
                {
                    _npcs.RemoveAt(i--);
                    continue;
                }

                npc.Tick(dt);

                if (npc.NeedsDestination)
                    AssignNewDestination(npc);
                else if (npc.NeedsNextWaypoint)
                    AssignNextWaypoint(npc);
            }

            UpdateGroups();
        }

        private void UpdateGroups()
        {
            if (_groupSettings == null)
                return;

            UpdateFormations();
            FindNewGroups();
        }

        private void UpdateFormations()
        {
            float catchUpSqr = _groupSettings.CatchUpDistance * _groupSettings.CatchUpDistance;
            for (int g = _groups.Count - 1; g >= 0; g--)
            {
                NpcGroup group = _groups[g];
                if (!group.IsValid)
                {
                    Dissolve(group);
                    continue;
                }

                if (group.Leader.IsLeaderAtGroupPoint)
                {
                    Arrive(group);
                    continue;
                }

                for (int i = 1; i < group.Count; i++)
                {
                    NpcAgent follower = group.Members[i];
                    Vector3 slot = GetSlotPosition(group, i);
                    Vector3 offset = slot - follower.Body.position;
                    offset.y = 0f;
                    follower.FollowSlot(slot, offset.sqrMagnitude > catchUpSqr);
                }
            }
        }

        private void FindNewGroups()
        {
            float awarenessSqr = _groupSettings.AwarenessRadius * _groupSettings.AwarenessRadius;
            float zoneOverlap = _groupSettings.DestinationZoneRadius * 2f;
            float zoneOverlapSqr = zoneOverlap * zoneOverlap;

            for (int i = 0; i < _npcs.Count; i++)
            {
                NpcAgent first = _npcs[i];
                if (!first.CanFormGroup && !first.IsInGroup)
                    continue;

                for (int j = i + 1; j < _npcs.Count; j++)
                {
                    NpcAgent second = _npcs[j];
                    if (!first.CanFormGroup && !second.CanFormGroup)
                        continue;
                    if ((first.Body.position - second.Body.position).sqrMagnitude > awarenessSqr)
                        continue;

                    if (first.CanFormGroup && second.CanFormGroup)
                    {
                        if ((first.Destination - second.Destination).sqrMagnitude <= zoneOverlapSqr)
                            TryCreateGroup(first, second);
                    }
                    else if (first.CanFormGroup && second.IsInGroup)
                        TryJoinGroup(first, second.Group, zoneOverlapSqr);
                    else if (second.CanFormGroup && first.IsInGroup)
                        TryJoinGroup(second, first.Group, zoneOverlapSqr);
                }
            }
        }

        private void TryCreateGroup(NpcAgent first, NpcAgent second)
        {
            if (!RollJoin(first, second))
                return;

            NpcAgent leader = UnityEngine.Random.value < 0.5f ? first : second;
            NpcAgent follower = leader == first ? second : first;
            Vector3 commonPoint = GetCommonPoint(leader, (first.Destination + second.Destination) * 0.5f);

            NpcGroup group = new NpcGroup(leader, commonPoint, UnityEngine.Random.value < _groupSettings.DiscussChance);
            group.Add(follower);
            leader.LeadGroup(group);
            follower.FollowGroup(group);
            _groups.Add(group);
        }

        private void TryJoinGroup(NpcAgent npc, NpcGroup group, float zoneOverlapSqr)
        {
            if (group == null || !group.IsValid || group.Count >= _groupSettings.MaxGroupSize)
                return;
            if ((npc.Destination - group.CommonPoint).sqrMagnitude > zoneOverlapSqr)
                return;
            if (!RollJoin(npc, null))
                return;

            group.Add(npc);
            npc.FollowGroup(group);
        }

        private bool RollJoin(NpcAgent first, NpcAgent second)
        {
            if (UnityEngine.Random.value < _groupSettings.JoinChance)
                return true;

            first.DelayNextGroupAttempt(_groupSettings.RetryDelay);
            if (second != null)
                second.DelayNextGroupAttempt(_groupSettings.RetryDelay);
            return false;
        }

        private Vector3 GetCommonPoint(NpcAgent leader, Vector3 midpoint)
        {
            if (NavMesh.SamplePosition(midpoint, out NavMeshHit hit, DestinationSampleRadius, NavMesh.AllAreas)
                && leader.Agent.CalculatePath(hit.position, _path) && _path.status == NavMeshPathStatus.PathComplete)
                return hit.position;

            return leader.Destination;
        }

        private Vector3 GetSlotPosition(NpcGroup group, int slotIndex)
        {
            Transform leader = group.Leader.Body;
            int rank = (slotIndex + 1) / 2;
            float side = slotIndex % 2 == 1 ? 1f : -1f;
            Vector3 right = leader.right;
            right.y = 0f;

            Vector3 slot = leader.position + right.normalized * (side * rank * _groupSettings.FormationSpacing);
            return NavMesh.SamplePosition(slot, out NavMeshHit hit, WaypointSampleRadius, NavMesh.AllAreas) ? hit.position : leader.position;
        }

        private void Arrive(NpcGroup group)
        {
            _groups.Remove(group);

            if (!group.WillDiscuss)
            {
                for (int i = group.Count - 1; i >= 0; i--)
                    group.Members[i].ContinueAlone();
                return;
            }

            Vector3 centre = group.CommonPoint;
            float radius = _groupSettings.FormationSpacing * 0.5f * Mathf.Max(1, group.Count - 1);
            float startAngle = UnityEngine.Random.Range(0f, 360f);
            float duration = UnityEngine.Random.Range(_groupSettings.DiscussDurationRange.x, _groupSettings.DiscussDurationRange.y);

            for (int i = group.Count - 1; i >= 0; i--)
            {
                float angle = startAngle + i * 360f / group.Count;
                Vector3 spot = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                if (NavMesh.SamplePosition(spot, out NavMeshHit hit, WaypointSampleRadius, NavMesh.AllAreas))
                    spot = hit.position;

                group.Members[i].StartDiscussion(spot, centre, duration);
            }
        }

        private void Dissolve(NpcGroup group)
        {
            _groups.Remove(group);
            for (int i = group.Count - 1; i >= 0; i--)
            {
                if (group.Members[i] != null)
                    group.Members[i].ContinueAlone();
            }
        }

        private void AssignNewDestination(NpcAgent npc)
        {
            if (TryGetDestination(npc, out Vector3 destination))
            {
                bool isFinal = GetNextWaypoint(npc, destination, out Vector3 waypoint);
                npc.BeginWalk(destination, waypoint, isFinal);
            }
            else
                npc.Wait(1f);   // réessaie dans 1 s
        }

        private void AssignNextWaypoint(NpcAgent npc)
        {
            bool isFinal = GetNextWaypoint(npc, npc.Destination, out Vector3 waypoint);
            npc.SetWaypoint(waypoint, isFinal);
        }

        private bool GetNextWaypoint(NpcAgent npc, Vector3 destination, out Vector3 waypoint)
        {
            waypoint = destination;
            if (!npc.Agent.CalculatePath(destination, _path) || _path.status != NavMeshPathStatus.PathComplete)
                return true;

            int cornerCount = _path.GetCornersNonAlloc(_corners);
            float remaining = PathLength(_corners, cornerCount);
            NpcProfile profile = npc.Profile;
            if (remaining <= profile.WaypointSpacingRange.y)
                return true;

            Vector3 origin = npc.Body.position;
            Vector3 onPath = destination;
            for (int i = 0; i < _maxAttempts; i++)
            {
                float spacing = UnityEngine.Random.Range(profile.WaypointSpacingRange.x, profile.WaypointSpacingRange.y);
                onPath = PointAlongPath(_corners, cornerCount, spacing);
                float angle = UnityEngine.Random.Range(-profile.WaypointMaxAngle, profile.WaypointMaxAngle);
                Vector3 candidate = origin + Quaternion.Euler(0f, angle, 0f) * (onPath - origin);

                if (TryGetProgressingWaypoint(origin, candidate, destination, remaining - spacing * MinProgressRatio, out waypoint))
                    return false;
            }

            waypoint = onPath;
            return false;
        }

        private bool TryGetProgressingWaypoint(Vector3 origin, Vector3 candidate, Vector3 destination, float maxRemaining, out Vector3 waypoint)
        {
            waypoint = default;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, WaypointSampleRadius, NavMesh.AllAreas))
                return false;

            float directDistance = Vector3.Distance(origin, hit.position);
            if (!TryGetPathLength(origin, hit.position, out float lengthToWaypoint) || lengthToWaypoint > directDistance * MaxDetourRatio + 0.5f)
                return false;
            if (!TryGetPathLength(hit.position, destination, out float lengthAfter) || lengthAfter > maxRemaining)
                return false;

            waypoint = hit.position;
            return true;
        }

        private bool TryGetPathLength(Vector3 from, Vector3 to, out float length)
        {
            length = 0f;
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _checkPath) || _checkPath.status != NavMeshPathStatus.PathComplete)
                return false;

            length = PathLength(_checkCorners, _checkPath.GetCornersNonAlloc(_checkCorners));
            return true;
        }

        private static float PathLength(Vector3[] corners, int count)
        {
            float length = 0f;
            for (int i = 1; i < count; i++)
                length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        private static Vector3 PointAlongPath(Vector3[] corners, int count, float distance)
        {
            for (int i = 1; i < count; i++)
            {
                float segment = Vector3.Distance(corners[i - 1], corners[i]);
                if (distance <= segment)
                    return Vector3.Lerp(corners[i - 1], corners[i], segment > 0f ? distance / segment : 0f);
                distance -= segment;
            }

            return corners[count - 1];
        }

        private bool TryGetDestination(NpcAgent npc, out Vector3 result)
        {
            Vector2 range = npc.Profile.WalkDistanceRange;
            Vector3 origin = npc.Body.position;
            Vector3 farthest = default;
            float farthestDistance = 0f;

            for (int i = 0; i < _maxAttempts; i++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                if (direction == Vector2.zero) continue;

                float distance = UnityEngine.Random.Range(range.x, range.y);
                Vector3 candidate = origin + new Vector3(direction.x, 0f, direction.y) * distance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, DestinationSampleRadius, NavMesh.AllAreas)) continue;
                if (!npc.Agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete) continue;

                float reached = Vector3.Distance(origin, hit.position);
                if (reached >= range.x)
                {
                    result = hit.position;
                    return true;
                }

                if (reached > farthestDistance)
                {
                    farthestDistance = reached;
                    farthest = hit.position;
                }
            }

            result = farthest;
            return farthestDistance >= npc.Profile.WaypointSpacingRange.x;
        }

        private Vector3 RandomPointOnNavMesh()
        {
            float r = UnityEngine.Random.value * _totalArea;
            int lo = 0, hi = _cumulativeAreas.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_cumulativeAreas[mid] < r) lo = mid + 1; else hi = mid;
            }

            Vector3 a = _triangulation.vertices[_triangulation.indices[lo * 3]];
            Vector3 b = _triangulation.vertices[_triangulation.indices[lo * 3 + 1]];
            Vector3 c = _triangulation.vertices[_triangulation.indices[lo * 3 + 2]];

            float u = UnityEngine.Random.value, v = UnityEngine.Random.value;
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            return a + u * (b - a) + v * (c - a);
        }
    }
}
