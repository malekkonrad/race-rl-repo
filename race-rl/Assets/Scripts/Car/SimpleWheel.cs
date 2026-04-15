
using UnityEngine;

public class SimpleWheel : MonoBehaviour
{
    [Header("Wheel Settings")]
    public float radius = 0.34f;        // Wizualny promień koła
    public float castRadius = 0.15f;    // Promień "czujnika" (fizycznej kuli)
    public float suspensionDistance = 0.2f;
    public float springStrength = 35000f;
    public float springDamper = 2500f;

    [Header("Grip Settings")]
    public float sideStiffness = 1.5f;
    public AnimationCurve frictionCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Ground Filter")]
    public LayerMask groundLayers;
    public bool showDebug = true;

    private Rigidbody carRb;
    private bool isGrounded;

    public float CompressionRatio { get; private set; }

    void Start()
    {
        carRb = GetComponentInParent<Rigidbody>();

        // Zabezpieczenie krzywej
        if (frictionCurve.length == 0)
        {
            frictionCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 1.0f),
                new Keyframe(1.0f, 0.6f)
            );
        }
    }

    void FixedUpdate()
    {
        // Chcemy, żeby spód kuli sięgał tam, gdzie spód koła przy max wyproście.
        // Dystans środka = (max_zasięg_koła) - (promień_kuli_czujnika)
        float maxRayLength = (suspensionDistance + radius) - castRadius;

        if (Physics.SphereCast(transform.position, castRadius, -transform.up, out RaycastHit hit, maxRayLength, groundLayers, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
            
            // Obliczamy faktyczną odległość od środka koła do punktu styku
            float currentDistanceToGround = hit.distance + castRadius; 
            // Alternatywnie Vector3.Distance(transform.position, hit.point) też jest OK, 
            // ale hit.distance jest bardziej stabilny przy SphereCast na płaskim.

            // 1. SUSPENSION
            Vector3 springDir = transform.up;
            Vector3 tireWorldVel = carRb.GetPointVelocity(transform.position);

            // Ile sprężyna jest ściśnięta?
            float offset = suspensionDistance - (currentDistanceToGround - radius);
            CompressionRatio = Mathf.Clamp01(offset / suspensionDistance);

            float vel = Vector3.Dot(springDir, tireWorldVel);
            float force = (offset * springStrength) - (vel * springDamper);

            // Zapobiega wystrzeleniu w kosmos, jeśli offset błędnie wyjdzie ogromny.
            // Limit = Masa Auta * Grawitacja * Margines (np. 15G przeciążenia)
            // Zakładając masę auta ~1500kg, maxForce ~ 220,000. To wystarczy, by skakać, ale nie by zbugować Unity.
            float maxForce = carRb.mass * 15f * 9.81f; 
            force = Mathf.Clamp(force, 0f, maxForce); 
            // -----------------------------------------

            if (force > 0)
                carRb.AddForceAtPosition(springDir * force, hit.point);

            // 2. LATERAL FRICTION
            float steeringAngle = Vector3.Dot(transform.right, tireWorldVel);
            
            // Normalizacja poślizgu
            float slipFactor = Mathf.Clamp01(Mathf.Abs(steeringAngle) / (0.1f + carRb.linearVelocity.magnitude * 0.05f));
            float gripFactor = frictionCurve.Evaluate(slipFactor);

            // Siła boczna
            float desiredSideForce = -steeringAngle * force * sideStiffness * gripFactor;
            carRb.AddForceAtPosition(transform.right * desiredSideForce, hit.point);
        }
        else
        {
            isGrounded = false;
            CompressionRatio = 0f;
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebug) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;

        // Rysujemy linię maksymalnego zasięgu
        Vector3 endPos = transform.position - transform.up * (suspensionDistance + radius);
        Gizmos.DrawLine(transform.position, endPos);

        // Rysujemy kulę (SphereCast) w miejscu trafienia lub na końcu
        if (isGrounded)
        {
            // Rysujemy tam gdzie trafiło (symulacja SphereCasta)
            // Musimy odjąć castRadius, żeby narysować środek kuli w dobrym miejscu
            // (ponieważ SphereCast zwraca hit.distance do środka kuli)
            // Ale dla uproszczenia wizualizacji w Gizmos:
             Gizmos.DrawWireSphere(endPos, castRadius); // To pokazuje cel
        }
        else
        {
             Gizmos.DrawWireSphere(endPos, castRadius);
        }
    }

    public bool IsGrounded() => isGrounded;
}