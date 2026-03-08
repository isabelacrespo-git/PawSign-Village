using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Hands.Samples.GestureSample;

public class DetectGesture : MonoBehaviour
{
    [Serializable]
    public class GestureEntry
    {
        public XRHandShape handShape;
        public string animatorPoseName;   // e.g. "Sign_R", "Sign_A", "Sign_M"
        public float activateThreshold = 0.65f;
        public float releaseThreshold = 0.50f;
    }

    [SerializeField] private XRHandTrackingEvents handTrackingEvents;
    [SerializeField] private float gestureDetectionInterval = 0.1f;
    [SerializeField] private HandShapeCompletenessCalculator completenessCalculator;

    [Header("Pose Override")]
    private HandPoseOverride handPoseOverride => HandPoseOverride.ActiveRightHand;

    [Header("Gestures")]
    [SerializeField] private GestureEntry[] gestures;

    private Dictionary<string, GestureSmoother> smoothers = new Dictionary<string, GestureSmoother>();
    private Dictionary<string, GestureEntry> entryLookup = new Dictionary<string, GestureEntry>();
    private float timeOfLastConditionCheck;
    private string activeGesture = "";

    void OnEnable()
    {
        if (handTrackingEvents != null)
            handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);

        // Create one smoother per gesture (larger window = more stable)
        smoothers.Clear();
        entryLookup.Clear();
        if (gestures != null)
        {
            foreach (var g in gestures)
            {
                smoothers[g.animatorPoseName] = new GestureSmoother(8);
                entryLookup[g.animatorPoseName] = g;
            }
        }
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

        if (gestures == null || completenessCalculator == null)
            return;

        // Score every gesture
        Dictionary<string, float> scores = new Dictionary<string, float>();

        foreach (var entry in gestures)
        {
            bool ok = completenessCalculator.TryCalculateHandShapeCompletenessScore(
                eventArgs.hand,
                entry.handShape,
                out float rawScore);

            if (!ok) continue;

            float smoothed = smoothers[entry.animatorPoseName].GetSmoothedScore(rawScore);
            scores[entry.animatorPoseName] = smoothed;

            Debug.Log($"[DetectGesture] {entry.animatorPoseName}: raw={rawScore:F3}, smoothed={smoothed:F3}");
        }

        bool isTracked = handTrackingEvents.handIsTracked;

        // --- Hysteresis logic ---
        // If a gesture is already active, keep it until its score drops below releaseThreshold
        if (!string.IsNullOrEmpty(activeGesture) && isTracked)
        {
            if (scores.TryGetValue(activeGesture, out float currentScore))
            {
                float release = entryLookup[activeGesture].releaseThreshold;
                if (currentScore >= release)
                {
                    // Still holding — stay active, don't toggle
                    timeOfLastConditionCheck = Time.time;
                    return;
                }
            }
            // Score dropped below release threshold — deactivate
            Debug.Log($"[DetectGesture] Releasing {activeGesture}");
            handPoseOverride?.DeactivatePose();
            activeGesture = "";
        }

        // No gesture active — find the best new candidate above its activateThreshold
        string bestGesture = "";
        float bestScore = 0f;

        foreach (var kvp in scores)
        {
            GestureEntry entry = entryLookup[kvp.Key];
            if (kvp.Value >= entry.activateThreshold && kvp.Value > bestScore)
            {
                bestGesture = kvp.Key;
                bestScore = kvp.Value;
            }
        }

        if (isTracked && !string.IsNullOrEmpty(bestGesture))
        {
            Debug.Log($"[DetectGesture] >>> Activating {bestGesture} (score={bestScore:F3})");
            handPoseOverride?.ActivatePose(bestGesture);
            activeGesture = bestGesture;
        }

        if (!isTracked)
        {
            if (!string.IsNullOrEmpty(activeGesture))
            {
                handPoseOverride?.DeactivatePose();
                activeGesture = "";
            }
            foreach (var s in smoothers.Values)
                s.ResetBuffer();
        }

        timeOfLastConditionCheck = Time.time;
    }
}