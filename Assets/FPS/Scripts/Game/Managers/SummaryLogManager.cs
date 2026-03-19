using System.IO;
using System.Text;
using UnityEngine;

public class SummaryLogManager : MonoBehaviour
{
    public static SummaryLogManager Instance { get; private set; }

    string summaryPath;
    string sessionId;
    int latinRow;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    public void Init(string participantFolder, int latinRowIndex)
    {
        latinRow = latinRowIndex;

        summaryPath = Path.Combine(participantFolder, "summary.csv");

        if (!File.Exists(summaryPath))
        {
            File.WriteAllText(summaryPath,
                "sessionID,latinRow,round,sessionStart,now,latency,role,timewarp," +
                "score,enemySpeed,playerSpeed,totalShots,totalHits,errorAngle,accuracy," +
                "cornerShots,ttkAvg,tthAvg,q1,q2,playerDist,botDist,mouseMove\n"
            );
        }
    }

    public void LogRound(
        int round,
        float latency,
        string role,
        string timewarp,
        int score,
        float enemySpeed,
        float playerSpeed,
        int totalShots,
        int totalHits,
        float errorAngle,
        float accuracy,
        int cornerShots,
        float ttk,
        float tth,
        float q1,
        float q2,
        float playerDist,
        float botDist,
        float mouseMove
    )
    {
        string line =
            $"{sessionId},{latinRow},{round}," +
            $"{Time.realtimeSinceStartup},{System.DateTime.Now:o}," +
            $"{latency},{role},{timewarp}," +
            $"{score},{enemySpeed},{playerSpeed}," +
            $"{totalShots},{totalHits},{errorAngle},{accuracy}," +
            $"{cornerShots},{ttk},{tth},{q1},{q2}," +
            $"{playerDist},{botDist},{mouseMove}";

        File.AppendAllText(summaryPath, line + "\n", Encoding.UTF8);
    }
}