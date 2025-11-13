using UnityEngine;
using UnityEngine.UI;

public class TimerUI : MonoBehaviour
{
    [SerializeField] private Text timerText;  // Legacy Text

    void Awake()
    {
        Hide();
        //DontDestroyOnLoad(gameObject);           
    }

    void OnEnable()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnTimerTick += HandleTick;
        }
    }

    void OnDisable()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnTimerTick -= HandleTick;
        }
    }

    private void HandleTick(float timeRemaining)
    {
        int seconds = Mathf.CeilToInt(timeRemaining);
        int m = seconds / 60;
        int s = seconds % 60;
        if (timerText != null) timerText.text = $"{m:0}:{s:00}";
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
