using UnityEngine;

public class NodeWeight : MonoBehaviour
{
    public Transform ReferenceEnemy;

    public float LastVisitTime = 0f;

    [Header("Dynamic Weight Factors")]
    public float TimeFactor = 1f;
    public float DistanceFactor = 0.1f;
    public float randomFactor = Random.Range(0f, 2f);

    // This will store the static heat applied from historical player positions
    [HideInInspector]
    public float HeatmapWeight = 1f;

    public void MarkVisited()
    {
        if (Time.time - LastVisitTime >= 5f)
        {
            randomFactor = Random.Range(0.0f, 5f);
        }
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
        return GetDynamicWeight(botPosition) + HeatmapWeight + randomFactor;
    }

    void OnDrawGizmos()
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.blue, 0f),
                new GradientColorKey(Color.green, 0.25f),
                new GradientColorKey(Color.yellow, 0.5f),
                new GradientColorKey(new Color(1f, 0.5f, 0f), 0.75f), // orange
                new GradientColorKey(Color.red, 1f)
            },
            new GradientAlphaKey[] {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
        });

        float totalWeight = HeatmapWeight;

        if (Application.isPlaying && ReferenceEnemy != null)
        {
            totalWeight = GetTotalWeight(ReferenceEnemy.position);
        }

        Color color = gradient.Evaluate(Mathf.Clamp01(totalWeight / 20f));
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, 1.0f);
    }
}