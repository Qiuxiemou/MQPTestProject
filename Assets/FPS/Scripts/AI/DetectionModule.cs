using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    public class DetectionModule : MonoBehaviour
    {
        [Header("Vision Settings")]

        [Header("Reaction Time Settings")]
        public float MinReactionTime = 1f;   
        public float MaxReactionTime = 3f;   
        float _reactionEndTime = 0f;           
        bool _isReacting = false;             
        Actor _pendingTarget = null; 

        [Tooltip("Precision vision angle (e.g. 150 degrees in front)")]
        public float ViewAngle = 150f;

        [Tooltip("Maximum distance for precise vision")]
        public float DetectionRange = 20f;

        [Tooltip("Distance where target is detected even if not in FOV")]
        public float CloseDetectionRange = 15f;

        [Tooltip("360-degree peripheral alert radius")]
        public float PeripheralAlertRange = 6f;

        [Tooltip("Maximum distance for attacking the target")]
        public float AttackRange = 10f;

        [Tooltip("Raycast origin for vision checks (usually the head)")]
        public Transform DetectionSourcePoint;

        [Tooltip("Layers that block vision (walls, props, etc.)")]
        public LayerMask ObstructionLayers;

        [Tooltip("Time before forgetting target completely")]
        public float KnownTargetTimeout = 4f;

        public float missMaxRange=30f;
        public Animator Animator;

        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;
        public GameObject KnownDetectedTarget;
        public bool IsSeeingTarget { get; private set; }
        public bool IsTargetInAttackRange { get; private set; }
        public bool HadKnownTarget { get; private set; }

        // Last position where the target was seen — used for Search Mode
        public Vector3 LastSeenPosition { get; private set; }
        public bool HasLastSeenPosition => KnownDetectedTarget == null && Time.time - TimeLastSeenTarget < KnownTargetTimeout;

        float TimeLastSeenTarget = Mathf.NegativeInfinity;
        ActorsManager m_ActorsManager;

        private LayerMask _detectionLayerMask;

        const string k_AnimAttackParameter = "Attack";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        protected virtual void Start()
        {
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();
            _detectionLayerMask = LayerMask.GetMask("Player");
        }

        public virtual void HandleTargetDetection(Actor selfActor, Collider[] selfColliders)
        {
            IsSeeingTarget = false;

            float sqrDetectionRange = DetectionRange * DetectionRange;
            float sqrPeripheralRange = PeripheralAlertRange * PeripheralAlertRange;
            float sqrCloseRange = CloseDetectionRange * CloseDetectionRange;   // NEW

            Actor seenActor = null;
            float seenDist = 0f;

            foreach (Actor other in m_ActorsManager.Actors)
            {
                if (other.Affiliation == selfActor.Affiliation)
                    continue;

                Vector3 dir = other.AimPoint.position - DetectionSourcePoint.position;
                float sqrDist = dir.sqrMagnitude;

                if (sqrDist > sqrDetectionRange)
                    continue;

                float angle = Vector3.Angle(transform.forward, dir.normalized);
                if (angle > ViewAngle * 0.5f)
                    continue;

                if (Physics.Raycast(DetectionSourcePoint.position, dir.normalized,
                    out RaycastHit hit, DetectionRange, ObstructionLayers))
                {
                    Actor hitActor = hit.collider.GetComponentInParent<Actor>();
                    if (hitActor != other)
                        continue;
                }

                // this actor is visible this frame
                seenActor = other;
                seenDist = Mathf.Sqrt(sqrDist);
                break;
            }

            // ========== player in sight ==========
            if (seenActor != null)
            {
                IsSeeingTarget = true;
                LastSeenPosition = seenActor.AimPoint.position;
                TimeLastSeenTarget = Time.time;

                float distance01 = Mathf.Clamp01(seenDist / missMaxRange);
                float reactionTime = Mathf.Lerp(MinReactionTime, MaxReactionTime, distance01);

                if (!_isReacting)
                {
                    _isReacting = true;
                    _pendingTarget = seenActor;
                    _reactionEndTime = Time.time + reactionTime;
                }
            }
            else
            {
                _isReacting = false;
                _pendingTarget = null;
                _reactionEndTime = 0f;
            }

            // ========== reaction finished ==========
            if (_isReacting && Time.time >= _reactionEndTime)
            {
                KnownDetectedTarget = _pendingTarget.AimPoint.gameObject;
                onDetectedTarget?.Invoke();

                _isReacting = false;
                _pendingTarget = null;
            }

            // ========== player disappeared: reset reaction ==========
            if (!IsSeeingTarget && Time.time - TimeLastSeenTarget > KnownTargetTimeout)
            {
                KnownDetectedTarget = null;
                _isReacting = false;
                _pendingTarget = null;
                _reactionEndTime = 0f;
            }

            // attack range
            if (KnownDetectedTarget)
            {
                IsTargetInAttackRange =
                    Vector3.Distance(transform.position, KnownDetectedTarget.transform.position) <= AttackRange;
            }
            else
            {
                IsTargetInAttackRange = false;
            }
        }

        public virtual void OnDamaged(GameObject attacker)
        {
            TimeLastSeenTarget = Time.time;
            KnownDetectedTarget = attacker;

            if (Animator)
                Animator.SetTrigger(k_AnimOnDamagedParameter);
        }

        public virtual void OnAttack()
        {
            if (Animator)
                Animator.SetTrigger(k_AnimAttackParameter);
        }
    }
}
