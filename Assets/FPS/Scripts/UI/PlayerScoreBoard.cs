using System.Collections.Generic;
using TMPro;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using static RoundManager;

namespace Unity.FPS.UI
{
    public class PlayerScoreDisplay : MonoBehaviour, IScoreDisplay
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

        //public EnemyController enemyController;
        EnemyController enemyController;
        HiderController hiderController;

        int score = 0;

        void Start()
        {
            foreach (HealthEntry health in m_healthbars)
            {
                health.m_health.OnDamaged += OnDamaged;
            }

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.RegisterScoreDisplay(this);
                RoundManager.Instance.OnBotRoleChanged += OnRoleChanged;

                var enemy = RoundManager.Instance.CurrentEnemy;
                if (enemy != null)
                {
                    SetEnemy(enemy);
                }
            }
        }

        void OnEnable()
        {
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.RegisterScoreDisplay(this);
                RoundManager.Instance.OnBotRoleChanged += OnRoleChanged;

                var enemy = RoundManager.Instance.CurrentEnemy;
                if (enemy != null)
                {
                    SetEnemy(enemy);
                }
            }
        }
        void OnDisable()
        {
            if (RoundManager.Instance != null)
                RoundManager.Instance.OnBotRoleChanged -= OnRoleChanged;
        }

        void FixedUpdate()
        {
            if (enemyController == null && hiderController == null)
                return;

            bool isSeeing = false;

            if (enemyController != null)
                isSeeing = enemyController.IsSeeingTarget;
            else if (hiderController != null)
                isSeeing = hiderController.IsSeeingTarget;

            if (IsSeeker && !isSeeing)
                score += 1;
            else if (!IsSeeker && !isSeeing)
                score -= 1;

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

        public void SetEnemy(GameObject enemy)
        {
            enemyController = null;
            hiderController = null;

            if (enemy == null)
                return;

            enemyController = enemy.GetComponent<EnemyController>();
            hiderController = enemy.GetComponent<HiderController>();

            IsSeeker = RoundManager.Instance.IsSeeker;

            // Reset score once per round
            score = IsSeeker ? 1000 : 4000;

            ScoreText.text = $"Score: {score}";
        }


        void OnRoleChanged(bool seeker)
        {
            IsSeeker = seeker;

            // reset score each round
            score = IsSeeker ? 1000 : 4000;
            ScoreText.text = $"Score: {score}";
        }
    }
}