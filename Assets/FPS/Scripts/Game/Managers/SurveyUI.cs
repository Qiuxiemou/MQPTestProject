using UnityEngine;
using UnityEngine.UI;

public class SurveyUI : MonoBehaviour
{
    [Header("Sliders (1–5)")]
    public Slider smoothnessSlider;
    public Slider responsivenessSlider;
    public Slider fairnessSlider;
    public Slider qoeSlider;
    public Slider funSlider;

    [Header("Optional")]
    public InputField notesInput;     // can be null
    public Text titleText;            // "Round X Survey"

    [Header("Buttons")]
    public Button submitButton;
    public Button exitButton;
    public Button newgameButton;

    int _roundShown = 0;

    void Awake()
    {
        Setup(smoothnessSlider);
        Setup(responsivenessSlider);
        Setup(fairnessSlider);
        Setup(qoeSlider);
        Setup(funSlider);

        Hide();

        if (submitButton) submitButton.onClick.AddListener(OnSubmit);
        if (exitButton) exitButton.onClick.AddListener(OnExit);

        DontDestroyOnLoad(gameObject);           // safe even if you stay in one scene
    }

    static void Setup(Slider s)
    {
        if (!s) return;
        s.minValue = 1f;
        s.maxValue = 5f;
        s.wholeNumbers = false; // allow decimals
        s.value = 3f;
    }

    public void Show(int roundNumber)
    {
        _roundShown = roundNumber;
        if (titleText) titleText.text = $"Round {roundNumber} Survey";
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    void OnSubmit()
    {
        var data = new SurveyData
        {
            round = _roundShown,
            smoothness = smoothnessSlider ? smoothnessSlider.value : 3f,
            responsiveness = responsivenessSlider ? responsivenessSlider.value : 3f,
            fairness = fairnessSlider ? fairnessSlider.value : 3f,
            qoe = qoeSlider ? qoeSlider.value : 3f,
            fun = funSlider ? funSlider.value : 3f,
            notes = notesInput ? notesInput.text : ""
        };

        Hide();
        RoundManager.Instance.SubmitSurvey(data);
    }

    void OnExit()
    {
        Hide();
        RoundManager.Instance.ExitGame();
    }
}
