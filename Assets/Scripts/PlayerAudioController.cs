using UnityEngine;

public class PlayerAudioController : MonoBehaviour
{
    public AudioManagerMain audioManager;
    private Vector3 lastPosition;
    private bool wasMoving = false;

    void Start() {
        lastPosition = transform.position;
    }

    void Update() {
        if (audioManager == null) {
            return;
        }

        Vector3 horizontalDisplacement = new Vector3(transform.position.x - lastPosition.x, 0f, transform.position.z - lastPosition.z);
        float horizontalSpeed = horizontalDisplacement.magnitude / Time.deltaTime;
        bool isMoving = horizontalSpeed > 0.5f;
        if (isMoving && !wasMoving) {
            audioManager.StartWalkingSound();
        } else if (!isMoving && wasMoving) {
            audioManager.StopWalkingSound();
        }
        wasMoving = isMoving;
        lastPosition = transform.position;
    }
}
