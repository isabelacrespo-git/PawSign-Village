using UnityEngine;
using UnityEngine.Video;
using System.Collections.Generic;
using System.Linq;



//Manages playback tutorial videos for different signs in the game
public class SignVideoManager : MonoBehaviour
{
    
    [System.Serializable]
    public class SignVideoMapping
    {
        public string signName;
        public VideoClip clip;
    }

    //panel that shows video player UI
    [Header("UI References")]
    public GameObject videoPanel;

    [Header("Video Settings")]
    public VideoPlayer videoPlayer;
    //adding the videos to the inspector as a list so the user is able to add all video clips to the object
    public List<SignVideoMapping> signVideos;

    public delegate void VideoFinishedHandler();
    public event VideoFinishedHandler OnVideoPlaybackComplete;

    //hides npc panel when video is playing
    private GameObject currentActiveNpcPanel;

    private void Start()
    {
     
        if (videoPanel != null)
            videoPanel.SetActive(false);

        if (videoPlayer != null)
            videoPlayer.errorReceived += OnVideoError;
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.errorReceived -= OnVideoError;
    }

    //show tutorial video for specific sign
    public void ShowTutorial(string signName, GameObject activeNpcPanel)
    {
        Debug.Log("ShowTutorial Entered for sign: " + signName);
        var mapping = signVideos.FirstOrDefault(m => m.signName == signName);

        Debug.Log("ShowTutorial called for sign: " + signName);

        if (mapping == null || mapping.clip == null)
        {
            Debug.LogWarning($"SignVideoManager: No video clip found for this sign '{signName}'");
            return;
        }

        currentActiveNpcPanel = activeNpcPanel;

        //show video panel
        if (videoPanel != null)
            videoPanel.SetActive(true);

        videoPlayer.Stop();
        videoPlayer.clip = mapping.clip;
        videoPlayer.isLooping = true;

        //ensuring event calls are removed to avoid duplicate calls
        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.Prepare();
    }

    private void OnPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPrepared;
        vp.Play();
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log("Video finished.");
        vp.loopPointReached -= OnVideoFinished;
        HideTutorial();
        OnVideoPlaybackComplete?.Invoke();
    }

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError("VideoPlayer error: " + message);
    }

    //hides video panel and restores the npc panel
    public void HideTutorial()
    {
        if (videoPlayer.isPlaying)
            videoPlayer.Stop();

        if (videoPanel != null)
            videoPanel.SetActive(false);

        currentActiveNpcPanel = null;
        }
    }