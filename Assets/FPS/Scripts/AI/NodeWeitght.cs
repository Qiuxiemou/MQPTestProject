using UnityEngine;

public class NodeWeight : MonoBehaviour
{
    public float LastVisitTime = 0f;
    public float Weight=0f;
    [Header("Weight Factors")]
    public float TimeFactor = 1f;
    public float DistanceFactor = 0.1f;

    public float GetWeight(Vector3 botPosition)
    {
        float timeSince = Time.time - LastVisitTime;
        float distance = Vector3.Distance(botPosition, transform.position);
        Weight = timeSince * TimeFactor - distance * DistanceFactor;
        return Weight;
    }

    public void MarkVisited()
    {
        LastVisitTime = Time.time;
    }
}
