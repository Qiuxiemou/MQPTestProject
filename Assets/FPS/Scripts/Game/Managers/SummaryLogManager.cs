using System.IO;
using System.Text;
using Unity.FPS.Game;
using UnityEngine;
using static UnityEngine.UI.CanvasScaler;

public class SummaryLogManager : MonoBehaviour
{
    public static SummaryLogManager Instance { get; private set; }

    string summaryPath;
    string sessionId;

    int totalShots = 0;
    int totalHits = 0;

    int delayedBotHits = 0;
    int damageDealt = 0;
    int damageReceived = 0;

    float surveyQ1 = 0;
    float surveyQ2 = 0;



    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        EventManager.AddListener<FireShotEvent>(OnFireShot);
        EventManager.AddListener<HitEvent>(OnHit);
    }

    public void Init(string participantFolder)
    {

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

    void OnFireShot(FireShotEvent e)
    {
        if (e.ShooterId == "Player") totalShots++;
    }

    void OnHit(HitEvent e)
    {
        // Player shot someone
        if (e.ShooterId == "Player")
        {
            totalHits++;
            damageDealt += Mathf.RoundToInt(e.Damage);

            if (e.TargetId.Contains("HitBox"))
                delayedBotHits++;
        }

        // Player got shot
        if (e.TargetId == "Player")
        {
            damageReceived += Mathf.RoundToInt(e.Damage);
        }
    }

    public void SetSurvey(float q1, float q2)
    {
        surveyQ1 = q1;
        surveyQ2 = q2;
    }

    public void LogRound(
        int latinRow,
        int round,
        float latency,
        string role,
        string timewarp,
        int score,
        float enemySpeed,
        float playerSpeed,
        //int totalShots,
        //int totalHits,
        float errorAngle,
        //float accuracy,
        int cornerShots,
        float ttk,
        float tth,
        //float q1,
        //float q2,
        float playerDist,
        float botDist,
        float mouseMove
    )
    {
        float acc = this.totalShots > 0? (float)this.totalHits / this.totalShots * 100f : 0f;
        string line =
            $"{sessionId},{latinRow},{round}," +
            $"{Time.realtimeSinceStartup},{System.DateTime.Now:o}," +
            $"{latency},{role},{timewarp}," +
            $"{score},{enemySpeed},{playerSpeed}," +
            $"{this.totalShots},{this.totalHits},{errorAngle},{acc}," +
            $"{cornerShots},{ttk},{tth},{surveyQ1},{surveyQ2}," +
            $"{playerDist},{botDist},{mouseMove}";

        File.AppendAllText(summaryPath, line + "\n", Encoding.UTF8);

        // reset for next round
        totalShots = 0;
        totalHits = 0;
        delayedBotHits = 0;
        damageDealt = 0;
        damageReceived = 0;
        surveyQ1 = 0;
        surveyQ2 = 0;
    }
}