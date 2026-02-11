using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.FPS.Game;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Round Settings")]
    public float roundLengthSeconds = 60f;
    public Behaviour[] disableWhilePaused;

    // ================= LATENCY =================
    //[Header("Latency Settings")]
    //public float[] latencyOptionsMs = { 0f, 50f, 100f, 150f, 200f, 500f, 1000f };
    public float CurrentLatencyMs { get; private set; }
    //public float CurrentLatencyMs = 0f;

    [Header("Study Config")]
    [SerializeField] private TextAsset roundConditionFile;

    private List<RoundCondition> rounds;
    public int currentRoundIndex = 3;
    private RoundCondition currentCondition;

    // ================= TIME WARP =================
    [Header("Time Warp Settings")]
    public bool randomizeTimeWarp = false;
    public bool defaultTimeWarpEnabled = true;
    public bool IsTimeWarpEnabled { get; private set; }
    //public event Action<bool> OnTimeWarpChanged;

    // ================= UI =================
    [Header("UI References")]
    public TimerUI timerUI;
    public SurveyUI surveyUI;

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

    public event Action<bool> OnPlayerRoleChanged;
    public event Action<float> OnTimerTick;

    float _timeRemaining;
    bool _isSeeker;

    bool waitingToStartRound = false;
    int _lastPrintedSecond = -1;


    // ================= LOGGING =================
    readonly List<SurveyData> _buffer = new();
    string _surveyCsvPath;
    string _roleLogPath;


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

        string logsDir = GetProjectLogsPath();

        _surveyCsvPath = Path.Combine(
            logsDir,
            $"survey_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
        );

        _roleLogPath = Path.Combine(
            logsDir,
            $"role_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"
        );

        File.WriteAllText(_surveyCsvPath,
            "timestamp,round,smoothness,responsiveness,fairness,qoe,fun,notes\n");

        File.WriteAllText(_roleLogPath,
            "timestamp,round,timeRemaining,role\n");
    }

    void Start()
    {
        //BindUIIfNeeded();
        //StartRound();

        rounds = RoundConfigLoader.Load(roundConditionFile);

        LM.write($"[RoundManager] Loaded {rounds.Count} round conditions");

        BindUIIfNeeded();
        //StartNextRound();
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

    void PickLatencyForRound()
    {
        //int index = UnityEngine.Random.Range(0, latencyOptionsMs.Length);
        //CurrentLatencyMs = latencyOptionsMs[index];

        //LM.write($"[RoundManager] Latency this round: {CurrentLatencyMs} ms");
    }

    void PickTimeWarpForRound()
    {
        //IsTimeWarpEnabled = defaultTimeWarpEnabled;
            //randomizeTimeWarp
            //? UnityEngine.Random.value > 0.5f
            //: defaultTimeWarpEnabled;

        //LM.write($"[RoundManager] TimeWarp: {IsTimeWarpEnabled}");
        
    }

    void OnTimeWarpChanged(bool enabled)
    {
        if (_isSeeker) { return; }
        BotHealthProxy delayedBotHealthProxy = _pastBot.GetComponent<BotHealthProxy>();
        if (delayedBotHealthProxy)
        {
            delayedBotHealthProxy.PropagateBackwards(enabled);
            delayedBotHealthProxy.SetLatency(CurrentLatencyMs);
        }

        delayedBotHealthProxy = _futureBot.GetComponent<BotHealthProxy>();
        if (delayedBotHealthProxy)
        {
            //delayedBotHealthProxy.PropagateBackwards(enabled);
            delayedBotHealthProxy.SetLatency(CurrentLatencyMs);
        }

        if (enabled)
        {
            Transform hitboxTransform = _pastBot.transform.Find("HitBox");
            //LM.write(hitboxTransform);
            if (hitboxTransform != null)
            {
                hitboxTransform.gameObject.layer = 0;
            }

            hitboxTransform = _futureBot.transform.Find("HitBox");
            if (hitboxTransform != null)
            {
                hitboxTransform.gameObject.layer = 3;
            }
        }
        else
        {
            Transform hitboxTransform = _pastBot.transform.Find("HitBox");
            if (hitboxTransform != null)
            {
                hitboxTransform.gameObject.layer = 3;
            }

            hitboxTransform = _futureBot.transform.Find("HitBox");
            if (hitboxTransform != null)
            {
                hitboxTransform.gameObject.layer = 0;
            }
        }
    }

    // ================= ROUND FLOW =================
    void StartNextRound()
    {
        if (currentRoundIndex >= rounds.Count)
        {
            EndStudy();
            return;
        }

        currentCondition = rounds[currentRoundIndex];
        currentRoundIndex++;

        BindUIIfNeeded();

        LM.write($"[RoundManager] Starting round {currentCondition.id}");

        ApplyCondition(currentCondition);

        RoundRunning = true;
        _timeRemaining = roundLengthSeconds;

        SpawnEnemyForCurrentRole();
        RespawnPlayer();

        OnPlayerRoleChanged?.Invoke(_isSeeker);

        OnTimeWarpChanged(IsTimeWarpEnabled);

        SetGameplayPause(false);
        if (surveyUI) surveyUI.Hide();
        if (timerUI) timerUI.Show();

        StopAllCoroutines();

        LM.write("StartNext Round: before start coroutine");
        StartCoroutine(RoundTick());
    }

    void ApplyCondition(RoundCondition c)
    {
        _isSeeker = (c.player == PlayerRole.Seeker);
        IsTimeWarpEnabled = c.timewarp != TimewarpMode.None;
        CurrentLatencyMs = c.latencyMs;


        LM.write(
            $"[RoundManager] Condition → Bot={c.player}, Latency={c.latencyMs}, Timewarp={c.timewarp}"
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
                LM.write($"[Timer] {secondsLeft} s remaining");
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

        SetGameplayPause(true);

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

        if (timerUI) timerUI.Hide();
        if (surveyUI) surveyUI.Show(currentCondition.id);
    }

    void EndStudy()
    {
        LM.write("[RoundManager] Study complete");
        FlushBuffer();
    }


    //// ---------------- ROUND FLOW ----------------
    //public void StartRound()
    //{
    //    if (RoundRunning) return;

    //    BindUIIfNeeded();

    //    RoundRunning = true;
    //    _timeRemaining = roundLengthSeconds;

    //    PickLatencyForRound();
    //    PickTimeWarpForRound();

    //    _isSeeker = true;
    //        //UnityEngine.Random.value > 0.5f;
    //    //_roleSwitchTimer = GetNextRoleInterval();

    //    //LogRoleChange();

    //    SpawnEnemyForCurrentRole();
    //    RespawnPlayer();

    //    OnPlayerRoleChanged?.Invoke(_isSeeker);

    //    SetGameplayPause(false);
    //    if (surveyUI) surveyUI.Hide();
    //    if (timerUI) timerUI.Show();

    //    OnTimeWarpChanged(defaultTimeWarpEnabled);

    //    StopAllCoroutines();
    //    StartCoroutine(RoundTick());
    //}

    //IEnumerator RoundTick()
    //{
    //    while (_timeRemaining > 0f)
    //    {
    //        _timeRemaining -= Time.deltaTime;
    //        //HandleRoleTimer();
    //        OnTimerTick?.Invoke(TimeRemaining);
    //        yield return null;
    //    }

    //    EndRound();
    //}

    //public void EndRound()
    //{
    //    if (!RoundRunning) return;
    //    RoundRunning = false;

    //    SetGameplayPause(true);

    //    if (_futureBot != null)
    //    {
    //        Destroy(_futureBot);
    //        _futureBot = null;
    //    }

    //    if (timerUI) timerUI.Hide();
    //    if (surveyUI) surveyUI.Show(CurrentRound);
    //}

    //public void EndRoundOnPlayerDeath()
    //{
    //    if (!RoundRunning) return;
    //    RoundRunning = false;

    //    SetGameplayPause(true);

    //    if (_futureBot != null)
    //    {
    //        Destroy(_futureBot);
    //        _futureBot = null;
    //    }

    //    if (timerUI) timerUI.Hide();
    //    if (surveyUI) surveyUI.Show(CurrentRound);
    //}

    void BindSceneReferences()
    {
        // Player
        if (!player)
            player = GameObject.FindGameObjectWithTag("Player");

        // Player spawn
        if (!playerSpawnPoint)
            playerSpawnPoint = GameObject.FindGameObjectWithTag("PlayerSpawn")?.transform;

        // Enemy spawn
        if (!enemySpawnPoint)
            enemySpawnPoint = GameObject.FindGameObjectWithTag("EnemySpawn")?.transform;
    }

    IEnumerator DelayedBindAndStart()
    {
        yield return null; // wait one frame so scene objects exist

        BindUIIfNeeded();
        BindSceneReferences();

        RoundRunning = false;
        StartNextRound();
    }


    // ---------------- ROLE SWITCHING ----------------
    //void HandleRoleTimer()
    //{
    //    _roleSwitchTimer -= Time.deltaTime;
    //    if (_roleSwitchTimer <= 0f)
    //    {
    //        _isSeeker = !_isSeeker;
    //        LogRoleChange();
    //        SpawnEnemyForCurrentRole();
    //        RespawnPlayer();
    //        OnPlayerRoleChanged?.Invoke(_isSeeker);
    //        _roleSwitchTimer = GetNextRoleInterval();
    //    }
    //}

    //float GetNextRoleInterval()
    //{
    //    float min = Mathf.Max(10f, minRoleSwitchSeconds);
    //    float max = Mathf.Max(min + 0.01f, maxRoleSwitchSeconds);
    //    return UnityEngine.Random.Range(min, max);
    //}

    void SpawnEnemyForCurrentRole()
    {
        // Cleanup old bots
        if (_futureBot) Destroy(_futureBot);
        if (_trueBot) Destroy(_trueBot);
        if (_pastBot) Destroy(_pastBot);

        //GameObject prefab = _isSeeker ? seekerBotPrefab : hiderBotPrefab;
        //_currentEnemy = Instantiate(prefab, enemySpawnPoint.position, enemySpawnPoint.rotation);

        if (_isSeeker)
        {
            // Player is Hider → Enemy is SEEKER
            _futureBot = Instantiate(
                seekerBotPrefab,
                enemySpawnPoint.position,
                enemySpawnPoint.rotation
            );
        }
        else
        {

            // Player is Seeker → Enemy is HIDER
            _futureBot = Instantiate(
                hiderBotPrefab,
                enemySpawnPoint.position,
                enemySpawnPoint.rotation
            );

            AssignPeekNodes(_futureBot);

            SpawnHiderTimelineBots(_futureBot);
        }

        //if (_isSeeker)
        //    AssignPeekNodes(_currentEnemy);
    }

    void SpawnHiderTimelineBots(GameObject futureHider)
    {
        Vector3 pos = futureHider.transform.position;
        Quaternion rot = futureHider.transform.rotation;

        LM.write("RoundManager: " + CurrentLatencyMs);

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
        AssignHealthProxy(_trueBot, pastHealth, serverHealth, futureHealth);
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



    void LogRoleChange()
    {
        string role = _isSeeker ? "Seeker" : "Hider";
        File.AppendAllText(
            _roleLogPath,
            $"{DateTime.Now:o},{CurrentRound},{TimeRemaining:F2},{role}\n"
        );
    }


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

    public void SubmitSurvey(SurveyData data)
    {
        _buffer.Add(data);
        CurrentRound += 1;
        SceneManager.LoadScene("MainScene");
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

        File.WriteAllText(_surveyCsvPath, sb.ToString(), Encoding.UTF8);
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
