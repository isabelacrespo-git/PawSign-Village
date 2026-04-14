using UnityEngine;

public class AudioManagerMain : MonoBehaviour
{
    public static AudioManagerMain Instance { get; private set; }

    [SerializeField] AudioSource typingSource;
    [SerializeField] AudioSource walkingSource;
    [SerializeField] AudioSource oneTimeSource;
    public AudioClip confetti;
    public AudioClip reward;
    public AudioClip UIClick;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureAudioSources();
    }

    public static AudioManagerMain GetOrCreate() {
        AudioManagerMain manager = FindFirstObjectByType<AudioManagerMain>();
        if (manager != null) {
            return manager;
        }

        GameObject go = new GameObject("Audio Manager");
        manager = go.AddComponent<AudioManagerMain>();
        return manager;
    }

    public void PlaySFXOnce(AudioClip clip) {
        if (oneTimeSource == null || clip == null) {
            return;
        }

        oneTimeSource.PlayOneShot(clip);
    }

    public void StartTypingSound() {
        if (typingSource != null && !typingSource.isPlaying) 
            typingSource.Play();
    }

    public void StopTypingSound() {
        if (typingSource != null) {
            typingSource.Stop();
        }
    }

    public void StartWalkingSound() {
        if (walkingSource != null && !walkingSource.isPlaying)
            walkingSource.Play();
    }

    public void StopWalkingSound() {
        if (walkingSource != null) {
            walkingSource.Stop();
        }
    }

    private void EnsureAudioSources() {
        if (typingSource == null) {
            typingSource = CreateChildAudioSource("Typing SFX", true, false, 0.5f);
        }

        if (walkingSource == null) {
            walkingSource = CreateChildAudioSource("Walking SFX", true, false, 0.2f);
        }

        if (oneTimeSource == null) {
            oneTimeSource = CreateChildAudioSource("One Time SFX", false, false, 0.5f);
        }
    }

    private AudioSource CreateChildAudioSource(string childName, bool loop, bool playOnAwake, float volume) {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);

        AudioSource source = child.AddComponent<AudioSource>();
        source.loop = loop;
        source.playOnAwake = playOnAwake;
        source.volume = volume;
        return source;
    }
}
