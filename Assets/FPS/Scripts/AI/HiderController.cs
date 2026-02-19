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
            UnknownPlayer,   // doesnt know where player is peeks to gain info
            SeePlayer,        // has a LastSeenPosition runs to best cover
            LostPlayerRecent // not seen player for 3 sec
        }

        // Small helper so we don't repeat LM.write formatting
        void Log(string msg)
        {
            //LM.write($"[HIDER] {msg}");
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
        public float coverOffset = 5f;

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

        [Header("Lost Player Timing")]
        public float TimeToLostRecent = 2f;   // not seeing for 2s -> LostPlayerRecent
        public float TimeToForget = 5f;       // not seeing for 5s -> UnknownPlayer
        public float LostToUnknownAfterHide = 2f; // after reaching hide, wait 2s then Unknown

        float _lastTimeSawPlayer = -999f;
        bool _lostRecentEntered = false;
        Coroutine _lostWaitCoroutine;

        


        void Start()
        {
            //Debug.////Log("[HIDER] Debug.Log is working and Start() ran");
            if (PeekNodes == null || PeekNodes.Length == 0)
            {
                PeekNodes = FindPeekNodesInScene();
                //Debug.////Log($"[HIDER] Found {PeekNodes.Length} peek nodes at spawn");
            }

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
                ////Log("Registered with EnemyManager");
            }

            // Subscribe to health events
            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += OnDamaged;
            

            // Initial state  cover position (start where the bot spawns)
            _state = HiderState.UnknownPlayer;
            _coverPos = transform.position;

            ////Log($"Start at {_coverPos} | Initial State = {_state}");

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

            // Only face last known player position when it makes sense
            if (_state == HiderState.SeePlayer || _state == HiderState.LostPlayerRecent)
            {
                Vector3 toLastSeen = _lastKnownPlayerPos - transform.position;

                if (toLastSeen.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toLastSeen.normalized);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRot,
                        Time.deltaTime * OrientationSpeed);
                }
            } 


            // Handle arrival at chosen cover (for SeePlayer state)
            if (_isChoosingCover && NavMeshAgent.remainingDistance <= PathReachingRadius && !NavMeshAgent.pathPending)
            {
                //Log($"Reached new cover at {transform.position} | switching to UnknownPlayer + will peek again");
                // Reached new cover
                _coverPos = transform.position;
                _isChoosingCover = false;

                // After reaching cover, go back to peeking to regain info
                //_state = HiderState.UnknownPlayer;
                if (_state == HiderState.SeePlayer)
                {
                    //Log($"Reached cover in SeePlayer -> UnknownPlayer (peek again)");
                    _state = HiderState.UnknownPlayer;
                }
                else if (_state == HiderState.LostPlayerRecent)
                {
                    //Log($"Reached cover in LostPlayerRecent -> will wait then Unknown");
                    // stay in LostPlayerRecent; the state will handle the wait->Unknown
                }

            }
        }

        IEnumerator StateMachineLoop()
        {
            while (true)
            {
                // 1. State transitions based on detection
                if (DetectionModule != null)
                {
                    //if (DetectionModule.HadKnownTarget)
                    if (DetectionModule.IsSeeingTarget)
                    {
                        // We see the player right now
                        _lastKnownPlayerPos = DetectionModule.LastSeenPosition;
                        _lastTimeSawPlayer = Time.time;

                        if (_state != HiderState.SeePlayer)
                        {
                            //Debug.Log($"DetectionModule sees target at {_lastKnownPlayerPos} -> State SeePlayer");
                            _state = HiderState.SeePlayer;
                            _lostRecentEntered = false;

                            if (_lostWaitCoroutine != null) { StopCoroutine(_lostWaitCoroutine); _lostWaitCoroutine = null; }
                        }
                    }
                    //} else if (DetectionModule.KnownDetectedTarget == null)
                    //{
                    //    _state = HiderState.UnknownPlayer;
                    //}
                    else
                    {
                        float sinceSeen = Time.time - _lastTimeSawPlayer;

                        if (sinceSeen >= TimeToForget)
                        {
                            if (_state != HiderState.UnknownPlayer)
                                //Debug.//Log($"No sight for {sinceSeen:F1}s -> UnknownPlayer (forget)");
                            _state = HiderState.UnknownPlayer;
                            _lostRecentEntered = false;
                            //Debug.//Log("State UnknownPlayer");
                        }
                        else if (sinceSeen >= TimeToLostRecent)
                        {
                            if (_state != HiderState.LostPlayerRecent)
                                //Debug.//Log($"No sight for {sinceSeen:F1}s -> LostPlayerRecent");
                            _state = HiderState.LostPlayerRecent;
                        }
                        // else: still in SeePlayer for a short “grace” window
                    }
                }
                

                // 2. State behaviour
                switch (_state)
                {
                    case HiderState.UnknownPlayer:
                        {
                            LM.write("State UnknownPlayer");
                            if (!_isPeeking)
                            {
                                //Debug.//Log("State UnknownPlayer: starting UnknownPlayerRoutine");
                                StartCoroutine(PeekRoutine());
                                //StartCoroutine(UnknownPlayerRoutine());
                            }
                            // Start wall-looking scan if not already running
                            if (!_isUnknownRoutineRunning && _hasValidPeekPositions)
                            {
                                StartCoroutine(LookAlongWallRoutine());
                            }
                            break;
                        }

                    case HiderState.SeePlayer:
                        {
                            // Find cover based on lastKnownPlayerPos and move there
                            

                            if (!_isChoosingCover)
                            {
                                LM.write($"State SeePlayer: choosing cover vs player at {_lastKnownPlayerPos}");
                                if (ReachedDestination())
                                {
                                    ChooseCoverAndMove(_lastKnownPlayerPos);
                                }

                            }
                            break;
                        }

                    case HiderState.LostPlayerRecent:
                        {
                            if (!_lostRecentEntered && !_isChoosingCover)
                            {
                                _lostRecentEntered = true;
                                LM.write($"State LostPlayerRecent: relocate using lastKnown={_lastKnownPlayerPos}");

                                if (ReachedDestination())
                                    ChooseCoverAndMove(_lastKnownPlayerPos);
                            }

                            // once we have arrived (i.e., not choosing cover anymore), start the wait->Unknown timer
                            if (!_isChoosingCover && _lostWaitCoroutine == null)
                            {
                                _lostWaitCoroutine = StartCoroutine(LostRecentWaitThenUnknown());
                            }
                            break;
                        }

                }

                yield return null;
            }
        }

        IEnumerator LostRecentWaitThenUnknown()
        {
            float t = 0f;
            while (t < LostToUnknownAfterHide)
            {
                if (DetectionModule != null && DetectionModule.IsSeeingTarget)
                {
                    _state = HiderState.SeePlayer;
                    _lostWaitCoroutine = null;
                    yield break;
                }

                t += Time.deltaTime;
                yield return null;
            }

            _state = HiderState.UnknownPlayer;
            _lostWaitCoroutine = null;
        }


        void EnsureIsWithinLevelBounds()
        {
            if (transform.position.y < SelfDestructYHeight)
            {
                //Log($"Below SelfDestructYHeight ({SelfDestructYHeight}) � destroying hider");
                Destroy(gameObject);
                return;
            }
        }

        #region Peek Logic

        IEnumerator PeekRoutine()
        {
            //Debug.//Log($"[HIDER] >>> PeekRoutine ENTER (state={_state})");
            _isPeeking = true;
            _coverPos = transform.position;
            //Log($"PeekRoutine started from coverPos = {_coverPos}");

            //Debug.//Log("peek");
            // 1. Recompute peek positions around current cover location
            RecomputePeekPositions(_coverPos);

            if (!_hasValidPeekPositions)
            {
                //Log("PeekRoutine aborted, no valid peek positions");
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
            SetDestinationWithDebug(_coverPos);
            while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                yield return null;

            // 4. Move from cover to the chosen corner (still mostly safe)
            SetDestinationWithDebug(basePeek);
            while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                yield return null;

            // 5. Prepare outward direction and peekOut position
            //    wallNormal is wall -> bot; outward is bot -> exposed
            Vector3 outward = -_wallNormal;
            Vector3 peekOut = basePeek + outward * forwardOffset;

            //Log($"PeekRoutine: basePeek={basePeek}, outward={outward}, peekOut={peekOut}");

            // 6. Jiggle peek: out-in-out-in for peekJiggleCount cycles
            peekJiggleCount = Random.Range(0, 15);
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
                //Log($"PeekRoutine: Jiggle #{i + 1}/{peekJiggleCount} -> STEP OUT to {peekOut}");
                SetDestinationWithDebug(peekOut);
                while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                    yield return null;

                // Wait out in the open for a short time � midDelay is your "exposed time"
                yield return new WaitForSeconds(midDelay);

                bool sawDuringPeek = DetectionModule != null && DetectionModule.IsSeeingTarget;
                //Log($"PeekRoutine: Jiggle #{i + 1} exposed, IsSeeingTarget={sawDuringPeek}");

                if (sawDuringPeek)
                {
                    _lastKnownPlayerPos = DetectionModule.LastSeenPosition;
                    //Log($"PeekRoutine: PLAYER SPOTTED at {_lastKnownPlayerPos} -> State SeePlayer");
                    _state = HiderState.SeePlayer;
                    _isPeeking = false;
                    yield break;
                }

                // ---- STEP BACK (safe again) ----
                //Log($"PeekRoutine: Jiggle #{i + 1} -> STEP BACK to basePeek {basePeek}");
                SetDestinationWithDebug(basePeek);
                while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                    yield return null;

                // Small delay behind cover before next jiggle (optional)
                if (i < peekJiggleCount - 1 && betweenPeekDelay > 0f)
                    yield return new WaitForSeconds(betweenPeekDelay);
            }

            // 7. After all jiggling, go fully back to the main cover position
            //Log("PeekRoutine: finished all jiggle peeks, returning to _coverPos");
            SetDestinationWithDebug(_coverPos);
            while (NavMeshAgent.remainingDistance > reachThreshold && !NavMeshAgent.pathPending)
                yield return null;

            //Debug.//Log($"[HIDER] <<< PeekRoutine EXIT (state={_state})");
            _isPeeking = false;

            // 8. After peeking, possibly move to a new nearby cover (your existing behavior)
            float roll = Random.value;
            //Log($"PeekRoutine: finished, roll={roll:F2}, moveChance={IdleMoveCoverChance:F2}");

            if (roll < IdleMoveCoverChance)
            {
                //Log("PeekRoutine: roll succeeded -> MoveToRandomNearbyCover");
                MoveToRandomNearbyCover();
            }
            else
            {
                //Log("PeekRoutine: staying at current cover");
            }
        }
        void RecomputePeekPositions(Vector3 origin)
        {
            _hasValidPeekPositions = false;

            if (PeekNodes == null || PeekNodes.Length == 0)
            {
                //Log("RecomputePeekPositions: No PeekNodes assigned!");
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
                //Log("RecomputePeekPositions: Could not find closest node!");
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

            //Log($"RecomputePeekPositions: A={closestA.name}, B={closestB.name}, C={closestC.name}");

            Debug.DrawLine(origin, _peekLeftPos, Color.green, 2f);
            Debug.DrawLine(origin, _peekRightPos, Color.blue, 2f);
            Debug.DrawLine(origin, _peekThirdPos, Color.yellow, 2f);
        }


        #endregion

        bool ReachedDestination()
        {
            if (NavMeshAgent == null) return false;
            if (!NavMeshAgent.isActiveAndEnabled) return false;
            if (!NavMeshAgent.isOnNavMesh) return false;

            if (NavMeshAgent.pathPending) return false;
            if (NavMeshAgent.remainingDistance > NavMeshAgent.stoppingDistance) return false;
            if (NavMeshAgent.hasPath) return false;

            return true;
        }


        //IEnumerator LookAlongWallRoutine()
        //{
        //    _isUnknownRoutineRunning = true;

        //    //Debug.//Log("LookAlongWallRoutine started");

        //    // Directions toward left & right wall edges
        //    Vector3 leftDir = (_peekLeftPos - transform.position).normalized;
        //    Vector3 rightDir = (_peekRightPos - transform.position).normalized;

        //    float timer = 0f;

        //    while (_state == HiderState.UnknownPlayer)
        //    {
        //        timer += Time.deltaTime;

        //        // Ping-pong between left and right
        //        float t = Mathf.PingPong(timer, IdleScanDuration) / IdleScanDuration;
        //        Vector3 lookDir = Vector3.Slerp(leftDir, rightDir, t);

        //        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        //        transform.rotation = Quaternion.Slerp(
        //            transform.rotation,
        //            targetRot,
        //            Time.deltaTime * OrientationSpeed);

        //        Debug.DrawRay(transform.position + Vector3.up, lookDir * 2f, Color.yellow);
        //        Debug.DrawRay(transform.position + Vector3.up, leftDir * 2f, Color.green);
        //        Debug.DrawRay(transform.position + Vector3.up, rightDir * 2f, Color.blue);


        //        // Immediately stop scanning if player is seen
        //        if (DetectionModule != null && DetectionModule.IsSeeingTarget)
        //        {
        //            _lastKnownPlayerPos = DetectionModule.LastSeenPosition;
        //            _state = HiderState.SeePlayer;
        //            break;
        //        }

        //        yield return null;
        //    }


        //    _isUnknownRoutineRunning = false;
        //}

        IEnumerator LookAlongWallRoutine()
        {
            _isUnknownRoutineRunning = true;

            Vector3 mapCenter = new Vector3(55f, 3f, 15f);

            float timer = 0f;

            while (_state == HiderState.UnknownPlayer)
            {
                timer += Time.deltaTime;

                // Direction toward map center (flatten Y so we don't tilt)
                Vector3 toCenter = mapCenter - transform.position;
                toCenter.y = 0f;

                if (toCenter.sqrMagnitude < 0.001f)
                    yield break;

                Quaternion baseRotation = Quaternion.LookRotation(toCenter.normalized);

                // PingPong angle between -90 and +90
                float angle = Mathf.Lerp(
                    -50f,
                    50f,
                    Mathf.PingPong(timer, IdleScanDuration) / IdleScanDuration
                );

                Quaternion offsetRotation = Quaternion.Euler(0f, angle, 0f);

                Quaternion targetRot = baseRotation * offsetRotation;

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    Time.deltaTime * OrientationSpeed
                );

                // Debug rays
                Debug.DrawRay(transform.position + Vector3.up,
                    targetRot * Vector3.forward * 2f,
                    Color.yellow);

                // Stop immediately if player seen
                if (DetectionModule != null && DetectionModule.IsSeeingTarget)
                {
                    _lastKnownPlayerPos = DetectionModule.LastSeenPosition;
                    _state = HiderState.SeePlayer;
                    break;
                }

                yield return null;
            }

            _isUnknownRoutineRunning = false;
        }


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

                Vector3 wallPos = wall.bounds.center;
                float dist = Vector3.Distance(botPos, wallPos);

                // coverPos is a bit in front of the wall from the bot's viewpoint
                Vector3 dirBotToWall = (wallPos - botPos).normalized;
                Vector3 coverPos = wallPos - dirBotToWall * coverOffset;

                candidates.Add((dist, coverPos));
            }

            if (candidates.Count == 0)
            {
                ////Log("MoveToRandomNearbyCover: no nearby walls");
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
                SetDestinationWithDebug(_currentCoverTarget);

                ////Log($"MoveToRandomNearbyCover: moving to idle cover index={idx} at {_currentCoverTarget}");
                if (showDebugCover)
                    Debug.DrawLine(transform.position + Vector3.up,
                                   _currentCoverTarget + Vector3.up,
                                   Color.cyan, 1f);
            }
            else
            {
                ////Log($"MoveToRandomNearbyCover: NavMesh.SamplePosition failed near {chosenPos}");
            }
        }

        float GetNavMeshPathLength(Vector3 target)
        {
            NavMeshPath path = new NavMeshPath();

            if (!NavMeshAgent.CalculatePath(target, path))
                return Mathf.Infinity;

            float length = 0f;

            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return length;
        }


        void ChooseCoverAndMove(Vector3 playerPos)
        {
            Vector3 botPos = transform.position;

            Collider[] walls = Physics.OverlapSphere(botPos, 30f, wallMask);

            // Store all valid candidates with their scores
            var candidates = new List<CoverCandidate>();

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

                float distBot = GetNavMeshPathLength(coverPos);
                float distPlayer = Vector3.Distance(playerPos, coverPos);

                // ---------- Calculate combined score ----------
                // You can adjust these weights to change bot behavior

                // 1. Retreat: far from player, but don't punish bot distance as much
                float retreatScore = distPlayer - 0.5f * distBot;

                //// 2. Quick: just how fast to reach (closer is better)
                //float quickScore = -distBot;

                //// 3. Flank: want roughly 90Â° off the player, not straight back
                //Vector3 toPlayer = (playerPos - botPos).normalized;
                //Vector3 toCover = (coverPos - botPos).normalized;
                //float angle = Vector3.Angle(toPlayer, toCover);
                //float flankAngleScore = 1f - Mathf.Abs(angle - 90f) / 90f;
                //float flankScore = flankAngleScore + 0.2f * (distPlayer / (coverSearchRadius + 0.001f));

                // Combined score (you can weight these differently)
                float finalScore = retreatScore * 1.0f; //+ quickScore * 0.3f + flankScore * 0.5f;

                var cand = new CoverCandidate
                {
                    group = groupKey,
                    pos = coverPos,
                    distBot = distBot,
                    distPlayer = distPlayer,
                    retreatScore = retreatScore,
                    //quickScore = quickScore,
                    //flankScore = flankScore
                };

                // Store in candidates list - we'll keep ALL of them for weighted selection
                // But only keep best score per wall group
                var existing = candidates.FirstOrDefault(c => c.group == groupKey);
                if (existing != null)
                {
                    // Compare combined scores
                    float existingScore = existing.retreatScore * 1.0f + existing.quickScore * 0.3f + existing.flankScore * 0.5f;
                    if (finalScore > existingScore)
                    {
                        candidates.Remove(existing);
                        candidates.Add(cand);
                    }
                }
                else
                {
                    candidates.Add(cand);
                }
            }

            if (candidates.Count == 0)
            {
                //LM.write("[HIDER] ChooseCoverAndMove: no valid covers found");
                return;
            }

            // ---------- WEIGHTED RANDOM SELECTION ----------

            // Calculate final scores for all candidates
            var scores = new List<float>();
            float totalScore = 0f;

            foreach (var cand in candidates)
            {
                // Combined score
                float score = cand.retreatScore * 1.0f + cand.quickScore * 0.3f + cand.flankScore * 0.5f;

                // Clamp negative scores to 0 (so they have 0 chance)
                score = Mathf.Max(0f, score);

                scores.Add(score);
                totalScore += score;
            }

            // If all scores are 0 or negative, just pick randomly
            if (totalScore <= 0f)
            {
                //LM.write("[HIDER] All scores <= 0, picking random candidate");
                int randomIdx = Random.Range(0, candidates.Count);
                var randomChoice = candidates[randomIdx];

                if (NavMesh.SamplePosition(randomChoice.pos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                {
                    SetDestinationWithDebug(navHit.position);
                    Debug.DrawLine(transform.position + Vector3.up, navHit.position + Vector3.up, Color.magenta, 1f);
                    //LM.write($"[HIDER] Random chosen cover = {navHit.position}");
                }
                return;
            }

            // Generate random value between 0 and totalScore
            float randomValue = Random.Range(0f, totalScore);

            // Find which candidate this random value lands on
            float cumulativeScore = 0f;
            CoverCandidate chosenCandidate = candidates[0]; // fallback
            int chosenIndex = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                cumulativeScore += scores[i];

                if (randomValue <= cumulativeScore)
                {
                    chosenCandidate = candidates[i];
                    chosenIndex = i;
                    break;
                }
            }

            // Log the probabilities for debugging
            //LM.write($"[HIDER] Cover selection probabilities (Total Score: {totalScore:F2}):");
            for (int i = 0; i < candidates.Count; i++)
            {
                float probability = (scores[i] / totalScore) * 100f;
                string marker = (i == chosenIndex) ? " <- CHOSEN" : "";
                LM.write($"  Candidate {i}: Score={scores[i]:F2}, Probability={probability:F1}%{marker}");

                // Draw debug lines - chosen one is bright blue, others are dim
                Color debugColor = (i == chosenIndex) ? Color.blue : Color.grey;
                Debug.DrawLine(playerPos + Vector3.up, candidates[i].pos, debugColor, 0.5f);
            }

            // ---------- Move to chosen cover ----------
            Vector3 chosenPos = chosenCandidate.pos;

            if (NavMesh.SamplePosition(chosenPos, out NavMeshHit finalHit, 5f, NavMesh.AllAreas))
            {
                SetDestinationWithDebug(finalHit.position);
                //Debug.DrawLine(transform.position + Vector3.up, finalHit.position, Color.cyan, 1f);
                LM.write($"[HIDER] Weighted random chosen cover = {finalHit.position}");
            }
            else
            {
                LM.write($"[HIDER] ChooseCoverAndMove: no NavMesh near chosen cover {chosenPos}");
            }            
        }

        #endregion

        void SetDestinationWithDebug(Vector3 destination, Color color = default)
        {
            if (NavMeshAgent == null || !NavMeshAgent.isActiveAndEnabled)
                return;

            NavMeshAgent.SetDestination(destination);
            if (color == default)
                color = Color.red;

            // Draw line from current position to target
            Debug.DrawLine(
                transform.position + Vector3.up,
                destination + Vector3.up,
                color,
                1.5f
            );

            Debug.Log($"[HIDER] Moving to {destination}");
        }


        Transform[] FindPeekNodesInScene()
        {
            GameObject[] nodes = GameObject.FindGameObjectsWithTag("PeekNode");

            Transform[] result = new Transform[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                result[i] = nodes[i].transform;

            return result;
        }

        #region Damage / Death (minimal)
        void OnDamaged(float damage, GameObject damageSource)
        {
            //Log($"OnDamaged: damage={damage}, source={(source ? source.name : "null")}");

            // Optional: you could make hider immediately switch to SeePlayer when shot
            //if (source != null && !source.GetComponent<HiderController>())
            //{
            //    if (DetectionModule != null)
            //    {
            //        DetectionModule.OnDamaged(source);
            //        ////Log("OnDamaged: forwarded to DetectionModule.OnDamaged");
            //    }
            //}

            if (damageSource && !damageSource.GetComponent<HiderController>())
            {
                _lastKnownPlayerPos = damageSource.transform.position;

                _lastTimeSawPlayer = Time.time;

                DetectionModule.OnDamaged(damageSource);

                //onDamaged?.Invoke();
            }
        }

        
        void OnDie()
        {
            ////Log("OnDie: unregistering and destroying hider");
            ///
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.RespawnPlayerAfterDeath();
                return;
            }

            if (m_EnemyManager != null)
            {
                m_EnemyManager.UnregisterEnemy(null);
            }

            
            //Destroy(gameObject);
        }


        #endregion
    }
}
