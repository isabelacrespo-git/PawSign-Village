using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

public class Slot : MonoBehaviour
{
    private Image slotImage;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket;
    Color originalColor;

    void Awake() {
        socket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        slotImage = GetComponentInChildren<Image>();
        originalColor = slotImage.color;
        // Listen for when object is snapped into the socket
        socket.selectEntered.AddListener(OnItemInserted);
        socket.selectExited.AddListener(OnItemRemoved);
    }

    // Called when object is snapped into slot
    private void OnItemInserted(SelectEnterEventArgs args) {
        GameObject obj = args.interactableObject.transform.gameObject;
        obj.GetComponent<Rigidbody>().isKinematic = true;
        obj.transform.SetParent(gameObject.transform, true);
        slotImage.color = Color.gray;
    }

    // Called when object leaves slot
    private void OnItemRemoved(SelectExitEventArgs args) {
        slotImage.color = originalColor;
    }
}
