using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class HeatmapImporterEditor : EditorWindow
{
    string csvPath = "";
    HeatmapData heatmapAsset;

    [MenuItem("Tools/Heatmap Importer")]
    static void Init()
    {
        GetWindow<HeatmapImporterEditor>("Heatmap Importer");
    }

    void OnGUI()
    {
        GUILayout.Label("CSV Heatmap Importer", EditorStyles.boldLabel);

        heatmapAsset = (HeatmapData)EditorGUILayout.ObjectField(
            "Heatmap Asset", heatmapAsset, typeof(HeatmapData), false);

        GUILayout.BeginHorizontal();
        GUILayout.Label("CSV File", GUILayout.Width(70));
        csvPath = GUILayout.TextField(csvPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string file = EditorUtility.OpenFilePanel("Select CSV", "", "csv");
            if (!string.IsNullOrEmpty(file))
                csvPath = file;
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Import CSV"))
        {
            ImportCSV();
        }
    }

    void ImportCSV()
    {
        if (heatmapAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "Assign a HeatmapData asset first!", "OK");
            return;
        }

        if (!File.Exists(csvPath))
        {
            EditorUtility.DisplayDialog("Error", "CSV file not found!", "OK");
            return;
        }

        string[] lines = File.ReadAllLines(csvPath);
        heatmapAsset.Positions.Clear();

        // Find column indices dynamically in case order changes later
        string[] header = lines[0].Split(',');

        int xIndex = System.Array.IndexOf(header, "pos_x");
        int yIndex = System.Array.IndexOf(header, "pos_y");
        int zIndex = System.Array.IndexOf(header, "pos_z");

        if (xIndex < 0 || yIndex < 0 || zIndex < 0)
        {
            EditorUtility.DisplayDialog("Error",
                "CSV missing one or more required columns: pos_x, pos_y, pos_z",
                "OK");
            return;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = line.Split(',');

            float x = float.Parse(cols[xIndex]);
            float y = float.Parse(cols[yIndex]);
            float z = float.Parse(cols[zIndex]);

            heatmapAsset.Positions.Add(new HeatmapData.Entry
            {
                Position = new Vector3(x, y, z)
            });
        }

        EditorUtility.SetDirty(heatmapAsset);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success", "CSV imported successfully!", "OK");
    }
}