using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;



namespace Unity.FPS.Game
{
    public class BotDelayed : MonoBehaviour
    {

        [Header("References")]
        [Tooltip("First Bot")]
        public Transform firstBot;          
        public float latencyMs = 200f;      // Delay in milliseconds

        private Queue<(Vector3 pos, Quaternion rot, float applyTime)> stateBuffer = new Queue<(Vector3, Quaternion, float)>();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (firstBot == null) return;

            // Step 1: Record the current state of the logic bot with a future applyTime
            stateBuffer.Enqueue((firstBot.position, firstBot.rotation, Time.time + latencyMs / 1000f));

            // Step 2: Apply states that have reached their delay
            while (stateBuffer.Count > 0 && Time.time >= stateBuffer.Peek().applyTime)
            {
                var state = stateBuffer.Dequeue();
                transform.position = state.pos;
                transform.rotation = state.rot;
            }
        }
    }
}
