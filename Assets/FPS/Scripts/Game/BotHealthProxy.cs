using UnityEngine;

namespace Unity.FPS.Game
{
    // Put this on the client bot (or its hitbox)
    public class BotHealthProxy : MonoBehaviour
    {
        [Tooltip("Authority Health component on the server-side bot")]
        public Health serverHealth;

        [Tooltip("Optional: forward damage with same visual delay (ms)")]
        public float forwardDelayMs = 200f;

        void OnEnable()
        {
            if (serverHealth != null)
                serverHealth.OnDie += OnServerDie;
        }

        void OnDisable()
        {
            if (serverHealth != null)
                serverHealth.OnDie -= OnServerDie;
        }

        public void TakeDamage(float damage, GameObject source)
        {
            if (!serverHealth) return;
            StartCoroutine(Forward(damage, source));
        }

        System.Collections.IEnumerator Forward(float dmg, GameObject src)
        {
            if (forwardDelayMs > 0f)
                yield return new WaitForSeconds(forwardDelayMs / 1000f);
            serverHealth.TakeDamage(dmg, src);
        }

        void OnServerDie()
        {
            // destroy the client ghost when server dies
            Destroy(gameObject);
        }
    }
}
