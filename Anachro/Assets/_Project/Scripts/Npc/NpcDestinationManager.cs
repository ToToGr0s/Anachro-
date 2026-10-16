using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Anachronism.Npc
{
    // Fait apparaître les PNJ et leur choisit des destinations au hasard sur le NavMesh (tirage pondéré par l'aire).
    // TODO (Théo) : passage en réseau (spawn et IA côté serveur, même synchro que le méchant).
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

        private void Start()
        {
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
            NpcAgent npc = Instantiate(profile.Prefab, position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), _npcParent);
            npc.Agent.Warp(position);
            npc.Init(profile);
            _npcs.Add(npc);
            return npc;
        }

        public void Despawn(NpcAgent npc)
        {
            _npcs.Remove(npc);
            Destroy(npc.gameObject);
        }

        // ---------- Boucle ----------

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < _npcs.Count; i++)
            {
                NpcAgent npc = _npcs[i];
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
