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

    [Header("How much weight each recorded position adds")]
    public float HeatFactor = 1f;

    void Awake()
    {
        // Singleton setup
        Instance = this;

        // Find all nodes in the scene if not manually assigned
        if (AllNodes == null || AllNodes.Length == 0)
            AllNodes = FindObjectsOfType<NodeWeight>();

        // Clear previous data
        heatByNode.Clear();

        if (HeatmapSource != null && AllNodes != null && AllNodes.Length > 0)
        {
            foreach (var entry in HeatmapSource.Positions)
            {
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
                    closestNode.HeatmapWeight += HeatFactor;
                }
            }
        }
    }

    // Get the precomputed heat value for a node
    public float GetHeat(NodeWeight node)
    {
        if (node == null) return 0f;

        return heatByNode.TryGetValue(node, out float heat) ? heat : 0f;
    }
}
