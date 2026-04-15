using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; 

public class TrackCheckpoints : MonoBehaviour
{
    private List<Transform> carTransformList = new List<Transform>();
    private List<int> nextCheckpointIndexList = new List<int>();
    private List<CheckpointSingle> checkpointSingleList = new List<CheckpointSingle>();
    
    private readonly Dictionary<Transform, int> carIndexLookup = new Dictionary<Transform, int>();

    public event EventHandler<CarCheckpointEventArgs> OnCarCorrectCheckpoint;
    public event EventHandler<CarCheckpointEventArgs> OnCarWrongCheckpoint;

    public event EventHandler<CarCheckpointEventArgs> OnCarLapCompleted;

    
    private class CarProgress
    {
        public Transform transform;
        public int laps;
        public int currentCheckpointIndex;
        public float distanceToNext;
    }



    public class CarCheckpointEventArgs : EventArgs
    {
        public Transform carTransform;
    }


    private void Awake()
    {
        Transform checkpointsTransform = transform.Find("Checkpoints");

        foreach (Transform checkpointSingleTransform in checkpointsTransform)
        {
            CheckpointSingle checkpointSingle = checkpointSingleTransform.GetComponent<CheckpointSingle>();
            checkpointSingle.SetTrackCheckpoints(this);
            checkpointSingleList.Add(checkpointSingle);
        }
    }

    public void RegisterCar(Transform carTransform)
    {
        if (carTransform == null) return;
        if (carIndexLookup.ContainsKey(carTransform)) return;

        carTransformList.Add(carTransform);
        int idx = carTransformList.Count - 1;
        carIndexLookup[carTransform] = idx;
        nextCheckpointIndexList.Add(0);
    }

    public void UnregisterCar(Transform carTransform)
    {
        if (carTransform == null) return;
        if (!carIndexLookup.TryGetValue(carTransform, out int index)) return;

        carTransformList.RemoveAt(index);
        nextCheckpointIndexList.RemoveAt(index);
        carIndexLookup.Remove(carTransform);

        // Przemapuj indeksy po wycięciu
        for (int i = index; i < carTransformList.Count; i++)
        {
            carIndexLookup[carTransformList[i]] = i;
        }
    }





    public void AgentThroughCheckpoint(CheckpointSingle checkpointSingle, Transform carTransform)
    {
        if (!carIndexLookup.TryGetValue(carTransform, out int idx))
        {
            // Opcjonalnie: auto-rejestracja jeśli zapomniano
            RegisterCar(carTransform);
            if (!carIndexLookup.TryGetValue(carTransform, out idx)) return;
        }


        int nextCheckpointIndex = nextCheckpointIndexList[carTransformList.IndexOf(carTransform)];
        int checkpointIndex = checkpointSingleList.IndexOf(checkpointSingle);


        if (checkpointSingleList.IndexOf(checkpointSingle) == nextCheckpointIndex)
        {
            // czy kończymy okrążenie?
            bool lapCompleted = (nextCheckpointIndex == checkpointSingleList.Count - 1);


            // Debug.Log("Correct checkpoint");
            nextCheckpointIndexList[carTransformList.IndexOf(carTransform)] = (nextCheckpointIndex + 1) % checkpointSingleList.Count;
            OnCarCorrectCheckpoint?.Invoke(this, new CarCheckpointEventArgs { carTransform = carTransform });

            if (lapCompleted)
            {
                OnCarLapCompleted?.Invoke(this, new CarCheckpointEventArgs {carTransform = carTransform});
            }
        }
        else
        {
            // Debug.Log("Wrong checkpoint");
            OnCarWrongCheckpoint?.Invoke(this, new CarCheckpointEventArgs { carTransform = carTransform });
        }
    }

    public CheckpointSingle GetNexCheckpoint(Transform carTransform)
    {
        int carIndex = carTransformList.IndexOf(carTransform);
        if (carIndex == -1) return checkpointSingleList[0];
        
        return checkpointSingleList[nextCheckpointIndexList[carIndex]];
    }

    public void ResetCheckpoint(Transform carTransform)
    {
        int carIndex = carTransformList.IndexOf(carTransform);
        if (carIndex != -1)
        {
            nextCheckpointIndexList[carIndex] = 0;
        }
    }


    public int GetCarRank(Transform carTransform)
    {
        // 1. Zbieramy dane o wszystkich autach
        List<CarProgress> progressList = new List<CarProgress>();

        for (int i = 0; i < carTransformList.Count; i++)
        {
            Transform car = carTransformList[i];
            int nextCPIndex = nextCheckpointIndexList[i];
            
            // Obliczamy dystans do następnego checkpointa (im mniej tym lepiej)
            CheckpointSingle nextCP = checkpointSingleList[nextCPIndex];
            float dist = Vector3.Distance(car.position, nextCP.transform.position);

            // Obecny (zaliczony) checkpoint to next - 1
            // Ale musimy obsłużyć przypadek gdy next = 0 (start)
            int currentCP = nextCPIndex - 1;
            if (currentCP < 0) currentCP = checkpointSingleList.Count - 1;

            // Tutaj musiałbyś jakoś przechowywać liczbę okrążeń w TrackCheckpoints 
            // lub pobierać ją od agenta. Dla uproszczenia przyjmijmy, 
            // że sortujemy głownie po checkpoincie, a w przypadku równości po dystansie.
            // W idealnym świecie RacistAgent powinien raportować swoje okrążenie do TrackCheckpoints.
            
            // UWAGA: Aby to działało idealnie, przenieś licznik 'currentLap' z Agenta tutaj 
            // lub zrób publiczną metodę w Agencie GetLaps(). 
            // Poniżej wersja uproszczona (zakłada to samo okrążenie, co przy starcie równoległym wystarczy):

            progressList.Add(new CarProgress {
                transform = car,
                laps = 0, // Tu wstaw realne okrążenia jeśli agenty się dublują
                currentCheckpointIndex = currentCP, // lub nextCPIndex jako "cel"
                distanceToNext = dist
            });
        }

        // 2. Sortujemy: Najpierw kto ma wyższy checkpoint, potem kto jest bliżej następnego
        // Używamy nextCheckpointIndexList jako wskaźnika postępu
        
        var sortedCars = carTransformList
            .Select((t, index) => new { 
                Transform = t, 
                Index = nextCheckpointIndexList[index], 
                Dist = Vector3.Distance(t.position, checkpointSingleList[nextCheckpointIndexList[index]].transform.position) 
            })
            .OrderByDescending(x => x.Index) // Kto ma dalszy checkpoint (zakładając brak dublowania)
            .ThenBy(x => x.Dist) // Kto jest bliżej celu (mniejszy dystans)
            .ToList();

        // Znajdź pozycję (Rank 1 = index 0)
        for (int i = 0; i < sortedCars.Count; i++)
        {
            if (sortedCars[i].Transform == carTransform)
                return i + 1; // Pozycja 1, 2, 3...
        }

        return -1;
    }

}
