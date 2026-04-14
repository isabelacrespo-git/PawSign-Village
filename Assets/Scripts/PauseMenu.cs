using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public Transform camera;
    private float distance = 15f;
    public InputActionReference secondaryButton;
    public GameObject pauseMenuUI;
    public GameObject inventoryUI;
    public AudioManagerStartMenu audioManager;
    public GameObject moveProvider;
    public GameObject turnProvider;

    // Subscribe to when secondary button is pressed
    private void OnEnable()
    {
        secondaryButton.action.performed += OnButtonPressed;
        secondaryButton.action.Enable();
    }

    // Unsubscribe to when secondary button is pressed
    private void OnDisable()
    {
        secondaryButton.action.performed -= OnButtonPressed;
        secondaryButton.action.Disable();
    }

    // When secondary button is pressed
    private void OnButtonPressed(InputAction.CallbackContext context)
    {
        if (pauseMenuUI.activeInHierarchy) {
            pauseMenuUI.SetActive(false);
            Time.timeScale = 1f;
            audioManager.ResumeMusic();
            moveProvider.SetActive(true);
            turnProvider.SetActive(true);
        } else if (!inventoryUI.activeInHierarchy) {
            pauseMenuUI.SetActive(true);
            Time.timeScale = 0f;
            audioManager.PauseMusic();
            moveProvider.SetActive(false);
            turnProvider.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        pauseMenuUI.transform.position = camera.position + camera.forward * distance;
        pauseMenuUI.transform.LookAt(camera.position);
        pauseMenuUI.transform.Rotate(0, 180, 0);
    }
}
