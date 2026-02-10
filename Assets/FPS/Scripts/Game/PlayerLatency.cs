using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class PlayerLatency : MonoBehaviour
    {
        public Transform player;
        public float latency = 0.15f;

        private Queue<(float time, Vector3 pos)> buffer = new();

        void LateUpdate()
        {
            if (!player) return;

            buffer.Enqueue((Time.time, player.position));

            while (buffer.Count > 0 && Time.time - buffer.Peek().time >= latency)
            {
                transform.position = buffer.Dequeue().pos;
            }
        }
    }
}
