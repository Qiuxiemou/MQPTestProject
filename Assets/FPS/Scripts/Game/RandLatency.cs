using UnityEngine;

public class RandLatency : MonoBehaviour
{
    private int latency;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        latency = Random.Range(0, 501);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    int GetLatency()
    {
        return latency;
    }
}
