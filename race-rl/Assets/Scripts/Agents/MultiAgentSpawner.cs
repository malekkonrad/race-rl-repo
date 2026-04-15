using System.Collections.Generic;
using UnityEngine;

public class MultiAgentSpawner : MonoBehaviour
{
    [SerializeField] private GameObject agentPrefab;              
    [SerializeField] private TrackCheckpoints trackCheckpoints;
    [SerializeField] private Transform[] spawnPoints;             
    [SerializeField] private int agentsToSpawn = 3;               

    // NOWE: Referencja do kamery
    [Header("Camera Connection")]
    [SerializeField] private CarCamera carCamera; 

    private List<GameObject> spawned = new List<GameObject>();

    private void Start()
    {
        // Intentionally left blank
    }

    private void SpawnAgents()
    {
        ClearSpawned();
        int count = Mathf.Min(agentsToSpawn, spawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(agentPrefab, spawnPoints[i].position, spawnPoints[i].rotation);
            var agent = go.GetComponent<RacistAgent>();
            agent?.Init(trackCheckpoints, spawnPoints[i]);
            spawned.Add(go);
        }

        // NOWE: Po stworzeniu agentów, przekaż ich do kamery
        if (carCamera != null)
        {
            carCamera.SetTargets(spawned);
        }
    }

    private void ClearSpawned()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
    }

    public void RespawnAll()
    {
        SpawnAgents();
    }

    public void SetTrack(TrackCheckpoints tc, Transform[] spawns)
    {
        trackCheckpoints = tc;
        spawnPoints = spawns ?? new Transform[0];
    }
}