using UnityEngine;
using UnityEngine.Video;

public class TutorialLoader : MonoBehaviour
{
    public VideoClip tutorial1;
    public VideoClip tutorial2;
    public VideoClip nullclip;
    public VideoPlayer videoPlayer;

    public void LoadClip(bool isHider, bool isTimewarp)
    {
        Debug.Log("Loading tutorial clip...");

        if (isHider)
        {
            videoPlayer.clip = tutorial2;
            Debug.Log("Hider tutorial loaded");
            videoPlayer.Stop();
            videoPlayer.Play();
        }
        else if (!isTimewarp)
        {
            videoPlayer.clip = tutorial1;
            Debug.Log("Seeker tutorial loaded");
            videoPlayer.Stop();
            videoPlayer.Play();
        }
        else
        {
            videoPlayer.clip = nullclip;
            Debug.Log("No tutorial loaded");
            videoPlayer.Stop();
            videoPlayer.Play();
        }
    }
}
