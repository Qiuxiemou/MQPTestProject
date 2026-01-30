using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    // ---------------- ROUND SETTINGS ----------------
    [Header("Round Settings")]
    [Tooltip("Length of each round (seconds).")]
    public float roundLengthSeconds = 60f;

    [Tooltip("Components to disable while the survey is up (movement, shooting, etc.).")]
    public Behaviour[] disableWhilePaused;

    // ---------------- UI ----------------
    [Header("UI References")]
    public TimerUI timerUI;
    public SurveyUI surveyUI;

    // ---------------- ROLE SWITCHING ----------------
    [Header("Role Switching (Prototype)")]
    [Tooltip("Minimum seconds before a role switch is allowed.")]
    public float minRoleSwitchSeconds = 10f;

    [Tooltip("Maximum seconds before a role switch.")]
    public float maxRoleSwitchSeconds = 25f;

    // ---------------- ENEMY PREFABS ----------------
    [Header("Enemy Prefabs")]
    public GameObject hiderBotPrefab;
    public GameObject seekerBotPrefab;

    [Tooltip("Spawn location for the enemy.")]
    public Transform enemySpawnPoint;

    GameObject _currentEnemy;

    // ---------------- PUBLIC STATE ----------------
    public int CurrentRound { get; private set; } = 1;
    public bool RoundRunning { get; private set; }
    public float TimeRemaining => Mathf.Max(0f, _timeRemaining);
    public bool IsSeeker => _isSeeker;

    public event Action<float> OnTimerTick;

    // ---------------- INTERNALS ----------------
    float _timeRemaining;
    bool _isSeeker;
    float _roleSwitchTimer;

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
        BindUIIfNeeded();
        StartRound();
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

    // ---------------- ROUND FLOW ----------------
    public void StartRound()
    {
        if (RoundRunning) return;

        BindUIIfNeeded();

        RoundRunning = true;
        _timeRemaining = roundLengthSeconds;

        _isSeeker = UnityEngine.Random.value > 0.5f;
        _roleSwitchTimer = GetNextRoleInterval();
        LogRoleChange();

        SpawnEnemyForCurrentRole();

        SetGameplayPause(false);
        if (surveyUI) surveyUI.Hide();
        if (timerUI) timerUI.Show();

        StopAllCoroutines();
        StartCoroutine(RoundTick());
    }

    IEnumerator RoundTick()
    {
        while (_timeRemaining > 0f)
        {
            _timeRemaining -= Time.deltaTime;
            HandleRoleTimer();
            OnTimerTick?.Invoke(TimeRemaining);
            yield return null;
        }

        EndRound();
    }

    public void EndRound()
    {
        if (!RoundRunning) return;
        RoundRunning = false;

        SetGameplayPause(true);

        if (_currentEnemy != null)
        {
            Destroy(_currentEnemy);
            _currentEnemy = null;
        }

        if (timerUI) timerUI.Hide();
        if (surveyUI) surveyUI.Show(CurrentRound);
    }

    public void EndRoundOnPlayerDeath()
    {
        if (!RoundRunning) return;
        RoundRunning = false;

        SetGameplayPause(true);

        if (_currentEnemy != null)
        {
            Destroy(_currentEnemy);
            _currentEnemy = null;
        }

        if (timerUI) timerUI.Hide();
        if (surveyUI) surveyUI.Show(CurrentRound);
    }

    // ---------------- ROLE SWITCHING ----------------
    void HandleRoleTimer()
    {
        _roleSwitchTimer -= Time.deltaTime;
        if (_roleSwitchTimer <= 0f)
        {
            _isSeeker = !_isSeeker;
            LogRoleChange();
            SpawnEnemyForCurrentRole();
            _roleSwitchTimer = GetNextRoleInterval();
        }
    }

    float GetNextRoleInterval()
    {
        float min = Mathf.Max(10f, minRoleSwitchSeconds);
        float max = Mathf.Max(min + 0.01f, maxRoleSwitchSeconds);
        return UnityEngine.Random.Range(min, max);
    }

    void SpawnEnemyForCurrentRole()
    {
        if (_currentEnemy != null)
            Destroy(_currentEnemy);

        GameObject prefab = _isSeeker ? seekerBotPrefab : hiderBotPrefab;
        _currentEnemy = Instantiate(prefab, enemySpawnPoint.position, enemySpawnPoint.rotation);

        if (!_isSeeker)
            AssignPeekNodes(_currentEnemy);
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

                Debug.Log("[RoundManager] Peek nodes injected into hider.");
                return;
            }
        }

        Debug.LogWarning("[RoundManager] No peekNodes field found on hider.");
    }



    void LogRoleChange()
    {
        string role = _isSeeker ? "Seeker" : "Hider";
        File.AppendAllText(
            _roleLogPath,
            $"{DateTime.Now:o},{CurrentRound},{TimeRemaining:F2},{role}\n"
        );
    }

    // ---------------- SCENE / UI ----------------
    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainScene") return;
        StartCoroutine(DelayedBindAndStart());
    }

    IEnumerator DelayedBindAndStart()
    {
        yield return null;
        BindUIIfNeeded();
        RoundRunning = false;
        StartRound();
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
