using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource typingSource;
    [SerializeField] AudioSource walkingSource;
    [SerializeField] AudioSource oneTimeSource;
    public AudioClip confetti;
    public AudioClip grab;
    public AudioClip reward;
    public AudioClip UIClick;

    private void Start() {
        musicSource.Play();
    }

    public void PlaySFXOnce(AudioClip clip) {
        oneTimeSource.PlayOneShot(clip);
    }

    public void StartTypingSound() {
        if (!typingSource.isPlaying) 
            typingSource.Play();
    }

    public void StopTypingSound() {
        typingSource.Stop();
    }

    public void StartWalkingSound() {
        if (!walkingSource.isPlaying)
            walkingSource.Play();
    }

    public void StopWalkingSound() {
        walkingSource.Stop();
    }
}
