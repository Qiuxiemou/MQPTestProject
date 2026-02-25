using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

public class RoundStartUI : MonoBehaviour
{
    public GameObject panel;
    public Text text;

    public void Show(bool isSeeker)
    {
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        text.text = isSeeker
            ? "You're the Hider" 
            : "You're the Seeker";

        text.text += "\n\nPress [Tab] to start the round";
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
