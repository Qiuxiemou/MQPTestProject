using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class ProjectileStandard : ProjectileBase
    {
        [Header("Conditional Timewarp Reject")]
        [Tooltip("When hitting delayed AimPoint hitbox, validate line of sight to the REAL player's CURRENT capsule. If fully behind cover -> reject.")]
        public bool UseRealPositionReject = true;

        [Tooltip("World geometry layers used for cover checks (walls/level). EXCLUDE player layers.")]
        public LayerMask WorldObstructionLayers = -1;

        [Tooltip("Tag on the delayed hitbox collider (child under AimPoint).")]
        public string AimPointHitboxTag = "AimPointHitbox";

        [Header("General")] [Tooltip("Radius of this projectile's collision detection")]
        public float Radius = 0.01f;

        [Tooltip("Transform representing the root of the projectile (used for accurate collision detection)")]
        public Transform Root;

        [Tooltip("Transform representing the tip of the projectile (used for accurate collision detection)")]
        public Transform Tip;

        [Tooltip("LifeTime of the projectile")]
        public float MaxLifeTime = 5f;

        [Tooltip("VFX prefab to spawn upon impact")]
        public GameObject ImpactVfx;

        [Tooltip("LifeTime of the VFX before being destroyed")]
        public float ImpactVfxLifetime = 5f;

        [Tooltip("Offset along the hit normal where the VFX will be spawned")]
        public float ImpactVfxSpawnOffset = 0.1f;

        [Tooltip("Clip to play on impact")] 
        public AudioClip ImpactSfxClip;

        [Tooltip("Layers this projectile can collide with")]
        public LayerMask HittableLayers = -1;

        [Header("Movement")] [Tooltip("Speed of the projectile")]
        public float Speed = 20f;

        [Tooltip("Downward acceleration from gravity")]
        public float GravityDownAcceleration = 0f;

        [Tooltip(
            "Distance over which the projectile will correct its course to fit the intended trajectory (used to drift projectiles towards center of screen in First Person view). At values under 0, there is no correction")]
        public float TrajectoryCorrectionDistance = -1;

        [Tooltip("Determines if the projectile inherits the velocity that the weapon's muzzle had when firing")]
        public bool InheritWeaponVelocity = false;

        [Header("Damage")] [Tooltip("Damage of the projectile")]
        public float Damage = 40f;

        [Tooltip("Area of damage. Keep empty if you don<t want area damage")]
        public DamageArea AreaOfDamage;

        [Header("Debug")] [Tooltip("Color of the projectile radius debug view")]
        public Color RadiusColor = Color.cyan * 0.2f;

        ProjectileBase m_ProjectileBase;
        Vector3 m_LastRootPosition;
        Vector3 m_Velocity;
        bool m_HasTrajectoryOverride;
        float m_ShootTime;
        Vector3 m_TrajectoryCorrectionVector;
        Vector3 m_ConsumedTrajectoryCorrectionVector;
        List<Collider> m_IgnoredColliders;

        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        void OnEnable()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ProjectileStandard>(m_ProjectileBase, this,
                gameObject);

            m_ProjectileBase.OnShoot += OnShoot;

            Destroy(gameObject, MaxLifeTime);
        }

        new void OnShoot()
        {
            m_ShootTime = Time.time;
            m_LastRootPosition = Root.position;
            m_Velocity = transform.forward * Speed;
            m_IgnoredColliders = new List<Collider>();
            transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;

            // Ignore colliders of owner
            Collider[] ownerColliders = m_ProjectileBase.Owner.GetComponentsInChildren<Collider>();
            m_IgnoredColliders.AddRange(ownerColliders);

            // Handle case of player shooting (make projectiles not go through walls, and remember center-of-screen trajectory)
            PlayerWeaponsManager playerWeaponsManager = m_ProjectileBase.Owner.GetComponent<PlayerWeaponsManager>();
            if (playerWeaponsManager)
            {
                m_HasTrajectoryOverride = true;

                Vector3 cameraToMuzzle = (m_ProjectileBase.InitialPosition -
                                          playerWeaponsManager.WeaponCamera.transform.position);

                m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle,
                    playerWeaponsManager.WeaponCamera.transform.forward);
                if (TrajectoryCorrectionDistance == 0)
                {
                    transform.position += m_TrajectoryCorrectionVector;
                    m_ConsumedTrajectoryCorrectionVector = m_TrajectoryCorrectionVector;
                }
                else if (TrajectoryCorrectionDistance < 0)
                {
                    m_HasTrajectoryOverride = false;
                }

                if (Physics.Raycast(playerWeaponsManager.WeaponCamera.transform.position, cameraToMuzzle.normalized,
                    out RaycastHit hit, cameraToMuzzle.magnitude, HittableLayers, k_TriggerInteraction))
                {
                    if (IsHitValid(hit))
                    {
                        OnHit(hit.point, hit.normal, hit.collider);
                    }
                }
            }
        }

        void Update()
        {
            // Move
            transform.position += m_Velocity * Time.deltaTime;
            if (InheritWeaponVelocity)
            {
                transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;
            }

            // Drift towards trajectory override (this is so that projectiles can be centered 
            // with the camera center even though the actual weapon is offset)
            if (m_HasTrajectoryOverride && m_ConsumedTrajectoryCorrectionVector.sqrMagnitude <
                m_TrajectoryCorrectionVector.sqrMagnitude)
            {
                Vector3 correctionLeft = m_TrajectoryCorrectionVector - m_ConsumedTrajectoryCorrectionVector;
                float distanceThisFrame = (Root.position - m_LastRootPosition).magnitude;
                Vector3 correctionThisFrame =
                    (distanceThisFrame / TrajectoryCorrectionDistance) * m_TrajectoryCorrectionVector;
                correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
                m_ConsumedTrajectoryCorrectionVector += correctionThisFrame;

                // Detect end of correction
                if (m_ConsumedTrajectoryCorrectionVector.sqrMagnitude == m_TrajectoryCorrectionVector.sqrMagnitude)
                {
                    m_HasTrajectoryOverride = false;
                }

                transform.position += correctionThisFrame;
            }

            // Orient towards velocity
            transform.forward = m_Velocity.normalized;

            // Gravity
            if (GravityDownAcceleration > 0)
            {
                // add gravity to the projectile velocity for ballistic effect
                m_Velocity += Vector3.down * GravityDownAcceleration * Time.deltaTime;
            }

            // Hit detection
            {
                RaycastHit closestHit = new RaycastHit();
                closestHit.distance = Mathf.Infinity;
                bool foundHit = false;

                // Sphere cast
                Vector3 displacementSinceLastFrame = Tip.position - m_LastRootPosition;
                RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, Radius,
                    displacementSinceLastFrame.normalized, displacementSinceLastFrame.magnitude, HittableLayers,
                    k_TriggerInteraction);
                foreach (var hit in hits)
                {
                    if (IsHitValid(hit) && hit.distance < closestHit.distance)
                    {
                        foundHit = true;
                        closestHit = hit;
                    }
                }

                if (foundHit)
                {
                    // Handle case of casting while already inside a collider
                    if (closestHit.distance <= 0f)
                    {
                        closestHit.point = Root.position;
                        closestHit.normal = -transform.forward;
                    }

                    OnHit(closestHit.point, closestHit.normal, closestHit.collider);
                }
            }

            m_LastRootPosition = Root.position;
        }

        bool IsHitValid(RaycastHit hit)
        {
            // ignore hits with an ignore component
            if (hit.collider.GetComponent<IgnoreHitDetection>())
            {
                return false;
            }

            // ignore hits with triggers that don't have a Damageable component
            if (hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null)
            {
                return false;
            }

            // ignore hits with specific ignored colliders (self colliders, by default)
            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider))
            {
                return false;
            }

            return true;
        }

        bool RealPlayerFullyBehindCover(CharacterController cc, Vector3 shooterOrigin)
        {
            if (cc == null) return true;

            Vector3 center = cc.transform.TransformPoint(cc.center);
            Vector3 up = cc.transform.up;
            Vector3 right = cc.transform.right;

            float radius = cc.radius;
            float height = Mathf.Max(cc.height, radius * 2f);
            float half = (height * 0.5f) - radius;

            // Sample a few points on the real capsule
            Vector3[] samples =
            {
        center,
        center + up * half,
        center - up * half,
        center + right * radius,
        center - right * radius
    };

            // If ANY point is visible (not blocked by world), then player is NOT fully behind cover.
            for (int i = 0; i < samples.Length; i++)
            {
                if (!Physics.Linecast(shooterOrigin, samples[i], WorldObstructionLayers, QueryTriggerInteraction.Ignore))
                    return false;
            }

            // All points blocked => fully behind cover
            return true;
        }

        void OnHit(Vector3 point, Vector3 normal, Collider collider)
        {
            GameObject owner = m_ProjectileBase.Owner;
            Transform playerTf = GameObject.FindGameObjectWithTag("Player")?.transform;

            BotHealthProxy proxy = collider.GetComponentInParent<BotHealthProxy>();
            Health health = collider.GetComponentInParent<Health>();

            GameObject ownerGO = proxy ? proxy.gameObject
                              : health ? health.gameObject
                              : collider.transform.root.gameObject;

            bool isBotHit = proxy != null;
            bool isPlayerHit = collider.CompareTag("Player") || collider.CompareTag(AimPointHitboxTag);

            string eventType = isBotHit ? "bot_hit"
                             : isPlayerHit ? "player_hit"
                             : "world_hit";

            // damage
            if (AreaOfDamage)
            {
                // area damage
                //LM.write($"[ProjectileStandard] OnHit AreaOfDamage");
                AreaOfDamage.InflictDamageInArea(Damage, point, HittableLayers, k_TriggerInteraction,
                    m_ProjectileBase.Owner);
            }
            else
            {
                if (UseRealPositionReject && collider.CompareTag(AimPointHitboxTag))
                {
                    Debug.Log("[Projectile] Hit delayed AimPoint hitbox");
                    CharacterController realCC = collider.GetComponentInParent<CharacterController>();
                    Health realHealth = collider.GetComponentInParent<Health>();

             
                    Vector3 shooterOrigin = m_ProjectileBase != null ? m_ProjectileBase.InitialPosition : Root.position;

       
                    if (RealPlayerFullyBehindCover(realCC, shooterOrigin))
                    {
                        Debug.Log("[Projectile] REJECTED: Real player fully behind cover");
                        EventManager.Broadcast(new HitCsvEvent
                        {
                            EventType = "player_hit",
                            ShooterId = owner ? m_ProjectileBase.Owner.name : "Unknown",
                            TargetId = collider.name,
                            Damage = Damage,
                            HitPoint = point,

                            AcceptShot = false,
                            ShotAroundCorner = true

                        });

                        Destroy(gameObject);
                        return;
                    }
                    else
                    {
                        Debug.Log("[Projectile] ACCEPTED: Real player exposed");
                        EventManager.Broadcast(new HitCsvEvent
                        {
                            EventType = "player_hit",
                            ShooterId = owner ? m_ProjectileBase.Owner.name : "Unknown",
                            TargetId = collider.name,
                            Damage = Damage,
                            HitPoint = point,

                            AcceptShot = true,
                            ShotAroundCorner = false

                        });
                    }

                    if (realHealth != null)
                    {
                        realHealth.TakeDamage(Damage, m_ProjectileBase.Owner);
                    }
                }
                else
                {
                    // Existing behavior for bots/proxies/world
                    //var proxy = collider.GetComponentInParent<BotHealthProxy>();
                    if (proxy != null)
                    {
                        proxy.TakeDamage(Damage, m_ProjectileBase.Owner);
                    }
                    else
                    {
                        Damageable damageable = collider.GetComponent<Damageable>();
                        if (damageable)
                            damageable.InflictDamage(Damage, false, m_ProjectileBase.Owner);
                    }
                }
            }

            // ================= SHOT LOGIC =================

            string hitObjectName = ownerGO.name;

            bool shotAroundCorner = false;

            if (owner != null && playerTf != null)
            {
                bool shooterIsBot = owner.CompareTag("Bot");
                bool shooterIsPlayer = owner.CompareTag("Player");

                // ---------- BOT → PLAYER ----------
                if (shooterIsBot && isPlayerHit)
                {
                    Transform botTf = owner.transform;

                    // use player's REAL body (capsule), not aimpoint
                    Transform capsuleTf = playerTf.Find("Capsule");

                    if (botTf != null && capsuleTf != null)
                    {
                        shotAroundCorner = IsLineOfSightBlocked(botTf.position, capsuleTf.position);
                    }

                    hitObjectName = "Player_AimPoint";
                }

                // ---------- PLAYER → BOT ----------
                else if (shooterIsPlayer && isBotHit && proxy != null)
                {
                    //    Transform futureBotTf = proxy.futureHealth
                    //        ? proxy.futureHealth.transform
                    //        : null;

                    //    Transform capsuleTf = playerTf.Find("Capsule");

                    //    if (futureBotTf != null)
                    //    {
                    //        shotAroundCorner = IsLineOfSightBlocked(futureBotTf.position, capsuleTf.position);
                    //    }

                    //    // identify which bot got hit
                    //    if (proxy.futureHealth &&
                    //        collider.transform.IsChildOf(proxy.futureHealth.transform))
                    //    {
                    //        hitObjectName = "FutureBot";
                    //    }
                    //    else if (proxy.pastHealth &&
                    //             collider.transform.IsChildOf(proxy.pastHealth.transform))
                    //    {
                    //        hitObjectName = "PastBot";
                    //    }
                    //    else
                    //    {
                    //        hitObjectName = "Bot_Unknown";
                    //    }
                }

                // ---------- WORLD ----------
                else
                {
                    shotAroundCorner = false;
                    hitObjectName = collider.gameObject.name;

                    EventManager.Broadcast(new HitCsvEvent
                    {
                        EventType = "world_hit",
                        ShooterId = owner ? m_ProjectileBase.Owner.name : "Unknown",
                        TargetId = hitObjectName,
                        Damage = Damage,
                        HitPoint = point,

                        ShotAroundCorner = shotAroundCorner,
                    });
                }
            }


            // impact vfx
            if (ImpactVfx)
            {
                GameObject impactVfxInstance = Instantiate(ImpactVfx, point + (normal * ImpactVfxSpawnOffset),
                    Quaternion.LookRotation(normal));
                if (ImpactVfxLifetime > 0)
                {
                    Destroy(impactVfxInstance.gameObject, ImpactVfxLifetime);
                }
            }

            // impact sfx
            if (ImpactSfxClip)
            {
                AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
            }


            //if (AreaOfDamage)
            //{
            //    // area damage
            //    AreaOfDamage.InflictDamageInArea(Damage, point, HittableLayers, k_TriggerInteraction,
            //        m_ProjectileBase.Owner);
            //}
            //else
            //{
            //    var proxy = collider.GetComponentInParent<BotHealthProxy>();
            //    if (proxy != null)
            //    {
            //        //LM.write($"[ProjectileStandard] OnHit proxy != null");
            //        proxy.TakeDamage(Damage, m_ProjectileBase.Owner);
            //    }
            //    else
            //    {
            //        //LM.write($"[ProjectileStandard] OnHit else");
            //        Damageable damageable = collider.GetComponent<Damageable>();
            //        if (damageable)
            //            damageable.InflictDamage(Damage, false, m_ProjectileBase.Owner);
            //    }
            //}

            //// impact vfx
            //if (ImpactVfx)
            //{
            //    GameObject impactVfxInstance = Instantiate(ImpactVfx, point + (normal * ImpactVfxSpawnOffset),
            //        Quaternion.LookRotation(normal));
            //    if (ImpactVfxLifetime > 0)
            //    {
            //        Destroy(impactVfxInstance.gameObject, ImpactVfxLifetime);
            //    }
            //}

            //// impact sfx
            //if (ImpactSfxClip)
            //{
            //    AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
            //}

            // Self Destruct
            Destroy(this.gameObject);
        
        }

        bool IsLineOfSightBlocked(Vector3 origin, Vector3 target)
        {
            Vector3 dir = target - origin;
            float dist = dir.magnitude;

            if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist))
            {
                float targetDist = dist;
                if (hit.distance < targetDist - 0.01f)
                {
                    Debug.DrawRay(origin, dir, Color.red, 0.5f);
                    return true;
                }
                else
                {
                    Debug.DrawRay(origin, dir, Color.green, 0.5f);
                    return false;
                }
            }

            return false; // clear LOS
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = RadiusColor;
            Gizmos.DrawSphere(transform.position, Radius);
        }
    }
}