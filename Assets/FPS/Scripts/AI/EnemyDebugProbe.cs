using UnityEngine;
using UnityEngine.AI;

public class EnemyDebugProbe : MonoBehaviour
{
    public EnemyLineOfSightChecker los;
    public NavMeshAgent agent;

    void Start()
    {
        if (!los) los = GetComponent<EnemyLineOfSightChecker>();
        if (!agent) agent = GetComponent<NavMeshAgent>();

        los.OnGainSight += t => Debug.Log("[LOS] Gain: " + t.name);
        los.OnLoseSight  += t => Debug.Log("[LOS] Lose: " + t.name);
    }

    void Update()
    {
        if (agent != null)
        {
            Debug.Log($"[Agent] onNavMesh={agent.isOnNavMesh} hasPath={agent.hasPath} vel={agent.velocity.magnitude:F2}");
        }
    }
}
