using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;    // float3, float4x4

public class CheckpointGenerator : MonoBehaviour
{
    public SplineContainer spline;
    public GameObject checkpointPrefab;
    public float spacing = 5f;

    public void Generate()
    {
        if (spline == null || checkpointPrefab == null)
        {
            Debug.LogError("Missing spline or checkpoint prefab!");
            return;
        }

        // Find or create parent "Checkpoints"
        GameObject parent = GameObject.Find("Checkpoints");
        if (parent == null)
            parent = new GameObject("Checkpoints");

        // Clear old checkpoints
        foreach (Transform child in parent.transform)
        {
#if UNITY_EDITOR
            DestroyImmediate(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        // Bierzemy spline (zakładam jeden w containerze)
        Spline s = spline.Spline;

        // Długość w jednostkach lokalnych (transform = identity)
        float4x4 localTransform = float4x4.identity;
        float length = SplineUtility.CalculateLength(s, localTransform);

        // Macierz local -> world dla kontenera
        Matrix4x4 localToWorld = spline.transform.localToWorldMatrix;

        for (float dist = 0f; dist <= length; dist += spacing)
        {
            // Zamiana odległości (Distance) na znormalizowane t (0–1)
            float tNorm = SplineUtility.GetNormalizedInterpolation(
                s,
                dist,
                PathIndexUnit.Distance
            );

            // Pozycja i tangent w PRZESTRZENI LOKALNEJ splajnu
            float3 posLocal = SplineUtility.EvaluatePosition(s, tNorm);
            float3 tanLocal = SplineUtility.EvaluateTangent(s, tNorm);

            // Na world space
            Vector3 posWorld = localToWorld.MultiplyPoint3x4((Vector3)posLocal);
            Vector3 tanWorld = localToWorld.MultiplyVector((Vector3)tanLocal);

            Vector3 right = Vector3.Cross(Vector3.up, tanWorld);

            // rotacja checkpointu: poprzecznie do toru
            Quaternion rot = Quaternion.LookRotation(right, Vector3.up);

            Instantiate(checkpointPrefab, posWorld, rot, parent.transform);
        }

        Debug.Log("Generated checkpoints under 'Checkpoints'.");
    }

    public GameObject wallPrefab;
    public float wallOffset = 5f;   // odległość od osi drogi
    public float wallSpacing = 2f;  // co ile metrów stawiać segment ściany

    public void GenerateWalls()
    {
        GameObject parent = GameObject.Find("Walls");
        if (parent == null)
            parent = new GameObject("Walls");

        foreach (Transform child in parent.transform)
            DestroyImmediate(child.gameObject);

        Spline s = spline.Spline;

        float4x4 localTransform = float4x4.identity;
        float length = SplineUtility.CalculateLength(s, localTransform);

        Matrix4x4 localToWorld = spline.transform.localToWorldMatrix;

        for (float dist = 0f; dist <= length; dist += wallSpacing)
        {
            float t = SplineUtility.GetNormalizedInterpolation(s, dist, PathIndexUnit.Distance);

            float3 posLocal = SplineUtility.EvaluatePosition(s, t);
            float3 tanLocal = SplineUtility.EvaluateTangent(s, t);

            Vector3 posWorld = localToWorld.MultiplyPoint3x4((Vector3)posLocal);
            Vector3 tanWorld = localToWorld.MultiplyVector((Vector3)tanLocal);

            Vector3 right = Vector3.Cross(Vector3.up, tanWorld).normalized;

            Vector3 rightWallPos = posWorld + right * wallOffset;
            Vector3 leftWallPos  = posWorld - right * wallOffset;

            Quaternion rot = Quaternion.LookRotation(tanWorld);

            Instantiate(wallPrefab, rightWallPos, rot, parent.transform);
            Instantiate(wallPrefab, leftWallPos, rot, parent.transform);
        }

        Debug.Log("Walls generated!");
    }




}
