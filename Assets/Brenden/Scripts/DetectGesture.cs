using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Hands.Samples.GestureSample;

public class DetectGesture : MonoBehaviour
{
    public event Action<XRHandShape, string, float, float, bool> StaticGestureFrameEvaluated;
    public event Action<string, float> StaticGestureActivated;
    public event Action<string> StaticGestureReleased;

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
    [SerializeField] private int smoothingFrames = 8;
    [SerializeField] private HandShapeCompletenessCalculator completenessCalculator;

    [Header("Pose Override")]
    private HandPoseOverride handPoseOverride => HandPoseOverride.ActiveRightHand;
    [SerializeField] private ExpectedSignMatcher expectedSignMatcher;
    [SerializeField] private bool onlyOverrideDuringExpectedSign = true;

    [Header("Expected Sign Scoring")]
    [Tooltip("When waiting for an expected sign, only score gestures that map to that expected sign.")]
    [SerializeField] private bool scoreOnlyExpectedWhenWaiting = true;
    [Tooltip("If expected-only scoring is enabled but no matching gesture entry exists, fallback to scoring all gestures.")]
    [SerializeField] private bool fallbackToAllScoringIfExpectedMissing = true;

    [Header("Gestures")]
    [SerializeField] private GestureEntry[] gestures;

    private Dictionary<string, GestureSmoother> smoothers = new Dictionary<string, GestureSmoother>();
    private Dictionary<string, GestureEntry> entryLookup = new Dictionary<string, GestureEntry>();
    private float timeOfLastConditionCheck;
    private string activeGesture = "";
    private float activeGestureScore;

    public string CurrentActiveGestureKey => activeGesture;
    public string CurrentActiveGestureSign => NormalizeSignName(activeGesture);
    public float CurrentActiveGestureScore => activeGestureScore;

    private static bool HasAnimatorPose(GestureEntry entry)
    {
        return entry != null && !string.IsNullOrWhiteSpace(entry.animatorPoseName);
    }

    private static string BuildGestureKey(GestureEntry entry, int index)
    {
        if (entry == null)
            return $"Gesture_{index}";

        if (!string.IsNullOrWhiteSpace(entry.animatorPoseName))
            return entry.animatorPoseName;

        if (entry.handShape != null && !string.IsNullOrWhiteSpace(entry.handShape.name))
            return $"{entry.handShape.name}_{index}";

        return $"Gesture_{index}";
    }

    private bool ShouldApplyPoseOverride()
    {
        if (!onlyOverrideDuringExpectedSign)
            return true;

        return expectedSignMatcher != null && expectedSignMatcher.IsWaitingForSign;
    }

    private bool ShouldRestrictToExpectedSign()
    {
        return onlyOverrideDuringExpectedSign
            && expectedSignMatcher != null
            && expectedSignMatcher.IsWaitingForSign
            && !string.IsNullOrEmpty(expectedSignMatcher.CurrentExpectedSign);
    }

    private bool ShouldScoreOnlyExpectedSign()
    {
        return scoreOnlyExpectedWhenWaiting
            && expectedSignMatcher != null
            && expectedSignMatcher.IsWaitingForSign
            && !string.IsNullOrEmpty(expectedSignMatcher.CurrentExpectedSign);
    }

    private bool IsExpectedGestureKey(string gestureKey)
    {
        if (expectedSignMatcher == null)
            return false;

        string expected = expectedSignMatcher.CurrentExpectedSign;
        if (string.IsNullOrEmpty(expected))
            return false;

        return NormalizeSignName(gestureKey) == expected;
    }

    private static string NormalizeSignName(string signName)
    {
        if (string.IsNullOrEmpty(signName))
            return "";

        string normalized = signName.Trim().ToUpperInvariant();

        int trailingUnderscore = normalized.LastIndexOf('_');
        if (trailingUnderscore >= 0 && trailingUnderscore < normalized.Length - 1)
        {
            bool suffixIsDigits = true;
            for (int i = trailingUnderscore + 1; i < normalized.Length; i++)
            {
                if (!char.IsDigit(normalized[i]))
                {
                    suffixIsDigits = false;
                    break;
                }
            }

            if (suffixIsDigits)
                normalized = normalized.Substring(0, trailingUnderscore);
        }

        if (normalized.StartsWith("SIGN_"))
            normalized = normalized.Substring(5);

        if (normalized.EndsWith("_POSE"))
            normalized = normalized.Substring(0, normalized.Length - 5);
        else if (normalized.EndsWith("POSE") && normalized.Length > 4)
            normalized = normalized.Substring(0, normalized.Length - 4);

        if (normalized.EndsWith("_RIGHT"))
            normalized = normalized.Substring(0, normalized.Length - 6);
        else if (normalized.EndsWith("_LEFT"))
            normalized = normalized.Substring(0, normalized.Length - 5);

        int sideSeparator = normalized.IndexOf(" - ");
        if (sideSeparator > 0)
            normalized = normalized.Substring(0, sideSeparator);

        return normalized.Trim();
    }

    void OnEnable()
    {
        if (handTrackingEvents != null)
            handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);

        if (expectedSignMatcher == null)
            expectedSignMatcher = FindFirstObjectByType<ExpectedSignMatcher>();

        // Create one smoother per gesture (larger window = more stable)
        smoothers.Clear();
        entryLookup.Clear();
        if (gestures != null)
        {
            for (int i = 0; i < gestures.Length; i++)
            {
                var g = gestures[i];
                var key = BuildGestureKey(g, i);
                smoothers[key] = new GestureSmoother(Mathf.Max(1, smoothingFrames));
                entryLookup[key] = g;
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

        // Score every gesture, or only the expected sign when configured.
        Dictionary<string, float> scores = new Dictionary<string, float>();
        bool scoreOnlyExpected = ShouldScoreOnlyExpectedSign();
        bool sawExpectedCandidate = false;

        for (int i = 0; i < gestures.Length; i++)
        {
            var entry = gestures[i];
            var key = BuildGestureKey(entry, i);

            if (scoreOnlyExpected)
            {
                bool isExpected = IsExpectedGestureKey(key);
                if (!isExpected)
                    continue;

                sawExpectedCandidate = true;
            }

            bool ok = completenessCalculator.TryCalculateHandShapeCompletenessScore(
                eventArgs.hand,
                entry.handShape,
                out float rawScore);

            if (!ok) continue;

            float smoothed = smoothers[key].GetSmoothedScore(rawScore);
            scores[key] = smoothed;

            string debugName = !string.IsNullOrWhiteSpace(entry.animatorPoseName)
                ? entry.animatorPoseName
                : (entry.handShape != null ? entry.handShape.name : key);

            Debug.Log($"[DetectGesture] {debugName}: raw={rawScore:F3}, smoothed={smoothed:F3}");
        }

        if (scoreOnlyExpected && !sawExpectedCandidate && fallbackToAllScoringIfExpectedMissing)
        {
            Debug.LogWarning("[DetectGesture] Expected-only scoring enabled, but no gesture entry matched the expected sign. Falling back to full scoring for this frame.");

            for (int i = 0; i < gestures.Length; i++)
            {
                var entry = gestures[i];
                var key = BuildGestureKey(entry, i);

                bool ok = completenessCalculator.TryCalculateHandShapeCompletenessScore(
                    eventArgs.hand,
                    entry.handShape,
                    out float rawScore);

                if (!ok) continue;

                float smoothed = smoothers[key].GetSmoothedScore(rawScore);
                scores[key] = smoothed;

                string debugName = !string.IsNullOrWhiteSpace(entry.animatorPoseName)
                    ? entry.animatorPoseName
                    : (entry.handShape != null ? entry.handShape.name : key);

                Debug.Log($"[DetectGesture] {debugName}: raw={rawScore:F3}, smoothed={smoothed:F3}");
            }
        }

        bool isTracked = handTrackingEvents.handIsTracked;

        string topGestureName = "";
        XRHandShape topGestureShape = null;
        float topGestureScore = 0f;
        float secondBestScore = 0f;
        foreach (var kvp in scores)
        {
            if (kvp.Value > topGestureScore)
            {
                secondBestScore = topGestureScore;
                topGestureName = kvp.Key;
                topGestureShape = entryLookup[kvp.Key].handShape;
                topGestureScore = kvp.Value;
            }
            else if (kvp.Value > secondBestScore)
            {
                secondBestScore = kvp.Value;
            }
        }
        float topScoreMargin = topGestureScore - secondBestScore;
        StaticGestureFrameEvaluated?.Invoke(topGestureShape, topGestureName, topGestureScore, topScoreMargin, isTracked);

        if (!ShouldApplyPoseOverride())
        {
            if (!string.IsNullOrEmpty(activeGesture))
            {
                handPoseOverride?.DeactivatePose();
                StaticGestureReleased?.Invoke(activeGesture);
                activeGesture = "";
                activeGestureScore = 0f;
            }

            timeOfLastConditionCheck = Time.time;
            return;
        }

        // --- Hysteresis logic ---
        // If a gesture is already active, keep it until its score drops below releaseThreshold
        if (!string.IsNullOrEmpty(activeGesture) && isTracked)
        {
            if (ShouldRestrictToExpectedSign() && !IsExpectedGestureKey(activeGesture))
            {
                handPoseOverride?.DeactivatePose();
                StaticGestureReleased?.Invoke(activeGesture);
                activeGesture = "";
                activeGestureScore = 0f;
            }

            if (scores.TryGetValue(activeGesture, out float currentScore))
            {
                activeGestureScore = currentScore;
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
            StaticGestureReleased?.Invoke(activeGesture);
            activeGesture = "";
            activeGestureScore = 0f;
        }

        // No gesture active — find the best new candidate above its activateThreshold
        string bestGesture = "";
        float bestScore = 0f;

        foreach (var kvp in scores)
        {
            GestureEntry entry = entryLookup[kvp.Key];
            if (!HasAnimatorPose(entry))
                continue;

            if (ShouldRestrictToExpectedSign() && !IsExpectedGestureKey(kvp.Key))
                continue;

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
            activeGestureScore = bestScore;
            StaticGestureActivated?.Invoke(bestGesture, bestScore);
        }

        if (!isTracked)
        {
            if (!string.IsNullOrEmpty(activeGesture))
            {
                handPoseOverride?.DeactivatePose();
                StaticGestureReleased?.Invoke(activeGesture);
                activeGesture = "";
                activeGestureScore = 0f;
            }
            foreach (var s in smoothers.Values)
                s.ResetBuffer();
        }

        timeOfLastConditionCheck = Time.time;
    }
}