using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class WorldspaceHealthBar : MonoBehaviour
    {
        [Tooltip("Health component to track")] public Health Health;

        [Tooltip("Image component displaying health left")]
        public Image HealthBarImage;

        [Tooltip("The floating healthbar pivot transform")]
        public Transform HealthBarPivot;

        [Tooltip("Whether the health bar is visible when at full health or not")]
        public bool HideFullHealthBar = true;

        [Tooltip("Whether the health bar should be visible at all")]
        public bool VisibleHealthBar = true;


        public void setHealthVisibility(bool visible)
        {
            VisibleHealthBar = visible;
        }


        void Update()
        {
            // update health bar value
            HealthBarImage.fillAmount = Health.CurrentHealth / Health.MaxHealth;

            // rotate health bar to face the camera/player
            HealthBarPivot.LookAt(Camera.main.transform.position);

            // hide health bar if needed
            if (!VisibleHealthBar)
                HealthBarPivot.gameObject.SetActive(false);
            else if (HideFullHealthBar)
                HealthBarPivot.gameObject.SetActive(HealthBarImage.fillAmount != 1);

            if (Health.CurrentHealth <= 0f)
            {
                HealthBarPivot.gameObject.SetActive(false);
            }
        }
    }
}