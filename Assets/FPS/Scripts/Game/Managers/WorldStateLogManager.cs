using UnityEngine;

namespace Unity.FPS.Game
{
    public class WorldStateLogManager : MonoBehaviour
    {
        [SerializeField] private bool includeCamera = false; 

        string _sessionId;

        void Awake()
        {
            _sessionId = EventLogManager.Instance ? EventLogManager.Instance.GetSessionId()
                         : System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        void Update()
        {
            double wallMs = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
            float  gameT  = Time.time;

            foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                var go = h.gameObject;
                var tf = go.transform;

                string id   = go.name;           
                string type = DeduceType(id);  
                var    pos  = tf.position;
                var    rot  = tf.rotation.eulerAngles;
                float  hp   = h.CurrentHealth;

                EventLogManager.Instance?.LogWorldRow(_sessionId, wallMs, gameT, id, type, pos, rot, hp);
            }

            if (includeCamera)
            {
                var cam = Camera.main;
                if (cam)
                {
                    var tf = cam.transform;
                    EventLogManager.Instance?.LogWorldRow(
                        _sessionId, wallMs, gameT,
                        "MainCamera", "Camera",
                        tf.position, tf.rotation.eulerAngles, -1f);
                }
            }
        }

        static string DeduceType(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Entity";
            if (name.Contains("OrigBot"))    return "OrigBot";
            if (name.Contains("DelayedBot")) return "DelayedBot";
            if (name.Contains("Bot"))        return "Bot";
            return "Entity";
        }
    }
}
