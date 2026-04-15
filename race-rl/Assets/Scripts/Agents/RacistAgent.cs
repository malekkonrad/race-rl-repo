using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.VisualScripting;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(DecisionRequester))]
public class RacistAgent : Agent
{

    [SerializeField] private TrackCheckpoints trackCheckpoints;
    [SerializeField] private Transform spawnPosition;

    // dodatkowe pola żeby zapobiegać przewracaniu się - w teorii teraz już nie powinno być z tym problemu ale myślę 
    // że jak się dołoży kilku agentów i zderzenia to może być różnie
    [SerializeField] private float flippedEndDelay = 0.75f;        // ile sekund warunek ma trwać 
    // [SerializeField, Range(-1f, 1f)] private float upsideDownDotThreshold = -0.2f; // < 0 znaczy "głową w dół"
    private float flippedTimer = 0f;

    private SimpleCar carDriver;

    [SerializeField] public int lapsPerEpisode = 10;
    private int currentLap;

    [SerializeField] private bool ignoreAgentCollisions = true;


    // Rewards Section:
    private const float WallCollisionPenalty = -1.0f;
    private const float AgentCollisionEnterPenalty = -.5f;
    private const float AgentCollisionStayPenalty = -.02f;
    

    [SerializeField] private float spawnGracePeriod = 0.2f;
    private float lastSpawnTime = -10f;




    [SerializeField] private Rigidbody rb;              // NOWE
    [SerializeField] private float speedRewardScale = 0.001f;  // NOWE
    [SerializeField] private float positionRewardScale = 0.002f;


    private int currentRank = 0;


    public void Init(TrackCheckpoints checkpoints, Transform spawn)
    {
        trackCheckpoints = checkpoints;
        spawnPosition = spawn;

        // auto register
        // trackCheckpoints?.RegisterCar(transform);

        // auto register
        // trackCheckpoints?.RegisterCar(transform);
        // If already registered to another TrackCheckpoints, unregister first
        if (trackCheckpoints != null)
        {
            trackCheckpoints.OnCarCorrectCheckpoint -= TrackCheckpoints_OnCarCorrectCheckpoint;
            trackCheckpoints.OnCarWrongCheckpoint -= TrackCheckpoints_OnWrongCorrectCheckpoint;
            trackCheckpoints.OnCarLapCompleted -= TrackCheckpoints_OnCarLapCompleted;
            trackCheckpoints.UnregisterCar(transform);
        }

        trackCheckpoints = checkpoints;
        spawnPosition = spawn;

        // Register and subscribe to events on the new TrackCheckpoints (if any)
        if (trackCheckpoints != null)
        {
            trackCheckpoints.RegisterCar(transform);
            trackCheckpoints.OnCarCorrectCheckpoint += TrackCheckpoints_OnCarCorrectCheckpoint;
            trackCheckpoints.OnCarWrongCheckpoint += TrackCheckpoints_OnWrongCorrectCheckpoint;
            trackCheckpoints.OnCarLapCompleted += TrackCheckpoints_OnCarLapCompleted;
        }
    }



    protected override void Awake()
    {
        base.Awake();
        carDriver = GetComponent<SimpleCar>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }
        


    private void TrackCheckpoints_OnCarCorrectCheckpoint(object sender, TrackCheckpoints.CarCheckpointEventArgs e)
    {
        if (e.carTransform == transform)
        {
            AddReward(0.5f);
        }
    }

    private void TrackCheckpoints_OnWrongCorrectCheckpoint(object sender, TrackCheckpoints.CarCheckpointEventArgs e)
    {
        if (e.carTransform == transform)
        {
            AddReward(-0.3f);
        }
    }

    private void TrackCheckpoints_OnCarLapCompleted(object sender, TrackCheckpoints.CarCheckpointEventArgs e) {
        if (e.carTransform == transform)
        {
            currentLap++;
            Debug.Log("Lap "+ currentLap + "/" + lapsPerEpisode);
            AddReward(5.0f);                // nagroda za całe okrążenie

            if (currentLap >= lapsPerEpisode)
            {
                AddReward(2.0f);
                
                // EndEpisode();       // kończymy epizod
                LevelManager.Instance.FinishRaceForEveryone();
            }
        }
        
    }


    public override void OnEpisodeBegin()
    {
        if (spawnPosition == null)
        {
            Debug.LogWarning($"{name}: spawnPosition not set in OnEpisodeBegin — using current transform as fallback.");
            spawnPosition = transform;
        }
        carDriver?.StopCompletely();

        transform.position = spawnPosition.position; 
        transform.forward = spawnPosition.forward;

        trackCheckpoints?.ResetCheckpoint(transform);
        
        
        // Zmienne
        currentLap = 0;
        flippedTimer = 0f;


        currentRank = 100;

        // curriculum na liczbę okrążeń (opcjonalne, patrz YAML poniżej)
        // var envParams = Academy.Instance.EnvironmentParameters;
        // int lapsFromEnv = (int)envParams.GetWithDefault("laps_per_episode", lapsPerEpisode);
        // lapsPerEpisode = Mathf.Max(1, lapsFromEnv);

    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) Kierunek względem checkpointa
        var next = trackCheckpoints?.GetNexCheckpoint(transform);
        float directionDot = 0f;
        if (next != null)
        {
            Vector3 checkpointForward = next.transform.forward;
            directionDot = Vector3.Dot(transform.forward, checkpointForward);
        }
        sensor.AddObservation(directionDot);


        // 2) predkość 
        if (rb != null)
        {
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

            // forward speed (z) i boczna (x), znormalizowane do [-1, 1]
            float maxSpeed = 50f; // oszacuj maksymalną sensowną prędkość na swoim torze
            sensor.AddObservation(Mathf.Clamp(localVel.z / maxSpeed, -1f, 1f)); // do przodu/tyłu
            sensor.AddObservation(Mathf.Clamp(localVel.x / maxSpeed, -1f, 1f)); // ślizg bokiem
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }



    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Continuous actions: [0] = throttle (-1..1), [1] = steer (-1..1)
        float forwardAmount = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turnAmount = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        carDriver.SetInputs(forwardAmount, turnAmount);


        // --- NAGRODY ZA WYPRZEDZANIE ---
        if (trackCheckpoints != null)
        {
            // 1. Pobierz aktualną pozycję (1 = lider)
            int newRank = trackCheckpoints.GetCarRank(transform);

            // Inicjalizacja przy pierwszym kroku
            if (currentRank > 90) currentRank = newRank;

            // --- CZĘŚĆ 1: IMPULS (Twój kod - nagroda za zmianę) ---
            // Działa jak "strzał dopaminy" w momencie sukcesu
            if (newRank < currentRank && currentRank - newRank < 20)
            {
                // Awansowaliśmy (np. z 3 na 2 miejsce)
                AddReward(2.0f); 
                // Debug.Log("Overtake! New Rank: " + newRank);
            }
            else if (newRank > currentRank)
            {
                // Spadliśmy (ktoś nas wyprzedził)
                AddReward(-0.5f); 
            }

            // Aktualizujemy zapamiętaną pozycję
            currentRank = newRank;


            // --- CZĘŚĆ 2: CIĄGŁA PRESJA (To o co pytałeś) ---
            // Działa w każdej klatce. Motywuje do UTRZYMANIA pozycji po wyprzedzeniu.
            if (newRank > 0 && rb != null)
            {
                // Wzór: 1 / Pozycja
                // Lider dostaje 1.0 * skala
                // Drugi dostaje 0.5 * skala
                // Trzeci dostaje 0.33 * skala
                // float rankBonus = 1.0f / (float)newRank;
                
                // AddReward(rankBonus * positionRewardScale);
                float forwardSpeed = Mathf.Max(0f, transform.InverseTransformDirection(rb.linearVelocity).z);
            
                // Minimalna prędkość żeby dostawać bonus (np. 5 m/s)
                float minSpeed = 5f;
                
                if (forwardSpeed > minSpeed)
                {
                    float rankBonus = 1.0f / (float)newRank;
                    
                    // Skaluj bonus względem prędkości
                    float speedFactor = Mathf.Clamp01((forwardSpeed - minSpeed) / 20f); // 0-1 między 5-25 m/s
                    
                    AddReward(rankBonus * positionRewardScale * speedFactor * Time.deltaTime);
                }
                else
                {
                    // KARA za stanie na wysokiej pozycji
                    if (newRank <= 3) // Tylko dla top 3
                    {
                        AddReward(-0.01f * Time.deltaTime);
                    }
                }
            }
        }



        // --- REWARD ZA PRĘDKOŚĆ DO PRZODU ---
        if (rb != null)
        {
            // prędkość w lokalnym układzie auta
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            float forwardSpeed = Mathf.Max(0f, localVel.z); // tylko do przodu


            // float reward = Mathf.Clamp(forwardSpeed * speedRewardScale, 0f, 1f); // Max 1 pkt na sekundę za prędkość
            //             AddReward(reward * Time.deltaTime);
            
            float speedReward = Mathf.Clamp(forwardSpeed * 0.01f, 0f, 0.5f); // Max 0.5/klatkę
            AddReward(speedReward * Time.deltaTime);

            // mała nagroda proporcjonalna do prędkości
            // AddReward(forwardSpeed * speedRewardScale * Time.deltaTime);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxis("Vertical");   // throttle: W/S
        continuous[1] = Input.GetAxis("Horizontal"); // steer: A/D
    }


    private void OnCollisionEnter(Collision collision)
    {
        // ignoruj "spawn collisions" przez krótki okres po teleporcie
        if (Time.time - lastSpawnTime < spawnGracePeriod)
        {
            return;
        }

        // Debug.Log($"Collision Enter: {collision.gameObject.name}, relVel={collision.relativeVelocity.magnitude}, impulse={collision.impulse.magnitude}");
        if (collision.gameObject.TryGetComponent<Wall>(out Wall wall))
        {
            Debug.Log("Ściana");
            AddReward(-.5f);
            EndEpisode();
            return;
        }

        if (collision.gameObject.TryGetComponent<RacistAgent>(out RacistAgent otherAgent))
        {
            // Debug.Log("Kara za zderzenie!");
            AddReward(AgentCollisionEnterPenalty);
            otherAgent.AddReward(AgentCollisionEnterPenalty * 0.5f);
        }

    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<Wall>(out Wall wall))
        {
            AddReward(-0.2f);
            EndEpisode();
            return;
        }
        if (collision.gameObject.TryGetComponent<RacistAgent>(out RacistAgent otherAgent))
        {
            // Debug.Log("Kara utrzymanie zderzenia");
            AddReward(AgentCollisionStayPenalty);
            otherAgent.AddReward(AgentCollisionStayPenalty);
        }
    }


    private void FixedUpdate()
    {
        if (carDriver != null)
        {
            // Wewnątrz metody OnActionReceived lub FixedUpdate w RacistAgent
            float speed = rb.linearVelocity.magnitude;

            // Jeśli auto leci szybciej niż np. 100 m/s (360 km/h) lub spadło pod mapę
            if (speed > 500f || transform.position.y < -10f)    //speed > 500f || 
            {
                Debug.Log($"przekorczenie predkosci {speed} x { transform.position.x} y {transform.position.y} z {transform.position.z}");
                // Opcjonalnie: mała kara, żeby nie dążył do tego
                SetReward(-1f); 
                EndEpisode();
                return;
            }


            SimpleWheel[] wheels = { carDriver.frontLeft, carDriver.frontRight, carDriver.rearLeft, carDriver.rearRight };
            int counter = 0;
            foreach (var wheel in wheels)
            {
                if (!wheel.IsGrounded())
                {
                    counter++;
                }
            }

            // float upDot = Vector3.Dot(transform.up, Vector3.up); // 1 = prosto, -1 = do góry nogami
            // bool upsideDown = upDot < upsideDownDotThreshold;

            if (counter == 4 )
            {
                flippedTimer += Time.fixedDeltaTime;
                if (flippedTimer >= flippedEndDelay)
                {
                    // AddReward(-1f);
                    EndEpisode();
                }
            }
            else
            {
                flippedTimer = 0f;
            }
        }
    }


    /// <summary>
    /// Zamiast metody Start
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable(); // KLUCZOWE: inicjalizuje ML-Agents (sensors, policy itd.)

        // Ustaw warstwę "Agent" na całym prefabie i wyłącz kolizje Agent↔Agent
        EnsureAgentLayerAndIgnoreSelf();

        lastSpawnTime = Time.time;


        if (trackCheckpoints == null)
            trackCheckpoints = FindFirstObjectByType<TrackCheckpoints>();

        trackCheckpoints?.RegisterCar(transform);

        if (trackCheckpoints != null)
        {
            trackCheckpoints.OnCarCorrectCheckpoint += TrackCheckpoints_OnCarCorrectCheckpoint;
            trackCheckpoints.OnCarWrongCheckpoint += TrackCheckpoints_OnWrongCorrectCheckpoint;
            trackCheckpoints.OnCarLapCompleted += TrackCheckpoints_OnCarLapCompleted;
        }

        var dr = GetComponent<DecisionRequester>();
        if (dr != null)
        {
            if (dr.DecisionPeriod <= 0) dr.DecisionPeriod = 5;
            dr.TakeActionsBetweenDecisions = true;
        }
    }

    protected override void OnDisable()
    {
        if (trackCheckpoints != null)
        {
            trackCheckpoints.OnCarCorrectCheckpoint -= TrackCheckpoints_OnCarCorrectCheckpoint;
            trackCheckpoints.OnCarWrongCheckpoint -= TrackCheckpoints_OnWrongCorrectCheckpoint;
            trackCheckpoints.OnCarLapCompleted -= TrackCheckpoints_OnCarLapCompleted;
            trackCheckpoints.UnregisterCar(transform);
        }

        base.OnDisable(); // KLUCZOWE: czyści rejestracje w Academy
    }


    // TODO REFACTOR

    private static int s_AgentLayer = -1;
    private static bool s_IgnoredSelfCollision = false;

    private void EnsureAgentLayerAndIgnoreSelf()
    {
        if (s_AgentLayer == -1)
            s_AgentLayer = LayerMask.NameToLayer("Agent");

        if (s_AgentLayer == -1)
        {
            Debug.LogError("Brak warstwy 'Agent'. Dodaj ją w Project Settings > Tags and Layers.");
            return;
        }

        // Ustaw warstwę na całym obiekcie (root + dzieci)
        SetLayerRecursively(gameObject, s_AgentLayer);

        // Raz na proces wyłącz kolizje tej warstwy z samą sobą
        if (!s_IgnoredSelfCollision)
        {
            Physics.IgnoreLayerCollision(s_AgentLayer, s_AgentLayer, ignoreAgentCollisions);
            s_IgnoredSelfCollision = ignoreAgentCollisions;
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
        }
    }

}
