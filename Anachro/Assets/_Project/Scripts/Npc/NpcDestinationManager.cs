using System;
using System.Collections.Generic;
using Anachronism.Network;
using FishNet;
using FishNet.Managing;
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

        private readonly List<NpcAgent> _npcs = new();
        private NavMeshTriangulation _triangulation;
        private float[] _cumulativeAreas;
        private float _totalArea;
        private NetworkManager _networkManager; // null hors ligne (scène de test lancée sans Boot)

        private bool IsOnline => _networkManager != null && _networkManager.IsServerStarted;

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager != null && _networkManager.IsClientStarted && !_networkManager.IsServerStarted)
            {
                enabled = false;
                return;
            }

            BuildTriangleData();
            SpawnAll();
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

            Vector3 position = RandomPointOnNavMesh();
            // En ligne, pas de parent : FishNet ne synchronise pas un parent qui n'est pas un objet réseau.
            Transform parent = IsOnline ? null : _npcParent;
            NpcAgent npc = Instantiate(profile.Prefab, position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), parent);
            // Désactivé dans le prefab : un client ne doit jamais déplacer un PNJ lui-même.
            npc.Agent.enabled = true;
            npc.Agent.Warp(position);
            npc.Init(profile);

            if (IsOnline)
            {
                if (npc.TryGetComponent(out NetworkNpc networkNpc))
                    networkNpc.SetSpawnProfile(profile);
                _networkManager.ServerManager.Spawn(npc.gameObject, null, gameObject.scene);
            }

            _npcs.Add(npc);
            return npc;
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
            SpawnAll();
        }

        /// <summary>Réglages de l'hôte : chaque PNJ reçoit un nouveau skin au hasard, sans changer de comportement.</summary>
        public void RerollSkins()
        {
            if (!IsOnline)
                return;

            foreach (NpcAgent npc in _npcs)
            {
                if (npc != null && npc.TryGetComponent(out NetworkNpc networkNpc))
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
                _networkManager.ServerManager.Despawn(npc.gameObject);
            else
                Destroy(npc.gameObject);
        }

        // ---------- Boucle ----------

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < _npcs.Count; i++)
            {
                NpcAgent npc = _npcs[i];
                // Détruit sans passer par Despawn (arrêt du serveur, déchargement de la map).
                if (npc == null)
                {
                    _npcs.RemoveAt(i--);
                    continue;
                }

                npc.Tick(dt);

                if (npc.NeedsDestination)
                    AssignNewDestination(npc);
            }
        }

        private void AssignNewDestination(NpcAgent npc)
        {
            if (TryGetDestination(npc, out Vector3 point))
                npc.BeginWalk(point);
            else
                npc.Wait(1f);   // réessaie dans 1 s
        }

        private bool TryGetDestination(NpcAgent npc, out Vector3 result)
        {
            NavMeshPath path = new NavMeshPath();
            Vector2 range = npc.Profile.WalkDistanceRange;
            Vector3 origin = npc.transform.position;

            for (int i = 0; i < _maxAttempts; i++)
            {
                Vector3 candidate = RandomPointOnNavMesh();

                float distance = Vector3.Distance(origin, candidate);
                if (distance < range.x || distance > range.y) continue;

                if (npc.Agent.CalculatePath(candidate, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    result = candidate;
                    return true;
                }
            }

            result = default;
            return false;
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
