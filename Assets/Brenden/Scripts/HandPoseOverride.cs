using UnityEngine;

public class HandPoseOverride : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private Animator handAnimator; 
    [SerializeField] private Behaviour liveTrackingScript; 

    private string activePoseParameter = ""; 
    private bool poseActive;

    private void Reset()
    {
        handAnimator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// Activation signal from gesture detector.
    /// Example: ActivatePose("Sign_R"), ActivatePose("Sign_M")
    /// </summary>
    public void ActivatePose(string poseName)
    {
        if (string.IsNullOrWhiteSpace(poseName) || handAnimator == null || liveTrackingScript == null)
            return;

        if (poseActive && activePoseParameter == poseName)
            return;

        // 1. Turn OFF the live tracking
        liveTrackingScript.enabled = false;

        // 2. Turn ON the Animator so it can take control
        handAnimator.enabled = true; 

        if (poseActive && !string.IsNullOrEmpty(activePoseParameter))
            handAnimator.SetBool(activePoseParameter, false);

        handAnimator.SetBool(poseName, true);

        activePoseParameter = poseName;
        poseActive = true;
    }

    public void DeactivatePose()
    {
        if (handAnimator == null || liveTrackingScript == null)
            return;

        if (!poseActive)
            return;

        if (!string.IsNullOrEmpty(activePoseParameter))
            handAnimator.SetBool(activePoseParameter, false);

        activePoseParameter = "";
        poseActive = false;

        // 1. Turn OFF the Animator so it releases the bones
        handAnimator.enabled = false; 

        // 2. Turn ON the live tracking
        liveTrackingScript.enabled = true;
    }
    /// <summary>
    /// Convenience API if detector emits true/false for a specific pose.
    /// </summary>
    public void TogglePose(string poseName, bool isActive)
    {
        if (isActive) ActivatePose(poseName);
        else DeactivatePose();
    }

    private void OnDisable()
    {
        // Safety: never leave tracking disabled when this component turns off
        if (liveTrackingScript != null)
            liveTrackingScript.enabled = true;
    }

    private void Start()
    {
        // When the Hand Visualizer clones this hand into the game, 
        // automatically search this clone for the live tracking script!
        if (liveTrackingScript == null)
        {
            // This searches the current hand clone (and any of its children) for the XR Skeleton Driver
            liveTrackingScript = GetComponentInChildren<UnityEngine.XR.Hands.XRHandSkeletonDriver>();
            
            if (liveTrackingScript == null)
            {
                Debug.LogWarning("HandPoseOverride: Could not find the XRHandSkeletonDriver on this hand clone!");
            }
        }
    }
}
