//using UnityEngine;
//using UnityEngine.AI;
//using Unity.FPS.Game;
//using Unity.FPS.AI;

//public class AISeeker : MonoBehaviour
//{
//    public enum SeekerState
//    {
//        Patrol,
//        Chase,
//        GoToLastSeen
//    }

//    [Header("References")]
//    public DetectionModule Detection;
//    public NavMeshAgent Agent;
//    public Transform[] Waypoints;

//    [Header("Distances & Timing")]
//    [Tooltip("How close to a waypoint to consider it 'reached'")]
//    public float WaypointReachThreshold = 1f;

//    [Tooltip("How close to last seen position to consider it 'reached'")]
//    public float LastSeenReachThreshold = 1f;

//    [Tooltip("How long after losing sight to still move to last seen position")]
//    public float LostSightSearchTime = 2f;

//    SeekerState _currentState = SeekerState.Patrol;
//    Actor _selfActor;
//    int _currentWaypointIndex = 0;

//    // Time we last had the player in FOV
//    float _lastTimeSawTarget = Mathf.NegativeInfinity;

//    void Awake()
//    {
//        if (Agent == null)
//            Agent = GetComponent<NavMeshAgent>();

//        if (Detection == null)
//            Detection = GetComponent<DetectionModule>();

//        _selfActor = GetComponent<Actor>();
//    }

//    void Start()
//    {
//        // Start at closest waypoint so we don't suddenly snap across the map
//        SetClosestWaypointAsCurrent();
//        GoToCurrentWaypoint();
//        _currentState = SeekerState.Patrol;
//    }

//    void Update()
//    {
//        // Update detection (pure vision + last seen position)
//        Detection.HandleTargetDetection(_selfActor);

//        // Update last time we saw the target
//        if (Detection.HasTargetInFOV)
//        {
//            _lastTimeSawTarget = Time.time;
//        }

//        switch (_currentState)
//        {
//            case SeekerState.Patrol:
//                UpdatePatrol();
//                break;

//            case SeekerState.Chase:
//                UpdateChase();
//                break;

//            case SeekerState.GoToLastSeen:
//                UpdateGoToLastSeen();
//                break;
//        }
//    }

//    // ----------------- PATROL -----------------
//    void UpdatePatrol()
//    {
//        // If we see the player -> start chasing
//        if (Detection.HasTargetInFOV && Detection.CurrentTarget != null)
//        {
//            _currentState = SeekerState.Chase;
//            return;
//        }

//        // Move along waypoints
//        if (Waypoints == null || Waypoints.Length == 0)
//            return;

//        if (!Agent.pathPending && Agent.remainingDistance <= WaypointReachThreshold)
//        {
//            // Go to next waypoint
//            _currentWaypointIndex = (_currentWaypointIndex + 1) % Waypoints.Length;
//            GoToCurrentWaypoint();
//        }
//    }

//    // ----------------- CHASE -----------------
//    void UpdateChase()
//    {
//        // If we still see the player, keep chasing their current position
//        if (Detection.HasTargetInFOV && Detection.CurrentTarget != null)
//        {
//            Agent.SetDestination(Detection.CurrentTarget.transform.position);
//            return;
//        }

//        // We lost sight of the player
//        float timeSinceLastSeen = Time.time - _lastTimeSawTarget;

//        // If we lost sight very recently (< LostSightSearchTime) -> go to last seen position
//        if (timeSinceLastSeen <= LostSightSearchTime && Detection.HasLastSeenPosition)
//        {
//            Agent.SetDestination(Detection.LastSeenPosition);
//            _currentState = SeekerState.GoToLastSeen;
//        }
//        else
//        {
//            // Lost track for too long -> return to closest waypoint and Patrol
//            SetClosestWaypointAsCurrent();
//            GoToCurrentWaypoint();
//            _currentState = SeekerState.Patrol;
//        }
//    }

//    // ----------------- GO TO LAST SEEN -----------------
//    void UpdateGoToLastSeen()
//    {
//        // If we see the player again -> back to Chase
//        if (Detection.HasTargetInFOV && Detection.CurrentTarget != null)
//        {
//            _currentState = SeekerState.Chase;
//            return;
//        }

//        // Still going to last seen position
//        if (Detection.HasLastSeenPosition)
//        {
//            // If we reached that point, and still don't see the player for more than LostSightSearchTime,
//            // go back to Patrol (closest waypoint).
//            float dist = Vector3.Distance(transform.position, Detection.LastSeenPosition);

//            if (dist <= LastSeenReachThreshold)
//            {
//                float timeSinceLastSeen = Time.time - _lastTimeSawTarget;

//                if (timeSinceLastSeen > LostSightSearchTime)
//                {
//                    SetClosestWaypointAsCurrent();
//                    GoToCurrentWaypoint();
//                    _currentState = SeekerState.Patrol;
//                }
//                // If you want, here you could add a small local search behavior instead.
//            }
//        }
//        else
//        {
//            // We don't even remember a last seen position anymore -> just return to patrol
//            SetClosestWaypointAsCurrent();
//            GoToCurrentWaypoint();
//            _currentState = SeekerState.Patrol;
//        }
//    }

//    // ----------------- HELPERS -----------------
//    void GoToCurrentWaypoint()
//    {
//        if (Waypoints == null || Waypoints.Length == 0)
//            return;

//        Agent.SetDestination(Waypoints[_currentWaypointIndex].position);
//    }

//    void SetClosestWaypointAsCurrent()
//    {
//        if (Waypoints == null || Waypoints.Length == 0)
//            return;

//        float bestSqr = Mathf.Infinity;
//        int bestIndex = 0;
//        Vector3 pos = transform.position;

//        for (int i = 0; i < Waypoints.Length; i++)
//        {
//            float sqr = (Waypoints[i].position - pos).sqrMagnitude;
//            if (sqr < bestSqr)
//            {
//                bestSqr = sqr;
//                bestIndex = i;
//            }
//        }

//        _currentWaypointIndex = bestIndex;
//    }
//}
