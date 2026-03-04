using System.IO;
using UnityEngine;
using System.Text;
public class ParticipantLogManager : MonoBehaviour
{
    public static ParticipantLogManager Instance { get; private set; }

    public string ParticipantFolder { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void InitializeParticipant(int participantIndex)
    {
        string logsRoot = GetProjectLogsPath();

        ParticipantFolder = Path.Combine(
            logsRoot,
            $"participant_{participantIndex}"
        );

        Directory.CreateDirectory(ParticipantFolder);

        Debug.Log($"[ParticipantLogManager] Created: {ParticipantFolder}");
    }

    string GetProjectLogsPath()
    {
        string root = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..")
        );

        string dir = Path.Combine(root, "Logs");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return dir;
    }

    public string CurrentRoundFolder { get; private set; }
    public void StartNewRound(int roundNumber)
    {
        CurrentRoundFolder = Path.Combine(
            ParticipantFolder,
            $"round_{roundNumber}"
        );

        Directory.CreateDirectory(CurrentRoundFolder);

        Directory.CreateDirectory(Path.Combine(CurrentRoundFolder, "condition"));
        Directory.CreateDirectory(Path.Combine(CurrentRoundFolder, "events"));
        Directory.CreateDirectory(Path.Combine(CurrentRoundFolder, "world"));
        Directory.CreateDirectory(Path.Combine(CurrentRoundFolder, "stats"));
        Directory.CreateDirectory(Path.Combine(CurrentRoundFolder, "survey"));

        Debug.Log($"[ParticipantLogManager] Round folder ready: {CurrentRoundFolder}");
    }

    [System.Serializable]
    private class RoundConditionData
    {
        public int round;
        public string role;
        public float latencyMs;
        public string timewarpMode;
        public float roundLengthSeconds;
    }

    public void SaveRoundCondition(int roundId, string role, float latency, string timewarp, float roundLength)
    {
        string conditionFolder = Path.Combine(CurrentRoundFolder, "condition");

        RoundConditionData data = new RoundConditionData
        {
            round = roundId,
            role = role,
            latencyMs = latency,
            timewarpMode = timewarp,
            roundLengthSeconds = roundLength
        };

        string json = JsonUtility.ToJson(data, true);

        File.WriteAllText(
            Path.Combine(conditionFolder, "config.json"),
            json,
            Encoding.UTF8
        );

        string txtLine =
            $"{roundId}," +
            $"{role}," +
            $"{latency}," +
            $"{timewarp}," +
            $"{roundLength}";

        File.WriteAllText(
            Path.Combine(conditionFolder, "config.txt"),
            txtLine,
            Encoding.UTF8
        );
    }


}