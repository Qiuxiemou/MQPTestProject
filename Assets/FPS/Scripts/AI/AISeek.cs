using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class AISeek : MonoBehaviour
    {
        public enum AIState
        {
            Patrol,
            Follow,
            Attack,
            Search,
        }

        public Animator Animator;

        [Tooltip("Fraction of the enemy's attack range at which it will stop moving towards target while attacking")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;

        [Tooltip("The random hit damage effects")]
        public ParticleSystem[] RandomHitSparks;

        public ParticleSystem[] OnDetectVfx;
        public AudioClip OnDetectSfx;

        [Header("Sound")] public AudioClip MovementSound;
        public MinMaxFloat PitchDistortionMovementSpeed;

        [Header("Search / Look Around")]
        public float SearchWaitTime = 1.5f;
        public float SearchLookAroundDuration = 2.0f;
        public float SearchLookAroundSpeed = 90f;
        public float SearchLookAroundAngle = 60f;
        public float SearchArriveDistance = 4.0f;

        Coroutine m_SearchRoutine;
        bool m_SearchRoutineRunning = false;
        public AIState AiState { get; private set; }
        EnemyController m_EnemyController;
        AudioSource m_AudioSource;

        Vector3 lastKnownPosition;

        const string k_AnimMoveSpeedParameter = "MoveSpeed";
        const string k_AnimAttackParameter = "Attack";
        const string k_AnimAlertedParameter = "Alerted";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        void Start()
        {
            m_EnemyController = GetComponent<EnemyController>();
            DebugUtility.HandleErrorIfNullGetComponent<EnemyController, EnemyMobile>(m_EnemyController, this,
                gameObject);

            m_EnemyController.onAttack += OnAttack;
            m_EnemyController.onDetectedTarget += OnDetectedTarget;
            m_EnemyController.onLostTarget += OnLostTarget;
            m_EnemyController.SetPathDestinationToClosestNode();
            m_EnemyController.onDamaged += OnDamaged;

            // Start patrolling
            AiState = AIState.Patrol;

            // adding a audio source to play the movement sound on it
            m_AudioSource = GetComponent<AudioSource>();
            DebugUtility.HandleErrorIfNullGetComponent<AudioSource, EnemyMobile>(m_AudioSource, this, gameObject);
            m_AudioSource.clip = MovementSound;
            m_AudioSource.Play();
        }

        void Update()
        {
            UpdateAiStateTransitions();
            UpdateCurrentAiState();

            float moveSpeed = m_EnemyController.NavMeshAgent.velocity.magnitude;

            // Update animator speed parameter
            Animator.SetFloat(k_AnimMoveSpeedParameter, moveSpeed);

            // changing the pitch of the movement sound depending on the movement speed
            m_AudioSource.pitch = Mathf.Lerp(PitchDistortionMovementSpeed.Min, PitchDistortionMovementSpeed.Max,
                moveSpeed / m_EnemyController.NavMeshAgent.speed);
        }

        void UpdateAiStateTransitions()
        {
            // Handle transitions 
            switch (AiState)
            {
                case AIState.Follow:
                    // Transition to attack when there is a line of sight to the target
                    if (m_EnemyController.IsSeeingTarget && m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Attack;
                        m_EnemyController.SetNavDestination(transform.position);
                    }

                    break;
                case AIState.Attack:
                    // Transition to follow when no longer a target in attack range
                    if (!m_EnemyController.IsTargetInAttackRange)
                    {
                        AiState = AIState.Follow;
                    }

                    break;
                case AIState.Search:
                    break;
            }
        }

        void UpdateCurrentAiState()
        {
            // Handle logic 
            switch (AiState)
            {
                case AIState.Patrol:
                    StopSearchRoutine();
                    Vector3 dest = m_EnemyController.GetBestPatrolDestination();
                    m_EnemyController.SetNavDestination(dest);
                    break;
                case AIState.Follow:
                    StopSearchRoutine();
                    if (m_EnemyController.KnownDetectedTarget == null)
                    {
                        AiState = AIState.Patrol;   // or some LostTarget state
                        return;
                    }

                    m_EnemyController.SetNavDestination(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.OrientTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.OrientWeaponsTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    break;

                case AIState.Attack:
                    StopSearchRoutine();
                    if (m_EnemyController.KnownDetectedTarget == null ||
                        m_EnemyController.DetectionModule == null ||
                        m_EnemyController.DetectionModule.DetectionSourcePoint == null)
                    {
                        AiState = AIState.Patrol;   // or GoToLastSeen, etc.
                        return;
                    }

                    if (Vector3.Distance(
                            m_EnemyController.KnownDetectedTarget.transform.position,
                            m_EnemyController.DetectionModule.DetectionSourcePoint.position)
                        >= (AttackStopDistanceRatio * m_EnemyController.DetectionModule.AttackRange))
                    {
                        m_EnemyController.SetNavDestination(m_EnemyController.KnownDetectedTarget.transform.position);
                    }
                    else
                    {
                        m_EnemyController.SetNavDestination(transform.position);
                    }

                    m_EnemyController.OrientTowards(m_EnemyController.KnownDetectedTarget.transform.position);
                    m_EnemyController.TryAtack(m_EnemyController.KnownDetectedTarget.transform.position);
                    break;
                case AIState.Search:
                    StartSearchRoutine();
                    break;
            }
        }

        void OnAttack()
        {
            Animator.SetTrigger(k_AnimAttackParameter);
        }

        void OnDetectedTarget()
        {
            if (AiState == AIState.Patrol)
            {
                AiState = AIState.Follow;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Play();
            }

            if (OnDetectSfx)
            {
                AudioUtility.CreateSFX(OnDetectSfx, transform.position, AudioUtility.AudioGroups.EnemyDetection, 1f);
            }

            Animator.SetBool(k_AnimAlertedParameter, true);
        }

        void OnLostTarget()
        {
            StopSearchRoutine();
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                lastKnownPosition = m_EnemyController.DetectionModule.LastSeenPosition; 
                AiState = AIState.Search;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Stop();
            }

            Animator.SetBool(k_AnimAlertedParameter, false);
        }

        void OnDamaged()
        {
            if (RandomHitSparks.Length > 0)
            {
                int n = Random.Range(0, RandomHitSparks.Length - 1);
                RandomHitSparks[n].Play();
            }

            Animator.SetTrigger(k_AnimOnDamagedParameter);
        }

        void StartSearchRoutine()
        {
            if (m_SearchRoutineRunning) return;
            m_SearchRoutine = StartCoroutine(SearchLookAroundRoutine());
        }

        void StopSearchRoutine()
        {
            if (m_SearchRoutine != null)
                StopCoroutine(m_SearchRoutine);

            m_SearchRoutine = null;
            m_SearchRoutineRunning = false;
        }

        System.Collections.IEnumerator SearchLookAroundRoutine()
        {
            m_SearchRoutineRunning = true;
            while (AiState == AIState.Search)
            {
                Debug.Log("Searching: moving to last known position");

                if (m_EnemyController.IsSeeingTarget && m_EnemyController.KnownDetectedTarget != null)
                {
                    AiState = (m_EnemyController.IsTargetInAttackRange) ? AIState.Attack : AIState.Follow;
                    m_SearchRoutineRunning = false;
                    yield break;
                }

                m_EnemyController.SetNavDestination(lastKnownPosition);
                Vector2 a = new Vector2(transform.position.x, transform.position.z);
                Vector2 b = new Vector2(lastKnownPosition.x, lastKnownPosition.z);
                Debug.Log("Searching: distance to last known position: " + Vector2.Distance(a, b));
                Debug.Log("Searching: SearchArriveDistance: " + SearchArriveDistance);
                if (Vector2.Distance(a, b) <= SearchArriveDistance)
                {
                    Debug.Log("Arrived at last known position");
                    break;
                }
                    

                yield return null;
            }
            Debug.Log("Searching: arrived at last known position, looking around");
            m_EnemyController.SetNavDestination(transform.position);


            float waitEnd = Time.time + SearchWaitTime;
            while (AiState == AIState.Search && Time.time < waitEnd)
            {
                if (m_EnemyController.IsSeeingTarget && m_EnemyController.KnownDetectedTarget != null)
                {
                    AiState = (m_EnemyController.IsTargetInAttackRange) ? AIState.Attack : AIState.Follow;
                    m_SearchRoutineRunning = false;
                    yield break;
                }
                yield return null;
            }

            // Disable NavMeshAgent rotation
            var agent = m_EnemyController.NavMeshAgent;
            bool oldUpdateRotation = agent.updateRotation;
            agent.updateRotation = false;

            Quaternion baseRot = transform.rotation;
            float t = 0f;
            while (AiState == AIState.Search && t < SearchLookAroundDuration)
            {
                if (m_EnemyController.IsSeeingTarget && m_EnemyController.KnownDetectedTarget != null)
                {
                    AiState = (m_EnemyController.IsTargetInAttackRange) ? AIState.Attack : AIState.Follow;
                    m_SearchRoutineRunning = false;
                    yield break;
                }

                t += Time.deltaTime;
                float yaw = Mathf.Sin(t * Mathf.Deg2Rad * SearchLookAroundSpeed) * SearchLookAroundAngle;
                transform.rotation = baseRot * Quaternion.Euler(0f, yaw, 0f);

                yield return null;
            }

            // Restore NavMeshAgent rotation
            agent.updateRotation = oldUpdateRotation;

            if (AiState == AIState.Search)
                AiState = AIState.Patrol;

            m_SearchRoutineRunning = false;
        }

    }
}