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

    [Header("Gestures")]
    [SerializeField] private GestureEntry[] gestures;

    private Dictionary<string, GestureSmoother> smoothers = new Dictionary<string, GestureSmoother>();
    private Dictionary<string, GestureEntry> entryLookup = new Dictionary<string, GestureEntry>();
    private float timeOfLastConditionCheck;
    private string activeGesture = "";

    //cache all festure scores 
    private readonly Dictionary<string, float> lastScores = new Dictionary<string, float>();

    //looks through stored scores and finds which one best contributes to the current letter
    public bool TryGetBestScoreForSign(string signName, out float bestScore)
    {
        bestScore = 0f;

        //if nothing is passed or we have no stored gestures, we have nothing to search through
        if (string.IsNullOrEmpty(signName) || lastScores.Count == 0)
            return false;

        //normalize signs the sign the caller asked for
        string wanted = NormalizeSignKey(signName);
        //tracks if we found a valid entry
        bool found = false;

        //lastScores contains gesture keys to keep their latest scores
        foreach (var kvp in lastScores)
        {
            if (NormalizeSignKey(kvp.Key) == wanted)
            {   
                //if this is the first match or this match has a better score than the one we have saved
                //we keep this higher score
                if (!found || kvp.Value > bestScore)
                    bestScore = kvp.Value;

                found = true;
            }
        }

        //if we return true, we one at least one score for the sign if false, no entries matched
        return found;
    }

    private static string NormalizeSignKey(string signName)
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

        int sideSeparator = normalized.IndexOf(" - ");
        if (sideSeparator > 0)
            normalized = normalized.Substring(0, sideSeparator);

        return normalized.Trim();
    }
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

    void OnEnable()
    {
        if (handTrackingEvents != null)
            handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);

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

        // Score every gesture
        Dictionary<string, float> scores = new Dictionary<string, float>();

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

        lastScores.Clear();
        foreach (var kvp in scores)
        {
            lastScores[kvp.Key] = kvp.Value;
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
            StaticGestureReleased?.Invoke(activeGesture);
            activeGesture = "";
        }

        // No gesture active — find the best new candidate above its activateThreshold
        string bestGesture = "";
        float bestScore = 0f;

        foreach (var kvp in scores)
        {
            GestureEntry entry = entryLookup[kvp.Key];
            if (!HasAnimatorPose(entry))
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
            StaticGestureActivated?.Invoke(bestGesture, bestScore);
        }

        if (!isTracked)
        {
            if (!string.IsNullOrEmpty(activeGesture))
            {
                handPoseOverride?.DeactivatePose();
                StaticGestureReleased?.Invoke(activeGesture);
                activeGesture = "";
            }
            foreach (var s in smoothers.Values)
                s.ResetBuffer();
        }

        timeOfLastConditionCheck = Time.time;
    }
}