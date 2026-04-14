using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayAudioFromAudioManager : MonoBehaviour
{
    public string target;

    public void Play()
    {
        AudioManagerStartMenu.instance.Play(target);
    }

    public void Play(string audioName)
    {
        AudioManagerStartMenu.instance.Play(audioName);
    }
}
