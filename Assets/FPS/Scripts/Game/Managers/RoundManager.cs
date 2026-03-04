using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Round Settings")]
    public float roundLengthSeconds = 60f;
    public Behaviour[] disableWhilePaused;

    // ================= LATENCY =================
    public float CurrentLatencyMs { get; private set; }
    //public float CurrentLatencyMs = 0f;

    public bool LatencyTest = false;
    public float SimulatedLatencyMs = 200f;

    [Header("Study Config")]
    [SerializeField] private TextAsset roundConditionFile;

    private List<RoundCondition> rounds;
    public int currentRoundIndex = 0;
    private RoundCondition currentCondition;

    [SerializeField] private TextAsset latinSquareFile;

    private string _participantCounterPath;

    // ================= TIME WARP =================
    [Header("Time Warp Settings")]
    public TimewarpMode CurrentTimewarpMode { get; private set; }

    // ================= UI =================
    [Header("UI References")]
    public TimerUI timerUI;
    public SurveyUI surveyUI;

    [Header("Round Start UI")]
    public RoundStartUI roundStartUI;

    bool waitingForPlayerInput = false;

    // ================= PLAYER =================
    [Header("Player")]
    public GameObject player;
    public Transform playerSpawnPoint;

    // ================= ENEMIES =================
    [Header("Enemy Prefabs")]
    public GameObject seekerBotPrefab;
    public GameObject hiderBotPrefab;
    public GameObject hiderTrueBotPrefab;
    public GameObject hiderPastBotPrefab;
    public Transform enemySpawnPoint;

    GameObject _futureBot;
    GameObject _trueBot;
    GameObject _pastBot;

    // ================= STATE =================
    public int CurrentRound { get; private set; } = 1;
    public bool RoundRunning { get; private set; }
    public bool IsSeeker => _isSeeker;
    public float TimeRemaining => Mathf.Max(0, _timeRemaining);

    public event Action<bool> OnBotRoleChanged;
    public event Action<float> OnTimerTick;

    Coroutine _roundCoroutine;
    float _timeRemaining;
    bool _isSeeker;

    bool waitingToStartRound = false;
    int _lastPrintedSecond = -1;

    // ================ SCORE ====================
    IScoreDisplay scoreDisplay;
    public GameObject CurrentEnemy => _futureBot;


    public interface IScoreDisplay
    {
        void SetEnemy(GameObject enemy);
    }

    public void RegisterScoreDisplay(IScoreDisplay display)
    {
        scoreDisplay = display;

        // If enemy already exists, assign immediately
        if (_futureBot != null)
        {
            LM.write("Enemy already exists, assigning now");
            scoreDisplay.SetEnemy(_futureBot);
        }
    }


    // ================= LOGGING =================
    readonly List<SurveyData> _buffer = new();
    //string _surveyCsvPath;
    //string _roleLogPath;


    // ---------------- UNITY ----------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        //string logsDir = GetProjectLogsPath();

        //_surveyCsvPath = Path.Combine(
        //    logsDir,
        //    $"survey_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
        //);

        //_roleLogPath = Path.Combine(
        //    logsDir,
        //    $"role_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
        //);

        //_participantCounterPath = Path.Combine(
        //    GetProjectLogsPath(),
        //    "participant_counter.txt"
        //);

        //File.WriteAllText(_surveyCsvPath,
        //    "timestamp,round,smoothness,responsiveness,fairness,qoe,fun,notes\n");

        //File.WriteAllText(_roleLogPath,
        //    "timestamp,round,timeRemaining,role\n");

        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string logsDir = Path.Combine(root, "Logs");
        Directory.CreateDirectory(logsDir);

        _participantCounterPath = Path.Combine(logsDir, "participant_counter.txt");
    }

    void Start()
    {
        // Load all possible conditions
        var allConditions = RoundConfigLoader.Load(roundConditionFile);

        // Load Latin square order
        var latinOrders = LoadLatinSquare(latinSquareFile);

        // Pick row for this participant
        int participantIndex = GetAndIncrementParticipantIndex(latinOrders.Count);

        // Initialize participant in log manager
        ParticipantLogManager.Instance.InitializeParticipant(participantIndex);

        List<int> orderRow = latinOrders[participantIndex];

        // Rebuild rounds list based on ID order
        rounds = new List<RoundCondition>();

        foreach (int id in orderRow)
        {
            RoundCondition match = allConditions.Find(c => c.id == id);
            if (match.id == 0)
            {
                LM.write($"Condition ID {id} not found!");
                continue;
            }
            rounds.Add(match);
        }

        LM.write($"[RoundManager] Loaded {rounds.Count} round conditions");

        // Log ID order for this participant
        StringBuilder orderLog = new StringBuilder();
        orderLog.Append($"[RoundManager] Participant {participantIndex} order: ");

        for (int i = 0; i < rounds.Count; i++)
        {
            orderLog.Append(rounds[i].id);

            if (i < rounds.Count - 1)
                orderLog.Append(", ");
        }

        LM.write(orderLog.ToString());


        BindUIIfNeeded();
        //StartNextRound();
    }

    private int GetAndIncrementParticipantIndex(int totalSequences)
    {
        int index = 0;

        if (File.Exists(_participantCounterPath))
        {
            string content = File.ReadAllText(_participantCounterPath);
            int.TryParse(content, out index);
        }

        // Save incremented value
        index = index % totalSequences;

        int nextIndex = (index + 1) % totalSequences;

        File.WriteAllText(_participantCounterPath, nextIndex.ToString());

        return index;
    }

    private List<List<int>> LoadLatinSquare(TextAsset csvFile)
    {
        var result = new List<List<int>>();

        if (csvFile == null)
        {
            LM.write("Latin square file missing!");
            return result;
        }

        string[] lines = csvFile.text.Split('\n');

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string cleaned = line.Trim();

            string[] tokens = cleaned.Split(
                new char[] { ',', '\t', ' ' },
                System.StringSplitOptions.RemoveEmptyEntries
            );

            List<int> row = new List<int>();

            foreach (string token in tokens)
            {
                if (int.TryParse(token, out int value))
                    row.Add(value);
            }

            if (row.Count > 0)
                result.Add(row);
        }

        return result;
    }
    void Update()
    {
        if (waitingForPlayerInput && Input.GetKeyDown(KeyCode.Tab))
        {
            StartRoundGameplay();
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnApplicationQuit()
    {
        FlushBuffer();
    }

    void LateUpdate()
    {
        if (!RoundRunning)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void ApplyTimewarpMode()
    {
        if (_isSeeker) return; // only applies to hider timeline

        if (_futureBot == null) return;

        var futureProxy = _futureBot.GetComponent<BotHealthProxy>();
        var pastProxy = _pastBot?.GetComponent<BotHealthProxy>();

        if (futureProxy != null)
        {
            futureProxy.SetLatency(CurrentLatencyMs);
            futureProxy.SetTimewarpMode(CurrentTimewarpMode);
        }

        if (pastProxy != null)
        {
            pastProxy.SetLatency(CurrentLatencyMs);
            pastProxy.SetTimewarpMode(CurrentTimewarpMode);
        }

        LM.write($"[RoundManager] Applied Timewarp Mode: {CurrentTimewarpMode}");

        Transform futureHitbox = _futureBot.transform.Find("HitBox");
        Transform pastHitbox = _pastBot?.transform.Find("HitBox");

        if (CurrentTimewarpMode == TimewarpMode.None)
        {
            // Future is hittable
            if (futureHitbox != null)
                futureHitbox.gameObject.layer = 0;

            if (pastHitbox != null)
                pastHitbox.gameObject.layer = 3;
        }
        else
        {
            // Past is hittable
            if (futureHitbox != null)
                futureHitbox.gameObject.layer = 3;

            if (pastHitbox != null)
                pastHitbox.gameObject.layer = 0;
        }
    }


    // ================= ROUND FLOW =================
    void StartRoundGameplay()
    {
        waitingForPlayerInput = false;

        roundStartUI.Hide();

        RoundRunning = true;

        SetGameplayPause(false);

        if (timerUI) timerUI.Show();

        if (_roundCoroutine != null)
            StopCoroutine(_roundCoroutine);

        _roundCoroutine = StartCoroutine(RoundTick());
    }

    void StartNextRound()
    {

        LM.write($"Start Next Round Index: {currentRoundIndex}");

        if (currentRoundIndex >= rounds.Count)
        {
            EndStudy();
            return;
        }

        currentCondition = rounds[currentRoundIndex];
        currentRoundIndex++;

        ParticipantLogManager.Instance.StartNewRound(currentCondition.id);

        EventLogManager.Instance.StartNewRound(
            ParticipantLogManager.Instance.CurrentRoundFolder
        );

        BindUIIfNeeded();

        LM.write($"[RoundManager] Starting round {currentCondition.id}");

        ApplyCondition(currentCondition);

        ParticipantLogManager.Instance.SaveRoundCondition(
            currentCondition.id,
            _isSeeker ? "Seeker" : "Hider",
            CurrentLatencyMs,
            CurrentTimewarpMode.ToString(),
            roundLengthSeconds
        );

        //RoundRunning = true;
        _timeRemaining = roundLengthSeconds;

        SpawnEnemyForCurrentRole();
        ApplyTimewarpMode();

        StartCoroutine(BindScoreDisplayNextFrame());

        RespawnPlayer();

        OnBotRoleChanged?.Invoke(_isSeeker);
        //OnTimeWarpChanged(IsTimeWarpEnabled);


        //SetGameplayPause(false);
        //if (surveyUI) surveyUI.Hide();
        //if (timerUI) timerUI.Show();

        //if (_roundCoroutine != null)
        //    StopCoroutine(_roundCoroutine);

        //_roundCoroutine = StartCoroutine(RoundTick());

        // Freeze gameplay
        SetGameplayPause(true);

        if (timerUI) timerUI.Hide();
        if (surveyUI) surveyUI.Hide();

        roundStartUI.Show(_isSeeker);
        waitingForPlayerInput = true;

    }

    void ApplyCondition(RoundCondition c)
    {
        _isSeeker = (c.bot == BotRole.Seeker);
        CurrentTimewarpMode =c.timewarp;
        CurrentLatencyMs = LatencyTest ? SimulatedLatencyMs : c.latencyMs;


        LM.write(
            $"[RoundManager] Condition → Bot={c.bot}, Latency={c.latencyMs}, Timewarp={c.timewarp}"
        );
    }

    IEnumerator RoundTick()
    {
        LM.write("[RoundTick] Coroutine started");

        _lastPrintedSecond = Mathf.CeilToInt(_timeRemaining);

        while (_timeRemaining > 0f)
        {
            _timeRemaining -= Time.deltaTime;
            OnTimerTick?.Invoke(TimeRemaining);

            int secondsLeft = Mathf.CeilToInt(_timeRemaining);

            if (secondsLeft != _lastPrintedSecond)
            {
                _lastPrintedSecond = secondsLeft;
            }

            yield return null;
        }

        LM.write("[RoundTick] Timer ended");
        EndRound();
    }

    void EndRound()
    {
        if (!RoundRunning) return;
        RoundRunning = false;

        //SetGameplayPause(true);
        SetGameplayPause(false); 
        Time.timeScale = 1f; 

        if (_futureBot != null)
        {
            Destroy(_futureBot);
            _futureBot = null;
        }

        if (_trueBot != null)
        {
            Destroy(_trueBot);
            _trueBot = null;
        }

        if (_pastBot != null)
        {
            Destroy(_pastBot);
            _pastBot = null;
        }

        GameStatsLogManager.Instance?.EndRoundLogStats();

        if (timerUI) timerUI.Hide();
        if (surveyUI) surveyUI.Show(currentRoundIndex);
    }

    void EndStudy()
    {
        LM.write("[RoundManager] Study complete");
        FlushBuffer();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

    void BindSceneReferences()
    {
        player = GameObject.FindGameObjectWithTag("Player");

        playerSpawnPoint = GameObject.FindGameObjectWithTag("PlayerSpawn")?.transform;

        enemySpawnPoint = GameObject.FindGameObjectWithTag("EnemySpawn")?.transform;

        timerUI = FindFirstObjectByType<TimerUI>(FindObjectsInactive.Include);
        surveyUI = FindFirstObjectByType<SurveyUI>(FindObjectsInactive.Include);
    }

    IEnumerator DelayedBindAndStart()
    {
        yield return null; 

        BindUIIfNeeded();
        BindSceneReferences();

        RoundRunning = false;
        waitingToStartRound = false;
        StartNextRound();
    }


    void SpawnEnemyForCurrentRole()
    {
        // Cleanup old bots
        if (_futureBot) Destroy(_futureBot);
        if (_trueBot) Destroy(_trueBot);
        if (_pastBot) Destroy(_pastBot);

        if (_isSeeker)
        {
            // Enemy is Seeker, Player is Hider
            _futureBot = Instantiate(
                seekerBotPrefab,
                enemySpawnPoint.position,
                enemySpawnPoint.rotation
            );

            SetVisualsVisible(_futureBot.transform, true);

        }
        else
        {

            // Enemy is Hider, Player is Seeker
            _futureBot = Instantiate(
                hiderBotPrefab,
                enemySpawnPoint.position,
                enemySpawnPoint.rotation
            );

            AssignPeekNodes(_futureBot);

            SpawnHiderTimelineBots(_futureBot);

            SetVisualsVisible(_futureBot.transform, false);

        }
        //if (_isSeeker)
        //    AssignPeekNodes(_currentEnemy);
    }
    IEnumerator BindScoreDisplayNextFrame()
    {
        yield return null;

        if (scoreDisplay != null)
        {
            //LM.write("Binding score display to enemy");
            scoreDisplay.SetEnemy(_futureBot);
        }
        else
        {
            //LM.write("ScoreDisplay still null after 1 frame");
        }
    }

    void SetVisualsVisible(Transform root, bool visible)
    {
        if (!root) return;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = visible;
            LM.write($"Renderer {r.name} enabled = {r.enabled}");
        }
        
        foreach (var p in root.GetComponentsInChildren<Projector>(true))
            p.enabled = visible;

        // Disable health bar component (if exists)
        //var healthBar = root.GetComponentInChildren<WorldspaceHealthBar>(true);
        //if (healthBar != null)
        //    healthBar.setHealthVisibility(visible);

        //foreach (var c in root.GetComponentsInChildren<Canvas>(true))
        //    c.enabled = visible;


        // Disable any world-space canvases
        foreach (var c in root.GetComponentsInChildren<Canvas>(true))
            c.enabled = visible;
    }


    void SpawnHiderTimelineBots(GameObject futureHider)
    {
        Vector3 pos = futureHider.transform.position;
        Quaternion rot = futureHider.transform.rotation;

        // Spawn TRUE bot (follows Future)
        _trueBot = Instantiate(hiderTrueBotPrefab, pos, rot);

        var trueDelayed = _trueBot.GetComponent<BotDelayed>();
        if (trueDelayed != null)
        {
            trueDelayed.firstBot = futureHider.transform;
            trueDelayed.SetLatency(CurrentLatencyMs);
        }

        // Spawn PAST bot (follows True)
        _pastBot = Instantiate(hiderPastBotPrefab, pos, rot);

        var pastDelayed = _pastBot.GetComponent<BotDelayed>();
        if (pastDelayed != null)
        {
            pastDelayed.firstBot = _trueBot.transform;
            pastDelayed.SetLatency(CurrentLatencyMs);
        }

        Health futureHealth = futureHider.GetComponent<Health>();
        Health serverHealth = _trueBot.GetComponent<Health>();
        Health pastHealth = _pastBot.GetComponent<Health>();

        AssignHealthProxy(futureHider, pastHealth, serverHealth, futureHealth);
        AssignHealthProxy(_pastBot, pastHealth, serverHealth, futureHealth);

        LM.write("Health wired: "
            + $"Past={pastHealth.gameObject.name}, "
            + $"Server={serverHealth.gameObject.name}, "
            + $"Future={futureHealth.gameObject.name}");
    }

    void AssignHealthProxy(
    GameObject bot,
    Health past,
    Health server,
    Health future)
{
    var proxy = bot.GetComponent<BotHealthProxy>();
    if (proxy == null)
    {
        LM.write($"No BotHealthProxy on {bot.name}");
        return;
    }

    proxy.pastHealth   = past;
    proxy.serverHealth = server;
    proxy.futureHealth = future;
}


    void AssignPeekNodes(GameObject hider)
    {
        // Find ANY component on this object that has a "peekNodes" field
        var behaviours = hider.GetComponents<MonoBehaviour>();

        foreach (var b in behaviours)
        {
            var field = b.GetType().GetField(
                "PeekNodes",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

            if (field != null)
            {
                GameObject[] nodes = GameObject.FindGameObjectsWithTag("PeekNode");

                Transform[] transforms = new Transform[nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                    transforms[i] = nodes[i].transform;

                field.SetValue(b, transforms);

                // Wake up the AI (optional but recommended)
                b.SendMessage("SetPeekNodes", transforms, SendMessageOptions.DontRequireReceiver);

                LM.write("[RoundManager] Peek nodes injected into hider.");
                return;
            }
        }

        LM.write("[RoundManager] No peekNodes field found on hider.");
    }

    public void RespawnPlayer()
    {
        if (!playerSpawnPoint) return;

        var controller = player.GetComponent<CharacterController>();
        if (controller) controller.enabled = false;

        player.transform.SetPositionAndRotation(
            playerSpawnPoint.position,
            playerSpawnPoint.rotation
        );

        //player.ResetVelocity();

        if (controller) controller.enabled = true;

        ResetPlayerHealth();

        if (IsSeeker) {
            var playerLatency = player.GetComponentInChildren<PlayerLatency>();
            if (playerLatency != null)
            {
                playerLatency.latency = CurrentLatencyMs / 1000f;
                LM.write($"[RoundManager] Player latency set to {CurrentLatencyMs} ms");
            }
            else
            {
                LM.write("[RoundManager] PlayerLatency component not found");
            }
        } 
        else
        {
            var playerLatency = player.GetComponentInChildren<PlayerLatency>();
            if (playerLatency != null)
            {
                playerLatency.latency = 0;
                LM.write($"[RoundManager] Player latency set to {CurrentLatencyMs} ms");
            }
            else
            {
                LM.write("[RoundManager] PlayerLatency component not found");
            }
        }
    }



    void ResetPlayerHealth()
    {
        var health = player.GetComponent<Unity.FPS.Game.Health>();
        if (!health)
        {
            LM.write("[RoundManager] Player has no Health component.");
            return;
        }

        // Heal up to full
        health.Heal(health.MaxHealth);
    }

    //void LogRoleChange()
    //{
    //    string role = _isSeeker ? "Seeker" : "Hider";
    //    File.AppendAllText(
    //        _roleLogPath,
    //        $"{DateTime.Now:o},{CurrentRound},{TimeRemaining:F2},{role}\n"
    //    );
    //}

    public void RespawnPlayerAfterDeath()
    {
        LM.write("[RoundManager] Player died -> respawning");

        RespawnPlayer();
        SpawnEnemyForCurrentRole();
    }

    public void RespawnBotAfterDeath()
    {
        LM.write("[RoundManager] Bot died -> respawning");

        RespawnPlayer();
        SpawnEnemyForCurrentRole();

    }

    // ---------------- SCENE / UI ----------------
    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;

        var systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        Debug.Log($"[DEBUG] EventSystem count = {systems.Length}");

        foreach (var es in systems)
            Debug.Log($"[DEBUG] EventSystem: {es.gameObject.name}, active={es.gameObject.activeInHierarchy}");


        if (systems.Length > 1)
        {
            for (int i = 1; i < systems.Length; i++)
                Destroy(systems[i].gameObject);
        }
        if (scene.name != "MainScene") return;
        if (waitingToStartRound) return;

        waitingToStartRound = true;
        StartCoroutine(DelayedBindAndStart());
    }

    void BindUIIfNeeded()
    {
        if (!timerUI)
            timerUI = FindFirstObjectByType<TimerUI>(FindObjectsInactive.Include);

        if (!surveyUI)
            surveyUI = FindFirstObjectByType<SurveyUI>(FindObjectsInactive.Include);

        if (!roundStartUI)
            roundStartUI = FindFirstObjectByType<RoundStartUI>(FindObjectsInactive.Include);
    }

    // ---------------- PAUSE / LOGGING ----------------
    void SetGameplayPause(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;

        if (disableWhilePaused != null)
        {
            foreach (var b in disableWhilePaused)
                if (b) b.enabled = !paused;
        }

        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    //public void SubmitSurvey(SurveyData data)
    //{
    //    _buffer.Add(data);
    //    CurrentRound += 1;

    //    //Time.timeScale = 1f;

    //    //SceneManager.LoadScene("MainScene");
    //    //StartNextRound();
    //}
    public void SubmitSurvey(SurveyData data)
    {
        string surveyFolder = Path.Combine(
            ParticipantLogManager.Instance.CurrentRoundFolder,
            "survey"
        );

        string surveyPath = Path.Combine(surveyFolder, "survey.csv");

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("timestamp,round,smoothness,responsiveness,fairness,qoe,fun,notes");

        string notes = (data.notes ?? "").Replace(",", ";");

        sb.AppendLine(
            $"{DateTime.Now:o},{data.round},{data.smoothness}," +
            $"{data.responsiveness},{data.fairness}," +
            $"{data.qoe},{data.fun},{notes}"
        );

        File.WriteAllText(surveyPath, sb.ToString(), Encoding.UTF8);

        CurrentRound += 1;
    }

    public void ExitGame()
    {
        FlushBuffer();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void FlushBuffer()
    {
        if (_buffer.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine("timestamp,round,smoothness,responsiveness,fairness,qoe,fun,notes");

        foreach (var d in _buffer)
        {
            var notes = (d.notes ?? "").Replace(",", ";");
            sb.AppendLine(
                $"{DateTime.Now:o},{d.round},{d.smoothness},{d.responsiveness}," +
                $"{d.fairness},{d.qoe},{d.fun},{notes}");
        }

        //File.WriteAllText(_surveyCsvPath, sb.ToString(), Encoding.UTF8);
        _buffer.Clear();
    }

    static string GetProjectLogsPath()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string dir = Path.Combine(root, "Logs", "SurveyLogs");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return dir;
    }
}

[Serializable]
public struct SurveyData
{
    public int round;
    public float smoothness;
    public float responsiveness;
    public float fairness;
    public float qoe;
    public float fun;
    public string notes;
}
