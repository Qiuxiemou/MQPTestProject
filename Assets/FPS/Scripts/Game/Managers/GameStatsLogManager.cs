using UnityEngine;

namespace Unity.FPS.Game
{
    public class GameStatsLogManager : MonoBehaviour
    {
        public static GameStatsLogManager Instance { get; private set; }

        int totalShotsFired = 0;
        int totalHits = 0;
        int delayedBotHits = 0;

        string _sessionId;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sessionId = EventLogManager.Instance ? EventLogManager.Instance.GetSessionId()
                         : System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

            EventManager.AddListener<FireShotEvent>(OnFireShot);
            EventManager.AddListener<HitEvent>(OnHit);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                EventManager.RemoveListener<FireShotEvent>(OnFireShot);
                EventManager.RemoveListener<HitEvent>(OnHit);

                float acc  = totalShotsFired > 0 ? (float)totalHits        / totalShotsFired * 100f : 0f;
                float dacc = totalShotsFired > 0 ? (float)delayedBotHits   / totalShotsFired * 100f : 0f;
                double wallMs = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();

                EventLogManager.Instance?.LogStats(_sessionId, wallMs, totalShotsFired, totalHits, acc, delayedBotHits, dacc);
            }
        }

        void OnFireShot(FireShotEvent e)
        {
            if (e.ShooterId == "Player") totalShotsFired++;
        }

        void OnHit(HitEvent e)
        {
            if (e.ShooterId != "Player") return;
            totalHits++;
            if (e.TargetId.Contains("HitBox")) delayedBotHits++;
        }
    }
}
