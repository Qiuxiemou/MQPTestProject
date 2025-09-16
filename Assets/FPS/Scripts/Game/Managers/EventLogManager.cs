using System.IO;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class EventLogManager : MonoBehaviour
    {
        public static EventLogManager Instance { get; private set; }

        private string logFilePath;

        void Awake()
        {
            // Singleton pattern to prevent duplicates
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitLogFile();
            SubscribeEvents();
        }

        private void InitLogFile()
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string folderPath = Path.Combine(projectPath, "Logs/EventLogs");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            logFilePath = Path.Combine(folderPath, $"EventLog_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");


            Debug.Log("Event log will be written to: " + logFilePath);
            WriteLog("===== Event Log Started =====");
        }

        private void SubscribeEvents()
        {
            EventManager.AddListener<FireShotEvent>(OnFireShot);
            EventManager.AddListener<HitEvent>(OnHit);
            EventManager.AddListener<DeathEvent>(OnDeath);
        }

        private void OnDestroy()
        {
            // Clean up if this manager is destroyed
            if (Instance == this)
            {
                EventManager.Clear();
                WriteLog("===== Event Log Ended =====");
            }
        }

        private void WriteLog(string message)
        {
            string line = $"[{System.DateTime.Now:HH:mm:ss.fff}] {message}";
            File.AppendAllText(logFilePath, line + "\n");
            Debug.Log(line); // also log to Unity console

        }

        // Event Handlers
        private void OnFireShot(FireShotEvent evt)
        {
            WriteLog($"FireShot: {evt.ShooterId} fired");
        }

        private void OnHit(HitEvent evt)
        {
            WriteLog($"Hit: {evt.ShooterId} hit {evt.TargetId} for {evt.Damage} damage");
        }

        private void OnDeath(DeathEvent evt)
        {
            WriteLog($"Death: {evt.VictimId} was killed by {evt.KillerId}");
        }
    }
}
