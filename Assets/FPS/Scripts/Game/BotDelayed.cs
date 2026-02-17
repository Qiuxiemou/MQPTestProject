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
        public static float latencyMs = 500f;      // Delay in milliseconds
        public float modifier = 1;

        private Queue<(Vector3 pos, Quaternion rot, float applyTime)> stateBuffer = new Queue<(Vector3, Quaternion, float)>();

        // Current target from the queue
        private Vector3 targetPos;
        private Quaternion targetRot;

        // Movement speed control
        public float baseMoveSpeed = 5f;  // Normal speed
        public float catchupMultiplier = 2f; // How much faster to move when behind

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        void Update()
        {
            if (firstBot == null) return;

            // Record the current state of the leader
            stateBuffer.Enqueue((firstBot.position, firstBot.rotation, Time.time + latencyMs * modifier / 1000f));

            // If there are states ready to apply, update target
            while (stateBuffer.Count > 0 && Time.time >= stateBuffer.Peek().applyTime)
            {
                var state = stateBuffer.Dequeue();
                targetPos = state.pos;
                targetRot = state.rot;
            }

            // Move toward target instead of snapping
            //float distance = Vector3.Distance(transform.position, targetPos);

            //// Increase speed if we’re far behind
            //float moveSpeed = baseMoveSpeed;
            //if (distance > 0.1f)
            //{
            //    // scale speed based on how far we are behind
            //    moveSpeed += distance * catchupMultiplier;
            //}

            //transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            //transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);

            transform.position = targetPos;
            transform.rotation = targetRot;
        }

        public void SetLatency(float ms)
        {
            // If we shorten the delay, old future-stamped states would feel wrong.
            // Clearing gives an immediate, predictable change.
            if (ms < latencyMs) stateBuffer.Clear();
            latencyMs = Mathf.Max(0f, ms * modifier);
                LM.write("BotDelay: " + latencyMs);
        }

        public float GetLatency()
        {
            return latencyMs;
        }

        // Update is called once per frame
        //void Update()
        //{
        //    if (firstBot == null) return;

        //    // Step 1: Record the current state of the logic bot with a future applyTime
        //    stateBuffer.Enqueue((firstBot.position, firstBot.rotation, Time.time + latencyMs * modifier / 1000f));

        //    // Step 2: Apply states that have reached their delay
        //    while (stateBuffer.Count > 0 && Time.time >= stateBuffer.Peek().applyTime)
        //    {
        //        var state = stateBuffer.Dequeue();
        //        transform.position = state.pos;
        //        transform.rotation = state.rot;
        //    }
        //}
    }
}
