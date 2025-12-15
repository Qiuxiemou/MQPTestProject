using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    //[RequireComponent(typeof(Health), typeof(Actor), typeof(NavMeshAgent), typeof(DetectionModule))]
    [RequireComponent(typeof(Health), typeof(Actor), typeof(NavMeshAgent))]
    //[RequireComponent(typeof(DetectionModule))]

    class CoverCandidate
    {
        public Transform group;     // WallGroup root
        public Vector3 pos;         // cover position
        public float distBot;       // distance from bot
        public float distPlayer;    // distance from player
        public float retreatScore;  // �safe retreat�
        public float quickScore;    // �fast to reach�
        public float flankScore;    // �good flank angle�
    }


    public class HiderController : MonoBehaviour
    {
        public enum HiderState
        {
            UnknownPlayer,   // doesn�t know where player is � peeks to gain info
            SeePlayer,        // has a LastSeenPosition � runs to best cover
            LostPlayerRecent // not seen player for 3 sec
        }

        // Small helper so we don't repeat LM.write formatting
        void Log(string msg)
        {
            LM.write($"[HIDER] {msg}");
        }

        [Header("General")]
        [Tooltip("The Y height at which the enemy will be automatically killed (if it falls off of the level)")]
        public float SelfDestructYHeight = -20f;

        [Tooltip("The distance at which the enemy considers that it has reached its current path destination point")]
        public float PathReachingRadius = 0.5f;

        [Tooltip("The speed at which the enemy rotates")]
        public float OrientationSpeed = 10f;

        [Header("Peek Settings")]
        [Tooltip("Radius to search for nearby walls around current cover")]
        public float wallSearchRadius = 3f;

        [Tooltip("Wall layers used for peeking and hiding")]
        public LayerMask wallMask;

        [Tooltip("Sideways offset along the wall for base peek position")]
        public float sideOffset = 0.7f;

        [Tooltip("Extra step out from the wall when fully peeking")]
        public float forwardOffset = 20f;

        [Tooltip("How close to destination before we consider it reached")]
        public float reachThreshold = 0.02f;

        [Tooltip("Pause duration while exposed (peeking) before returning to cover")]
        public float midDelay = 0.3f;

        [Header("Cover Selection (when SeePlayer)")]
        [Tooltip("Search radius for candidate cover walls when hiding from player")]
        public float coverSearchRadius = 20f;

        [Tooltip("Offset from wall center along direction away from player")]
        public float coverOffset = 3f;

        [Tooltip("Draw debug lines for chosen cover positions")]
        public bool showDebugCover = true;

        [Header("Idle Scan (UnknownPlayer)")]
        [Tooltip("Half angle for look-around (total sweep is 2 * IdleScanHalfAngle)")]
        public float IdleScanHalfAngle = 90f;   // 180� total

        [Tooltip("Time (seconds) for one left-right sweep")]
        public float IdleScanDuration = 2f;

        [Tooltip("How many left-right sweeps to do each time we scan")]
        public int IdleScanCycles = 1;

        [Range(0f, 1f)]
        [Tooltip("Chance, after scanning, to move to a new nearby cover spot")]
        public float IdleMoveCoverChance = 0.3f;

        [Tooltip("How many quick out-in peeks to do at the chosen corner")]
        public int peekJiggleCount = 10;

        [Tooltip("Pause time while back at the corner between jiggled peeks")]
        public float betweenPeekDelay = 0.5f;

        bool _isUnknownRoutineRunning;

        [SerializeField]
        public Transform[] PeekNodes;

        // References
        public NavMeshAgent NavMeshAgent { get; private set; }
        public DetectionModule DetectionModule { get; private set; }
        public GameObject KnownDetectedTarget => DetectionModule.KnownDetectedTarget;
        public bool IsTargetInAttackRange => DetectionModule.IsTargetInAttackRange;
        public bool IsSeeingTarget => DetectionModule.IsSeeingTarget;
        public bool HadKnownTarget => DetectionModule.HadKnownTarget;

        public UnityAction onDamaged;

        Health m_Health;
        Actor m_Actor;
        Collider[] m_SelfColliders;
        EnemyManager m_EnemyManager;
        GameFlowManager m_GameFlowManager;

        // State machine
        HiderState _state = HiderState.UnknownPlayer;

        // Cover peek data
        Vector3 _coverPos;          // current "behind cover" anchor
        Vector3 _peekLeftPos;       // dynamic peek position (left corner)
        Vector3 _peekRightPos;      // dynamic peek position (right corner)
        Vector3 _peekThirdPos;      
        Vector3 _wallNormal;        // outward normal of the wall
        bool _hasValidPeekPositions = false;

        bool _isPeeking = false;

        // Hiding / cover movement when SeePlayer
        bool _isChoosingCover = false;
        Vector3 _currentCoverTarget;
        Vector3 _lastKnownPlayerPos;


        void Start()
        {
            // Managers
            m_EnemyManager = FindAnyObjectByType<EnemyManager>();
            m_GameFlowManager = FindAnyObjectByType<GameFlowManager>();
            m_Actor = GetComponent<Actor>();
            m_Health = GetComponent<Health>();
            NavMeshAgent = GetComponent<NavMeshAgent>();


            var detectionModules = GetComponentsInChildren<DetectionModule>();
            DebugUtility.HandleErrorIfNoComponentFound<DetectionModule, EnemyController>(detectionModules.Length, this,
                gameObject);
            DebugUtility.HandleWarningIfDuplicateObjects<DetectionModule, EnemyController>(detectionModules.Length,
                this, gameObject);
            // Initialize detection module
            DetectionModule = detectionModules[0];

            m_SelfColliders = GetComponentsInChildren<Collider>();

            if (m_EnemyManager != null)
            {
                // optional: if you use EnemyManager just for counting, you can pass null
                m_EnemyManager.RegisterEnemy(null);
                Log("Registered with EnemyManager");
            }

            // Subscribe to health events
            if (m_Health != null)
            {
                m_Health.OnDie += OnDie;
                m_Health.OnDamaged += OnDamaged;
            }

            // Initial state  cover position (start where the bot spawns)
            _state = HiderState.UnknownPlayer;
            _coverPos = transform.position;

            Log($"Start at {_coverPos} | Initial State = {_state}");

            _lastKnownPlayerPos = new Vector3(72.8f, 2.24f, 17.34f);

            // Start FSM as a coroutine so peeking can use coroutines easily
            StartCoroutine(StateMachineLoop());
        }

        void Update()
        {
            EnsureIsWithinLevelBounds();
            if (DetectionModule != null && m_Actor != null && m_SelfColliders != null)
            {
                DetectionModule.HandleTargetDetection(m_Actor, m_SelfColliders);
            }


            // Always run detection every frame

            // Lock to last see player position
            Vector3 toLastSeen = _lastKnownPlayerPos - transform.position;

            Vector3 lookDir = toLastSeen.normalized;
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * OrientationSpeed);

            // Handle arrival at chosen cover (for SeePlayer state)
            if (_isChoosingCover && NavMeshAgent.remainingDistance <= PathReachingRadius && !NavMeshAgent.pathPending)
            {
                Log($"Reached new cover at {transform.position} | switching to UnknownPlayer + will peek again");
                // Reached new cover
                _coverPos = transform.position;
                _isChoosingCover = false;
                // After reaching cover, go back to peeking to regain info
                _state = HiderState.UnknownPlayer;
            }
        }

        IEnumerator StateMachineLoop()
        {
            while (true)
            {
                // 1. State transitions based on detection
                if (DetectionModule != null)
                {
                    if (DetectionModule.HadKnownTarget)
                    {
                        // We see the player right now
                        _lastKnownPlayerPos = DetectionModule.LastSeenPosition;

                        if (_state != HiderState.SeePlayer)
                        {
                            Log($"DetectionModule sees target at {_lastKnownPlayerPos} -> State SeePlayer");
                            _state = HiderState.SeePlayer;
                        }
                    }
                    //} else if (DetectionModule.KnownDetectedTarget == null)
                    //{
                    //    _state = HiderState.UnknownPlayer;
                    //}
                    else
                    {
                        _state = HiderState.UnknownPlayer;
                    }
                }
                

                // 2. State behaviour
                switch (_state)
                {
                    case HiderState.UnknownPlayer:
                        {
                            if (!_isPeeking)
                            {
                                Log("State UnknownPlayer: starting UnknownPlayerRoutine");
                                StartCoroutine(PeekRoutine());
                                //StartCoroutine(UnknownPlayerRoutine());
                            }
                            break;
                        }

                    case HiderState.SeePlayer:
                        {
                            // Find cover based on lastKnownPlayerPos and move there
                            if (!_isChoosingCover)
                            {
                                Log($"State SeePlayer: choosing cover vs player at {_lastKnownPlayerPos}");
                                if (ReachedDestination())
                                {
                                    ChooseCoverAndMove(_lastKnownPlayerPos);

                                }

                            }
                            break;
                        }
                }

                yield return null;
            }
        }

        void EnsureIsWithinLevelBounds()
        {
            if (transform.position.y < SelfDestructYHeight)
            {
                Log($"Below SelfDestructYHeight ({SelfDestructYHeight}) � destroying hider");
                Destroy(gameObject);
                return;
            }
        }

        #region Peek Logic

        IEnumerator PeekRoutine()
        {
            _isPeeking = true;
            _coverPos = transform.position;
            Log($"PeekRoutine started from coverPos = {_coverPos}");

            // 1. Recompute peek positions around current cover location
            RecomputePeekPositions(_coverPos);
            if (!_hasValidPeekPositions)
            {
                Log("PeekRoutine aborted � no valid peek positions");
                _isPeeking = false;
                yield break;
            }

            // 2. Randomly choose left or right corner ONCE for this whole jiggle sequence
            Vector3 basePeek = Vector3.zero;
            if (Random.value >0.66)
            {
                basePeek = _peekLeftPos;
            } else if (Random.value > 0.33)
            {
                basePeek = _peekRightPos;
            } else
            {
                basePeek = _peekThirdPos;
            }
            // 3. Go to exact cover position first (tuck in behind the wall)
            NavMeshAgent.SetDestination(_coverPos);
            while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                yield return null;

            // 4. Move from cover to the chosen corner (still mostly safe)
            NavMeshAgent.SetDestination(basePeek);
            while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                yield return null;

            // 5. Prepare outward direction and peekOut position
            //    wallNormal is wall -> bot; outward is bot -> exposed
            Vector3 outward = -_wallNormal;
            Vector3 peekOut = basePeek + outward * forwardOffset;

            Log($"PeekRoutine: basePeek={basePeek}, outward={outward}, peekOut={peekOut}");

            // 6. Jiggle peek: out-in-out-in for peekJiggleCount cycles
            for (int i = 0; i < peekJiggleCount; i++)
            {

                Vector3 toLastSeen = _lastKnownPlayerPos - transform.position;
                Vector3 lookDir = toLastSeen.normalized;
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    Time.deltaTime * OrientationSpeed);


                // ---- STEP OUT (expose) ----
                Log($"PeekRoutine: Jiggle #{i + 1}/{peekJiggleCount} -> STEP OUT to {peekOut}");
                NavMeshAgent.SetDestination(peekOut);
                while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                    yield return null;

                // Wait out in the open for a short time � midDelay is your "exposed time"
                yield return new WaitForSeconds(midDelay);

                bool sawDuringPeek = DetectionModule != null && DetectionModule.IsSeeingTarget;
                Log($"PeekRoutine: Jiggle #{i + 1} exposed, IsSeeingTarget={sawDuringPeek}");

                if (sawDuringPeek)
                {
                    _lastKnownPlayerPos = DetectionModule.LastSeenPosition;
                    Log($"PeekRoutine: PLAYER SPOTTED at {_lastKnownPlayerPos} -> State SeePlayer");
                    _state = HiderState.SeePlayer;
                    _isPeeking = false;
                    yield break;
                }

                // ---- STEP BACK (safe again) ----
                Log($"PeekRoutine: Jiggle #{i + 1} -> STEP BACK to basePeek {basePeek}");
                NavMeshAgent.SetDestination(basePeek);
                while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                    yield return null;

                // Small delay behind cover before next jiggle (optional)
                if (i < peekJiggleCount - 1 && betweenPeekDelay > 0f)
                    yield return new WaitForSeconds(betweenPeekDelay);
            }

            // 7. After all jiggling, go fully back to the main cover position
            Log("PeekRoutine: finished all jiggle peeks, returning to _coverPos");
            NavMeshAgent.SetDestination(_coverPos);
            while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                yield return null;

            _isPeeking = false;

            // 8. After peeking, possibly move to a new nearby cover (your existing behavior)
            float roll = Random.value;
            Log($"PeekRoutine: finished, roll={roll:F2}, moveChance={IdleMoveCoverChance:F2}");

            if (roll < IdleMoveCoverChance)
            {
                Log("PeekRoutine: roll succeeded -> MoveToRandomNearbyCover");
                MoveToRandomNearbyCover();
            }
            else
            {
                Log("PeekRoutine: staying at current cover");
            }
        }

        /// <summary>
        /// Compute peek positions by finding the closest wall, its face normal,
        /// then detecting approximate left/right corners and peeking slightly past them.
        /// </summary>
        //void RecomputePeekPositions(Vector3 origin)
        //{
        //    _hasValidPeekPositions = false;

        //    Log($"RecomputePeekPositions: origin = {origin}, radius = {wallSearchRadius}");

        //    // 1. Find nearby walls
        //    Collider[] walls = Physics.OverlapSphere(origin, wallSearchRadius, wallMask);
        //    if (walls.Length == 0)
        //    {
        //        Log("RecomputePeekPositions: no walls found");
        //        return;
        //    }

        //    // 2. Find closest wall
        //    Collider bestWall = null;
        //    float bestDist = float.MaxValue;

        //    foreach (Collider wall in walls)
        //    {
        //        Vector3 closest = wall.ClosestPoint(origin);
        //        float sq = (closest - origin).sqrMagnitude;
        //        if (sq < bestDist)
        //        {
        //            bestDist = sq;
        //            bestWall = wall;
        //        }
        //    }

        //    if (bestWall == null)
        //    {
        //        Log("RecomputePeekPositions: bestWall is null");
        //        return;
        //    }

        //    // 3. Raycast from bot to wall center to get real face & normal
        //    Vector3 originRay = origin + Vector3.up;
        //    Vector3 wallCenter = bestWall.bounds.center + Vector3.up;

        //    if (!Physics.Linecast(originRay, wallCenter, out RaycastHit faceHit, wallMask))
        //    {
        //        Log($"RecomputePeekPositions: linecast to wall {bestWall.name} failed");
        //        return;
        //    }

        //    Vector3 hitPoint = faceHit.point;
        //    Vector3 wallNormal = faceHit.normal;
        //    wallNormal.y = 0f;
        //    if (wallNormal.sqrMagnitude < 0.0001f)
        //        wallNormal = -transform.forward;
        //    wallNormal.Normalize();
        //    _wallNormal = wallNormal;

        //    // 4. Tangent along wall surface
        //    Vector3 tangent = Vector3.Cross(Vector3.up, wallNormal).normalized;

        //    // 5. Find approximate left/right corners by walking along tangent inside bounds
        //    float cornerStep = 0.2f;
        //    float maxCornerScan = 10f;

        //    Vector3 leftCorner = hitPoint;
        //    Vector3 rightCorner = hitPoint;

        //    // LEFT corner
        //    for (float t = 0; t < maxCornerScan; t += cornerStep)
        //    {
        //        Vector3 p = hitPoint - tangent * t;
        //        if (!bestWall.bounds.Contains(p))
        //        {
        //            leftCorner = p - wallNormal * 0.1f;
        //            break;
        //        }
        //        leftCorner = p;
        //    }

        //    // RIGHT corner
        //    for (float t = 0; t < maxCornerScan; t += cornerStep)
        //    {
        //        Vector3 p = hitPoint + tangent * t;
        //        if (!bestWall.bounds.Contains(p))
        //        {
        //            rightCorner = p - wallNormal * 0.1f;
        //            break;
        //        }
        //        rightCorner = p;
        //    }

        //    // 6. Peek points slightly outside wall, away from its surface
        //    Vector3 leftPeek = leftCorner - wallNormal * forwardOffset;
        //    Vector3 rightPeek = rightCorner - wallNormal * forwardOffset;

        //    // 7. NavMesh sampling
        //    if (!NavMesh.SamplePosition(leftPeek, out NavMeshHit leftHit, 10f, NavMesh.AllAreas))
        //        leftHit.position = leftPeek;
        //    if (!NavMesh.SamplePosition(rightPeek, out NavMeshHit rightHit, 10f, NavMesh.AllAreas))
        //        rightHit.position = rightPeek;

        //    _peekLeftPos = leftHit.position;
        //    _peekRightPos = rightHit.position;
        //    _hasValidPeekPositions = true;

        //    Log($"RecomputePeekPositions: wall={bestWall.name}, hitPoint={hitPoint}, normal={_wallNormal}");
        //    Log($"RecomputePeekPositions: LEFT={_peekLeftPos}, RIGHT={_peekRightPos}");

        //    Debug.DrawLine(origin, _peekLeftPos + Vector3.up * 0.1f, Color.green, 1f);
        //    Debug.DrawLine(origin, _peekRightPos + Vector3.up * 0.1f, Color.blue, 1f);
        //    //Debug.DrawRay(hitPoint, wallNormal, Color.red, 1f);
        //}

        void RecomputePeekPositions(Vector3 origin)
        {
            _hasValidPeekPositions = false;

            if (PeekNodes == null || PeekNodes.Length == 0)
            {
                Log("RecomputePeekPositions: No PeekNodes assigned!");
                return;
            }

            Transform closestA = null;
            Transform closestB = null;
            Transform closestC = null;

            float bestA = float.MaxValue;
            float bestB = float.MaxValue;
            float bestC = float.MaxValue;

            foreach (Transform node in PeekNodes)
            {
                if (node == null) continue;

                float d = (node.position - origin).sqrMagnitude;

                if (d < bestA)
                {
                    // shift A -> B -> C
                    bestC = bestB; closestC = closestB;
                    bestB = bestA; closestB = closestA;

                    bestA = d; closestA = node;
                }
                else if (d < bestB)
                {
                    // shift B -> C
                    bestC = bestB; closestC = closestB;

                    bestB = d; closestB = node;
                }
                else if (d < bestC)
                {
                    bestC = d; closestC = node;
                }
            }

            if (closestA == null)
            {
                Log("RecomputePeekPositions: Could not find closest node!");
                return;
            }

            // Fallbacks if you have fewer than 3 nodes
            if (closestB == null) closestB = closestA;
            if (closestC == null) closestC = closestB;

            // Assign
            _peekLeftPos = closestA.position;
            _peekRightPos = closestB.position;
            _peekThirdPos = closestC.position;

            // A simple "outward" direction based on the closest node
            _wallNormal = (origin - closestA.position).normalized;

            _hasValidPeekPositions = true;

            Log($"RecomputePeekPositions: A={closestA.name}, B={closestB.name}, C={closestC.name}");

            Debug.DrawLine(origin, _peekLeftPos, Color.green, 2f);
            Debug.DrawLine(origin, _peekRightPos, Color.blue, 2f);
            Debug.DrawLine(origin, _peekThirdPos, Color.yellow, 2f);
        }


        #endregion

        bool ReachedDestination()
        {
            if (NavMeshAgent.pathPending) return false;
            if (NavMeshAgent.remainingDistance > NavMeshAgent.stoppingDistance) return false;
            if (NavMeshAgent.hasPath && NavMeshAgent.velocity.sqrMagnitude > 0.001f) return false;
            return true;
        }

        #region Looking around

        IEnumerator UnknownPlayerRoutine()
        {
            _isUnknownRoutineRunning = true;

            // remember the �center� yaw we scan around
            float baseYaw = transform.eulerAngles.y;

            Log($"UnknownPlayerRoutine: starting idle scan at yaw={baseYaw:F1}");

            for (int cycle = 0; cycle < IdleScanCycles; cycle++)
            {
                float timer = 0f;
                while (timer < IdleScanDuration)
                {
                    timer += Time.deltaTime;
                    float t = timer / IdleScanDuration; // 0..1

                    // Ping-pong from -1 to +1
                    float normalized = Mathf.PingPong(t * 2f, 1f) * 2f - 1f; // -1..1
                    float targetYaw = baseYaw + normalized * IdleScanHalfAngle;

                    Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRot,
                        Time.deltaTime * OrientationSpeed);

                    // If we see player while scanning, bail out
                    if (DetectionModule != null && DetectionModule.IsSeeingTarget)
                    {
                        _lastKnownPlayerPos = DetectionModule.LastSeenPosition;
                        Log($"UnknownPlayerRoutine: spotted target while scanning at {_lastKnownPlayerPos} -> SeePlayer");
                        _state = HiderState.SeePlayer;
                        _isUnknownRoutineRunning = false;
                        yield break;
                    }

                    yield return null;
                }
            }

            // After scanning, maybe move to a new nearby cover
            float roll = Random.value;
            Log($"UnknownPlayerRoutine: finished scan, roll={roll:F2}, moveChance={IdleMoveCoverChance:F2}");

            if (roll < IdleMoveCoverChance)
            {
                Log("UnknownPlayerRoutine: roll succeeded -> MoveToRandomNearbyCover");
                MoveToRandomNearbyCover();
            }
            else
            {
                Log("UnknownPlayerRoutine: staying at current cover");
            }

            _isUnknownRoutineRunning = false;
        }

        #endregion

        #region Cover Selection 

        /// <summary>
        /// Without knowing player position, pick 1 of the 2 closest walls
        /// and move to a point in front of that wall (becoming new _coverPos).
        /// </summary>
        void MoveToRandomNearbyCover()
        {
            Vector3 botPos = transform.position;
            Collider[] walls = Physics.OverlapSphere(botPos, coverSearchRadius, wallMask);

            var candidates = new List<(float dist, Vector3 pos)>();

            foreach (Collider wall in walls)
            {
                if (wall.attachedRigidbody && wall.attachedRigidbody.transform == transform)
                    continue;

                // skip floors
                float upDot = Vector3.Dot(Vector3.up, wall.transform.up);
                if (upDot > 0.8f)
                    continue;

                Vector3 wallPos = wall.transform.position;
                float dist = Vector3.Distance(botPos, wallPos);

                // coverPos is a bit in front of the wall from the bot's viewpoint
                Vector3 dirBotToWall = (wallPos - botPos).normalized;
                Vector3 coverPos = wallPos - dirBotToWall * coverOffset;

                candidates.Add((dist, coverPos));
            }

            if (candidates.Count == 0)
            {
                Log("MoveToRandomNearbyCover: no nearby walls");
                return;
            }

            // sort by distance, take up to 2
            candidates = candidates.OrderBy(c => c.dist).ToList();
            int count = Mathf.Min(2, candidates.Count);
            int idx = Random.Range(0, count);
            Vector3 chosenPos = candidates[idx].pos;

            if (NavMesh.SamplePosition(chosenPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                _currentCoverTarget = navHit.position;
                _isChoosingCover = true;        // reuse arrival logic in Update()
                NavMeshAgent.SetDestination(_currentCoverTarget);

                Log($"MoveToRandomNearbyCover: moving to idle cover index={idx} at {_currentCoverTarget}");
                if (showDebugCover)
                    Debug.DrawLine(transform.position + Vector3.up,
                                   _currentCoverTarget + Vector3.up,
                                   Color.cyan, 1f);
            }
            else
            {
                Log($"MoveToRandomNearbyCover: NavMesh.SamplePosition failed near {chosenPos}");
            }
        }



        /// <summary>
        /// Choose one of the top-3 best covers based on playerPos and run there.
        /// </summary>/**
        //void ChooseCoverAndMove(Vector3 playerPos)
        //{
        //    _isChoosingCover = true;

        //    Vector3 botPos = transform.position;
        //    Log($"ChooseCoverAndMove: botPos={botPos}, playerPos={playerPos}, radius={coverSearchRadius}");

        //    Collider[] walls = Physics.OverlapSphere(botPos, coverSearchRadius, wallMask);

        //    var validCovers = new List<(float score, Vector3 pos)>();

        //    foreach (Collider wall in walls)
        //    {
        //        if (wall.attachedRigidbody && wall.attachedRigidbody.transform == transform)
        //            continue;

        //        // skip floors (mostly horizontal surfaces)
        //        float upDot = Vector3.Dot(Vector3.up, wall.transform.up);
        //        if (upDot > 0.8f)
        //            continue;

        //        Vector3 dirPlayerToWall = (wall.transform.position - playerPos).normalized;
        //        Vector3 coverPos = wall.transform.position + dirPlayerToWall * coverOffset;

        //        // make sure wall actually blocks LOS
        //        bool blocked = Physics.Linecast(playerPos + Vector3.up,
        //                                        coverPos + Vector3.up,
        //                                        out RaycastHit hit,
        //                                        wallMask);
        //        if (!blocked)
        //            continue;

        //        float distBot = Vector3.Distance(botPos, coverPos);
        //        float distPlayer = Vector3.Distance(playerPos, coverPos);

        //        // prefer far from player but relatively close to bot
        //        float score = distPlayer - distBot;
        //        validCovers.Add((score, coverPos));

        //        if (showDebugCover)
        //            Debug.DrawLine(playerPos + Vector3.up, coverPos + Vector3.up, Color.red, 0.4f);
        //    }

        //    Log($"ChooseCoverAndMove: valid cover candidates = {validCovers.Count}");

        //    if (validCovers.Count == 0)
        //    {
        //        Log("ChooseCoverAndMove: NO valid covers found");
        //        _isChoosingCover = false;
        //        return;
        //    }

        //    // Sort by score descending, take top 3, pick random among them
        //    validCovers = validCovers.OrderByDescending(v => v.score).ToList();
        //    int takeCount = Mathf.Min(3, validCovers.Count);
        //    var topCovers = validCovers.Take(takeCount).ToList();

        //    for (int i = 0; i < topCovers.Count; i++)
        //    {
        //        Log($"ChooseCoverAndMove: Top[{i}] score={topCovers[i].score:F2}, pos={topCovers[i].pos}");
        //    }

        //    var chosen = topCovers[Random.Range(0, topCovers.Count)];
        //    Vector3 chosenPos = chosen.pos;

        //    Log($"ChooseCoverAndMove: CHOSEN cover score={chosen.score:F2}, rawPos={chosenPos}");

        //    if (NavMesh.SamplePosition(chosenPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        //    {
        //        _currentCoverTarget = navHit.position;
        //        NavMeshAgent.SetDestination(_currentCoverTarget);

        //        Log($"ChooseCoverAndMove: NavMesh position = {_currentCoverTarget}, starting move");
        //        if (showDebugCover)
        //            Debug.DrawLine(transform.position + Vector3.up, _currentCoverTarget + Vector3.up, Color.blue, 1.0f);
        //    }
        //    else
        //    {
        //        Log($"ChooseCoverAndMove: FAILED NavMesh.SamplePosition near {chosenPos}");
        //        _isChoosingCover = false;
        //    }
        //}


        //void ChooseCoverAndMove(Vector3 playerPos)
        //{
        //    Vector3 botPos = transform.position;

        //    Collider[] walls = Physics.OverlapSphere(botPos, 30f, wallMask);

        //    Vector3 bestCoverPos = Vector3.zero;
        //    Vector3 secondCoverPos = Vector3.zero;
        //    Vector3 thirdCoverPos = Vector3.zero;

        //    float bestScore = float.MinValue;
        //    float secondScore = float.MinValue;
        //    float thirdScore = float.MinValue;

        //    foreach (Collider wall in walls)
        //    {
        //        //if (wall.attachedRigidbody && wall.attachedRigidbody.transform == transform)
        //        //    continue; // skip self

        //        // Skip floors (mostly horizontal)
        //        //float upDot = Vector3.Dot(Vector3.up, wall.transform.up);
        //        //if (upDot > 0.8f)
        //        //    continue;

        //        Vector3 dirPlayerToObstacle = (wall.transform.position - playerPos).normalized;
        //        Vector3 coverPos = wall.transform.position + dirPlayerToObstacle * coverOffset;

        //        // Check if this wall actually blocks LOS from player -> coverPos
        //        bool blocked = Physics.Linecast(playerPos + Vector3.up,
        //                                        coverPos + Vector3.up,
        //                                        out RaycastHit hit,
        //                                        wallMask);
        //        if (!blocked)
        //            continue;

        //        float distBot = Vector3.Distance(botPos, coverPos);
        //        float distPlayer = Vector3.Distance(playerPos, coverPos);

        //        float score = distPlayer - distBot; // prefer far from player but near bot

        //        //if (score > bestScore)
        //        //{
        //        //    bestScore = score;
        //        //    bestCoverPos = coverPos;
        //        //}



        //        // --- Maintain top 3 scores ---
        //        if (score > bestScore)
        //        {
        //            // shift down
        //            thirdScore = secondScore;
        //            thirdCoverPos = secondCoverPos;

        //            secondScore = bestScore;
        //            secondCoverPos = bestCoverPos;

        //            bestScore = score;
        //            bestCoverPos = coverPos;
        //        }
        //        else if (score > secondScore)
        //        {
        //            thirdScore = secondScore;
        //            thirdCoverPos = secondCoverPos;

        //            secondScore = score;
        //            secondCoverPos = coverPos;
        //        }
        //        else if (score > thirdScore)
        //        {
        //            thirdScore = score;
        //            thirdCoverPos = coverPos;
        //        }

        //        //Debug.DrawLine(playerPos + Vector3.up, coverPos + Vector3.up, Color.blue, 0.2f);
        //    }

        //    // No valid covers at all
        //    //if (bestScore == float.NegativeInfinity)
        //    //{
        //    //    LM.write("[HIDER] ChooseCoverAndMove: no valid covers found");
        //    //    return;
        //    //}

        //    Debug.DrawLine(playerPos + Vector3.up, bestCoverPos + Vector3.up, Color.red, 0.2f);
        //    Debug.DrawLine(playerPos + Vector3.up, secondCoverPos + Vector3.up, Color.green, 0.2f);
        //    Debug.DrawLine(playerPos + Vector3.up, thirdCoverPos + Vector3.up, Color.yellow, 0.2f);

        //    // ----- Weighted random pick -----
        //    Vector3 chosenPos;

        //    if (thirdScore == float.NegativeInfinity)
        //    {
        //        // Only 1 or 2 covers available -> simple fallback
        //        if (secondScore == float.NegativeInfinity)
        //        {
        //            chosenPos = bestCoverPos; // only one choice
        //        }
        //        else
        //        {
        //            // 2 choices -> give a bit more weight to best
        //            float r = Random.value;
        //            chosenPos = (r < 0.6f) ? bestCoverPos : secondCoverPos;
        //        }
        //    }
        //    else
        //    {
        //        // Full top-3: best 0.4, second 0.3, third 0.3
        //        float r = Random.value;
        //        if (r < 0.4f)
        //            chosenPos = bestCoverPos;
        //        else if (r < 0.7f) // 0.7�0.4 => 0.3
        //            chosenPos = secondCoverPos;
        //        else              // 1-0.7 => 0.3
        //            chosenPos = thirdCoverPos;
        //    }

        //    //Vector3 chosenPos = bestCoverPos;
        //    LM.write($"chosenPos = {chosenPos}");

        //    // ----- Move to chosen cover -----
        //    if (NavMesh.SamplePosition(chosenPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        //    {
        //        NavMeshAgent.SetDestination(navHit.position);
        //        Debug.DrawLine(transform.position + Vector3.up, navHit.position + Vector3.up, Color.blue, 1f);
        //        LM.write($"[HIDER] ChooseCoverAndMove: chosen cover = {navHit.position}");
        //    }
        //    else
        //    {
        //        LM.write($"[HIDER] ChooseCoverAndMove: no NavMesh near chosen cover {chosenPos}");
        //    }


        //}

        //void ChooseCoverAndMove(Vector3 playerPos)
        //{
        //    Vector3 botPos = transform.position;

        //    Collider[] walls = Physics.OverlapSphere(botPos, 30f, wallMask);

        //    // key = WallGroup transform, value = best (score, coverPos) for that group
        //    var groupBest = new Dictionary<Transform, (float score, Vector3 coverPos)>();

        //    foreach (Collider wall in walls)
        //    {
        //        if (wall == null) continue;

        //        // 1) Find which wall group this collider belongs to
        //        WallGroup wg = wall.GetComponentInParent<WallGroup>();
        //        Transform groupKey = wg != null ? wg.transform : wall.transform;

        //        // -------- scoring for this collider ----------
        //        Vector3 dirPlayerToObstacle = (wall.transform.position - playerPos).normalized;
        //        Vector3 coverPos = wall.transform.position + dirPlayerToObstacle * coverOffset;

        //        // must actually block line of sight
        //        bool blocked = Physics.Linecast(playerPos + Vector3.up,
        //                                        coverPos + Vector3.up,
        //                                        out RaycastHit hit,
        //                                        wallMask);
        //        if (!blocked)
        //            continue;

        //        float distBot = Vector3.Distance(botPos, coverPos);
        //        float distPlayer = Vector3.Distance(playerPos, coverPos);
        //        float score = distPlayer - distBot; // far from player, close to bot

        //        // 2) For this WallGroup, keep only the BEST collider
        //        if (!groupBest.TryGetValue(groupKey, out var current) || score > current.score)
        //        {
        //            groupBest[groupKey] = (score, coverPos);
        //        }
        //    }

        //    if (groupBest.Count == 0)
        //    {
        //        LM.write("[HIDER] ChooseCoverAndMove: no valid covers found");
        //        return;
        //    }

        //    // 3) Sort groups by score and take top 3 distinct walls
        //    var topGroups = groupBest.Values
        //        .OrderByDescending(g => g.score)
        //        .Take(3)
        //        .ToList();

        //    // Debug � each color = different wall group
        //    if (topGroups.Count > 0) Debug.DrawLine(playerPos + Vector3.up, topGroups[0].coverPos + Vector3.up, Color.red, 0.5f);
        //    if (topGroups.Count > 1) Debug.DrawLine(playerPos + Vector3.up, topGroups[1].coverPos + Vector3.up, Color.yellow, 0.5f);
        //    if (topGroups.Count > 2) Debug.DrawLine(playerPos + Vector3.up, topGroups[2].coverPos + Vector3.up, Color.cyan, 0.5f);

        //    // 4) Weighted random pick across **different walls**
        //    Vector3 chosenPos;
        //    if (topGroups.Count == 1)
        //    {
        //        chosenPos = topGroups[0].coverPos;
        //    }
        //    else if (topGroups.Count == 2)
        //    {
        //        float r = Random.value;
        //        chosenPos = (r < 0.6f) ? topGroups[0].coverPos : topGroups[1].coverPos;
        //    }
        //    else
        //    {
        //        float r = Random.value;
        //        if (r < 0.4f) chosenPos = topGroups[0].coverPos;
        //        else if (r < 0.7f) chosenPos = topGroups[1].coverPos;
        //        else chosenPos = topGroups[2].coverPos;
        //    }

        //    // 5) Move there on NavMesh
        //    if (NavMesh.SamplePosition(chosenPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        //    {
        //        NavMeshAgent.SetDestination(navHit.position);
        //        Debug.DrawLine(transform.position + Vector3.up, navHit.position + Vector3.up, Color.blue, 1f);
        //        LM.write($"[HIDER] ChooseCoverAndMove: chosen cover = {navHit.position}");
        //    }
        //    else
        //    {
        //        LM.write($"[HIDER] ChooseCoverAndMove: no NavMesh near chosen cover {chosenPos}");
        //    }
        //}

        void ChooseCoverAndMove(Vector3 playerPos)
        {
            Vector3 botPos = transform.position;

            Collider[] walls = Physics.OverlapSphere(botPos, 30f, wallMask);

            // best cover per WallGroup
            var groupBest = new Dictionary<Transform, CoverCandidate>();

            foreach (Collider wall in walls)
            {
                if (wall == null) continue;

                // 1) Which wall group?
                WallGroup wg = wall.GetComponentInParent<WallGroup>();
                Transform groupKey = wg != null ? wg.transform : wall.transform;

                // ---------- candidate cover pos for THIS collider ----------
                Vector3 dirPlayerToObstacle = (wall.transform.position - playerPos).normalized;
                Vector3 coverPos = wall.transform.position + dirPlayerToObstacle * coverOffset;

                // Must actually block line of sight
                bool blocked = Physics.Linecast(
                    playerPos + Vector3.up,
                    coverPos + Vector3.up,
                    out RaycastHit hit,
                    wallMask);

                if (!blocked)
                    continue;

                float distBot = Vector3.Distance(botPos, coverPos);
                float distPlayer = Vector3.Distance(playerPos, coverPos);

                // ---------- 3 different �human� scores ----------

                // 1. Retreat: far from player, but don�t punish bot distance as much
                float retreatScore = distPlayer - 0.5f * distBot;

                // 2. Quick: just how fast to reach (closer is better)
                float quickScore = -distBot;   // smaller dist => larger score

                // 3. Flank: want roughly 90� off the player, not straight back
                Vector3 toPlayer = (playerPos - botPos).normalized;
                Vector3 toCover = (coverPos - botPos).normalized;
                float angle = Vector3.Angle(toPlayer, toCover); // 0 = in front, 180 = behind
                                                                // value in [0,1], 1 when angle = 90�, 0 when = 0 or 180
                float flankAngleScore = 1f - Mathf.Abs(angle - 90f) / 90f;
                // small bonus for being a bit away from the player
                float flankScore = flankAngleScore + 0.2f * (distPlayer / (coverSearchRadius + 0.001f));

                var cand = new CoverCandidate
                {
                    group = groupKey,
                    pos = coverPos,
                    distBot = distBot,
                    distPlayer = distPlayer,
                    retreatScore = retreatScore,
                    quickScore = quickScore,
                    flankScore = flankScore
                };

                // For each WallGroup, keep the **best retreat** candidate (as baseline)
                if (!groupBest.TryGetValue(groupKey, out var current) ||
                    cand.retreatScore > current.retreatScore)
                {
                    groupBest[groupKey] = cand;
                }
            }

            if (groupBest.Count == 0)
            {
                LM.write("[HIDER] ChooseCoverAndMove: no valid covers found");
                return;
            }

            var list = groupBest.Values.ToList();

            // ---------- pick 3 �styles� from different walls if possible ----------

            // safest retreat
            CoverCandidate safest =
                list.OrderByDescending(c => c.retreatScore).First();

            // fastest to reach (different wall if we can)
            CoverCandidate fastest =
                list.Where(c => c.group != safest.group)
                    .OrderByDescending(c => c.quickScore)
                    .DefaultIfEmpty(safest)
                    .First();

            // best flank (different from others if possible)
            CoverCandidate flanker =
                list.Where(c => c.group != safest.group && c.group != fastest.group)
                    .OrderByDescending(c => c.flankScore)
                    .DefaultIfEmpty(safest)
                    .First();

            // Debug lines: red = safest, yellow = fastest, cyan = flanker
            Debug.DrawLine(playerPos + Vector3.up, safest.pos + Vector3.up, Color.red, 0.5f);
            if (fastest != safest)
                Debug.DrawLine(playerPos + Vector3.up, fastest.pos + Vector3.up, Color.yellow, 0.5f);
            if (flanker != safest && flanker != fastest)
                Debug.DrawLine(playerPos + Vector3.up, flanker.pos + Vector3.up, Color.cyan, 0.5f);

            // ---------- weighted random between these three �styles� ----------

            Vector3 chosenPos;
            float r = Random.value;

            if (r < 0.4f)          // 40% safest
                chosenPos = safest.pos;
            else if (r < 0.7f)     // 30% fastest
                chosenPos = fastest.pos;
            else                   // 30% flanker
                chosenPos = flanker.pos;

            // ---------- move there on NavMesh ----------

            if (NavMesh.SamplePosition(chosenPos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                NavMeshAgent.SetDestination(navHit.position);
                Debug.DrawLine(transform.position + Vector3.up, navHit.position + Vector3.up, Color.blue, 1f);
                LM.write($"[HIDER] ChooseCoverAndMove: chosen cover = {navHit.position}");
            }
            else
            {
                LM.write($"[HIDER] ChooseCoverAndMove: no NavMesh near chosen cover {chosenPos}");
            }
        }





        #endregion



        #region Damage / Death (minimal)

        void OnDamaged(float damage, GameObject source)
        {
            Log($"OnDamaged: damage={damage}, source={(source ? source.name : "null")}");

            // Optional: you could make hider immediately switch to SeePlayer when shot
            if (source != null && !source.GetComponent<HiderController>())
            {
                if (DetectionModule != null)
                {
                    DetectionModule.OnDamaged(source);
                    Log("OnDamaged: forwarded to DetectionModule.OnDamaged");
                }
            }
        }

        void OnDie()
        {
            Log("OnDie: unregistering and destroying hider");

            if (m_EnemyManager != null)
            {
                m_EnemyManager.UnregisterEnemy(null);
            }

            Destroy(gameObject);
        }

        #endregion
    }
}
