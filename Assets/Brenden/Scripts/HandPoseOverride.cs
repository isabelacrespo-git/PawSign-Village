using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Single-prefab approach: swaps control between XRHandSkeletonDriver and Animator
/// on the SAME hand mesh. No duplicate models needed.
/// </summary>
public class HandPoseOverride : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    [Tooltip("The Animator on this hand prefab. Must have an AnimatorController with your pose Bool parameters.")]
    [SerializeField] private Animator handAnimator;

    [Tooltip("The live tracking script (XRHandSkeletonDriver). Auto-found on Start if empty.")]
    [SerializeField] private Behaviour liveTrackingScript;

    private string activePoseParameter = "";
    private bool poseActive;

     // Static registry so DetectGesture can find the runtime clone
    public static HandPoseOverride ActiveRightHand { get; private set; }

    private void OnEnable()
    {
        // When the XR system spawns the clone and enables it, register here
        ActiveRightHand = this;
        Debug.Log($"[HandPoseOverride] Registered on: {gameObject.name}");
    }

    

    private void Start()
    {
        // Auto-find components on this hand prefab if not assigned
        if (handAnimator == null)
            handAnimator = GetComponentInChildren<Animator>();

        if (liveTrackingScript == null)
            liveTrackingScript = GetComponentInChildren<XRHandSkeletonDriver>();

        // Validate
        if (handAnimator == null)
            Debug.LogError("[HandPoseOverride] No Animator found on this hand!", this);
        else if (handAnimator.runtimeAnimatorController == null)
            Debug.LogError("[HandPoseOverride] Animator has NO AnimatorController! Drag your controller asset onto the Animator component.", handAnimator);

        if (liveTrackingScript == null)
            Debug.LogWarning("[HandPoseOverride] No XRHandSkeletonDriver found on this hand.", this);

        // Start in live tracking mode: driver ON, animator OFF
        SetMode(live: true);
    }

    /// <summary>
    /// Called by DetectGesture when a pose is recognized.
    /// </summary>
    public void ActivatePose(string poseName)
    {
        if (string.IsNullOrWhiteSpace(poseName))
            return;

        if (handAnimator == null || handAnimator.runtimeAnimatorController == null)
        {
            Debug.LogError("[HandPoseOverride] Cannot activate — Animator or Controller missing!");
            return;
        }

        // Already showing this exact pose, nothing to do
        if (poseActive && activePoseParameter == poseName)
            return;

        // If switching from a different pose, clear it first
        if (poseActive && !string.IsNullOrEmpty(activePoseParameter))
            handAnimator.SetBool(activePoseParameter, false);

        // PAUSE live tracking, ENABLE animator
        SetMode(live: false);

        // Snap to requested pose
        handAnimator.SetBool(poseName, true);
        activePoseParameter = poseName;
        poseActive = true;

        Debug.Log($"[HandPoseOverride] Pose ON: {poseName}");
    }

    /// <summary>
    /// Called by DetectGesture when score drops below release threshold.
    /// </summary>
    public void DeactivatePose()
    {
        if (!poseActive)
            return;

        // Clear the animator pose
        if (handAnimator != null && !string.IsNullOrEmpty(activePoseParameter))
        {
            handAnimator.SetBool(activePoseParameter, false);
            Debug.Log($"[HandPoseOverride] Pose OFF: {activePoseParameter}");
        }

        activePoseParameter = "";
        poseActive = false;

        // DISABLE animator, RESUME live tracking
        SetMode(live: true);
    }

    /// <summary>
    /// Swaps control between live tracking and Animator.
    /// live=true  → skeleton driver ON,  animator OFF  (fingers follow real hand)
    /// live=false → skeleton driver OFF, animator ON   (fingers follow animation clip)
    /// </summary>
       private void SetMode(bool live)
    {
        if (liveTrackingScript != null)
            liveTrackingScript.enabled = live;

        if (handAnimator != null)
        {
            handAnimator.enabled = !live;

            // Force immediate initialization so SetBool works on the same frame
            if (!live)
                handAnimator.Rebind();
        }
    }

        private void OnDisable()
    {
        if (liveTrackingScript != null)
            liveTrackingScript.enabled = true;
        if (handAnimator != null)
            handAnimator.enabled = false;

        // Unregister
        if (ActiveRightHand == this)
            ActiveRightHand = null;
    }
}