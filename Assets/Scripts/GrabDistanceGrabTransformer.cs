using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class GrabDistanceGrabTransformer : XRBaseGrabTransformer
{
    [SerializeField]
    [Tooltip("2D axis action used to adjust held item distance. Vertical input (Y) is used.")]
    private InputActionProperty adjustDistanceAction;

    [SerializeField, Min(0.01f)]
    [Tooltip("How quickly the held item moves closer/farther per second.")]
    private float adjustSpeed = 0.35f;

    [SerializeField, Min(0f)]
    [Tooltip("Closest distance allowed between hand/interactor and held object.")]
    private float minDistance = 0.05f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Farthest distance allowed between hand/interactor and held object.")]
    private float maxDistance = 0.75f;

    [SerializeField, Min(0f)]
    [Tooltip("Ignore tiny thumbstick movement around center.")]
    private float deadZone = 0.2f;

    [SerializeField]
    [Tooltip("Initial distance when the item is first grabbed.")]
    private float defaultDistance = 0.2f;

    private float currentDistance;
    private bool initializedForCurrentGrab;

    protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

    public override void OnLink(XRGrabInteractable grabInteractable)
    {
        base.OnLink(grabInteractable);
        currentDistance = Mathf.Clamp(defaultDistance, minDistance, maxDistance);
    }

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
        var selectingInteractors = grabInteractable.interactorsSelecting;
        if (selectingInteractors == null || selectingInteractors.Count == 0)
        {
            initializedForCurrentGrab = false;
            return;
        }

        IXRSelectInteractor interactor = selectingInteractors[0];
        Transform interactorAttach = interactor.GetAttachTransform(grabInteractable);
        if (interactorAttach == null)
            return;

        if (!initializedForCurrentGrab)
        {
            float currentGrabDistance = Vector3.Distance(targetPose.position, interactorAttach.position);
            currentDistance = Mathf.Clamp(currentGrabDistance, minDistance, maxDistance);
            initializedForCurrentGrab = true;
        }

        float inputY = ReadInputY();
        if (Mathf.Abs(inputY) > deadZone)
        {
            currentDistance += inputY * adjustSpeed * Time.deltaTime;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }

        targetPose.position = interactorAttach.position + interactorAttach.forward * currentDistance;
    }

    private float ReadInputY()
    {
        var action = adjustDistanceAction.action;
        if (action == null)
            return 0f;

        Vector2 axis = action.ReadValue<Vector2>();
        return axis.y;
    }
}