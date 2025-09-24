using System.IO;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class GameStatsLogManager : MonoBehaviour
    {
        public static GameStatsLogManager Instance { get; private set; }

        private string logFilePathTxt;
        private string logFilePathCsv;

        private int totalShotsFired = 0;
        private int totalHits = 0;
        private int delayedBotHits = 0;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitLogFiles();
            SubscribeEvents();
        }

        private void InitLogFiles()
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string folderPath = Path.Combine(projectPath, "Logs/GameStats");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

            logFilePathTxt = Path.Combine(folderPath, $"GameStats_{timestamp}.txt");
            logFilePathCsv = Path.Combine(folderPath, $"GameStats_{timestamp}.csv");

            Debug.Log("Game stats logs will be written to: " + logFilePathTxt + " and " + logFilePathCsv);

            // only write CSV header now, data later
            File.WriteAllText(logFilePathCsv, "TotalShots,TotalHits,Accuracy(%),DelayedBotHits,DelayedBotAccuracy(%)\n");
        }

        private void SubscribeEvents()
        {
            EventManager.AddListener<FireShotEvent>(OnFireShot);
            EventManager.AddListener<HitEvent>(OnHit);
        }

        private void OnFireShot(FireShotEvent evt)
        {
            if (evt.ShooterId == "Player") // only track player
            {
                totalShotsFired++;
            }
        }

        private void OnHit(HitEvent evt)
        {
            if (evt.ShooterId == "Player")
            {
                totalHits++;

                if (evt.TargetId.Contains("HitBox"))
                {
                    delayedBotHits++;
                }
            }
        }

        private void FlushStats()
        {
            float accuracy = totalShotsFired > 0 ? (totalHits / (float)totalShotsFired) * 100f : 0f;
            float delayedBotAccuracy = totalShotsFired > 0 ? (delayedBotHits / (float)totalShotsFired) * 100f : 0f;

            // TXT summary
            string txtSummary =
                $"===== Game Stats =====\n" +
                $"Total Shots Fired: {totalShotsFired}\n" +
                $"Total Hits: {totalHits}\n" +
                $"Accuracy: {accuracy:F2}%\n" +
                $"Hits on DelayedBot: {delayedBotHits}\n" +
                $"Accuracy vs DelayedBot: {delayedBotAccuracy:F2}%\n" +
                $"======================";

            File.WriteAllText(logFilePathTxt, txtSummary + "\n");

            // CSV row
            string csvLine = $"{totalShotsFired},{totalHits},{accuracy:F2},{delayedBotHits},{delayedBotAccuracy:F2}";
            File.AppendAllText(logFilePathCsv, csvLine + "\n");

            Debug.Log(txtSummary);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                FlushStats();
                EventManager.Clear();
            }
        }
    }
}
