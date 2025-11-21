using System.IO;
using System.Text;
using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Very simple global logger.
    /// Usage (anywhere, in any script with 'using Unity.FPS.Game;'):
    ///     LM.write("Hello from hider bot");
    /// Writes lines like:
    ///     2025-02-19 13:45:12.345,Hello from hider bot
    /// </summary>
    public class LogManager : MonoBehaviour
    {
        public static LogManager Instance { get; private set; }

        [Header("Output")]
        [Tooltip("If true, writes under Assets/Logs in the editor; otherwise uses persistentDataPath.")]
        public bool WriteUnderAssets = true;

        [Tooltip("Optional extra subfolder name, e.g. 'Logs'. Leave blank for none.")]
        public string SubFolder = "Logs";

        [Tooltip("Base file name prefix, timestamp will be added.")]
        public string FileNamePrefix = "_debug_log";

        [Tooltip("If true and file already exists, it will be overwritten.")]
        public bool ClearOnStart = true;

        [Header("Flush")]
        [Tooltip("If true, flush each line to disk immediately (safer, slightly slower).")]
        public bool FlushEachWrite = true;

        private string _logPath;
        private StreamWriter _writer;
        private string _sessionId;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            InitWriter();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                CloseWriter();
                Instance = null;
            }
        }

        void InitWriter()
        {
#if UNITY_EDITOR
            string rootFolder = WriteUnderAssets ? Application.dataPath : Application.persistentDataPath;
#else
            string rootFolder = Application.persistentDataPath;
#endif

            string folder = string.IsNullOrEmpty(SubFolder)
                ? rootFolder
                : Path.Combine(rootFolder, SubFolder);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _logPath = Path.Combine(folder, $"{FileNamePrefix}.txt");

            // If not clearing, open existing file and append; otherwise create new.
            FileMode mode = ClearOnStart ? FileMode.Create : FileMode.Append;

            var fs = new FileStream(_logPath, mode, FileAccess.Write, FileShare.Read, 1 << 16, FileOptions.None);
            _writer = new StreamWriter(fs, new UTF8Encoding(false))
            {
                AutoFlush = FlushEachWrite
            };

            // Optional header line
            _writer.WriteLine($"# SimpleLog session {_sessionId}");
            _writer.WriteLine("# timestamp,message");
            _writer.Flush();
        }

        void CloseWriter()
        {
            try
            {
                _writer?.Flush();
                _writer?.Close();
            }
            catch { /* ignore */ }

            _writer = null;
        }

        // Timestamp helper
        static string TS()
        {
            return System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        static string San(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // Replace newlines and commas just in case you open in CSV editor
            return s.Replace("\n", " ").Replace("\r", " ").Replace(",", ";");
        }

        /// <summary>
        /// Instance method that actually writes the line.
        /// Usually you want to call LM.write(...) instead of this directly.
        /// </summary>
        public void Write(string message)
        {
            if (_writer == null) return;

            string line = $"{TS()},{San(message)}";
            _writer.WriteLine(line);

            if (FlushEachWrite)
                _writer.Flush();
        }

        /// <summary>
        /// Static helper. Safe to call even if SimpleLogManager is not in the scene.
        /// </summary>
        public static void Log(string message)
        {
            if (Instance != null)
            {
                Instance.Write(message);
            }
            else
            {
                // Fallback: log to Unity console so you notice the setup is missing
                Debug.LogWarning($"[SimpleLogManager] No instance in scene. Message: {message}");
            }
        }

        /// <summary>
        /// Returns the current log file path (for debugging / UI).
        /// </summary>
        public string GetLogPath() => _logPath;
        public string GetSessionId() => _sessionId;
    }

    /// <summary>
    /// Tiny static wrapper so you can just call LM.write("text") from anywhere.
    /// </summary>
    public static class LM
    {
        public static void write(string message)
        {
            LogManager.Log(message);
        }

        // If you prefer PascalCase as well:
        public static void Write(string message)
        {
            LogManager.Log(message);
        }
    }
}
