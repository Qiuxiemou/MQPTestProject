using UnityEngine;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.AI;
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

        public EnemyController enemyController;

        int score = 0;

        void Start()
        {
            foreach (HealthEntry health in m_healthbars)
            {
                health.m_health.OnDamaged += OnDamaged;
            }
            if (IsSeeker)
                score = 4000;
            else
                score = 1000;
        }

        void FixedUpdate()
        {
            if (IsSeeker && enemyController.IsSeeingTarget == false)
            {
                score -= 1;
            }
            else if (!IsSeeker && enemyController.IsSeeingTarget == false)
            {
                score += 1;
            }
            ScoreText.text = $"Score: {score}";
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
                if (IsSeeker)
                {
                    score += 50;
                }
                else
                {
                    score -= 50;
                }
            }
        }
    }
}