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
            WatchAround
        }

        public Animator Animator;

        [Tooltip("Fraction of the enemy's attack range at which it will stop moving towards target while attacking")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;

        [Tooltip("The random hit damage effects")]
        public ParticleSystem[] RandomHitSparks;

        public ParticleSystem[] OnDetectVfx;
        public AudioClip OnDetectSfx;
        public bool m_HasDetected = false;

        [Header("Sound")] public AudioClip MovementSound;
        public MinMaxFloat PitchDistortionMovementSpeed;

        [Header("Watch Around")]
        public float WatchTurnSpeed = 720f;    
        float m_WatchTimer = 0f;
        int m_WatchStage = 0;
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

            }
        }

        void UpdateCurrentAiState()
        {
            // Handle logic 
            switch (AiState)
            {
                case AIState.Patrol:
                    Vector3 dest = m_EnemyController.GetBestPatrolDestination();
                    m_EnemyController.SetNavDestination(dest);
                    break;
                case AIState.Follow:
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
                    if (m_EnemyController.IsSeeingTarget && m_EnemyController.KnownDetectedTarget != null)
                    {
                        AiState = m_EnemyController.IsTargetInAttackRange ? AIState.Attack : AIState.Follow;
                        break;
                    }

                   
                    m_EnemyController.SetNavDestination(lastKnownPosition);

                    var agent = m_EnemyController.NavMeshAgent;
                    if (!agent.pathPending && agent.remainingDistance <= 1f)
                    {
                        AiState = AIState.WatchAround;
                        m_WatchStage = 0;  
                        m_WatchTimer = 0f;
                        m_EnemyController.SetNavDestination(transform.position);
                    }
                    break;
                case AIState.WatchAround:
                    {
                        if (m_EnemyController.IsSeeingTarget && m_EnemyController.KnownDetectedTarget != null)
                        {
                            AiState = m_EnemyController.IsTargetInAttackRange ? AIState.Attack : AIState.Follow;
                            break;
                        }
                        if (m_EnemyController.IsSeeingTarget && m_EnemyController.KnownDetectedTarget != null)
                        {
                            AiState = m_EnemyController.IsTargetInAttackRange ? AIState.Attack : AIState.Follow;
                            break;
                        }

                        m_WatchTimer += Time.deltaTime;

   
                        if (m_WatchStage == 0)
                        {
                            transform.Rotate(0f, -WatchTurnSpeed * Time.deltaTime, 0f);

                            if (m_WatchTimer >= 0.3f)
                            {
                                m_WatchTimer = 0f;
                                m_WatchStage = 1;
                            }
                        }

                        else if (m_WatchStage == 1)
                        {
                            if (m_WatchTimer >= 0.25f)
                            {
                                m_WatchTimer = 0f;
                                m_WatchStage = 2;
                            }
                        }

                        else if (m_WatchStage == 2)
                        {
                            transform.Rotate(0f, WatchTurnSpeed * Time.deltaTime, 0f);
                            if (m_WatchTimer >= 0.6f)
                            {
                                AiState = AIState.Patrol;
                            }
                        }

                        break;
                    }
            }
        }

        void OnAttack()
        {
            Animator.SetTrigger(k_AnimAttackParameter);
        }

        void OnDetectedTarget()
        {
            if (m_HasDetected)
                return;
            m_HasDetected = true;

            Debug.Log("Playing enemy detect sound");
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
            m_HasDetected = false;
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

    }
}