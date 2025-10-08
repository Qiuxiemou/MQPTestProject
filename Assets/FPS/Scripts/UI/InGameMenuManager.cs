using TMPro;

using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UnityEditor.Experimental.GraphView.GraphView;

namespace Unity.FPS.UI
{
    public class InGameMenuManager : MonoBehaviour
    {
        [Tooltip("Root GameObject of the menu used to toggle its activation")]
        public GameObject MenuRoot;

        [Tooltip("Master volume when menu is open")] [Range(0.001f, 1f)]
        public float VolumeWhenMenuOpen = 0.5f;

        [Tooltip("Slider component for look sensitivity")]
        public Slider LookSensitivitySlider;

        [Tooltip("Toggle component for shadows")]
        public Toggle ShadowsToggle;

        [Tooltip("Toggle component for invincibility")]
        public Toggle InvincibilityToggle;

        [Tooltip("Toggle component for framerate display")]
        public Toggle FramerateToggle;

        [Tooltip("GameObject for the controls")]
        public GameObject ControlImage;

        // UI
        [Header("Latency Controls")]
        [Tooltip("Slider that controls delayed bot latency (ms)")]
        public Slider LatencySlider;

        [Tooltip("Text element showing current latency in ms (optional)")]
        public TMP_Text LatencyValueLabel;

        // Targets
        [Tooltip("The delayed bot component to control")]
        public BotDelayed DelayedBot;

        [Tooltip("(Optional) Damage proxy to apply same visual delay")]
        public BotHealthProxy DelayedBotHealthProxy;

        // Bot Visibility Toggles (ADD) 
        [Header("Bot Toggles")]
        [Tooltip("Toggle to show/hide the Original Bot's visuals only")]
        public Toggle OrigBotToggle;

        [Tooltip("Toggle to show/hide the Delayed Bot's visuals only")]
        public Toggle DelayedBotToggle;

        [Tooltip("Toggle to enable/disable timewarp")]
        public Toggle TimeWarpToggle;

        [Tooltip("Root object of Future Bot (e.g., Enemy_OrigBot)")]
        public GameObject FutureBotRoot;

        [Tooltip("Root object of Server Bot (e.g., Enemy_DelayedBot)")]
        public GameObject ServerBotRoot;

        [Tooltip("Root object of Past Bot")]
        public GameObject PastBotRoot;


        PlayerInputHandler m_PlayerInputsHandler;
        Health m_PlayerHealth;
        FramerateCounter m_FramerateCounter;
        
        private InputAction m_SubmitAction;
        private InputAction m_CancelAction;
        private InputAction m_NavigateAction;
        private InputAction m_MenuAction;

        void Start()
        {
            m_PlayerInputsHandler = FindFirstObjectByType<PlayerInputHandler>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerInputHandler, InGameMenuManager>(m_PlayerInputsHandler,
                this);

            m_PlayerHealth = m_PlayerInputsHandler.GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, InGameMenuManager>(m_PlayerHealth, this, gameObject);

            m_FramerateCounter = FindFirstObjectByType<FramerateCounter>();
            DebugUtility.HandleErrorIfNullFindObject<FramerateCounter, InGameMenuManager>(m_FramerateCounter, this);

            DelayedBot = FindAnyObjectByType<BotDelayed>();

            MenuRoot.SetActive(false);

            LookSensitivitySlider.value = m_PlayerInputsHandler.LookSensitivity;
            LookSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);

            ShadowsToggle.isOn = QualitySettings.shadows != ShadowQuality.Disable;
            ShadowsToggle.onValueChanged.AddListener(OnShadowsChanged);

            InvincibilityToggle.isOn = m_PlayerHealth.Invincible;
            InvincibilityToggle.onValueChanged.AddListener(OnInvincibilityChanged);

            FramerateToggle.isOn = m_FramerateCounter.UIText.gameObject.activeSelf;
            FramerateToggle.onValueChanged.AddListener(OnFramerateCounterChanged);

            m_SubmitAction = InputSystem.actions.FindAction("UI/Submit");
            m_CancelAction = InputSystem.actions.FindAction("UI/Cancel");
            m_NavigateAction = InputSystem.actions.FindAction("UI/Navigate");
            m_MenuAction = InputSystem.actions.FindAction("UI/Menu");
            
            m_SubmitAction.Enable();
            m_CancelAction.Enable();
            m_NavigateAction.Enable();
            m_MenuAction.Enable();

            //  Latency control setup 
            if (LatencySlider)
            {
                // sensible defaults; override in Inspector if you like
                if (LatencySlider.minValue == 0f) LatencySlider.minValue = 0f;
                if (LatencySlider.maxValue <= 0f) LatencySlider.maxValue = 2000f; // 0–2000 ms

                float startMs = DelayedBot ? DelayedBot.GetLatency() : 0f;
                LatencySlider.value = startMs;
                UpdateLatencyLabel(startMs);
                LatencySlider.onValueChanged.AddListener(OnLatencyChanged);
            }

            // Bot visibility toggle setup (ADD) 
            if (OrigBotToggle && FutureBotRoot)
            {
                OrigBotToggle.isOn = GetVisualsVisible(FutureBotRoot.transform);
                OrigBotToggle.onValueChanged.AddListener(OnOrigBotToggleChanged);
            }

            if (DelayedBotToggle && ServerBotRoot)
            {
                DelayedBotToggle.isOn = GetVisualsVisible(ServerBotRoot.transform);
                DelayedBotToggle.onValueChanged.AddListener(OnDelayedBotToggleChanged);
            }

            TimeWarpToggle.isOn = true;
            TimeWarpToggle.onValueChanged.AddListener(OnTimeWarpChanged);
        }

        /// Added functions to toggle bot visibility

        // Toggle only the visuals so bots keep simulating
        void SetVisualsVisible(Transform root, bool visible)
        {
            if (!root) return;

            // All renderers: MeshRenderer, SkinnedMeshRenderer, SpriteRenderer, LineRenderer,
            // TrailRenderer, ParticleSystemRenderer, etc.
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = visible;

            // Microgame often uses legacy Projector (e.g., ShadowProjector)
            var projectors = root.GetComponentsInChildren<Projector>(true);
            foreach (var p in projectors) p.enabled = visible;

            // If you ALSO want to hide world-space health bars, uncomment this:
            // var canvases = root.GetComponentsInChildren<Canvas>(true);
            // foreach (var c in canvases) c.enabled = visible;
        }

        // Read current visible state from any Renderer/Projector under the root
        bool GetVisualsVisible(Transform root)
        {
            if (!root) return true;
            var r = root.GetComponentInChildren<Renderer>(true);
            if (r) return r.enabled;
            var p = root.GetComponentInChildren<Projector>(true);
            if (p) return p.enabled;
            return true; // default if none found
        }

        void OnOrigBotToggleChanged(bool visible)
        {
            if (FutureBotRoot)
            {
                SetVisualsVisible(FutureBotRoot.transform, visible);
                FutureBotRoot.GetComponent<WorldspaceHealthBar>().setHealthVisibility(visible);
            }
        }

        void OnDelayedBotToggleChanged(bool visible)
        {
            if (ServerBotRoot)
            {
                SetVisualsVisible(ServerBotRoot.transform, visible);
                ServerBotRoot.GetComponent<WorldspaceHealthBar>().setHealthVisibility(visible);
            }
        }

        void OnTimeWarpChanged(bool enabled)
        {
            if (DelayedBotHealthProxy) DelayedBotHealthProxy.PropagateBackwards(enabled);
            
            if (enabled)
            {
                Transform hitboxTransform = PastBotRoot.transform.Find("HitBox");
                Debug.Log(hitboxTransform);
                if (hitboxTransform != null)
                {
                    hitboxTransform.gameObject.layer = 0;
                }

                hitboxTransform = FutureBotRoot.transform.Find("HitBox");
                if (hitboxTransform != null)
                {
                    hitboxTransform.gameObject.layer = 3;
                }
            }
            else
            {
                Transform hitboxTransform = PastBotRoot.transform.Find("HitBox");
                if (hitboxTransform != null)
                {
                    hitboxTransform.gameObject.layer = 3;
                }

                hitboxTransform = FutureBotRoot.transform.Find("HitBox");
                if (hitboxTransform != null)
                {
                    hitboxTransform.gameObject.layer = 0;
                }
            }
                TimeWarpToggle.isOn = enabled;
        }

        ///END Bot visibility toggle setup (ADD) 



        void OnLatencyChanged(float newMs)
        {
            if (DelayedBot) DelayedBot.SetLatency(newMs);
            if (DelayedBotHealthProxy) DelayedBotHealthProxy.SetDelay(newMs);
            UpdateLatencyLabel(newMs);
        }

        void UpdateLatencyLabel(float ms)
        {
            if (LatencyValueLabel) LatencyValueLabel.text = $"{Mathf.RoundToInt(ms)} ms";
        }


        void Update()
        {
            // Lock cursor when clicking outside of menu
            if (!MenuRoot.activeSelf && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (m_MenuAction.WasPressedThisFrame()
                || (MenuRoot.activeSelf && m_CancelAction.WasPressedThisFrame()))
            {
                if (ControlImage.activeSelf)
                {
                    ControlImage.SetActive(false);
                    return;
                }

                SetPauseMenuActivation(!MenuRoot.activeSelf);

            }

            if (m_NavigateAction.ReadValue<Vector2>().y != 0)
            {
                if (EventSystem.current.currentSelectedGameObject == null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    LookSensitivitySlider.Select();
                }
            }
        }

        public void ClosePauseMenu()
        {
            SetPauseMenuActivation(false);
        }

        void SetPauseMenuActivation(bool active)
        {
            MenuRoot.SetActive(active);

            if (MenuRoot.activeSelf)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Time.timeScale = 0f;
                AudioUtility.SetMasterVolume(VolumeWhenMenuOpen);

                EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Time.timeScale = 1f;
                AudioUtility.SetMasterVolume(1);
            }

        }

        void OnMouseSensitivityChanged(float newValue)
        {
            m_PlayerInputsHandler.LookSensitivity = newValue;
        }

        void OnShadowsChanged(bool newValue)
        {
            QualitySettings.shadows = newValue ? ShadowQuality.All : ShadowQuality.Disable;
        }

        void OnInvincibilityChanged(bool newValue)
        {
            m_PlayerHealth.Invincible = newValue;
        }

        void OnFramerateCounterChanged(bool newValue)
        {
            m_FramerateCounter.UIText.gameObject.SetActive(newValue);
        }

        public void OnShowControlButtonClicked(bool show)
        {
            ControlImage.SetActive(show);
        }
    }
}