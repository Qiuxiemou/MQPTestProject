using UnityEngine;

public class NodeWeight : MonoBehaviour
{
    public Transform ReferenceEnemy;

    public float LastVisitTime = 0f;

    [Header("Dynamic Weight Factors")]
    public float TimeFactor = 1f;
    public float DistanceFactor = 0.1f;

    // This will store the static heat applied from historical player positions
    [HideInInspector]
    public float HeatmapWeight = 0f;

    public void MarkVisited()
    {
        LastVisitTime = Time.time;
    }

    public float GetDynamicWeight(Vector3 botPosition)
    {
        float timeSince = Time.time - LastVisitTime;
        float distance = Vector3.Distance(botPosition, transform.position);
        return timeSince * TimeFactor - distance * DistanceFactor;
    }

    // Get total weight (dynamic + heatmap)
    public float GetTotalWeight(Vector3 botPosition)
    {
        return GetDynamicWeight(botPosition) + HeatmapWeight;
    }

    void OnDrawGizmos()
    {
        float totalWeight = HeatmapWeight;

        if (Application.isPlaying && ReferenceEnemy != null)
        {
            totalWeight = GetTotalWeight(ReferenceEnemy.position);
        }

        Color color = Color.Lerp(Color.blue, Color.red, Mathf.Clamp01(totalWeight / 20f));
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, 1.0f);
    }
}