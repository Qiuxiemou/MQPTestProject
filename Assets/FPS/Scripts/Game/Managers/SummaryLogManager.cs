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

    // ===== MOVEMENT =====
    Vector3 lastPlayerPos;
    Vector3 lastBotPos;
    float playerDistance = 0f;
    float botDistance = 0f;

    // ===== SPEED =====
    float totalPlayerSpeed = 0f;
    float totalBotSpeed = 0f;
    int speedSamples = 0;

    // ===== MOUSE =====
    float totalMouseMovement = 0f;

    // ===== CORNER =====
    int cornerShots = 0;

    // ===== TIME =====
    float roundStartTime;
    float lastHitTime = -1f;
    float totalTimeToHit = 0f;

    float totalTimeToKill = 0f;
    int killCount = 0;

    // ================= ROUND CONTEXT =================
    int _participantID;
    int _latinRow;
    int _roundID;
    int _roundConditionID;
    float _latency;
    string _role;
    string _timewarp;
    float _speed;
    int _weaponIndex;


    string _time;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        EventManager.AddListener<FireShotEvent>(OnFireShot);
        EventManager.AddListener<HitEvent>(OnHit);
    }

    public void Init(string participantFolder, int participantID)
    {

        // summaryPath = Path.Combine(participantFolder, "summary.csv");
        summaryPath = Path.Combine(participantFolder, $"summary_{participantID}.csv");

        if (!File.Exists(summaryPath))
        {
            //File.WriteAllText(summaryPath,
            //    "sessionID,latinRow,round,sessionStart,now,latency,rcole,timewarp," +
            //    "score,enemySpeed,playerSpeed,totalShots,totalHits,errorAngle,accuracy," +
            //    "cornerShots,ttkAvg,q1,q2,playerDist,botDist,mouseMove\n"
            //);
            File.WriteAllText(summaryPath,
                "startRoundTime," +
                "participantID,latinRow,roundNumber,conditionID,latency,role,timewarp," +
                "enemySpeed,playerSpeed," +
                "weaponIndex,"+
                "totalShots,totalHits,errorAngle,accuracy,cornerShots,ttkAvg,score," +
                "playerDist,botDist,mouseMove," +
                "surveyQ1,surveyQ2" +
                "\n"
            );
        }
    }
    public void SetRoundContext(int participantID, int latinRow, int roundID, int roundConditionID, float latency, string role, string timewarp, string time, float speed, int weaponIndex)
    {
        _participantID = participantID;
        _latinRow = latinRow;
        _roundID = roundID;
        _roundConditionID = roundConditionID;
        _latency = latency;
        _role = role;
        _timewarp = timewarp;
        _time = time;
        _speed = speed;
        _weaponIndex = weaponIndex;
    }
    void OnFireShot(FireShotEvent e)
    {
        if (e.ShooterId == "Player") totalShots++;
    }
    void OnHit(HitEvent e)
    {
        if (e.ShooterId == "Player")
        {
            totalHits++;

            float now = Time.time;

            if (lastHitTime > 0f)
            {
                totalTimeToHit += (now - lastHitTime);
                //hitCountForTTH++;
            }

            lastHitTime = now;
        }

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
        //int latinRow,
        //int round,
        //float latency,
        //string role,
        //string timewarp,
        int score,
        float enemySpeed,
        float playerSpeed,
        //int totalShots,
        //int totalHits,
        float errorAngle,
        //float accuracy,
        int cornerShots,
        float ttk,
        //float tth, // Remove this
        //float q1,
        //float q2,
        float playerDist,
        float botDist,
        float mouseMove
    )
    {
        //float acc = this.totalShots > 0? (float)this.totalHits / this.totalShots * 100f : 0f;

        float acc = totalShots > 0 ? (float)totalHits / totalShots * 100f : 0f;

        float avgPlayerSpeed = speedSamples > 0 ? totalPlayerSpeed / speedSamples : 0f;
        float avgBotSpeed = speedSamples > 0 ? totalBotSpeed / speedSamples : 0f;

        //float avgTTH = hitCountForTTH > 0 ? totalTimeToHit / hitCountForTTH : 0f;
        float avgTTK = killCount > 0 ? totalTimeToKill / killCount : 0f;

        string line =
            $"{_time},{_participantID},{_latinRow},{_roundID},{_roundConditionID}," +
            //$"{Time.realtimeSinceStartup},{System.DateTime.Now:o}," +
            $"{_latency},{_role},{_timewarp}," +
            $"{enemySpeed},{playerSpeed}," +
            $"{_weaponIndex}," +
            $"{totalShots},{totalHits},{errorAngle},{acc},{cornerShots},{avgTTK},{score}," +
            $"{playerDist},{botDist},{mouseMove}," +
            $"{surveyQ1},{surveyQ2}";

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

    public void StartRound(Transform player, Transform bot)
    {
        roundStartTime = Time.time;

        lastPlayerPos = player.position;
        lastBotPos = bot.position;

        playerDistance = 0f;
        botDistance = 0f;

        totalPlayerSpeed = 0f;
        totalBotSpeed = 0f;
        speedSamples = 0;

        totalMouseMovement = 0f;

        cornerShots = 0;

        lastHitTime = -1f;
        totalTimeToHit = 0f;
        //hitCountForTTH = 0;

        totalTimeToKill = 0f;
        killCount = 0;
    }

    public void UpdateMovement(Transform player, Transform bot)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // ===== PLAYER =====
        float pDist = Vector3.Distance(player.position, lastPlayerPos);
        playerDistance += pDist;

        float pSpeed = pDist / dt;
        totalPlayerSpeed += pSpeed;

        lastPlayerPos = player.position;

        // ===== BOT =====
        float bDist = Vector3.Distance(bot.position, lastBotPos);
        botDistance += bDist;

        float bSpeed = bDist / dt;
        totalBotSpeed += bSpeed;

        lastBotPos = bot.position;

        speedSamples++;
    }
    public float GetPlayerDistance() { return playerDistance; }
    public float GetBotDistance() { return botDistance;}
    public void AddMouseMovement(float mx, float my) { totalMouseMovement += Mathf.Abs(mx) + Mathf.Abs(my); }
    public float GetMouseMovement() { return totalMouseMovement; }
    public void CountShotAroundCorner() { cornerShots++; }
    public void CountHits() { totalHits++; }
    public void OnKilled()
    {
        float ttk = Time.time - roundStartTime;
        totalTimeToKill += ttk;
        killCount++;
    }
}