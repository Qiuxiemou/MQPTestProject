// HitLogger.cs
using System.IO;
using System.Text;
using UnityEngine;

public static class HitLogger
{
    // Write under Assets/Logs/hit_log.csv
    private static readonly string FolderPath = Path.Combine(Application.dataPath, "Logs");
    private static readonly string FilePath   = Path.Combine(FolderPath, "hit_log.csv");

    private static bool _initialized;

    private static void EnsureReady()
    {
        if (_initialized) return;

        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var header =
            "timestamp,forward_delay_ms,damage," +
            "client_x,client_y,client_z,client_hp," +
            "server_x,server_y,server_z,server_hp\n";
        File.WriteAllText(FilePath, header, Encoding.UTF8);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh(); 
#endif
        _initialized = true;
    }

    public static void LogHit(
        float forwardDelayMs,
        float damage,
        Transform clientTf,
        Unity.FPS.Game.Health clientHealth,
        Transform serverTf,
        Unity.FPS.Game.Health serverHealth)
    {
        EnsureReady();

        Vector3 cPos = clientTf ? clientTf.position : Vector3.zero;
        Vector3 sPos = serverTf ? serverTf.position : Vector3.zero;

        float cHp = clientHealth ? clientHealth.CurrentHealth : -1f;

        float sHp = serverHealth ? serverHealth.CurrentHealth : -1f;

        string ts = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        var line =
            $"{ts},{forwardDelayMs},{damage}," +
            $"{cPos.x:F4},{cPos.y:F4},{cPos.z:F4},{cHp:F2}," +
            $"{sPos.x:F4},{sPos.y:F4},{sPos.z:F4},{sHp:F2}\n";

        File.AppendAllText(FilePath, line, Encoding.UTF8);
    }

    public static string GetLogPath() => FilePath;
}
