using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpawnPoints : MonoBehaviour
{
    [Tooltip("Jeśli puste, metoda CollectFromChildren() uzupełni automatycznie z dzieci tego obiektu")]
    public Transform[] points;

    // Możesz wywołać w edytorze: komponent -> trzy kropki -> Collect From Children
    [ContextMenu("Collect From Children")]
    public void CollectFromChildren()
    {
        var list = new List<Transform>();
        foreach (Transform t in transform)
        {
            // ignoruj helpery/empty o nazwie "__" jeśli chcesz
            list.Add(t);
        }
        points = list.ToArray();
    }

    public Transform[] GetPoints()
    {
        // Bezpieczeństwo: zwróć pustą tablicę jeśli null
        return points ?? new Transform[0];
    }
}