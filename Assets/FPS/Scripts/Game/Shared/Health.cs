using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    public class Health : MonoBehaviour
    {
        [Tooltip("Maximum amount of health")] public float MaxHealth = 10f;

        [Tooltip("Health ratio at which the critical health vignette starts appearing")]
        public float CriticalHealthRatio = 0.3f;

        public UnityAction<float, GameObject> OnDamaged;
        public UnityAction<float> OnHealed;
        public UnityAction OnDie;

        [Tooltip("Health on the past bot")]
        public Health pastHealth;

        [Tooltip("Health on the future bot")]
        public Health futureHealth;
        public float CurrentHealth { get; set; }
        public bool Invincible { get; set; }
        public bool CanPickup() => CurrentHealth < MaxHealth;

        public float GetRatio() => CurrentHealth / MaxHealth;
        public bool IsCritical() => GetRatio() <= CriticalHealthRatio;

        bool m_IsDead;

        void Start()
        {
            CurrentHealth = MaxHealth;
        }

        public void Heal(float healAmount)
        {
            float healthBefore = CurrentHealth;
            CurrentHealth += healAmount;
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);

            // call OnHeal action
            float trueHealAmount = CurrentHealth - healthBefore;
            if (trueHealAmount > 0f)
            {
                OnHealed?.Invoke(trueHealAmount);
            }
        }

        public void TakeDamage(float damage, GameObject damageSource)
        {
            // Conditional Time Warp
            LM.write($"CTW Status: {TimewarpSettings.ConditionalTimeWarpEnabled}");

            if (TimewarpSettings.ConditionalTimeWarpEnabled)
            {
                LM.write("CTW: ACTIVE LOS");
                bool hasLOS = HasLineOfSightFromFutureToPlayer();
                if (!hasLOS)
                {
                    LM.write("CTW: HIT DENIED");
                    return;
                }
                LM.write("CTW: HIT ALLOWED");
            }

            if (Invincible)
                return;

            LM.write($"TakeDamage ENTER {transform.root.name} | frame {Time.frameCount}");


            float healthBefore = CurrentHealth;
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);

            // call OnDamage action
            float trueDamageAmount = healthBefore - CurrentHealth;
            if (trueDamageAmount > 0f)
            {
                LM.write($"[{gameObject.name}] took {damage} damage");
                OnDamaged?.Invoke(trueDamageAmount, damageSource);
            }

            HandleDeath();
        }

        public void Kill()
        {
            CurrentHealth = 0f;

            // call OnDamage action
            OnDamaged?.Invoke(MaxHealth, null);

            HandleDeath();
        }

        void HandleDeath()
        {
            if (m_IsDead)
                return;

            // call OnDie action
            if (CurrentHealth <= 0f)
            {
                m_IsDead = true;

                // Log Event Death
                EventManager.Broadcast(new DeathEvent
                {
                    VictimId = gameObject.name,
                    KillerId = "Unknown"
                });

                OnDie?.Invoke();
            }
        }

        bool HasLineOfSightFromFutureToPlayer()
        {
            // Check to see if future bot still has health to pass damage to
            if (!futureHealth) return false;

            // Check if player still exists to have LOS to
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (!player) return false;

            // Raycast from future bot's top to player's top to check LOS
            Vector3 origin = futureHealth.transform.position + Vector3.up * 1.5f;
            Vector3 target = player.transform.position + Vector3.up * 1.5f;

            Vector3 dir = target - origin;
            float dist = dir.magnitude;

            // Only consider walls and unhitable player layers' colliders as LOS blockers
            int mask = LayerMask.GetMask("Wall", "PlayerUnhitable");
            int playerLayer = LayerMask.NameToLayer("PlayerUnhitable");

            // yellow = attempted
            Debug.DrawRay(origin, dir, Color.yellow, 0.1f);

            // If we hit something and it's not the player, LOS is blocked
            if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, mask))
            {
                LM.write($"LOS hit: {hit.collider.name} | layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                //if (hit.collider.CompareTag("Player"))
                if (hit.collider.gameObject.layer == playerLayer)
                {
                    // green = clear LOS
                    Debug.DrawRay(origin, dir, Color.green, 0.1f);
                    return true;
                }
                else
                {
                    // red = blocked
                    LM.write("RED: LOS is Blocked");
                    Debug.DrawRay(origin, dir, Color.red, 0.1f);
                    return false;
                }
            }


            return false;
        }
    }
}