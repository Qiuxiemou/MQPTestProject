using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

public class AIHide : MonoBehaviour
{
    public enum HiderState { UnknownPlayer, SeePlayer }

    [Header("References")]
    public Transform eye;
    public NavMeshAgent agent;
    public DetectionModule detection;

    [Header("Peek Settings")]
    public float peekDistance = 0.45f;
    public float peekStepOut = 0.35f;
    public float reachThreshold = 0.2f;
    public float midDelay = 0.3f;

    [Header("Cover Settings")]
    private Vector3 _coverPos;      // current cover anchor

    // dynamic peek positions
    private Vector3 _peekLeftPos;
    private Vector3 _peekRightPos;
    private Vector3 _wallNormal;
    private bool _hasValidPeekPositions;

    [Header("Wall / Obstacle Settings")]
    [SerializeField] private LayerMask wallMask;      // wall/cover layers
    [SerializeField] private LayerMask obstacleMask;  // blocks vision (not used yet)
    [SerializeField] private float wallSearchRadius = 3f;

    [Header("Peek Offsets")]
    [Tooltip("Sideways offset along the wall from the cover position.")]
    [SerializeField] private float sideOffset = 3f;
    [Tooltip("Extra step out from the wall when fully peeking.")]
    [SerializeField] private float forwardOffset = 0.5f;

    // Hiding / cover selection
    private Vector3 playerPos;
    private bool _peeking = false;
    private HiderState _state = HiderState.UnknownPlayer;

    // --- cover choosing control ---
    private bool _isChoosingCover = false;   // prevents changing destination mid-way
    private Vector3 _currentCoverTarget;     // where we are going now
    public float coverOffset = 3f;
    public float pickRadius = 20f;
    public bool showDebug = true;

    void Start()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (detection == null) detection = GetComponent<DetectionModule>();

        _state = HiderState.UnknownPlayer;

        // Start by treating current pos as cover anchor
        _coverPos = transform.position;

        LM.Write($"[FSM] Start in UnknownPlayer at {_coverPos}");

        StartCoroutine(StateMachineLoop());
    }

    void Update()
    {
        // When we're moving to a chosen cover, wait until we arrive,
        // then unlock choosing again and treat that position as new cover.
        if (_isChoosingCover &&
            !agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            _isChoosingCover = false;
            _coverPos = agent.transform.position;
            LM.Write($"[FSM] Arrived at cover, new coverPos = {_coverPos}");
        }
    }

    IEnumerator StateMachineLoop()
    {
        while (true)
        {
            // Refresh playerPos from DetectionModule if we have a known target
            if (detection.KnownDetectedTarget != null)
                playerPos = detection.KnownDetectedTarget.transform.position;
            else
                playerPos = detection.LastSeenPosition;

            // Handle detection-based state transitions
            if (detection.IsSeeingTarget)
            {
                if (_state != HiderState.SeePlayer)
                {
                    _state = HiderState.SeePlayer;
                    LM.Write($"[FSM] See Player ? State: SeePlayer POS : {playerPos}");
                }
            }
            else if (_state == HiderState.SeePlayer && !detection.HasLastSeenPosition)
            {
                // totally lost the player
                //_state = HiderState.UnknownPlayer;
                //LM.Write("[FSM] Lost target ? State: UnknownPlayer");
            }

            // FSM main behavior
            switch (_state)
            {
                case HiderState.UnknownPlayer:
                    // Just peek occasionally from current cover
                    if (!_peeking && !_isChoosingCover)
                        StartCoroutine(PeekRoutine());
                    break;

                case HiderState.SeePlayer:
                    // When we see the player, pick a cover ONCE and run there
                    if (!_isChoosingCover)
                    {
                        ChooseCoverAndMove();
                    }
                    // No other action yet (you could add "peek from new cover" later)
                    break;
            }

            yield return null;
        }
    }

    IEnumerator PeekRoutine()
    {
        _peeking = true;
        LM.Write("[FSM] PeekRoutine started");

        // recompute peek points around current cover position
        RecomputePeekPositions(_coverPos);
        if (!_hasValidPeekPositions)
        {
            LM.Write("[FSM] PeekRoutine: no valid peek positions, aborting.");
            _peeking = false;
            yield break;
        }

        bool startLeft = (Random.value < 0.5f);
        Vector3 basePeek = startLeft ? _peekLeftPos : _peekRightPos;
        LM.Write($"[FSM] Peek side: {(startLeft ? "Left" : "Right")} base={basePeek}");

        // 1. Go to cover first
        agent.SetDestination(_coverPos);
        while (agent.pathPending || agent.remainingDistance > reachThreshold)
            yield return null;

        // 2. Move to peek base position
        agent.SetDestination(basePeek);
        while (agent.pathPending || agent.remainingDistance > reachThreshold)
            yield return null;
        LM.Write("[FSM] Reached base peek position");

        // 3. Step outward slightly to expose (outwards from cover)
        Vector3 peekOutDir = (basePeek - _coverPos);
        peekOutDir.y = 0f;
        if (peekOutDir.sqrMagnitude < 0.0001f)
            peekOutDir = transform.forward;

        peekOutDir.Normalize();
        Vector3 peekOut = basePeek + peekOutDir * peekStepOut;

        agent.SetDestination(peekOut);
        while (agent.pathPending || agent.remainingDistance > reachThreshold)
            yield return null;

        LM.Write("[FSM] Now exposed ? scanning...");

        yield return new WaitForSeconds(midDelay);

        // 4. During exposed moment: if player seen ? switch state
        if (detection.IsSeeingTarget)
        {
            _state = HiderState.SeePlayer;
            LM.Write($"[FSM] Player spotted during peek ? SeePlayer POS {playerPos}");
            _peeking = false;
            yield break;
        }

        LM.Write("[FSM] Did not see player ? returning to cover");

        // 5. Return behind cover
        agent.SetDestination(_coverPos);
        while (agent.pathPending || agent.remainingDistance > reachThreshold)
            yield return null;

        _peeking = false;
    }

    // ---------------------------------------
    // Dynamic peek computation (closest wall)
    // ---------------------------------------
    private void RecomputePeekPositions(Vector3 origin)
    {
        // 1. Find nearby walls
        Collider[] walls = Physics.OverlapSphere(origin, wallSearchRadius, wallMask);
        if (walls.Length == 0)
        {
            LM.Write("[FSM] RecomputePeekPositions: no walls found.");
            _hasValidPeekPositions = false;
            return;
        }

        // 2. Find closest wall collider
        Collider bestWall = null;
        float bestDist = float.PositiveInfinity;

        foreach (Collider wall in walls)
        {
            Vector3 closest = wall.ClosestPoint(origin);
            float sq = (closest - origin).sqrMagnitude;
            if (sq < bestDist)
            {
                bestDist = sq;
                bestWall = wall;
            }
        }

        if (bestWall == null)
        {
            LM.Write("[FSM] RecomputePeekPositions: bestWall is null.");
            _hasValidPeekPositions = false;
            return;
        }

        // 3. Compute wall normal toward bot
        Vector3 wallClosest = bestWall.ClosestPoint(origin);

        // normal points from wall -> cover/bot
        Vector3 normal = (origin - wallClosest);
        normal.y = 0f;

        if (normal.sqrMagnitude < 0.001f)
        {
            // fallback
            normal = -transform.forward;
        }

        normal.Normalize();
        _wallNormal = normal;

        // 4. Compute tangent (left/right direction)
        Vector3 tangent = Vector3.Cross(Vector3.up, normal).normalized;

        // 5. Build peek positions
        Vector3 rawRight = origin + tangent * sideOffset;
        Vector3 rawLeft = origin - tangent * sideOffset;

        // 6. Snap to NavMesh
        bool rightOK = NavMesh.SamplePosition(rawRight, out NavMeshHit rightHit, 0.5f, NavMesh.AllAreas);
        bool leftOK = NavMesh.SamplePosition(rawLeft, out NavMeshHit leftHit, 0.5f, NavMesh.AllAreas);

        if (!rightOK && !leftOK)
        {
            LM.Write("[FSM] RecomputePeekPositions: both sides failed NavMesh.");
            _hasValidPeekPositions = false;
            return;
        }

        _peekRightPos = rightOK ? rightHit.position : rawRight;
        _peekLeftPos = leftOK ? leftHit.position : rawLeft;

        _hasValidPeekPositions = true;

        LM.Write("[FSM] RecomputePeekPositions: LEFT=" + _peekLeftPos +
                 " RIGHT=" + _peekRightPos + " normal=" + _wallNormal);

        Debug.DrawLine(origin, _peekLeftPos, Color.green, 1f);
        Debug.DrawLine(origin, _peekRightPos, Color.blue, 1f);
    }

    // ---------------------------------------
    // Cover choosing (top-3, random pick)
    // ---------------------------------------
    void ChooseCoverAndMove()
    {
        if (_isChoosingCover)
            return; // Already moving to a chosen cover — do NOT pick again

        _isChoosingCover = true;

        Vector3 botPos = transform.position;

        // If we somehow don't have a valid playerPos yet, fall back to current forward dir
        if (playerPos == Vector3.zero)
            playerPos = transform.position + transform.forward * 5f;

        Collider[] walls = Physics.OverlapSphere(botPos, pickRadius, wallMask);

        List<(float score, Vector3 pos)> validCovers = new List<(float, Vector3)>();

        foreach (Collider wall in walls)
        {
            if (wall.attachedRigidbody && wall.attachedRigidbody.transform == transform)
                continue;

            // Skip floors
            float upDot = Vector3.Dot(Vector3.up, wall.transform.up);
            if (upDot > 0.8f)
                continue;

            // Cover direction (away from player)
            Vector3 dirPlayerToObstacle = (wall.transform.position - playerPos).normalized;
            Vector3 coverPos = wall.transform.position + dirPlayerToObstacle * coverOffset;

            // Must block Line of Sight from player -> cover
            bool blocked = Physics.Linecast(
                playerPos + Vector3.up,
                coverPos + Vector3.up,
                out RaycastHit hit,
                wallMask
            );

            if (!blocked)
                continue;

            // Scoring - prefer cover far from player but close to bot
            float distBot = Vector3.Distance(botPos, coverPos);
            float distPlayer = Vector3.Distance(playerPos, coverPos);
            float score = distPlayer - distBot;

            validCovers.Add((score, coverPos));

            if (showDebug)
                Debug.DrawLine(playerPos + Vector3.up, coverPos + Vector3.up, Color.red, 0.3f);
        }

        if (validCovers.Count == 0)
        {
            Debug.LogWarning("[Hide] No valid covers found.");
            _isChoosingCover = false;
            return;
        }

        // Sort by best score descending
        validCovers = validCovers.OrderByDescending(v => v.score).ToList();

        // Take TOP 3 (or fewer if not enough)
        int takeCount = Mathf.Min(3, validCovers.Count);
        var topCovers = validCovers.Take(takeCount).ToList();

        // Pick one randomly
        var chosen = topCovers[Random.Range(0, topCovers.Count)];
        Vector3 chosenPos = chosen.pos;

        // Snap to NavMesh
        if (NavMesh.SamplePosition(chosenPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            _currentCoverTarget = navHit.position;
            agent.SetDestination(_currentCoverTarget);

            LM.Write($"[Hide] Chosen cover score={chosen.score:F2}, pos={_currentCoverTarget}");

            if (showDebug)
                Debug.DrawLine(transform.position + Vector3.up, _currentCoverTarget + Vector3.up, Color.blue, 1.0f);
        }
        else
        {
            Debug.LogWarning($"[Hide] Could not find NavMesh near chosen cover {chosenPos}");
            _isChoosingCover = false;
            return;
        }
    }
}
