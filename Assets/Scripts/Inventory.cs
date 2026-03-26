using UnityEngine;
using UnityEngine.InputSystem;

public class Inventory : MonoBehaviour
{
    public Transform camera;
    private float distance = 20f;
    public InputActionReference primaryButton;
    public GameObject inventoryUI;

    // Subscribe to when primary button is pressed
    private void OnEnable()
    {
        primaryButton.action.performed += OnButtonPressed;
        primaryButton.action.Enable();
    }

    // Unsubscribe to when primary button is pressed
    private void OnDisable()
    {
        primaryButton.action.performed -= OnButtonPressed;
        primaryButton.action.Disable();
    }

    // When primary button is pressed
    private void OnButtonPressed(InputAction.CallbackContext context)
    {
        if (inventoryUI.activeInHierarchy) {
            inventoryUI.SetActive(false);
        } else {
            inventoryUI.SetActive(true);
        }
    }

    // Update is called once per frame
    void Update()
    {
        inventoryUI.transform.position = camera.position + camera.forward * distance;
        inventoryUI.transform.LookAt(camera.position);
        inventoryUI.transform.Rotate(0, 180, 0);
    }
}
