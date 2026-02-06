using System.Collections;
using System.Runtime.InteropServices;
using Unity.FPS.Game;
using UnityEngine;
using static Codice.Client.BaseCommands.Import.Commit;

namespace Unity.FPS.Game
{
    [RequireComponent(typeof(Health))]
    public class BotHealthProxy : MonoBehaviour
    {
        [Header("Authority")]
        [Tooltip("Health on the past bot")]
        public Health pastHealth;

        [Tooltip("Health on the server bot")]
        public Health serverHealth;

        [Tooltip("Health on the future bot")]
        public Health futureHealth;

        [Header("Delays")]
        [Tooltip("Delay (ms) before forwarding damage to the server bot")]
        public static float forwardDelayMs = 200f;

        [Tooltip("Extra buffer (ms) after client death before destroying this proxy, to ensure all delayed hits are forwarded")]
        public float destroyBufferAfterClientDeathMs = 100f;

        private Health clientHealth;
        //private bool clientDying;
        private static bool propagateBackwards = false;

        void Awake()
        {
            clientHealth = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (clientHealth != null)
                clientHealth.OnDamaged += OnClientDamaged;


            if (clientHealth != null)
                clientHealth.OnDie += OnClientDie;

            // if (serverHealth != null) serverHealth.OnDie += OnServerDie;
        }

        void OnDisable()
        {
            if (clientHealth != null)
            {
                clientHealth.OnDamaged -= OnClientDamaged;
                clientHealth.OnDie -= OnClientDie;
            }
            // if (serverHealth != null) serverHealth.OnDie -= OnServerDie;
        }

        public void SetDelay(float ms)
        {
            // If we shorten the delay, old future-stamped states would feel wrong.
            // Clearing gives an immediate, predictable change.
            forwardDelayMs = Mathf.Max(0f, ms);
        }

        public float GetDelay()
        {
            return forwardDelayMs;
        }

        public void PropagateBackwards(bool propBack)
        {
            Debug.Log(propBack);
            propagateBackwards = !propBack;
            Debug.Log(propagateBackwards);
        }

        public IEnumerator DamageBackwards(float damage, GameObject source)
        {
            Debug.Log("Health Propagating backwards");
            LM.write("Health pass backwards");

            //futureHealth.TakeDamage(damage, source);

            if (forwardDelayMs > 0f)
                yield return new WaitForSeconds(forwardDelayMs / 1000f);

            if (serverHealth != null)
                serverHealth.TakeDamage(damage, source);

            if (forwardDelayMs > 0f)
                yield return new WaitForSeconds(forwardDelayMs / 1000f);

            if (pastHealth != null)
                pastHealth.TakeDamage(damage, source);
        }

        public IEnumerator DamageForwards(float damage, GameObject source) // Back propagate = false; time warp = true
        {
            Debug.Log("Health Propagating forwards");
            LM.write("Health pass forward");

            //pastHealth.TakeDamage(damage, source);

            if (futureHealth != null) futureHealth.TakeDamage(damage, source);

            if (forwardDelayMs > 0f)
                yield return new WaitForSeconds(forwardDelayMs / 1000f);

            if (serverHealth != null) serverHealth.TakeDamage(damage, source);
        }

        public void TakeDamage(float damage, GameObject source)
        {
            //if (clientHealth != null && !clientDying)
            //    clientHealth.TakeDamage(damage, source);


            //if (serverHealth != null)
            //    StartCoroutine(ForwardToServerAfterDelay(damage, source));

            LM.write($"{transform.root.name} takeDamage");

            Debug.Log(propagateBackwards);
            //if (propagateBackwards && futureHealth != null)
            //    StartCoroutine(DamageBackwards(damage, source));
            //else if (!propagateBackwards && pastHealth != null)
            //    StartCoroutine(DamageForwards(damage, source));

            // New logic with conditional time warp
            if (propagateBackwards && futureHealth != null)
            {
                StartCoroutine(DamageBackwards(damage, source));
                LM.write("No TimeWarp");
            }
            else if (!propagateBackwards && pastHealth != null)
            {
                LM.write("Attempting TimeWarp");
                LM.write($"CTW Status: {TimewarpSettings.ConditionalTimeWarpEnabled}");
                // Conditional Time Warp
                if (TimewarpSettings.ConditionalTimeWarpEnabled)
                {
                    LM.write("CTW: ACTIVE checking LOS");
                    if (!HasLineOfSightFromFutureToPlayer())
                    {
                        LM.write("CTW: HIT DENIED");
                        //return;
                    }
                    LM.write("CTW: HIT ALLOWED future bot has LOS");
                }

                StartCoroutine(DamageForwards(damage, source));
                LM.write("TimeWarp");
            }
        }

        IEnumerator ForwardToServerAfterDelay(float damage, GameObject source)
        {
            if (forwardDelayMs > 0f)
                yield return new WaitForSeconds(forwardDelayMs / 1000f);

            if (serverHealth != null)
                serverHealth.TakeDamage(damage, source);


            EventManager.Broadcast(new HitCsvEvent
            {
                EventType = "server_applied",
                ShooterId = source ? source.name : "Unknown",
                TargetId = serverHealth.gameObject.name,
                Damage = damage,
                ForwardDelayMs = forwardDelayMs,
                HitPoint = transform.position,
                HitBox = true,
                ClientTf = transform,
                ClientHealth = clientHealth,
                ServerTf = serverHealth.transform,
                ServerHealth = serverHealth
            });
        }

        void OnClientDamaged(float damage, GameObject source)
        {

        }

        void OnClientDie()
        {
            //clientDying = true;


            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;


            StartCoroutine(DelayedDestroy());
            //float wait = (forwardDelayMs + destroyBufferAfterClientDeathMs) / 1000f;
            //Destroy(gameObject, Mathf.Max(0.01f, wait));
        }

        IEnumerator DelayedDestroy()
        {
            // 2 delays max in backward case → forwardDelayMs * 2
            Debug.Log("Delaying death by" + forwardDelayMs * 2f);
            float maxChainTime = (forwardDelayMs * 2f + destroyBufferAfterClientDeathMs) / 1000f;
            yield return new WaitForSeconds(maxChainTime);
            Debug.Log("Waited for " + forwardDelayMs);

            Destroy(gameObject);
        }

        // void OnServerDie() { if (this) Destroy(gameObject); }
        bool HasLineOfSightFromFutureToPlayer()
        {
            if (!futureHealth) return false;
            LM.write("futureHealth is here");

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (!player) return false;
            LM.write("player is here");

            Vector3 origin = futureHealth.transform.position + Vector3.up * 1.5f;
            Vector3 target = player.transform.position + Vector3.up * 1.5f;

            Vector3 dir = target - origin;
            float dist = dir.magnitude;

            int mask = LayerMask.GetMask("Default", "Environment");

            // Debug ray: yellow for attempted, red for blocked, green for clear LOS
            Debug.DrawRay(origin, dir, Color.yellow, 0.1f);

            if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, mask))
            {
                if (!hit.collider.CompareTag("Player"))
                {
                    LM.write("player is here");
                    Debug.DrawRay(origin, dir, Color.red, 0.1f); Debug.DrawRay(origin, dir, Color.red, 0.1f);
                    return false;
                }
            }

            Debug.DrawRay(origin, dir, Color.green, 0.1f);
            return true;
        }
    }
}
