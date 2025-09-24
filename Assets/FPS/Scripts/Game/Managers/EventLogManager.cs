// EventLogManager.cs (replace file)
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.FPS.Game
{
    public class EventLogManager : MonoBehaviour
    {
        public static EventLogManager Instance { get; private set; }

        [Header("Output")]
        [Tooltip("Write CSV under Assets/Logs.\nIf OFF, writes under <ProjectRoot>/Logs")]
        public bool WriteUnderAssets = true;

        [Tooltip("Clear the CSV on first write each Play")]
        public bool ClearOnStart = true;

        private string folderPath;
        private string csvPath;
        private bool   initialized;
        string _eventsCsvPath;
        string _viewCsvPath;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitCsv();
            Subscribe();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Unsubscribe();
        }


        void InitCsv()
        {   
            var folder = WriteUnderAssets
                ? System.IO.Path.Combine(Application.dataPath, "Logs")
                : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath, "Logs");

            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);

            _eventsCsvPath = System.IO.Path.Combine(folder, "events.csv");
            _viewCsvPath   = System.IO.Path.Combine(folder, "view.csv");

            if (ClearOnStart || !System.IO.File.Exists(_eventsCsvPath))
            {
                var header =
                "timestamp,event,shooter,target,damage,forward_delay_ms," +
                "hit_x,hit_y,hit_z,hitbox," +
                "client_x,client_y,client_z,client_hp,client_ratio," +
                "server_x,server_y,server_z,server_hp,server_ratio";
                System.IO.File.WriteAllText(_eventsCsvPath, header + "\n", Encoding.UTF8);
            }

            if (ClearOnStart || !System.IO.File.Exists(_viewCsvPath))
            {
                var header = "timestamp,player,px,py,pz,yaw,pitch,roll,fx,fy,fz";
                System.IO.File.WriteAllText(_viewCsvPath, header + "\n", Encoding.UTF8);
            }

            #if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
            #endif
                initialized = true;
        }

        void AppendEvents(string line) =>
        System.IO.File.AppendAllText(_eventsCsvPath, line + "\n", Encoding.UTF8);


        void AppendView(string line) =>
        System.IO.File.AppendAllText(_viewCsvPath, line + "\n", Encoding.UTF8);


        static string TS() => System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        static string F3(float v) => float.IsNaN(v) ? "" : v.ToString("F3");
        static string F2(float v) => float.IsNaN(v) ? "" : v.ToString("F2");


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

            string row =
                $"{TS()},{e.EventType},{e.ShooterId},{e.TargetId},{F2(e.Damage)},{F2(e.ForwardDelayMs)}," +
                $"{F3(hp.x)},{F3(hp.y)},{F3(hp.z)},{(e.HitBox ? "1" : "0")}," +       
                $"{F3(cPos.x)},{F3(cPos.y)},{F3(cPos.z)},{F2(cHp)},{F3(cRt)}," +
                $"{F3(sPos.x)},{F3(sPos.y)},{F3(sPos.z)},{F2(sHp)},{F3(sRt)}";

            AppendEvents(row);
        }

        void OnFire(FireShotEvent e)
        {
            string row = $"{TS()},fire,{e.ShooterId},{e.WeaponId},,,,,,,,,,,,";
            AppendEvents(row);
        }

        void OnDeath(DeathEvent e)
        {
            string row = $"{TS()},death,,{e.VictimId},,,,,,,,,,,,";
            AppendEvents(row);
        }

        void OnKeyPress(KeyPressEvent e)
        {
            string row = $"{TS()},key,{e.PlayerId},{e.Key},{(e.Pressed ? "down" : "up")},,,,,,,,,,,,,";
            AppendEvents(row);
        }

        void OnViewSample(ViewSampleEvent e)
        {
            var p = e.Position;
            var r = e.RotationEuler;
            var f = e.Forward;

            string row =
                $"{TS()},{e.PlayerId}," +
                $"{F3(p.x)},{F3(p.y)},{F3(p.z)}," +
                $"{F3(r.y)},{F3(r.x)},{F3(r.z)}," + 
                $"{F3(f.x)},{F3(f.y)},{F3(f.z)}";
            AppendView(row);
        }
    }
}
