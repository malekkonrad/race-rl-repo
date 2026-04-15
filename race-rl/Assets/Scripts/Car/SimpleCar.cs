
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleCar : MonoBehaviour
{
    [Header("Wheels")]
    public SimpleWheel frontLeft;
    public SimpleWheel frontRight;
    public SimpleWheel rearLeft;
    public SimpleWheel rearRight;
    
    [Header("Engine & Performance")]
    public float motorForce = 1500f; // Zwiększone, bo fizyka opon teraz lepiej hamuje
    public float maxSpeed = 80f;     // m/s (ok 280 km/h)
    public AnimationCurve torqueCurve = AnimationCurve.Linear(0, 1, 1, 0.5f); // Krzywa momentu

    [Header("Steering")]
    public float maxSteerAngle = 35f;
    public float steerSpeed = 10f;

    [Header("Brakes")]
    public float brakeForce = 6000f;
    
    [Header("Physics Tweaks (F1 Style)")]
    public float downforce = 100f;      // Docisk aerodynamiczny
    public float antiRollForce = 10000f;// Sztywność stabilizatora
    [Range(0,1)] public float steerHelper = 0.3f; // Pomaga utrzymać kierunek (fake physics dla lepszego feelingu)

    private Rigidbody rb;
    private float currentSteerAngle;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Bardzo ważne: obniżenie środka ciężkości, żeby auto się nie wywracało
        rb.centerOfMass = new Vector3(0, -0.6f, 0.2f); 
        
        // Ustawienia masy dla stabilności (sugerowane: 1500)
        if(rb.mass < 500) rb.mass = 1500f; 
        
        // Zmniejszamy opór kątowy, żeby auto chętniej skręcało
        rb.angularDamping = 1.0f;
    }

    public void SetInputs(float forwardAmount, float turnAmount)
    {
        // ZABEZPIECZENIE: Jeśli z jakiegoś powodu rb nie istnieje, nie rób nic
        if (rb == null) return;

        float throttle = forwardAmount;
        float steer = turnAmount;
        
        HandleSteering(steer);
        HandleEngine(throttle);
        HandleAerodynamics();
        HandleStabilizers(); // Anti-roll bar
    }

    void HandleSteering(float steerInput)
    {
        // Płynne skręcanie
        float targetAngle = steerInput * maxSteerAngle;
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, Time.fixedDeltaTime * steerSpeed);

        // Geometria Ackermanna (wewnętrzne koło skręca mocniej)
        if (frontLeft != null && frontRight != null)
        {
            float farAngle = currentSteerAngle;
            float nearAngle = currentSteerAngle;
            
            // Prosta symulacja Ackermanna - zwiększamy kąt wewnętrznego koła
            if(steerInput > 0) nearAngle *= 1.1f; // Skręt w prawo, prawe koło mocniej
            else farAngle *= 1.1f; // Skręt w lewo, lewe koło mocniej
            
            frontLeft.transform.localRotation = Quaternion.Euler(0, steerInput > 0 ? farAngle : nearAngle, 0);
            frontRight.transform.localRotation = Quaternion.Euler(0, steerInput > 0 ? nearAngle : farAngle, 0);
        }

        // Steer Helper - sztuczna siła obracająca auto w stronę skrętu
        // Pomaga ML Agentom szybciej "zrozumieć" skręcanie bez driftowania
        if (Mathf.Abs(currentSteerAngle) > 1f && rb.linearVelocity.magnitude > 5f)
        {
            rb.AddRelativeTorque(Vector3.up * currentSteerAngle * steerHelper * rb.linearVelocity.magnitude);
        }
    }

    void HandleEngine(float throttle)
    {
        // Napęd (RWD)
        if (throttle > 0.1f)
        {
            ApplyMotor(rearLeft, throttle);
            ApplyMotor(rearRight, throttle);
        }
        // Hamowanie
        else if (throttle < -0.1f)
        {
            ApplyBrake(frontLeft, Mathf.Abs(throttle) * 0.7f); // Przód hamuje słabiej (balans hamulców)
            ApplyBrake(frontRight, Mathf.Abs(throttle) * 0.7f);
            ApplyBrake(rearLeft, Mathf.Abs(throttle));
            ApplyBrake(rearRight, Mathf.Abs(throttle));
        }
        else
        {
            // Hamowanie silnikiem (Drag)
            // ApplyBrake(rearLeft, 0.1f);
            // ApplyBrake(rearRight, 0.1f);
        }
    }

    void HandleAerodynamics()
    {
        if (rb == null) return;
        
        // Obliczamy siłę docisku
        float speed = rb.linearVelocity.magnitude;
        // Debug.Log($"Speed: {rb.linearVelocity.magnitude * 3.6f:F0} km/h");
        float downforceAmount = downforce * speed;
        
        float clampedDownforce = Mathf.Clamp(downforceAmount, 0, 50000f); // Limit max siły
        rb.AddForce(-transform.up * clampedDownforce);
    }

    void HandleStabilizers()
    {
        // Anti-Roll Bar: Przenosi siłę z koła ściśniętego na koło odciążone
        ApplyAntiRoll(frontLeft, frontRight);
        ApplyAntiRoll(rearLeft, rearRight);
    }

    void ApplyAntiRoll(SimpleWheel wl, SimpleWheel wr)
    {
        if (wl == null || wr == null) return;

        float travelL = wl.CompressionRatio;
        float travelR = wr.CompressionRatio;

        float antiRollForceAmount = (travelL - travelR) * antiRollForce;

        if (wl.IsGrounded())
            rb.AddForceAtPosition(wl.transform.up * -antiRollForceAmount, wl.transform.position);
        
        if (wr.IsGrounded())
            rb.AddForceAtPosition(wr.transform.up * antiRollForceAmount, wr.transform.position);
    }

    void ApplyMotor(SimpleWheel wheel, float input)
    {
        if (wheel != null && wheel.IsGrounded())
        {
            // Ogranicznik prędkości maksymalnej
            float currentSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
            if (currentSpeed > maxSpeed) return;

            // Krzywa momentu obrotowego (więcej mocy na starcie, mniej przy V-max)
            float speedRatio = Mathf.Clamp01(Mathf.Abs(currentSpeed) / maxSpeed);
            float availableTorque = torqueCurve.Evaluate(speedRatio) * motorForce * input;

            // Aplikujemy siłę w kierunku, w który patrzy koło (ważne dla RWD przy poślizgach)
            // Dla tylnych kół transform.forward bolidu jest ok, ale wheel.transform.forward jest bezpieczniejsze
            rb.AddForceAtPosition(wheel.transform.forward * availableTorque, wheel.transform.position);
        }
    }

    void ApplyBrake(SimpleWheel wheel, float input)
    {
        if (wheel != null && wheel.IsGrounded())
        {
            // Hamujemy przeciwnie do ruchu auta
            Vector3 velocity = rb.GetPointVelocity(wheel.transform.position);
            Vector3 brakeDir = -velocity.normalized;
            
            // Aplikujemy hamulec
            rb.AddForceAtPosition(brakeDir * brakeForce * input, wheel.transform.position);
        }
    }

    public void StopCompletely()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        currentSteerAngle = 0f;
    }
}









