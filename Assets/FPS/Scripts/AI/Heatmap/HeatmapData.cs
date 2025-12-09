using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HeatmapData", menuName = "AI/Heatmap Data")]
public class HeatmapData : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public Vector3 Position;
    }

    public List<Entry> Positions = new List<Entry>();
}