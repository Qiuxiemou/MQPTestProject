using System.Collections.Generic;
using UnityEngine;

public class PlayerLatency : MonoBehaviour
{
    public Transform player;
    public float latency = 0.15f;
    public Vector3 offset = new Vector3(0f, 1.6f, 0f);

    private readonly Queue<(float time, Vector3 pos)> buffer = new();
    private bool _initialized = false;
    private Vector3 initialPosition;
    float startTime;

    void LateUpdate()
    {
        if (!player) return;

        // Initialize: lock aimpoint to first sampled position
        if (!_initialized)
        {
            initialPosition = player.position + offset;
            startTime = Time.time;
            _initialized = true;
        }

        buffer.Enqueue((Time.time, player.position + offset));

        if (Time.time - startTime < latency)
        {
            transform.localPosition = transform.parent.InverseTransformPoint(initialPosition);
            return;
        }

        // Output delayed samples
        while (buffer.Count > 0 && Time.time - buffer.Peek().time >= latency)
        {
            transform.localPosition = transform.parent.InverseTransformPoint(buffer.Dequeue().pos);
        }
    }

    // Reset the latency buffer and hold the aimpoint at worldPos for `latency` seconds.
    // Call this right after player respawn to keep AimPoint at respawn position for the configured delay.
    public void ResetToPosition(Vector3 worldPos)
    {
        buffer.Clear();
        initialPosition = worldPos;
        startTime = Time.time;
        _initialized = true;

        // Immediately place the aimpoint at the provided world position
        if (transform.parent != null)
            transform.localPosition = transform.parent.InverseTransformPoint(initialPosition);
        else
            transform.position = initialPosition;
    }
}