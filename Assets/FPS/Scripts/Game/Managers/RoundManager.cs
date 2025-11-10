using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Round Settings")]
    [Tooltip("Length of each round (seconds).")]
    public float roundLengthSeconds = 60f;

    [Tooltip("Components to disable while the survey is up (movement, shooting, etc.).")]
    public Behaviour[] disableWhilePaused;

    [Header("UI References")]
    public TimerUI timerUI;
    public SurveyUI surveyUI;

    public int CurrentRound { get; private set; } = 1;
    public bool RoundRunning { get; private set; }
    public float TimeRemaining => Mathf.Max(0f, _timeRemaining);

    public event Action<float> OnTimerTick;


    // ---- internals ----
    float _timeRemaining;
    readonly List<SurveyData> _buffer = new();   // store all round results in memory
    string _csvPath;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);           // safe even if you stay in one scene

        // Write once at the end: <ProjectRoot>/Logs/SurveyLogs/survey_log_*.csv
        var logsDir = GetProjectLogsPath();
        var file = $"survey_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
        _csvPath = Path.Combine(logsDir, file);
    }

    void Start() => StartRound();
    void OnApplicationQuit() => FlushBuffer();

    void LateUpdate()
    {
        // Ensure cursor is usable while the survey is visible
        if (!RoundRunning) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    // ---------------- Round flow ----------------
    public void StartRound()
    {
        RoundRunning = true;
        _timeRemaining = roundLengthSeconds;

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
            OnTimerTick?.Invoke(TimeRemaining);   // <- notify TimerUI
            yield return null;
        }
        EndRound();
    }

    public void EndRound()
    {
        if (!RoundRunning) return;
        RoundRunning = false;

        SetGameplayPause(true);
        if (timerUI) timerUI.Hide();
        if (surveyUI) surveyUI.Show(CurrentRound);   // survey lives in MainScene
    }

    // Called by SurveyUI → buffer row, advance round, restart timer
    public void SubmitSurvey(SurveyData data)
    {
        _buffer.Add(data);         // no disk write yet
        CurrentRound += 1;
        StartRound();
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

    // --------------- helpers ---------------
    void SetGameplayPause(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;

        if (disableWhilePaused != null)
            foreach (var b in disableWhilePaused)
                if (b) b.enabled = !paused;

        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    // One write at the very end
    public void FlushBuffer()
    {
        if (_buffer.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine("timestamp,round,smoothness,responsiveness,fairness,qoe,fun,notes");

        foreach (var d in _buffer)
        {
            var notes = (d.notes ?? "").Replace(",", ";");
            sb.AppendLine($"{DateTime.Now:o},{d.round},{d.smoothness},{d.responsiveness},{d.fairness},{d.qoe},{d.fun},{notes}");
        }

        File.WriteAllText(_csvPath, sb.ToString(), Encoding.UTF8);
        Debug.Log($"[RoundManager] Wrote {_buffer.Count} survey rows → {_csvPath}");
        _buffer.Clear();
    }

    // <ProjectRoot>/Logs/SurveyLogs
    static string GetProjectLogsPath()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var logsDir = Path.Combine(projectRoot, "Logs", "SurveyLogs");
        if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
        return logsDir;
    }
}

// ---------------- Data ----------------
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

