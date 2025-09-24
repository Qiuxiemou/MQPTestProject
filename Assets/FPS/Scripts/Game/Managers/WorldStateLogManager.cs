using System.IO;
using UnityEngine;
using System.Collections.Generic;

namespace Unity.FPS.Game
{
    public class WorldStateLogManager : MonoBehaviour
    {
        public static WorldStateLogManager Instance { get; private set; }

        private string logFilePathTxt;
        private string logFilePathCsv;

        [SerializeField] private float logInterval = 0.1f; // snapshot interval in seconds
        private float nextLogTime = 0f;

        private List<string> csvBuffer = new List<string>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitLogFile();
        }

        private void InitLogFile()
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string folderPath = Path.Combine(projectPath, "Logs/WorldLogs");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            logFilePathTxt = Path.Combine(folderPath, $"WorldLog_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");
            logFilePathCsv = Path.Combine(folderPath, $"WorldLog_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");

            Debug.Log("World state logs will be written to: " + logFilePathTxt + " and " + logFilePathCsv);

            WriteLogTxt("===== World State Log Started =====");



            // Write immediately to file

            // CSV header row
            //File.AppendAllText(logFilePathCsv, "Time,Entity,PosX,PosY,PosZ,RotX,RotY,RotZ,Health\n");
            //File.AppendAllText(logFilePathCsv,
            //    "Time," +
            //    "Player_PosX,Player_PosY,Player_PosZ,Player_RotX,Player_RotY,Player_RotZ,Player_Health," +
            //    "Camera_PosX,Camera_PosY,Camera_PosZ,Camera_RotX,Camera_RotY,Camera_RotZ,Camera_Health," +
            //    "OrigBot_PosX,OrigBot_PosY,OrigBot_PosZ,OrigBot_RotX,OrigBot_RotY,OrigBot_RotZ,OrigBot_Health," +
            //    "DelayedBot_PosX,DelayedBot_PosY,DelayedBot_PosZ,DelayedBot_RotX,DelayedBot_RotY,DelayedBot_RotZ,DelayedBot_Health\n"
            //);

            // Buffer

            string header =
                "Time," +
                "Player_PosX,Player_PosY,Player_PosZ,Player_RotX,Player_RotY,Player_RotZ,Player_Health," +
                "Camera_PosX,Camera_PosY,Camera_PosZ,Camera_RotX,Camera_RotY,Camera_RotZ,Camera_Health," +
                "OrigBot_PosX,OrigBot_PosY,OrigBot_PosZ,OrigBot_RotX,OrigBot_RotY,OrigBot_RotZ,OrigBot_Health," +
                "DelayedBot_PosX,DelayedBot_PosY,DelayedBot_PosZ,DelayedBot_RotX,DelayedBot_RotY,DelayedBot_RotZ,DelayedBot_Health";

            csvBuffer.Add(header);


        }

        void Update()
        {
            if (Time.time >= nextLogTime)
            {
                CaptureWorldState();
                nextLogTime = Time.time + logInterval;
            }
        }

        private void WriteLogTxt(string message)
        {
            string line = $"[{System.DateTime.Now:HH:mm:ss.fff}] {message}";
            File.AppendAllText(logFilePathTxt, line + "\n");
            Debug.Log(line);
        }

        //private void WriteLogCsv(float time, EntityState e)
        //{
        //    string line = $"{time:F2},{e.Id},{e.Position.x:F3},{e.Position.y:F3},{e.Position.z:F3}," +
        //                  $"{e.Rotation.eulerAngles.x:F1},{e.Rotation.eulerAngles.y:F1},{e.Rotation.eulerAngles.z:F1}," +
        //                  $"{e.Health}";
        //    File.AppendAllText(logFilePathCsv, line + "\n");
        //}

        private void WriteLogCsv(WorldState ws)
        {
            // find each entity by name
            EntityState player = ws.Entities.Find(e => e.Id.Contains("Player"));
            EntityState camera = ws.Entities.Find(e => e.Id.Contains("Camera"));
            EntityState origBot = ws.Entities.Find(e => e.Id.Contains("OrigBot"));
            EntityState delayedBot = ws.Entities.Find(e => e.Id.Contains("DelayedBot"));

            string line = $"{ws.Time:F2}," +
                          // Player
                          FormatEntity(player) + "," +
                          // Camera
                          FormatEntity(camera) + "," +
                          // Original Bot
                          FormatEntity(origBot) + "," +
                          // Delayed Bot
                          FormatEntity(delayedBot);

            // Write immediately to file
            //File.AppendAllText(logFilePathCsv, line + "\n");
            // Buffer
            csvBuffer.Add(line);
        }

        private string FormatEntity(EntityState e)
        {
            if (e == null)
                return ",,,,,,,"; // 7 empty fields if missing
            return $"{e.Position.x:F3},{e.Position.y:F3},{e.Position.z:F3}," +
                   $"{e.Rotation.eulerAngles.x:F1},{e.Rotation.eulerAngles.y:F1},{e.Rotation.eulerAngles.z:F1}," +
                   $"{e.Health}";
        }


        private void CaptureWorldState()
        {
            WorldState ws = new WorldState();
            ws.Time = Time.time;

            // Capture all characters with a Health component
            //foreach (var character in FindObjectsOfType<Health>())
            foreach (var character in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                EntityState e = new EntityState();
                e.Id = character.gameObject.name;
                e.Position = character.transform.position;
                e.Rotation = character.transform.rotation;
                e.Health = character.CurrentHealth;

                ws.Entities.Add(e);
            }

            // Optionally: capture the main camera
            Camera cam = Camera.main;
            if (cam != null)
            {
                EntityState camState = new EntityState();
                camState.Id = "MainCamera";
                camState.Position = cam.transform.position;
                camState.Rotation = cam.transform.rotation;
                camState.Health = -1; // not applicable
                ws.Entities.Add(camState);
            }

            // Write to file in plain text (can switch to JSON later if you want)
            foreach (var e in ws.Entities)
            {
                WriteLogTxt($"[{ws.Time:F2}] {e.Id} pos={e.Position} rot={e.Rotation.eulerAngles} hp={e.Health}");
                //WriteLogCsv(ws.Time, e);
            }
            // Write single CSV row for this tick
            WriteLogCsv(ws);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                WriteLogTxt("===== World State Log Ended =====");

                // flush buffered CSV to disk
                File.WriteAllLines(logFilePathCsv, csvBuffer);
            }
        }
    }
}
