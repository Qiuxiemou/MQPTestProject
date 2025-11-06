using UnityEngine;
using UnityEngine.AI;

public class ChaseTest : MonoBehaviour
{
    public Transform player;
    NavMeshAgent agent;
    void Awake(){ agent = GetComponent<NavMeshAgent>(); }
    void Update()
    {
        if (player && agent.isOnNavMesh)
            agent.SetDestination(player.position);
    }
}
