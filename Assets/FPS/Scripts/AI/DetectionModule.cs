using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    public class DetectionModule : MonoBehaviour
    {
        [Header("Vision Settings")]

        [Tooltip("Precision vision angle (e.g. 120 degrees in front)")]
        public float ViewAngle = 120f;

        [Tooltip("Maximum distance for precise vision")]
        public float DetectionRange = 20f;

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

        public Animator Animator;

        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;

        public GameObject KnownDetectedTarget { get; private set; }
        public bool IsSeeingTarget { get; private set; }
        public bool IsTargetInAttackRange { get; private set; }
        public bool HadKnownTarget { get; private set; }

        // Last position where the target was seen — used for Search Mode
        public Vector3 LastSeenPosition { get; private set; }
        public bool HasLastSeenPosition => KnownDetectedTarget == null && Time.time - TimeLastSeenTarget < KnownTargetTimeout;

        float TimeLastSeenTarget = Mathf.NegativeInfinity;
        ActorsManager m_ActorsManager;

        const string k_AnimAttackParameter = "Attack";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        protected virtual void Start()
        {
            m_ActorsManager = FindAnyObjectByType<ActorsManager>();
        }

        public virtual void HandleTargetDetection(Actor selfActor, Collider[] selfColliders)
        {
  

            IsSeeingTarget = false;

            float sqrDetectionRange = DetectionRange * DetectionRange;
            float sqrPeripheralRange = PeripheralAlertRange * PeripheralAlertRange;

            float closestSqrDistance = Mathf.Infinity;

            foreach (Actor other in m_ActorsManager.Actors)
            {
                if (other.Affiliation == selfActor.Affiliation)
                    continue;

                Vector3 dir = other.AimPoint.position - DetectionSourcePoint.position;
                float sqrDist = dir.sqrMagnitude;
                float dist = Mathf.Sqrt(sqrDist);

                //Debug.Log("[Detection] Dist to " + other.name + " = " + dist.ToString("F2"));

                // ---------- 360° Peripheral Alert Range ----------
                if (sqrDist <= sqrPeripheralRange)
                {
                    LastSeenPosition = other.AimPoint.position;
                    TimeLastSeenTarget = Time.time;
                }

                // ---------- Precision Vision Range ----------
                if (sqrDist > sqrDetectionRange)
                {
                    //Debug.Log("[Detection] FAIL distance: too far, DetectionRange = " + DetectionRange);
                    continue;
                }

                // ---------- FOV Check ----------
                float angle = Vector3.Angle(transform.forward, dir.normalized);
                //Debug.Log("[Detection] Angle to " + other.name + " = " + angle.ToString("F1"));

                if (angle > ViewAngle * 0.5f)
                {
                    //Debug.Log("[Detection] FAIL FOV: " + angle.ToString("F1") + " > " + (ViewAngle * 0.5f));
                    continue;
                }


                // ---------- Raycast Obstruction ----------
                Ray ray = new Ray(DetectionSourcePoint.position, dir.normalized);

                if (Physics.Raycast(ray, out RaycastHit hit, DetectionRange, ObstructionLayers))
                {
                
                    Debug.DrawLine(DetectionSourcePoint.position, hit.point, Color.red, 0.1f);

                    //Debug.Log("[Detection] Raycast hit: " + hit.collider.name);

                    Actor hitActor = hit.collider.GetComponentInParent<Actor>();
                    if (hitActor != other)
                    {
                        //Debug.Log("[Detection] FAIL blocked by: " + hit.collider.name);
                        continue;
                    }
                }
                else
                {
                    Debug.DrawLine(
                        DetectionSourcePoint.position,
                        DetectionSourcePoint.position + dir.normalized * DetectionRange,
                        Color.green,
                        0.1f);
                }



                // Valid precise detection
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

            if (HadKnownTarget && KnownDetectedTarget == null){
                Debug.Log("[Detection] LOST target");
                onLostTarget?.Invoke();
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
