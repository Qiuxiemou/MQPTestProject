using UnityEngine;
using TMPro;
using Unity.FPS.Game;
using System.Collections.Generic;

namespace Unity.FPS.UI
{
    public class PlayerScoreDisplay : MonoBehaviour
    {
        public TextMeshProUGUI ScoreText;
        public bool IsSeeker;

        int m_ShotsHit;
        float m_SecondsPassed;

        [System.Serializable]
        public class HealthEntry
        {
            public Health m_health;
        }

        public List<HealthEntry> m_healthbars = new List<HealthEntry>();

        public Health m_PlayerHealth;

        void Start()
        {
            foreach (HealthEntry health in m_healthbars)
            {
                health.m_health.OnDamaged += OnDamaged;
            }
        }

        void FixedUpdate()
        {
            m_SecondsPassed = Time.time;
            ScoreText.text = $"Score: {CalculateScore()}";
        }

        void OnDestroy()
        {
            foreach (HealthEntry health in m_healthbars)
            {
                if (m_PlayerHealth != null)
                m_PlayerHealth.OnDamaged -= OnDamaged;
            }
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            if (damageSource != null)
            {
                m_ShotsHit++;
            }
        }

        int CalculateScore()
        {
            int seconds = Mathf.FloorToInt(m_SecondsPassed);

            if (IsSeeker)
                return 4000 - 50 * seconds + 50 * m_ShotsHit;
            else
                return 1000 + 50 * seconds - 50 * m_ShotsHit;
        }
    }
}