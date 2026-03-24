//using Codice.CM.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.FPS.Game;
//using Unity.FPS.Gameplay;
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
    public TimewarpMode TimeWarpmode = TimewarpMode.None;
    public bool BotIsSeeker = true;

    // ================= SPEED & WEAPON =================
    [Header("Weapon and Speed Setting")]
    public float CurrentSpeed { get; private set; }
    public float TestSpeed = 10;
    public int CurrentWeaponIndex { get; private set; }
    public int TestWeapon = 0;

    public static System.Action<int> OnRoundWeaponChanged;


    [Header("Study Config")]
    [SerializeField] private TextAsset roundConditionFile;

    private List<RoundCondition> rounds;
    public int currentRoundIndex = 0;
    public int CurrentRoundIndex => currentRoundIndex;

    private RoundCondition currentCondition;
    public int CurrentRoundID => currentCondition.id;

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
    public GameObject SeekerProjectilePrefab;

    [SerializeField] private Transform[] botSpawnPoints;
    Transform enemySpawnPoint;

    GameObject _futureBot;
    GameObject _trueBot;
    GameObject _pastBot;

    float lastEnemySpeed = 0f;

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
    public int ParticipantID { get; private set; }
    public int CurrentLatinRow { get; private set; }


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

        // Get true participant ID (keeps increasing)
        int participantID = GetAndIncrementParticipantID();
        ParticipantID = participantID;

        // Compute Latin row (cycles)
        CurrentLatinRow = participantID % latinOrders.Count;

        // Initialize participant in log manager
        ParticipantLogManager.Instance.InitializeParticipant(participantID);

        SummaryLogManager.Instance.Init(
            ParticipantLogManager.Instance.ParticipantFolder,
            participantID
        );

        //List<int> orderRow = latinOrders[participantID];
        List<int> orderRow = latinOrders[CurrentLatinRow];

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
        orderLog.Append($"[RoundManager] Participant {participantID} order: ");

        for (int i = 0; i < rounds.Count; i++)
        {
            orderLog.Append(rounds[i].id);

            if (i < rounds.Count - 1)
                orderLog.Append(", ");
        }

        LM.write(orderLog.ToString());


        BindUIIfNeeded();
    }

    private int GetAndIncrementParticipantID()
    {
        int id = 0;

        if (File.Exists(_participantCounterPath))
        {
            string content = File.ReadAllText(_participantCounterPath);
            int.TryParse(content, out id);
        }

        File.WriteAllText(_participantCounterPath, (id + 1).ToString());

        return id;
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
        } else
        {
            SummaryLogManager.Instance?.UpdateMovement(player.transform, CurrentEnemy.transform);

            // Check enemy speed for logging
            if (CurrentEnemy != null)
            {
                var agent = CurrentEnemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    lastEnemySpeed = agent.speed;
                }
            }
            // end of check enemy speed
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

    void ApplyTimewarpModeForPlayer()
    {
        if (!player) return;

        
        Transform aimPoint = player.transform.Find("AimPoint");

        if (!aimPoint)
        {
            // Try alternative names
            aimPoint = player.transform.Find("AimTarget")
                    ?? player.transform.Find("Aim");
        }

        if (!aimPoint)
        {
        
            return;
        }

        const int playerLayer = 6;           // Player layer
        const int playerUnhitableLayer = 14; // PlayerUnhitable layer

        if (CurrentTimewarpMode == TimewarpMode.None)
        {
            // No timewarp: hit real player position
            player.layer = playerLayer;
            aimPoint.gameObject.layer = playerUnhitableLayer;

        }
        else
        {
            // Timewarp enabled: hit rewound position (aimpoint)
            player.layer = playerUnhitableLayer;
            aimPoint.gameObject.layer = playerLayer;

        }
        if (SeekerProjectilePrefab != null)
        {
            var comps = SeekerProjectilePrefab.GetComponents<Component>();
            Component ps = null;

            foreach (var c in comps)
            {
                if (c != null && c.GetType().Name == "ProjectileStandard")
                {
                    ps = c;
                    break;
                }
            }

            if (ps != null)
            {
                bool enableReject = (CurrentTimewarpMode == TimewarpMode.Conditional);
                var field = ps.GetType().GetField("UseRealPositionReject");
                if (field != null)
                {
                    field.SetValue(ps, enableReject);
                    Debug.Log($"[RoundManager] Set ProjectileStandard.UseRealPositionReject = {enableReject}");
                }
                else
                {
                    Debug.LogWarning("[RoundManager] Field UseRealPositionReject not found on ProjectileStandard");
                }
            }
            else
            {
                Debug.LogWarning("[RoundManager] ProjectileStandard component not found on SeekerProjectilePrefab");
            }
        }
        else
        {
            Debug.LogWarning("[RoundManager] SeekerProjectilePrefab is null");
        }

    }


    void ConfigureEnemyShootingBehavior()
    {
        if (seekerBotPrefab == null) return;

        // Get EnemyController component (using reflection to avoid namespace issues)
        var enemyController = seekerBotPrefab.GetComponent("EnemyController") as MonoBehaviour;

        if (enemyController == null)
        {
            Debug.Log("[RoundManager] EnemyController not found on enemy bot");
            return;
        }

        var type = enemyController.GetType();

        // Configure based on TimewarpMode
        switch (CurrentTimewarpMode)
        {
            case TimewarpMode.None:
                // No timewarp: shoot at current position (no prediction)
                SetField(type, enemyController, "UseBlendAim", true);
                SetField(type, enemyController, "BlendToFuture", 0.6f);
                SetField(type, enemyController, "BlendRadius", 0.15f);
                //Debug.Log("[RoundManager] Enemy shooting: No prediction (Mode: None)");
                break;

            case TimewarpMode.Normal:
                // Normal timewarp: moderate prediction
                SetField(type, enemyController, "UseBlendAim", true);
                SetField(type, enemyController, "BlendToFuture", 0.1f);
                SetField(type, enemyController, "BlendRadius", 0.15f);
                //Debug.Log("[RoundManager] Enemy shooting: Moderate prediction (Mode: Normal)");
                break;

            case TimewarpMode.Conditional:
                // Conditional timewarp: high prediction
                SetField(type, enemyController, "UseBlendAim", true);
                SetField(type, enemyController, "BlendToFuture", 0.1f);
                SetField(type, enemyController, "BlendRadius", 0.15f);
                //Debug.Log("[RoundManager] Enemy shooting: High prediction (Mode: Conditional)");
                break;
        }
    }

    void SetField(System.Type type, object obj, string fieldName, object value)
    {
        var field = type.GetField(fieldName,
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(obj, value);
        }
        else
        {
            Debug.LogWarning($"[RoundManager] Field '{fieldName}' not found on EnemyController");
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

        ParticipantLogManager.Instance.StartNewRound(currentRoundIndex);

        EventLogManager.Instance.StartNewRound(
            ParticipantLogManager.Instance.CurrentRoundFolder
        );

        BindUIIfNeeded();

        LM.write($"[RoundManager] Starting round {currentCondition.id}");

        ApplyCondition(currentCondition);


        EventLogManager.Instance.SetRoundContext(
            ParticipantID,
            CurrentLatinRow,
            currentRoundIndex,
            CurrentRoundID,
            CurrentLatencyMs,
            _isSeeker ? "Hider" : "Seeker",
            CurrentTimewarpMode.ToString(),
            CurrentSpeed,
            CurrentWeaponIndex
        );

        SummaryLogManager.Instance.SetRoundContext(
            ParticipantID,
            CurrentLatinRow,
            currentRoundIndex,
            CurrentRoundID,
            CurrentLatencyMs,
            _isSeeker ? "Hider" : "Seeker",
            CurrentTimewarpMode.ToString(),
            System.DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            CurrentSpeed,
            CurrentWeaponIndex
        );

        ParticipantLogManager.Instance.SaveRoundCondition(
            currentCondition.id,
            _isSeeker ? "Hider" : "Seeker",
            CurrentLatencyMs,
            CurrentTimewarpMode.ToString(),
            roundLengthSeconds
        );

        //RoundRunning = true;
        _timeRemaining = roundLengthSeconds;

        SpawnEnemyForCurrentRole();
        ApplyTimewarpMode();
        ConfigureEnemyShootingBehavior();
        StartCoroutine(BindScoreDisplayNextFrame());

        RespawnPlayer();
        ApplyTimewarpModeForPlayer();
        ApplyPlayerSpeed();

        OnRoundWeaponChanged?.Invoke(CurrentWeaponIndex);

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

        SummaryLogManager.Instance.StartRound(player.transform, CurrentEnemy.transform);

    }

    void ApplyCondition(RoundCondition c)
    {
        _isSeeker = LatencyTest ? BotIsSeeker : (c.bot == BotRole.Seeker);
        CurrentTimewarpMode = LatencyTest ? TimeWarpmode : c.timewarp;
        CurrentLatencyMs = LatencyTest ? SimulatedLatencyMs : c.latencyMs;
        CurrentSpeed = LatencyTest ? TestSpeed : c.speed;
        CurrentWeaponIndex = LatencyTest ? TestWeapon : c.weapon;

        LM.write(
            $"[RoundManager] Condition → Bot={_isSeeker}, Latency={CurrentTimewarpMode}, Timewarp={CurrentLatencyMs}, Speed = {CurrentSpeed}, Weapon = {CurrentWeaponIndex}"
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

        // --- destroy bots ---
        if (_futureBot != null) { Destroy(_futureBot); _futureBot = null; }
        if (_trueBot != null) { Destroy(_trueBot); _trueBot = null; }
        if (_pastBot != null) { Destroy(_pastBot); _pastBot = null; }

        if (timerUI) timerUI.Hide();
        if (surveyUI) surveyUI.Show(currentRoundIndex);

        // end round
        GameStatsLogManager.Instance?.EndRoundLogStats();
        // Log Summary
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


        GameObject[] spawnObjects = GameObject.FindGameObjectsWithTag("EnemySpawn");
        botSpawnPoints = new Transform[spawnObjects.Length];

        for (int i = 0; i < spawnObjects.Length; i++)
        {
            botSpawnPoints[i] = spawnObjects[i].transform;
        }

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

    void ApplyPlayerSpeed()
    {
        if (player == null) return;

        var controller = player.GetComponent("PlayerCharacterController");
        if (controller != null)
        {
            var field = controller.GetType().GetField("MaxSpeedOnGround");
            if (field != null)
            {
                field.SetValue(controller, CurrentSpeed);
            }
        }
    }

    void ApplyEnemySpeed(GameObject enemy)
    {
        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = currentCondition.speed;
        }
    }


    void SpawnEnemyForCurrentRole()
    {
        // Cleanup old bots
        if (_futureBot) Destroy(_futureBot);
        if (_trueBot) Destroy(_trueBot);
        if (_pastBot) Destroy(_pastBot);

        int randomIndex = UnityEngine.Random.Range(0, botSpawnPoints.Length);
        enemySpawnPoint = botSpawnPoints[randomIndex];

        if (_isSeeker)
        {
            // Enemy is Seeker, Player is Hider
            _futureBot = Instantiate(
                seekerBotPrefab,
                enemySpawnPoint.position,
                enemySpawnPoint.rotation
            );

            ApplyEnemySpeed(_futureBot);

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

            ApplyEnemySpeed(_futureBot);

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

    public void RespawnAfterDeath()
    {
        LM.write("[RoundManager] Player died -> respawning");

        SummaryLogManager.Instance.OnKilled();

        RespawnPlayer();
        SpawnEnemyForCurrentRole();

        // Score
        StartCoroutine(BindScoreDisplayNextFrame());
    }

    // ---------------- SCENE / UI ----------------
    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;

        var systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        //Debug.Log($"[DEBUG] EventSystem count = {systems.Length}");

        //foreach (var es in systems)
        //    Debug.Log($"[DEBUG] EventSystem: {es.gameObject.name}, active={es.gameObject.activeInHierarchy}");


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
    public void SubmitSurvey(SurveyData data)
    {
        //// Survey log
        string surveyFolder = ParticipantLogManager.Instance.CurrentRoundFolder;
        string surveyPath = Path.Combine(surveyFolder, "survey.csv");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("timestamp,round,lag,hider,seeker");
        sb.AppendLine(
            $"{DateTime.Now:o},{data.round},{data.lag}," +
            $"{data.hider},{data.seeker}"
        );
        //File.WriteAllText(surveyPath, sb.ToString(), Encoding.UTF8);
        ////// End of survey log

        ////// Summary log

        // ---------------- Survey ----------------
        float q1 = data.lag;
        float q2 = IsSeeker
            ? data.hider   // player is hider
            : data.seeker; // player is seeker
        SummaryLogManager.Instance.SetSurvey(q1, q2);

        // ---------------- Score ----------------
        int finalScore = ScoreProvider.Instance != null? ScoreProvider.Instance.GetScore() : 0;
        //Debug.Log("ScoreProvider: " + ScoreProvider.Instance);
        Debug.Log("ScoreFinal: " + finalScore);

        // ---------------- Player Speed ----------------
        float playerSpeed = 0f;
        var player = GameObject.FindWithTag("Player");

        if (player != null)
        {
            var controller = player.GetComponent("PlayerCharacterController");
            if (controller != null)
            {
                var field = controller.GetType().GetField("MaxSpeedOnGround");
                if (field != null)
                {
                    playerSpeed = (float)field.GetValue(controller);
                }
            }
        }
        // ---------------- Enemy Speed ----------------
        float enemySpeed = lastEnemySpeed;

        SummaryLogManager.Instance.LogRound(
            //currentCondition.id,
            //currentRoundIndex,
            //CurrentLatencyMs,
            //_isSeeker ? "Hider" : "Seeker",
            //CurrentTimewarpMode.ToString(),
            finalScore, 
            enemySpeed, 
            playerSpeed, 
               //GameStatsLogManager.Instance.TotalShots,
               //GameStatsLogManager.Instance.TotalHits,
            0, // error angle
               //GameStatsLogManager.Instance.Accuracy,
            0, // corner shots
            0, // ttk
               //0, // tth
               //0, // q1
               //0, // q2
            SummaryLogManager.Instance.GetPlayerDistance(), // player distance
            SummaryLogManager.Instance.GetBotDistance(), // bot distance
            SummaryLogManager.Instance.GetMouseMovement()  // mouse move
        );
        ////// end of Summary log

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
        sb.AppendLine("timestamp,round,lag,hider,seeker");

        foreach (var d in _buffer)
        {

            sb.AppendLine(
                $"{DateTime.Now:o},{d.round},{d.lag},{d.hider}," +
                $"{d.seeker}");
        }

        //File.WriteAllText(_surveyCsvPath, sb.ToString(), Encoding.UTF8);
        _buffer.Clear();
    }
}

[Serializable]
public struct SurveyData
{
    public int round;
    public float lag;
    public float hider;
    public float seeker;
}
