using System.Collections.Generic;
using UnityEngine;

public class HeatmapManager : MonoBehaviour
{
    public static HeatmapManager Instance;

    [Header("Heatmap loaded once from CSV data")]
    public HeatmapData HeatmapSource;

    // Stores cumulative heat per node
    Dictionary<NodeWeight, float> heatByNode = new Dictionary<NodeWeight, float>();

    [Header("All patrol nodes in the scene")]
    public NodeWeight[] AllNodes;

    private int count = 0;

    void Awake()
    {
        // Singleton setup
        Instance = this;
        count = 0;

        // Find all nodes in the scene if not manually assigned
        if (AllNodes == null || AllNodes.Length == 0)
            AllNodes = FindObjectsOfType<NodeWeight>();

        // Clear previous data
        foreach (var node in AllNodes)
        {
            node.HeatmapWeight = 1f;
            Debug.Log($"Node {node.name} - Initial Heatmap Weight: {node.HeatmapWeight}");
        }

        if (HeatmapSource != null && AllNodes != null && AllNodes.Length > 0)
        {
            foreach (var entry in HeatmapSource.Positions)
            {
                count++;
                NodeWeight closestNode = null;
                float closestDist = float.PositiveInfinity;

                foreach (var node in AllNodes)
                {
                    float dist = Vector3.Distance(entry.Position, node.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestNode = node;
                    }
                }

                if (closestNode != null)
                {
                    closestNode.HeatmapWeight += 1;
                }
            }
        }

        foreach (var node in AllNodes)
        {
            Debug.Log($"Node {node.name} - Heat: {node.HeatmapWeight:F2} - Count: {count}");
            GetHeat(node);
        }
    }

    // Get the precomputed heat value for a node
    public void GetHeat(NodeWeight node)
    {
        if (node == null) return;

        node.HeatmapWeight = node.HeatmapWeight / count;
        Debug.Log($"Node {node.name} - Heatmap Weight: {node.HeatmapWeight:F2}");
    }
}
