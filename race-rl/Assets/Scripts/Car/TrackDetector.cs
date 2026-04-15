using UnityEngine;

/// <summary>
/// Zostawiem bo przyda się potem myślę
/// </summary>

public class TrackDetector : MonoBehaviour
{

    /// <summary>
    /// gdzeiś dać ta metode czy w powietrzu
    /// </summary>

    public Transform[] wheels;
    public LayerMask roadLayer;

    public int WheelsOnRoad()
    {
        int wheelsOnRoad = 0;

        foreach (var wheel in wheels)
        {
            if (Physics.Raycast(wheel.position, Vector3.down, 1f, roadLayer))
            {
                wheelsOnRoad++;
            }
        }

        return wheelsOnRoad;
    }

    // obecnie nie używane bo są ściany (niewidoczne), ale tego też jest ciekawy
    // można zrobić porównanie
    void Update()
    {
        // if (WheelsOnRoad() == 0)
        // {
        //     Debug.Log("brak");
        //     // bool allWheelsOffRoad = true;
        // }
        // else
        // {
        //     Debug.Log("Koła na torze: " + WheelsOnRoad());
        //     // bool allWheelsOffRoad = false;
        // }
    }
}

