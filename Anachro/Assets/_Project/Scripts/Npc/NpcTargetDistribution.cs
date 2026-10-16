using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NpcDestinationManager : MonoBehaviour
{
    [Serializable]
    public class SpawnEntry
    {
        public NpcProfile profile;
        public int count = 5;
    }

    public static NpcDestinationManager Instance { get; private set; }

    [Header("Spawn")]
    [SerializeField] private List<SpawnEntry> spawnEntries = new();
    [SerializeField] private Transform npcParent;

    [Header("Destinations")]
    [SerializeField] private int maxAttempts = 15;

    private readonly List<NpcAgent> npcs = new();
    private NavMeshTriangulation tri;
    private float[] cumulativeAreas;
    private float totalArea;

    private void Awake() => Instance = this;

    private void Start()
    {
        BuildTriangleData();
        SpawnAll();
    }

    public void BuildTriangleData()
    {
        tri = NavMesh.CalculateTriangulation();
        int count = tri.indices.Length / 3;
        cumulativeAreas = new float[count];
        totalArea = 0f;

        for (int i = 0; i < count; i++)
        {
            Vector3 a = tri.vertices[tri.indices[i * 3]];
            Vector3 b = tri.vertices[tri.indices[i * 3 + 1]];
            Vector3 c = tri.vertices[tri.indices[i * 3 + 2]];
            totalArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            cumulativeAreas[i] = totalArea;
        }
    }

    // ---------- Spawn ----------

    private void SpawnAll()
    {
        if (totalArea <= 0f) return;

        foreach (SpawnEntry entry in spawnEntries)
            for (int i = 0; i < entry.count; i++)
                SpawnOne(entry.profile);
    }

    public NpcAgent SpawnOne(NpcProfile profile)
    {
        if (profile == null || profile.prefab == null) return null;

        Vector3 pos = RandomPointOnNavMesh();
        NpcAgent npc = Instantiate(profile.prefab, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), npcParent);
        npc.Agent.Warp(pos);
        npc.Init(profile);
        npcs.Add(npc);
        return npc;
    }

    public void Despawn(NpcAgent npc)
    {
        npcs.Remove(npc);
        Destroy(npc.gameObject);
    }

    // ---------- Boucle ----------

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < npcs.Count; i++)
        {
            NpcAgent npc = npcs[i];
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
        Vector2 range = npc.Profile.walkDistanceRange;
        Vector3 origin = npc.transform.position;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 candidate = RandomPointOnNavMesh();

            float dist = Vector3.Distance(origin, candidate);
            if (dist < range.x || dist > range.y) continue;

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
        float r = UnityEngine.Random.value * totalArea;
        int lo = 0, hi = cumulativeAreas.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (cumulativeAreas[mid] < r) lo = mid + 1; else hi = mid;
        }

        Vector3 a = tri.vertices[tri.indices[lo * 3]];
        Vector3 b = tri.vertices[tri.indices[lo * 3 + 1]];
        Vector3 c = tri.vertices[tri.indices[lo * 3 + 2]];

        float u = UnityEngine.Random.value, v = UnityEngine.Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        return a + u * (b - a) + v * (c - a);
    }
}