using UnityEngine;
using UnityEngine.Video;

public class TutorialLoader : MonoBehaviour
{
    public VideoClip tutorial1;
    public VideoClip tutorial2;
    public VideoClip nullclip;
    public VideoPlayer videoPlayer;
    public RenderTexture renderTexture;

    public int LoadClip(bool isHider, bool isTimewarp)
    {
        videoPlayer.targetTexture = renderTexture;
        Debug.Log(videoPlayer.targetTexture);

        Debug.Log("Loading tutorial clip...");
        int result = 0;

        if (isHider)
        {
            videoPlayer.clip = tutorial2;
            Debug.Log("Hider tutorial loaded");
            result = 1;
        }
        else if (!isTimewarp)
        {
            videoPlayer.clip = tutorial1;
            Debug.Log("Seeker tutorial loaded");
            result = 2;
        }
        else
        {
            videoPlayer.clip = nullclip;
            Debug.Log("Timewarp tutorial loaded");
            result = 3;
        }

        videoPlayer.Stop();
        videoPlayer.SetDirectAudioMute(0, true);
        videoPlayer.Play();
        Debug.Log(videoPlayer.isPlaying);
        return result;
    }

    public RenderTexture GetRenderTexture()
    {
        return FindObjectOfType<RenderTexture>();
    }
}
