using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Assign scene track GameObjects in order (index = curriculum track_index)")]
    public GameObject[] tracks; // przypisz obiekty torów z hierarchii
    public int startIndex = 0;
    public MultiAgentSpawner spawner;

    private int currentIndex = -1;

    private bool subscribedToReset = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        currentIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, tracks.Length - 1));
        ActivateTrack(currentIndex);
        if (Academy.IsInitialized)
        {
            Academy.Instance.OnEnvironmentReset += OnEnvironmentReset;
            Debug.Log("Sucess?");
        } 
        // Ensure we subscribe to Academy reset even if Academy initializes after Start()
        TrySubscribeToAcademy();
    }

    void OnDestroy()
    {
        if (Academy.IsInitialized) Academy.Instance.OnEnvironmentReset -= OnEnvironmentReset;
    
        if (subscribedToReset && Academy.IsInitialized)
            Academy.Instance.OnEnvironmentReset -= OnEnvironmentReset;
    }

    private void OnEnvironmentReset()
    {
        int idx = (int)Academy.Instance.EnvironmentParameters.GetWithDefault("track_index", (float)startIndex);
        idx = Mathf.Clamp(idx, 0, Mathf.Max(0, tracks.Length - 1));
        if (idx != currentIndex) ActivateTrack(idx);
        else
        {
            // nawet jeśli track się nie zmienia, spawner może potrzebować respawnu przy resetcie
            spawner?.RespawnAll();
        }
    }

    private void ActivateTrack(int idx)
    {
        if (idx < 0 || idx >= tracks.Length) return;
        // dezaktywuj inne tory
        for (int i = 0; i < tracks.Length; i++)
            tracks[i].SetActive(i == idx);

        currentIndex = idx;

        // znajdź TrackCheckpoints w aktywnym torze
        var tc = tracks[idx].GetComponentInChildren<TrackCheckpoints>();
        // zbierz spawn points: child named "SpawnPoints" lub wszystkie children z tagiem/komponentem
        Transform[] spawns = CollectSpawnPoints(tracks[idx]);

        // poinformuj spawner i respawn
        spawner?.SetTrack(tc, spawns);
        spawner?.RespawnAll();

        Debug.Log($"LevelManager: activated track {idx}");
    }

    private Transform[] CollectSpawnPoints(GameObject track)
    {
        // szuka child o nazwie "SpawnPoints" i zwraca jego dzieci jako punkty startowe
        var spParent = track.transform.Find("SpawnPoints");
        if (spParent != null)
        {
            var list = new List<Transform>();
            foreach (Transform t in spParent) list.Add(t);
            return list.ToArray();
        }
        // fallback: spróbuj znaleźć obiekty z komponentem StartPoint (jeśli dodasz)
        return new Transform[0];
    }

    private void TrySubscribeToAcademy()
    {
        if (subscribedToReset) return;
        if (Academy.IsInitialized)
        {
            Academy.Instance.OnEnvironmentReset += OnEnvironmentReset;
            subscribedToReset = true;
            return;
        }

        // Wait until Academy initializes (ML‑Agents may initialize slightly later)
        StartCoroutine(WaitForAcademyAndSubscribe());
    }

    private IEnumerator WaitForAcademyAndSubscribe()
    {
        while (!Academy.IsInitialized)
        {
            yield return null;
        }
        if (!subscribedToReset)
        {
            Academy.Instance.OnEnvironmentReset += OnEnvironmentReset;
            subscribedToReset = true;
        }
    }

    private float lastTrackIndexParam = 0;

    void Update()   
    {
        if (Academy.IsInitialized)
        {
            float currentParam = Academy.Instance.EnvironmentParameters.GetWithDefault("track_index", (float)startIndex);
            if (currentParam == 1f && lastTrackIndexParam == 0)
            {
                Debug.Log($"Detected track_index change: {lastTrackIndexParam} -> {currentParam}");
                lastTrackIndexParam = currentParam;

                // ręcznie resetuj środowisko
                // Academy.Instance.EnvironmentStep(); // opcjonalnie
                OnEnvironmentReset(); // ręcznie wywołaj swoją logikę
            }
            else if (currentParam - lastTrackIndexParam == 1 && lastTrackIndexParam != -1)
            {
                Debug.Log($"Detected!!!!!!!!! track_index change: {lastTrackIndexParam} -> {currentParam}");
                lastTrackIndexParam = currentParam;

                // ręcznie resetuj środowisko
                // Academy.Instance.EnvironmentStep(); // opcjonalnie
                OnEnvironmentReset(); // ręcznie wywołaj swoją logikę
            }
            // else
            // {
            //     Debug.Log("test change");
            // }
        }
    }

    public void FinishRaceForEveryone()
    {
        // Znajdź wszystkich aktywnych agentów
        var agents = FindObjectsByType<RacistAgent>(FindObjectsSortMode.None);
        
        foreach (var agent in agents)
        {            
            agent.EndEpisode(); // To wyśle dane do trenera i zresetuje agenta
        }
        
    }
}
