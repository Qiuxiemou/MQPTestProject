using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

public class TimerUI : MonoBehaviour
{
    [SerializeField] private Text timerText;  
    [SerializeField] private Text latencyText;
    [SerializeField] private Text timewarpText;
    [SerializeField] private Image leadShotOn;

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

            //RoundManager.Instance.OnBotRoleChanged += UpdateRoundInfo;

            //UpdateRoundInfo(RoundManager.Instance.IsSeeker);
        }
    }

    void OnDisable()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.OnTimerTick -= HandleTick;
            //RoundManager.Instance.OnBotRoleChanged -= UpdateRoundInfo;
        }
    }

    private void HandleTick(float timeRemaining)
    {
        int seconds = Mathf.CeilToInt(timeRemaining);
        int m = seconds / 60;
        int s = seconds % 60;
        if (timerText != null) timerText.text = $"{m:0}:{s:00}";
    }

    private void UpdateRoundInfo(bool _)
    {
        if (RoundManager.Instance == null) return;

        if (latencyText != null)
        {
            LM.write($"Latency: {RoundManager.Instance.CurrentLatencyMs} ms");
            latencyText.text = $"Latency: {RoundManager.Instance.CurrentLatencyMs} ms";
        }

        if (timewarpText != null)
        {
            LM.write($"Timewarp: {RoundManager.Instance.CurrentTimewarpMode}");
            timewarpText.text = $"Timewarp: {RoundManager.Instance.CurrentTimewarpMode}";
        }
    }

    public void ForceRefresh()
    {
        if (RoundManager.Instance == null) return;        

        //timewarpText.text = $"Mode: {RoundManager.Instance.CurrentTimewarpMode}";
        if (RoundManager.Instance.CurrentTimewarpMode == TimewarpMode.None && !RoundManager.Instance.IsSeeker)
        {
            latencyText.text = $"PING: {RoundManager.Instance.CurrentLatencyMs * 2}ms";
            timewarpText.text = $"LEAD YOUR SHOT";
            Color c = leadShotOn.color;
            c.a = 1f;
            leadShotOn.color = c;
        }
        else if (RoundManager.Instance.CurrentTimewarpMode != TimewarpMode.None && !RoundManager.Instance.IsSeeker)
        {
            latencyText.text = $"";
            timewarpText.text = $"AIM DIRECTLY AT BOT";
            Color c = leadShotOn.color;
            c.a = 0f;
            leadShotOn.color = c;
        }
        else
        {
            timewarpText.text = $"";
            latencyText.text = $"";
            Color c = leadShotOn.color;
            c.a = 0f;
            leadShotOn.color = c;
        }
    }

    public void Show() { gameObject.SetActive(true); ForceRefresh(); }
    public void Hide() { gameObject.SetActive(false); }
}
