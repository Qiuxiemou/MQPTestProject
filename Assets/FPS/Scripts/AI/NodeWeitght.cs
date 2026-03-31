using UnityEngine;
using Unity.FPS.AI;
using System;

public class NodeWeight : MonoBehaviour
{
    public Transform ReferenceEnemy;

    public float LastVisitTime = 0f;

    [Header("Dynamic Weight Factors")]
    public float TimeFactor = 1f;
    public float DistanceFactor = 0.1f;
    public float randomFactor = 0f;
    [Header("Last Seen Player Influence")]
    public float LastSeenFactor = 2f;     // strength of influence
    public float LastSeenMaxDistance = 30f; // how far it affects nodes
    public float HeatWeight = 3;

    // This will store the static heat applied from historical player positions
    [HideInInspector]
    public float HeatmapWeight = 1f;

    void Awake()
    {
        randomFactor = UnityEngine.Random.Range(1f, 3f);
        LastVisitTime = Time.time;
    }

    public void MarkVisited()
    {
        if (Time.time - LastVisitTime >= 5f)
        {
            randomFactor = UnityEngine.Random.Range(1f, 3f);
        }
        LastVisitTime = Time.time;
    }

    public float GetLastSeenMultiplier(Vector3 lastSeenPosition, float sinceLastSeenPlayer)
    {
        float dist = Vector3.Distance(lastSeenPosition, transform.position);

        // Normalize distance (0 = close, 1 = far)
        float t = Mathf.Clamp01(dist / LastSeenMaxDistance);

        // Invert so close = high value, far = low value
        float proximity = 1f - t;

        return 1f + proximity * LastSeenFactor * Math.Max(sinceLastSeenPlayer - 10f, 0f);
    }

    public float GetDynamicWeight(Vector3 botPosition)
    {
        float timeSince = Time.time - LastVisitTime;
        float distance = Vector3.Distance(botPosition, transform.position);
        return Mathf.Max(0f, timeSince * TimeFactor - distance * DistanceFactor);
    }

    // Get total weight (dynamic + heatmap + seen)
    public float GetTotalWeight(Vector3 botPosition, Vector3 lastSeenPosition, float sinceLastSeenPlayer)
    {
        float dynamicWeight = GetDynamicWeight(botPosition);
        float heatMultiplier = 1f + HeatmapWeight * randomFactor * HeatWeight;
        float lastSeenMultiplier = GetLastSeenMultiplier(lastSeenPosition, sinceLastSeenPlayer);

        return dynamicWeight * heatMultiplier * lastSeenMultiplier;
    }

    // Get total weight (dynamic + heatmap)
    public float GetTotalWeight(Vector3 botPosition)
    {
        float dynamicWeight = GetDynamicWeight(botPosition);
        float heatMultiplier = 1f + HeatmapWeight * randomFactor * HeatWeight;

        return dynamicWeight * heatMultiplier;
    }

    void OnDrawGizmos()
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new GradientColorKey[] {
            new GradientColorKey(Color.blue, 0f),
            new GradientColorKey(Color.green, 0.25f),
            new GradientColorKey(Color.yellow, 0.5f),
            new GradientColorKey(new Color(1f, 0.5f, 0f), 0.75f),
            new GradientColorKey(Color.red, 1f)
            },
            new GradientAlphaKey[] {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
            }
        );

        float totalWeight = HeatmapWeight;

        if (Application.isPlaying && ReferenceEnemy != null)
        {
            EnemyController enemy = ReferenceEnemy.GetComponent<EnemyController>();

            if (enemy != null && enemy.HasLastSeenPlayer)
            {
                
                totalWeight = GetTotalWeight(
                    ReferenceEnemy.position,
                    enemy.LastSeenPlayerPosition,
                    enemy.TimeSinceLastSeenPlayer
                );
                //Debug.Log($"Node {name} - Dynamic: {GetDynamicWeight(ReferenceEnemy.position):F2}, Heat: {1f + HeatmapWeight * randomFactor}, LastSeenMult: {GetLastSeenMultiplier(enemy.LastSeenPlayerPosition, enemy.TimeSinceLastSeenPlayer):F2}, Total: {totalWeight:F2}");
            }
            else
            {
                totalWeight = GetTotalWeight(ReferenceEnemy.position);
                //Debug.Log($"Node {name} - Dynamic: {GetDynamicWeight(ReferenceEnemy.position):F2}, Heat: {1f + HeatmapWeight * randomFactor}, Total: {totalWeight:F2}");
            }
        }

        // Normalize weight better (dynamic scaling instead of hard /20)
        float normalized = Mathf.Clamp01(totalWeight / 20f);

        Color color = gradient.Evaluate(normalized);
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, 1.0f);
    }
}