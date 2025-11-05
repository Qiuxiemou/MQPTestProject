using Unity.FPS.AI;
using Unity.FPS.Game;  // gives access to the built-in Health system
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class BotMovement : MonoBehaviour
{
    [Header("References")]
    public Transform player;                 // drag your Player here

    [Header("Detection / Health")]
    public float detectionRange = 15f;       // player distance that triggers Hide
    public float safeDistance = 50f;         // distance to stop hiding
    public float lowHealthThreshold = 40f;   // below this, bot prefers health

    [Header("Navigation")]
    public float decisionDelay = 1.0f;       // how often the bot re-evaluates targets
    public float searchRadius = 70f;         // how far to search for cover/loot
    public float coverOffset = 3f;         // how far behind an obstacle to stand
    public float stopDistance = 0.3f;        // agent stopping distance

    [Header("Win Condition")]
    //public int winCountTotal = 25;       // have to eat at least amount of number

    [Header("Layers (assign in Inspector)")]
    public LayerMask wallMask;           // walls, crates, etc.
    //public LayerMask fruitMask;              // fruit pickups
    //public LayerMask healthMask;             // health pickups

    [Header("Debug")]
    public bool showDebugLines = true;

    // Internal references
    private NavMeshAgent agent;
    //private Animator anim;
    private Health health;                   // reference to built-in Health component

    // FSM data
    private float decisionTimer;
    //private string stateLootFruit = "LootFruit";
    //private string stateLootHealth = "LootHealth";
    //private string stateHide = "Hide";


    // Cached health value
    private float currentHealth;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        //anim = GetComponent<Animator>();
        agent.stoppingDistance = stopDistance;
        agent.autoBraking = true;

        health = GetComponent<Health>();

        Debug.Log("[HideBot] Registered with EnemyManager");
    }

    void Update()
    {
        if (health != null)
            currentHealth = health.CurrentHealth; // keep synced

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        bool playerNear = distToPlayer < detectionRange;
        bool lowHealth = currentHealth < lowHealthThreshold;

        // push to animator
        //anim.SetBool("PlayerNear", playerNear);
        //anim.SetBool("LowHealth", lowHealth);

        decisionTimer -= Time.deltaTime;
        //AnimatorStateInfo s = anim.GetCurrentAnimatorStateInfo(0);

        // FSM transitions
        //if (s.IsName(stateHide))
        //{
        //    if (playerNear)
        //    {
                if (decisionTimer <= 0f || ReachedDestination())
                {
                    decisionTimer = decisionDelay;
                    ChooseCoverAndMove();
                }
        //    }
        //    else
        //    {
        //        if (!anim.GetBool("PlayerNear"))
        //            agent.ResetPath();
        //    }
        //}
        //else if (s.IsName(stateLootHealth))
        //{
        //    if (decisionTimer <= 0f || ReachedDestination())
        //    {
        //        decisionTimer = decisionDelay;
        //        ChoosePickupAndMove(healthMask);
        //    }
        //}
        //else if (s.IsName(stateLootFruit))
        //{
        //    if (decisionTimer <= 0f || ReachedDestination())
        //    {
        //        decisionTimer = decisionDelay;
        //        ChoosePickupAndMove(fruitMask);
        //    }
        //}

        // fallback safety
        //if (!playerNear && agent.hasPath && agent.remainingDistance > 1000f)
        //    agent.ResetPath();

        //if (distToPlayer > safeDistance)
        //    anim.SetBool("PlayerNear", false);
    }

    bool ReachedDestination()
    {
        if (agent.pathPending) return false;
        if (agent.remainingDistance > agent.stoppingDistance) return false;
        if (agent.hasPath && agent.velocity.sqrMagnitude > 0.001f) return false;
        return true;
    }

    //==============================
    // State: Hide
    //==============================
    void ChooseCoverAndMove()
    {
        Vector3 botPos = transform.position;
        Vector3 playerPos = player.position;

        Collider[] walls = Physics.OverlapSphere(botPos, searchRadius, wallMask);

        Transform bestWall = null;
        Vector3 bestCoverPos = Vector3.zero;
        float bestScore = float.MinValue;

        foreach (Collider wall in walls)
        {
            if (wall.attachedRigidbody && wall.attachedRigidbody.transform == transform)
                continue; // skip self

            // Skip likely floors
            Vector3 up = Vector3.up;
            float upDot = Vector3.Dot(up, wall.transform.up);

            Vector3 dirPlayerToObstacle = (wall.transform.position - playerPos).normalized;
            Vector3 coverPos = wall.transform.position + dirPlayerToObstacle * coverOffset;

            // Check if obstacle blocks line of sight
            bool blocked = Physics.Linecast(playerPos + Vector3.up,
                                            coverPos + Vector3.up,
                                            out RaycastHit hit,
                                            wallMask);
            if (!blocked) continue;

            float distBot = Vector3.Distance(botPos, coverPos);
            float distPlayer = Vector3.Distance(playerPos, coverPos);

            float score = distPlayer - distBot; // prefer far from player but near bot

            if (score > bestScore)
            {
                bestScore = score;
                bestCoverPos = coverPos;
                bestWall = wall.transform;
            }

            if (showDebugLines)
                Debug.DrawLine(playerPos + Vector3.up, bestCoverPos + Vector3.up, Color.red, 0.2f);
        }

        //if (bestScore == float.MinValue)
        //{
        //    if (showDebugLines) Debug.Log("[Hide] No valid cover found.");
        //    agent.ResetPath();
        //    return;
        //}

        if (NavMesh.SamplePosition(bestCoverPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
            if (showDebugLines)
                Debug.DrawLine(transform.position + Vector3.up, navHit.position + Vector3.up, Color.blue, 1f);
        }
        else
        {
            if (showDebugLines) Debug.LogWarning($"[Hide] No NavMesh near cover {bestCoverPos}");
        }
    }


}
