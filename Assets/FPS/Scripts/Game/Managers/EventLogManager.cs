using System.IO;
using System.Text;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class EventLogManager : MonoBehaviour
    {
        public static EventLogManager Instance { get; private set; }

        [Header("Output")]
        public bool WriteUnderAssets = true;
        public bool ClearOnStart = false;

        [Header("Flush")]
        public int   FlushEveryNLines  = 200;
        public float FlushEverySeconds = 5f;

        string _eventsCsvPath, _viewCsvPath, _worldCsvPath, _statsCsvPath;

        StreamWriter _eventsWriter, _viewWriter, _worldWriter, _statsWriter;

        int   _linesSinceFlush = 0;
        float _nextFlushTime   = 0f;

        string _sessionId;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            InitCsvWriters();
            Subscribe();
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextFlushTime)
            {
                SafeFlush();
                _nextFlushTime = Time.unscaledTime + FlushEverySeconds;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Unsubscribe();
                CloseWriters();
            }
        }


        void InitCsvWriters()
    {
        _sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

    #if UNITY_EDITOR
        string folder = Path.Combine(Application.dataPath, "Logs");
#else
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var folder = Path.Combine(projectRoot, "Logs", "GameLogs");
#endif

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        _eventsCsvPath = Path.Combine(folder, $"events_{_sessionId}.csv");
        _viewCsvPath   = Path.Combine(folder, $"view_{_sessionId}.csv");
        _worldCsvPath  = Path.Combine(folder, $"world_{_sessionId}.csv");
        _statsCsvPath  = Path.Combine(folder, $"stats_{_sessionId}.csv");

        _eventsWriter = NewWriterWithHeader(_eventsCsvPath,
            "timestamp,event,shooter,target,damage,forward_delay_ms," +
            "hit_x,hit_y,hit_z,hitbox," +
            "client_x,client_y,client_z,client_hp,client_ratio," +
            "server_x,server_y,server_z,server_hp,server_ratio");

        _viewWriter = NewWriterWithHeader(_viewCsvPath,
            "timestamp,player,px,py,pz,yaw,pitch,roll,fx,fy,fz");

        _worldWriter = NewWriterWithHeader(_worldCsvPath,
            "session_id,wall_ts,game_t,entity_id,entity_type,pos_x,pos_y,pos_z,yaw,pitch,roll,hp");

        _statsWriter = NewWriterWithHeader(_statsCsvPath,
            "session_id,wall_ts,total_shots,total_hits,accuracy_pct,delayed_hits,delayed_accuracy_pct");

        _nextFlushTime = Time.unscaledTime + FlushEverySeconds;

        #if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh(); 
        #endif
        }

        StreamWriter NewWriterWithHeader(string path, string header)
        {
            var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1 << 16, FileOptions.None);
            var sw = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = false };
            sw.WriteLine(header);
            return sw;
        }

        void SafeFlush()
        {
            try { _eventsWriter?.Flush(); } catch {}
            try { _viewWriter  ?.Flush(); } catch {}
            try { _worldWriter ?.Flush(); } catch {}
            try { _statsWriter ?.Flush(); } catch {}
            _linesSinceFlush = 0;
        }

        void CloseWriters()
        {
            SafeFlush();
            try { _eventsWriter?.Close(); } catch {}
            try { _viewWriter  ?.Close(); } catch {}
            try { _worldWriter ?.Close(); } catch {}
            try { _statsWriter ?.Close(); } catch {}
            _eventsWriter = _viewWriter = _worldWriter = _statsWriter = null;
        }

        static string TS() => System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        static string F3(float v) => float.IsNaN(v) ? "" : v.ToString("F3");
        static string F2(float v) => float.IsNaN(v) ? "" : v.ToString("F2");
        static string San(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", "_").Replace("\n", " ").Replace("\r", " ");

        void BumpFlushCounter()
        {
            _linesSinceFlush++;
            if (_linesSinceFlush >= FlushEveryNLines) SafeFlush();
        }

        void Subscribe()
        {
            EventManager.AddListener<HitCsvEvent>(OnHitCsv);
            EventManager.AddListener<FireShotEvent>(OnFire);
            EventManager.AddListener<DeathEvent>(OnDeath);
            EventManager.AddListener<KeyPressEvent>(OnKeyPress);
            EventManager.AddListener<ViewSampleEvent>(OnViewSample);
        }
        void Unsubscribe()
        {
            EventManager.RemoveListener<HitCsvEvent>(OnHitCsv);
            EventManager.RemoveListener<FireShotEvent>(OnFire);
            EventManager.RemoveListener<DeathEvent>(OnDeath);
            EventManager.RemoveListener<KeyPressEvent>(OnKeyPress);
            EventManager.RemoveListener<ViewSampleEvent>(OnViewSample);
        }

        void OnHitCsv(HitCsvEvent e)
        {
            Vector3 cPos = e.ClientTf     ? e.ClientTf.position : Vector3.zero;
            float   cHp  = e.ClientHealth ? e.ClientHealth.CurrentHealth : -1f;
            float   cRt  = e.ClientHealth ? e.ClientHealth.GetRatio()    : -1f;

            Vector3 sPos = e.ServerTf     ? e.ServerTf.position : Vector3.zero;
            float   sHp  = e.ServerHealth ? e.ServerHealth.CurrentHealth : -1f;
            float   sRt  = e.ServerHealth ? e.ServerHealth.GetRatio()    : -1f;

            Vector3 hp   = e.HitPoint;

            _eventsWriter.WriteLine(
                $"{TS()},{e.EventType},{e.ShooterId},{e.TargetId},{F2(e.Damage)},{F2(e.ForwardDelayMs)}," +
                $"{F3(hp.x)},{F3(hp.y)},{F3(hp.z)},{(e.HitBox ? "1" : "0")}," +
                $"{F3(cPos.x)},{F3(cPos.y)},{F3(cPos.z)},{F2(cHp)},{F3(cRt)}," +
                $"{F3(sPos.x)},{F3(sPos.y)},{F3(sPos.z)},{F2(sHp)},{F3(sRt)}");
            BumpFlushCounter();
        }

        void OnFire(FireShotEvent e)
        {
            _eventsWriter.WriteLine($"{TS()},fire,{e.ShooterId},{e.WeaponId},,,,,,,,,,,,");

            BumpFlushCounter();
        }

        void OnDeath(DeathEvent e)
        {
            _eventsWriter.WriteLine($"{TS()},death,,{e.VictimId},,,,,,,,,,,,");

            BumpFlushCounter();
        }

        void OnKeyPress(KeyPressEvent e)
        {
            _eventsWriter.WriteLine($"{TS()},key,{e.PlayerId},{e.Key},{(e.Pressed ? "down" : "up")},,,,,,,,,,,,,");
            BumpFlushCounter();
        }

        void OnViewSample(ViewSampleEvent e)
        {
            var p = e.Position;
            var r = e.RotationEuler;
            var f = e.Forward;

            _viewWriter.WriteLine(
                $"{TS()},{e.PlayerId}," +
                $"{F3(p.x)},{F3(p.y)},{F3(p.z)}," +
                $"{F3(r.y)},{F3(r.x)},{F3(r.z)}," +
                $"{F3(f.x)},{F3(f.y)},{F3(f.z)}");
            BumpFlushCounter();
        }

        public string GetSessionId() => _sessionId;

        public void LogWorldRow(string sessionId, double wallTsMs, float gameTime,
                                string entityId, string entityType,
                                Vector3 pos, Vector3 euler, float hp)
        {
            if (_worldWriter == null) return;

            string wall = System.DateTimeOffset.FromUnixTimeMilliseconds((long)wallTsMs)
                                            .ToLocalTime()
                                            .ToString("yyyy-MM-dd HH:mm:ss.fff");

            _worldWriter.WriteLine(
                $"{San(sessionId)},{wall},{gameTime:F3}," +
                $"{San(entityId)},{San(entityType)}," +
                $"{pos.x:F3},{pos.y:F3},{pos.z:F3}," +
                $"{euler.y:F1},{euler.x:F1},{euler.z:F1}," +
                $"{hp:F1}");
            BumpFlushCounter();
        }

        public void LogStats(string sessionId, double wallTsMs,
                             int totalShots, int totalHits, float accPct,
                             int delayedHits, float delayedAccPct)
        {
            if (_statsWriter == null) return;

            string wall = System.DateTimeOffset.FromUnixTimeMilliseconds((long)wallTsMs)
                                            .ToLocalTime()
                                            .ToString("yyyy-MM-dd HH:mm:ss.fff");

            _statsWriter.WriteLine(
                $"{San(sessionId)},{wall}," +
                $"{totalShots},{totalHits},{accPct:F2}," +
                $"{delayedHits},{delayedAccPct:F2}");
            BumpFlushCounter();
        }
    }
}
