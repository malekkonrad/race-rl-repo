using UnityEngine;

public class CheckpointSingle : MonoBehaviour
{
    private TrackCheckpoints trackCheckpoints;

    private void OnTriggerEnter(Collider other)
    {
        // mozna tak a mozna tak
        // if (other.CompareTag("Player"))
        // {
        //     trackCheckpoints.AgentThroughCheckpoint(this, other.transform);
        // }

        // Szukamy agenta po rodzicach, bo collider zwykle jest na mesh/modelu
        var agent = other.GetComponentInParent<RacistAgent>();
        if (agent != null)
        {
            trackCheckpoints.AgentThroughCheckpoint(this, agent.transform);
        }
    }

    public void SetTrackCheckpoints(TrackCheckpoints trackCheckpoints)
    {
        this.trackCheckpoints = trackCheckpoints;
    }
}
