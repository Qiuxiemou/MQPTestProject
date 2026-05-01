using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoundStartMenuUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject root;

    public Toggle seekerToggle;

    public Slider latencySlider;
    public TMP_Text latencyValueText;

    public Slider speedSlider;
    public TMP_Text speedValueText;

    public TMP_Dropdown weaponDropdown;
    public TMP_Dropdown timewarpDropdown;

    public Button startButton;

    void Start()
    {
        latencySlider.onValueChanged.AddListener(UpdateLatencyText);
        speedSlider.onValueChanged.AddListener(UpdateSpeedText);

        startButton.onClick.AddListener(OnStartPressed);

        UpdateLatencyText(latencySlider.value);
        UpdateSpeedText(speedSlider.value);
    }

    void UpdateLatencyText(float value)
    {
        latencyValueText.text = $"Latency: {value:0} ms";
    }

    void UpdateSpeedText(float value)
    {
        speedValueText.text = $"Speed: {value:0.0}";
    }

    void OnStartPressed()
    {
        if (RoundManager.Instance == null)
        {
            Debug.LogWarning("RoundManager not found!");
            return;
        }

        RoundSettingsOverride settings = new RoundSettingsOverride
        {
            isSeeker = seekerToggle.isOn,
            latency = latencySlider.value,
            speed = speedSlider.value,
            weaponIndex = weaponDropdown.value,
            timewarpMode = (TimewarpMode)timewarpDropdown.value
        };

        Debug.Log($"Applying settings: Seeker={settings.isSeeker}, Latency={settings.latency}, Speed={settings.speed}, WeaponIndex={settings.weaponIndex}, TimewarpMode={settings.timewarpMode}");

        RoundManager.Instance.ApplyPlayerOverrides(settings);
        RoundManager.Instance.StartRoundFromUI();

        Hide();
    }

    public void Show() => root.SetActive(true);
    public void Hide() => root.SetActive(false);
}