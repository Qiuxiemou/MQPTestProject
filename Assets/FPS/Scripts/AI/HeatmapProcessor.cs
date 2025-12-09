using UnityEngine;

public static class HeatmapProcessor
{
    // Call this after CSV is imported and nodes exist in the scene
    public static void ApplyHeatmapToNodes(HeatmapData heatmap, NodeWeight[] nodes, float heatFactor = 1f)
    {
        if (heatmap == null || nodes == null || nodes.Length == 0) return;

        foreach (var entry in heatmap.Positions)
        {
            NodeWeight closestNode = null;
            float closestDist = float.PositiveInfinity;

            foreach (var node in nodes)
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
                // Add a heat value based on the nearest position
                closestNode.HeatmapWeight += heatFactor;
            }
        }
    }
}
