using UnityEngine;
using Unity.FPS.Game;
using System.Collections;

namespace Unity.FPS.Game
{
    [RequireComponent(typeof(Health))]
    public class BotHealthProxy : MonoBehaviour
    {
        [Header("Authority")]
        [Tooltip("Health on the server-side bot (authoritative)")]
        public Health serverHealth;

        [Header("Delays")]
        [Tooltip("Delay (ms) before forwarding damage to the server bot")]
        public float forwardDelayMs = 200f;

        [Tooltip("Extra buffer (ms) after client death before destroying this proxy, to ensure all delayed hits are forwarded")]
        public float destroyBufferAfterClientDeathMs = 100f;

        private Health clientHealth;
        private bool clientDying;

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
                clientHealth.OnDie     -= OnClientDie;
            }
            // if (serverHealth != null) serverHealth.OnDie -= OnServerDie;
        }

        public void TakeDamage(float damage, GameObject source)
        {
            if (clientHealth != null && !clientDying)
                clientHealth.TakeDamage(damage, source);


            if (serverHealth != null)
                StartCoroutine(ForwardToServerAfterDelay(damage, source));


        }

        IEnumerator ForwardToServerAfterDelay(float damage, GameObject source)
        {
            if (forwardDelayMs > 0f)
                yield return new WaitForSeconds(forwardDelayMs / 1000f);

            if (serverHealth != null)
                serverHealth.TakeDamage(damage, source);


            EventManager.Broadcast(new HitCsvEvent {
                EventType      = "server_applied",
                ShooterId      = source ? source.name : "Unknown",
                TargetId       = serverHealth.gameObject.name,
                Damage         = damage,
                ForwardDelayMs = forwardDelayMs,
                HitPoint       = transform.position, 
                HitBox         = true,              
                ClientTf       = transform,
                ClientHealth   = clientHealth,
                ServerTf       = serverHealth.transform,
                ServerHealth   = serverHealth
            });
        }

        void OnClientDamaged(float damage, GameObject source)
        {

        }

        void OnClientDie()
        {
            clientDying = true;


            foreach (var r in GetComponentsInChildren<Renderer>(true))  r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>(true))  c.enabled = false;

            float wait = (forwardDelayMs + destroyBufferAfterClientDeathMs) / 1000f;
            Destroy(gameObject, Mathf.Max(0.01f, wait));
        }

        // void OnServerDie() { if (this) Destroy(gameObject); }
    }
}
