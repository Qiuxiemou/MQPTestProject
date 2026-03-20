using System.IO;
using System.Text;
using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

namespace Unity.FPS.Game
{
    public class EventLogManager : MonoBehaviour
    {
        static EventLogManager _instance;
        public static EventLogManager Instance => _instance;

        [Header("Flush")]
        public int FlushEveryNLines = 200;
        public float FlushEverySeconds = 5f;

        // ================= FILE PATHS =================
        string _eventsCsvPath, _viewCsvPath, _worldCsvPath, _statsCsvPath;
        string _playerInputPath, _shotEventPath;

        // ================= WRITERS =================
        StreamWriter _eventsWriter, _viewWriter, _worldWriter, _statsWriter;
        StreamWriter _playerInputWriter, _shotEventWriter;

        int _linesSinceFlush = 0;
        float _nextFlushTime = 0f;

        // ================= ROUND CONTEXT =================
        int _participantID;
        int _latinRow;
        int _roundID;
        int _roundConditionID;
        float _latency;
        string _role;
        string _timewarp;

        // ================= UNITY =================
        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Subscribe();
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextFlushTime)
            {
                SafeFlush();
                _nextFlushTime = Time.unscaledTime + FlushEverySeconds;
            }

            if (RoundManager.Instance != null && RoundManager.Instance.RoundRunning)
            {
                LogWorldState();
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                Unsubscribe();
                CloseWriters();
            }
        }
        // ================= INIT =================
        public void StartNewRound(string roundFolder)
        {
            CloseWriters();

            //string eventsFolder = Path.Combine(roundFolder, "events");
            //string worldFolder = Path.Combine(roundFolder, "world");
            //string statsFolder = Path.Combine(roundFolder, "stats");

            //Directory.CreateDirectory(eventsFolder);
            //Directory.CreateDirectory(worldFolder);
            //Directory.CreateDirectory(statsFolder);

            string root = roundFolder;

            // ---------- Player Input ----------
            _playerInputPath = Path.Combine(root, "PlayerInput.csv");
            _playerInputWriter = NewWriterWithHeader(_playerInputPath,
                "time,participantID,latinRow,round,conditionID,latency,role,timewarp,mouseX,mouseY,key,eventType,posX,posY,posZ,rotX,rotY,rotZ,futureBotPosX,futureBotPosY,futureBotPosZ,pastBotPosX,pastBotPosY,pastBotPosZ");

            // ---------- Shot Event ----------
            _shotEventPath = Path.Combine(root, "ShotEvent.csv");
            _shotEventWriter = NewWriterWithHeader(_shotEventPath,
                "time,participantID,latinRow,round,conditionID,latency,role,timewarp,eventType,shooterID,damage,hitObject,shotAroundCorner,acceptShot,score,errorAngle");

            // ----------World Log----------
            _worldCsvPath = Path.Combine(root, "world.csv");
            //_worldWriter = NewWriterWithHeader(_worldCsvPath,
            //    "session_id,wall_ts,game_t,entity_id,entity_type,pos_x,pos_y,pos_z,yaw,pitch,roll,hp");

            _worldWriter = NewWriterWithHeader(_worldCsvPath,
                "time,participantID,latinRow,round,conditionID,latency,role,timewarp" +
                "player_x,player_y,player_z," +
                "cam_rot_x,cam_rot_y,cam_rot_z," +
                "futureBot_x,futureBot_y,futureBot_z," +
                "pastBot_x,pastBot_y,pastBot_z"
            );

            // ---------- Other logs ----------
            //_eventsCsvPath = Path.Combine(root, "events.csv");
            //_viewCsvPath = Path.Combine(root, "view.csv");
            //_statsCsvPath = Path.Combine(root, "stats.csv");

            //_eventsWriter = NewWriterWithHeader(_eventsCsvPath,
            //    "timestamp,event,shooter,target,damage,forward_delay_ms," +
            //    "hit_x,hit_y,hit_z,hitbox," +
            //    "client_x,client_y,client_z,client_hp,client_ratio," +
            //    "server_x,server_y,server_z,server_hp,server_ratio");

            //_viewWriter = NewWriterWithHeader(_viewCsvPath,
            //    "timestamp,player,px,py,pz,yaw,pitch,roll,fx,fy,fz");

            //_statsWriter = NewWriterWithHeader(_statsCsvPath,
            //    "session_id,wall_ts,total_shots,total_hits,accuracy_pct,delayed_hits,delayed_accuracy_pct,damage_dealt,damage_received");

        }

        StreamWriter NewWriterWithHeader(string path, string header)
        {
            var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            var sw = new StreamWriter(fs, new UTF8Encoding(false)) { AutoFlush = false };
            sw.WriteLine(header);
            return sw;
        }

        void SafeFlush()
        {
            try { _eventsWriter?.Flush(); } catch { }
            try { _viewWriter?.Flush(); } catch { }
            try { _worldWriter?.Flush(); } catch { }
            try { _statsWriter?.Flush(); } catch { }
            try { _playerInputWriter?.Flush(); } catch { }
            try { _shotEventWriter?.Flush(); } catch { }

            _linesSinceFlush = 0;
        }

        void CloseWriters()
        {
            SafeFlush();

            _eventsWriter?.Close();
            _viewWriter?.Close();
            _worldWriter?.Close();
            _statsWriter?.Close();
            _playerInputWriter?.Close();
            _shotEventWriter?.Close();

            _eventsWriter = _viewWriter = _worldWriter = _statsWriter = null;
            _playerInputWriter = _shotEventWriter = null;
        }

        void BumpFlushCounter()
        {
            _linesSinceFlush++;
            if (_linesSinceFlush >= FlushEveryNLines)
                SafeFlush();
        }

        static string TS() => System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        static string F3(float v) => float.IsNaN(v) ? "" : v.ToString("F3");
        static string F2(float v) => float.IsNaN(v) ? "" : v.ToString("F2");
        static string San(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", "_");

        // ================= CONTEXT =================
        public void SetRoundContext(int participantID, int latinRow, int roundID, int roundConditionID, float latency, string role, string timewarp)
        {
            _participantID = participantID;
            _latinRow = latinRow;
            _roundID = roundID;
            _roundConditionID = roundConditionID;
            _latency = latency;
            _role = role;
            _timewarp = timewarp;
        }
        string CustomTime()
        {
            return System.DateTime.Now.ToString("yy:MM:dd:HH:mm:ss:fff");
        }

        // ================= EVENTS =================
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
            // ===== SAFETY =====
            if (_shotEventWriter == null) return;

            LM.write("OnHitCsv triggered");

            // ===== BASIC REFERENCES =====
            Transform playerTf = e.ClientTf;
            Transform botTf = e.ServerTf;

            // ===== ERROR ANGLE =====
            float errorAngle = 0f;

            if (playerTf != null && botTf != null)
            {
                Vector3 playerPos = playerTf.position + Vector3.up * 1.5f;
                Vector3 botPos = botTf.position + Vector3.up * 1.5f;

                Vector3 toBot = (botPos - playerPos).normalized;

                Camera cam = playerTf.GetComponentInChildren<Camera>();
                Vector3 forward = cam ? cam.transform.forward : playerTf.forward;

                errorAngle = Vector3.Angle(forward, toBot);
            }

            // ===== SCORE (placeholder for now) =====
            int score = 0;

            // ===== ACCEPT SHOT =====
            bool acceptShot = (e.ShotAroundCorner && _timewarp == "Normal") || (!e.ShotAroundCorner) ? true : false;

            // ===== DEBUG =====
            //LM.write($"ShotAroundCorner: {e.ShotAroundCorner}");
            //LM.write($"AcceptShot: {acceptShot}");
            //LM.write($"ErrorAngle: {errorAngle}");

            // ===== 5. LOG TO CSV =====
            LogShotEvent(
                e.EventType,
                e.ShooterId,
                e.Damage,
                e.TargetId,
                e.ShotAroundCorner,
                acceptShot,
                score,
                errorAngle
            );

            if (e.ShotAroundCorner)
            {
                SummaryLogManager.Instance.CountShotAroundCorner();
            }

            if (e.EventType == "bot_damaged")
            {
                SummaryLogManager.Instance.CountHits();
            }
        }


        void OnFire(FireShotEvent e)
        {
            if (_eventsWriter == null) return;
            _eventsWriter.WriteLine($"{TS()},fire,{e.ShooterId},{e.WeaponId},,,,,,,,,,,,");

            BumpFlushCounter();
        }

        void OnDeath(DeathEvent e)
        {
            if (_eventsWriter == null) return;
            _eventsWriter.WriteLine($"{TS()},death,,{e.VictimId},,,,,,,,,,,,");

            BumpFlushCounter();
        }

        void OnKeyPress(KeyPressEvent e)
        {
            if (_playerInputWriter == null) return;

            _playerInputWriter.WriteLine(
                $"{CustomTime()},{_participantID},{_latinRow},{_roundID},{_roundConditionID},{_latency},{_role},{_timewarp}," +
                $"0,0,{San(e.Key.ToString())},{(e.Pressed ? "down" : "up")}," +
                $"0,0,0,0,0,0"
            );

            BumpFlushCounter();
        }

        void OnViewSample(ViewSampleEvent e)
        {
            if (_viewWriter == null) return;
            var p = e.Position;
            var r = e.RotationEuler;
            var f = e.Forward;

            _viewWriter.WriteLine(
                $"{TS()},{e.PlayerId}," +
                $"{F3(p.x)},{F3(p.y)},{F3(p.z)}," +
                $"{F3(r.y)},{F3(r.x)},{F3(r.z)}," +
                $"{F3(f.x)},{F3(f.y)},{F3(f.z)}"
            );

            BumpFlushCounter();
        }

        // ================= PUBLIC LOGS =================

        public void LogPlayerInput(Vector3 pos, Vector3 rot, float mx, float my)
        {
            if (_playerInputWriter == null) return;

            Vector3 futureBotPos = Vector3.zero;
            Vector3 pastBotPos = Vector3.zero;

            bool hasFuture = false;
            bool hasPast = false;

            var enemy = RoundManager.Instance?.CurrentEnemy;

            if (enemy != null)
            {
                var proxy = enemy.GetComponent<BotHealthProxy>();

                if (proxy != null)
                {
                    // ===== FUTURE BOT =====
                    if (proxy.futureHealth != null)
                    {
                        futureBotPos = proxy.futureHealth.transform.position;
                        hasFuture = true;
                    }

                    // ===== PAST BOT =====
                    if (proxy.pastHealth != null)
                    {
                        pastBotPos = proxy.pastHealth.transform.position;
                        hasPast = true;
                    }
                }
                else
                {
                    // fallback: single bot (seeker case)
                    futureBotPos = enemy.transform.position;
                    hasFuture = true;
                }
            }

            // helper to print empty if not exist
            string FBX = hasFuture ? futureBotPos.x.ToString("F3") : "";
            string FBY = hasFuture ? futureBotPos.y.ToString("F3") : "";
            string FBZ = hasFuture ? futureBotPos.z.ToString("F3") : "";

            string PBX = hasPast ? pastBotPos.x.ToString("F3") : "";
            string PBY = hasPast ? pastBotPos.y.ToString("F3") : "";
            string PBZ = hasPast ? pastBotPos.z.ToString("F3") : "";

            _playerInputWriter.WriteLine(
                $"{CustomTime()},{_participantID},{_latinRow},{_roundID},{_roundConditionID},{_latency},{_role},{_timewarp}," +
                $"{mx:F3},{my:F3},,," +
                $"{pos.x:F3},{pos.y:F3},{pos.z:F3}," +
                $"{rot.x:F3},{rot.y:F3},{rot.z:F3}," +
                $"{FBX},{FBY},{FBZ}," +
                $"{PBX},{PBY},{PBZ}"
            );

            BumpFlushCounter();
        }

        public void LogShotEvent(string eventType,string shooterId, float damage, string hitObject, bool shotAroundCorner, bool acceptShot, int score, float errorAngle)
        {
            if (_shotEventWriter == null) return;

            _shotEventWriter.WriteLine(
                $"{CustomTime()},{_participantID},{_latinRow},{_roundID},{_roundConditionID},{_latency},{_role},{_timewarp}," +
                $"{eventType},{San(shooterId)},{damage:F2},{San(hitObject)}," +
                $"{(shotAroundCorner ? 1 : 0)}," +
                $"{(acceptShot ? 1 : 0)}," +
                $"{score},{errorAngle:F3}"
            );

            BumpFlushCounter();
        }

        public string GetSessionId() => CustomTime();

        public void LogStats(string sessionId, double wallTsMs,
                     int totalShots, int totalHits, float accPct,
                     int delayedHits, float delayedAccPct,
                     int damageDealt, int damageReceived)
        {
            if (_statsWriter == null) return;

            string wall = System.DateTimeOffset.FromUnixTimeMilliseconds((long)wallTsMs)
                                            .ToLocalTime()
                                            .ToString("yyyy-MM-dd HH:mm:ss.fff");

            _statsWriter.WriteLine(
                $"{San(sessionId)},{wall}," +
                $"{totalShots},{totalHits},{accPct:F2}," +
                //$"{delayedHits},{delayedAccPct:F2}"
                $"{delayedHits},{delayedAccPct:F2}," +
                $"{damageDealt},{damageReceived}"
                );

            BumpFlushCounter();
        }

        //public void LogWorldRow(string sessionId, double wallTsMs, float gameTime,
        //                        string entityId, string entityType,
        //                        Vector3 pos, Vector3 euler, float hp)
        //{
        //    if (_worldWriter == null) return;

        //    string wall = System.DateTimeOffset.FromUnixTimeMilliseconds((long)wallTsMs)
        //                                    .ToLocalTime()
        //                                    .ToString("yyyy-MM-dd HH:mm:ss.fff");

        //    _worldWriter.WriteLine(
        //        $"{San(sessionId)},{wall},{gameTime:F3}," +
        //        $"{San(entityId)},{San(entityType)}," +
        //        $"{pos.x:F3},{pos.y:F3},{pos.z:F3}," +
        //        $"{euler.y:F1},{euler.x:F1},{euler.z:F1}," +
        //        $"{hp:F1}");
        //    BumpFlushCounter();
        //}

        public void LogWorldState()
        {
            if (_worldWriter == null) return;

            float gameT = Time.unscaledTime;

            // ================= PLAYER =================
            Transform playerTf = GameObject.FindGameObjectWithTag("Player")?.transform;

            Vector3 playerPos = playerTf ? playerTf.position : Vector3.zero;

            Camera cam = playerTf ? playerTf.GetComponentInChildren<Camera>() : null;
            Vector3 camRot = cam ? cam.transform.eulerAngles : Vector3.zero;

            // ================= BOT =================
            Vector3 futureBotPos = Vector3.zero;
            Vector3 pastBotPos = Vector3.zero;

            bool hasFuture = false;
            bool hasPast = false;

            var enemy = RoundManager.Instance?.CurrentEnemy;

            if (enemy != null)
            {
                var proxy = enemy.GetComponent<BotHealthProxy>();

                if (proxy != null)
                {
                    if (proxy.futureHealth != null)
                    {
                        futureBotPos = proxy.futureHealth.transform.position;
                        hasFuture = true;
                    }

                    if (proxy.pastHealth != null)
                    {
                        pastBotPos = proxy.pastHealth.transform.position;
                        hasPast = true;
                    }
                }
                else
                {
                    futureBotPos = enemy.transform.position;
                    hasFuture = true;
                }
            }

            // ================= WRITE =================
            _worldWriter.WriteLine(
                $"{CustomTime()},{_participantID},{_latinRow},{_roundID},{_roundConditionID},{_latency},{_role},{_timewarp}," +
                $"{playerPos.x:F3},{playerPos.y:F3},{playerPos.z:F3}," +
                $"{camRot.x:F2},{camRot.y:F2},{camRot.z:F2}," +
                $"{(hasFuture ? futureBotPos.x.ToString("F3") : "")}," +
                $"{(hasFuture ? futureBotPos.y.ToString("F3") : "")}," +
                $"{(hasFuture ? futureBotPos.z.ToString("F3") : "")}," +
                $"{(hasPast ? pastBotPos.x.ToString("F3") : "")}," +
                $"{(hasPast ? pastBotPos.y.ToString("F3") : "")}," +
                $"{(hasPast ? pastBotPos.z.ToString("F3") : "")}"
            );

            BumpFlushCounter();
        }
    }
}