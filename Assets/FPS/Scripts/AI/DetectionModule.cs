using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    public class DetectionModule : MonoBehaviour
    {
        [Header("Vision Settings")]

        [Header("Detection Timing")]
        public float DetectionInterval = 1f;   
        float _nextDetectionTime = 0f;

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
        public float missChanceAtMaxRange = 0.1f;
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
            bool noTargetYet = (KnownDetectedTarget == null);
            if (noTargetYet)
            {
                if (Time.time < _nextDetectionTime)
                    return;

                _nextDetectionTime = Time.time + DetectionInterval;
            }

            IsSeeingTarget = false;

            float sqrDetectionRange = DetectionRange * DetectionRange;
            float sqrPeripheralRange = PeripheralAlertRange * PeripheralAlertRange;
            float sqrCloseRange = CloseDetectionRange * CloseDetectionRange;   // NEW

            float closestSqrDistance = Mathf.Infinity;

            foreach (Actor other in m_ActorsManager.Actors)
            {
                if (other.Affiliation == selfActor.Affiliation)
                    continue;

                Vector3 dir = other.AimPoint.position - DetectionSourcePoint.position;
                float sqrDist = dir.sqrMagnitude;
                float dist = Mathf.Sqrt(sqrDist);

                // ---------- 360° Peripheral Alert Range ----------
                if (sqrDist <= sqrPeripheralRange)
                {
                    LastSeenPosition = other.AimPoint.position;
                    TimeLastSeenTarget = Time.time;
                }

                // ---------- Close-range auto detection (NEW) ----------
                bool inCloseRange = sqrDist <= sqrCloseRange;

                // ---------- Precision Vision Range ----------
                if (!inCloseRange && sqrDist > sqrDetectionRange)
                {
                    // too far for both normal and close detection
                    continue;
                }

                // ---------- FOV Check ----------
                if (!inCloseRange) // ONLY require FOV if NOT in close range
                {
                    float angle = Vector3.Angle(transform.forward, dir.normalized);
                    if (angle > ViewAngle * 0.5f)
                        continue;
                }

                // ---------- Raycast Obstruction ----------
                Ray ray = new Ray(DetectionSourcePoint.position, dir.normalized);
                RaycastHit[] hits = Physics.RaycastAll(ray, DetectionRange, ~0);

                if (hits.Length > 0)
                {
                    // Sort hits by distance
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                    bool blocked = false;

                    foreach (var hit in hits)
                    {
                        Actor hitActor = hit.collider.GetComponentInParent<Actor>();

                        if (hitActor == other)
                        {
                            // target is first thing hit → valid detection
                            break;
                        }
                        else if (hitActor != null)
                        {
                            // Some other actor, maybe ignore? Or treat as blocker
                            blocked = true;
                            break;
                        }
                        else
                        {
                            // hit something without Actor component → probably a wall
                            blocked = true;
                            break;
                        }
                    }

                    if (blocked)
                        continue; // vision blocked
                }
                // ---------- VALIDATE LAYER (only detect Player) ----------
                if (other.gameObject.layer != LayerMask.NameToLayer("Player"))
                    continue;
                else
                {
                    Debug.DrawLine(
                        DetectionSourcePoint.position,
                        DetectionSourcePoint.position + dir.normalized * DetectionRange,
                        Color.green,
                        0.1f);
                }

                // ---------- Miss chance (you can skip this for close range if you want) ----------
                float distance01 = Mathf.Clamp01(dist / missMaxRange);
                float missChance = distance01 * missChanceAtMaxRange;

                if (!inCloseRange && Random.value < missChance)   // NEW: no miss for close range
                {
                    Debug.Log($"[Detection] Random miss: dist={dist:F1}, chance={missChance:P0}");
                    continue;
                }
                Debug.Log($"[Detection] Random not miss: dist={dist:F1}, chance={missChance:P0}");

                // ---------- Valid detection ----------
                if (sqrDist < closestSqrDistance)
                {
                    closestSqrDistance = sqrDist;
                    KnownDetectedTarget = other.AimPoint.gameObject;
                    LastSeenPosition = other.AimPoint.position;
                    TimeLastSeenTarget = Time.time;

                    IsSeeingTarget = true;
                }
            }

            // Attack range check
            IsTargetInAttackRange =
                KnownDetectedTarget != null &&
                Vector3.Distance(transform.position, KnownDetectedTarget.transform.position) <= AttackRange;

            // Events
            if (!HadKnownTarget && KnownDetectedTarget != null)
            {
                Debug.Log("[Detection] First time SEE target: " + KnownDetectedTarget.name);
                onDetectedTarget?.Invoke();
            }

            if (HadKnownTarget && KnownDetectedTarget == null)
            {
                Debug.Log("[Detection] LOST target");
                onLostTarget?.Invoke();
                _nextDetectionTime = Time.time + DetectionInterval;
            }

            HadKnownTarget = KnownDetectedTarget != null;

            // Forget target completely if too long unseen
            if (!IsSeeingTarget && Time.time - TimeLastSeenTarget > KnownTargetTimeout)
            {
                KnownDetectedTarget = null;
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
