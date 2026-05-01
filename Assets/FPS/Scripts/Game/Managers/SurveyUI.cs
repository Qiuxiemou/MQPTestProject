using UnityEngine;
using UnityEngine.UI;

public class SurveyUI : MonoBehaviour
{
    [Header("Sliders (1–5)")]
    public Slider lagSlider;
    public Slider hiderSlider;
    public Slider seekerSlider;

    [Header("Title")]
    public Text titleText;            // "Round X Survey"

    [Header("Buttons")]
    public Button submitButton;
    public Button exitButton;
    public Button newgameButton;

    int _roundShown = 0;

    [SerializeField] private GameObject seekerQuestions;
    [SerializeField] private GameObject hiderQuestions;

    [SerializeField] private Text roleQuestionText;

    private bool lagTouched = false;
    private bool hiderTouched = false;
    private bool seekerTouched = false;

    void Awake()
    {
        if (submitButton)
            submitButton.interactable = true;

        Setup(lagSlider);
        Setup(hiderSlider);
        Setup(seekerSlider);
        if (lagSlider)
            lagSlider.onValueChanged.AddListener((v) =>
            {
                lagTouched = true;
                //CheckAllAnswered();
            });

        if (hiderSlider)
            hiderSlider.onValueChanged.AddListener((v) =>
            {
                hiderTouched = true;
                //CheckAllAnswered();
            });

        if (seekerSlider)
            seekerSlider.onValueChanged.AddListener((v) =>
            {
                seekerTouched = true;
                //CheckAllAnswered();
            });

        Hide();

        if (submitButton) submitButton.onClick.AddListener(OnSubmit);
        if (exitButton) exitButton.onClick.AddListener(OnExit);

        //DontDestroyOnLoad(gameObject);
        //Debug.Log("[DEBUG] SurveyUI Awake");
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

        lagTouched = false;
        hiderTouched = false;
        seekerTouched = false;

        if (submitButton)
            submitButton.interactable = true;

        //Debug.Log("[DEBUG] SurveyUI shown");
        //Debug.Log($"[DEBUG] Time.timeScale = {Time.timeScale}");
    }

    public void Hide() => gameObject.SetActive(false);

    void OnSubmit()
    {
        var data = new SurveyData
        /*
        public struct SurveyData{
        public int round;
        public float smoothness;
        public float responsiveness;
        public float fairness;
        public float qoe;
        public float fun;
        public string notes;}
        */
        {
            round = _roundShown,
            lag = lagSlider ? lagSlider.value : 3f,
            hider = hiderSlider ? hiderSlider.value : 3f,
            seeker = seekerSlider ? seekerSlider.value : 3f
        };

        Hide();
        RoundManager.Instance.SubmitSurvey(data);
    }
    void OnExit()
    {
        Hide();
        RoundManager.Instance.ExitGame();
    }
    void OnEnable()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnBotRoleChanged += UpdateRoleUI;
            UpdateRoleUI(RoundManager.Instance.IsSeeker);
        }
    }
    void OnDisable()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnBotRoleChanged -= UpdateRoleUI;
        }
    }
    private void UpdateRoleUI(bool isSeeker)
    {
        seekerQuestions.SetActive(!isSeeker); // bot is not seeker, player is seeker
        hiderQuestions.SetActive(isSeeker); // bot is seeker, player is hider
        //if (seekerQuestions) seekerQuestions.SetActive(!isSeeker);
        //if (hiderQuestions) hiderQuestions.SetActive(isSeeker);
        if (!isSeeker)
        {
            roleQuestionText.text =
                "Q2. How difficult was it to hit the hider?";
        }
        else
        {
            roleQuestionText.text =
                "Q2. How often were you shot around the corner?";
        }
    }
    void CheckAllAnswered()
    {
        //bool roleSpecificAnswered;

        //if (RoundManager.Instance != null && RoundManager.Instance.IsSeeker)
        //    roleSpecificAnswered = hiderTouched;   // player is hider
        //else
        //    roleSpecificAnswered = seekerTouched;    // player is seeker

        //if (lagTouched && roleSpecificAnswered)
        //    submitButton.interactable = true;
        //else
        //    submitButton.interactable = false;

        //submimtButton.interactable = true;
    }
}
