using System;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Hands.Samples.GestureSample;

public class DetectGesture : MonoBehaviour
{
    [SerializeField] private XRHandTrackingEvents handTrackingEvents;
    [SerializeField] private XRHandShape[] handShapes;
    [SerializeField] private float gestureDetectionInterval = 0.1f;
    [SerializeField] private HandShapeCompletenessCalculator completenessCalculator;

    [Header("Pose Override")]
    [SerializeField] private HandPoseOverride handPoseOverride;
    [SerializeField] private string targetHandShapeName = "R";
    [SerializeField] private string targetAnimatorPoseName = "Sign_R";
    [SerializeField] private float minimumThreshold = 0.9f;
    [SerializeField] private float releaseThreshold = 0.75f;

    // 15-frame smoothing window (~0.2s at 72 FPS)
    private GestureSmoother rPoseSmoother = new GestureSmoother(4);
    private float timeOfLastConditionCheck;

    void OnEnable()
    {
        if (handTrackingEvents != null)
            handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
    }

    void OnDisable()
    {
        if (handTrackingEvents != null)
            handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
    }

    void OnJointsUpdated(XRHandJointsUpdatedEventArgs eventArgs)
{
    if (Time.time - timeOfLastConditionCheck < gestureDetectionInterval)
        return;

    bool evaluatedTarget = false;

    foreach (var handShape in handShapes)
    {
        if (!string.Equals(handShape.name, targetHandShapeName, StringComparison.OrdinalIgnoreCase))
            continue;

        evaluatedTarget = true;

        bool ok = completenessCalculator.TryCalculateHandShapeCompletenessScore(
            eventArgs.hand,
            handShape,
            out float rawScore);

        if (!ok)
        {
            handPoseOverride?.DeactivatePose();
            break;
        }

        float smoothedScore = rPoseSmoother.GetSmoothedScore(rawScore);

        if (handTrackingEvents.handIsTracked && smoothedScore >= minimumThreshold)
            handPoseOverride?.ActivatePose(targetAnimatorPoseName);
        else if (!handTrackingEvents.handIsTracked || smoothedScore <= releaseThreshold)
            handPoseOverride?.DeactivatePose();

        break;
    }

    if (!handTrackingEvents.handIsTracked || !evaluatedTarget)
        rPoseSmoother.ResetBuffer();

    timeOfLastConditionCheck = Time.time;
}
}