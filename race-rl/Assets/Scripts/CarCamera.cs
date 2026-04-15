using UnityEngine;
using System.Collections.Generic;

public class CarCamera : MonoBehaviour
{
    [Header("Ustawienia")]
    public Vector3 offset = new Vector3(0, 3f, -6f);
    public float followSpeed = 10f;
    public float lookSpeed = 10f;

    // Prywatna lista celów (agentów)
    private List<Transform> targets = new List<Transform>();
    private int currentTargetIndex = 0;
    private Transform currentTarget;

    // Metoda, którą wywoła Spawner, żeby przekazać nowe agenty
    public void SetTargets(List<GameObject> agents)
    {
        targets.Clear();
        foreach (var agent in agents)
        {
            if(agent != null) targets.Add(agent.transform);
        }

        // Resetuj na pierwszego agenta
        if (targets.Count > 0)
        {
            currentTargetIndex = 0;
            currentTarget = targets[0];
        }
        else
        {
            currentTarget = null;
        }
    }

    void Update()
    {
        // Przełączanie kamerą spacją
        if (Input.GetKeyDown(KeyCode.Space) && targets.Count > 0)
        {
            currentTargetIndex = (currentTargetIndex + 1) % targets.Count;
            currentTarget = targets[currentTargetIndex];
        }
    }

    void LateUpdate()
    {
        // Jeśli nie ma celu (lub został zniszczony), nic nie rób
        if (currentTarget == null) return;

        // 1. Pozycja
        Vector3 targetPosition = currentTarget.TransformPoint(offset);
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);

        // 2. Obrót
        var direction = currentTarget.position - transform.position;
        if (direction != Vector3.zero)
        {
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, lookSpeed * Time.deltaTime);
        }
    }
}