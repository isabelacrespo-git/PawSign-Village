using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands.Gestures;

public class DynamicGestureRecognizer : MonoBehaviour
{
    [Serializable]
    public class DynamicSignEntry
    {
        [Tooltip("Logical sign name that is raised when the sequence is matched.")]
        public string signName;

        [Tooltip("Preferred: ordered XRHandShape checkpoints from DetectGesture entries.")]
        public XRHandShape[] requiredHandShapes;

        [Tooltip("Optional fallback: ordered pose names from DetectGesture (e.g. Sign_I, Sign_J_End).")]
        public string[] requiredPoses;

        [Tooltip("Maximum time in seconds allowed between sequence steps.")]
        public float maxGapBetweenSteps = 0.5f;

        [Tooltip("Minimum time in seconds between accepted sequence steps.")]
        public float minGapBetweenSteps = 0.06f;

        [Tooltip("Minimum total time from first checkpoint to final checkpoint.")]
        public float minSequenceDuration = 0.45f;

        [Tooltip("Maximum total time from first checkpoint to final checkpoint. Set to 0 to disable.")]
        public float maxSequenceDuration = 2.0f;

        [Tooltip("After recognition, requires leaving step 1 and re-entering it before accepting a new repetition.")]
        public bool requireRearmBetweenRepeats = true;

        [Tooltip("Minimum time after recognition before a new repetition can start.")]
        public float rearmDelay = 0.12f;

        [Tooltip("Cooldown in seconds after this sign is recognized.")]
        public float cooldown = 1f;
    }

    private class SequenceState
    {
        public int nextIndex;
        public float lastStepTime;
        public float sequenceStartTime;
        public float lastRecognizedTime = float.NegativeInfinity;
        public bool awaitingRearm;
        public bool leftStartSinceRecognition;
        public float rearmReadyTime;
    }

    public event Action<string> DynamicSignRecognized;

    [Header("Input")]
    [SerializeField] private DetectGesture detectGesture;

    [Header("Sequence Config")]
    [SerializeField] private DynamicSignEntry[] dynamicSigns;
    [SerializeField] private float minStaticPoseConfidence = 0.65f;
    [SerializeField] private float minTopScoreMargin = 0.05f;
    [SerializeField] private float minPoseHoldTime = 0.08f;
    [SerializeField] private bool debugSequenceProgress;

    private readonly Dictionary<DynamicSignEntry, SequenceState> sequenceStates = new Dictionary<DynamicSignEntry, SequenceState>();

    private XRHandShape currentShapeCandidate;
    private string currentPoseCandidate = "";
    private float currentPoseStartTime;
    private XRHandShape lastCommittedShape;
    private string lastCommittedPose = "";

    private void OnEnable()
    {
        if (detectGesture == null)
            detectGesture = GetComponent<DetectGesture>();

        sequenceStates.Clear();
        if (dynamicSigns != null)
        {
            foreach (var sign in dynamicSigns)
            {
                if (sign == null)
                    continue;
                sequenceStates[sign] = new SequenceState();
            }
        }

        if (detectGesture != null)
            detectGesture.StaticGestureFrameEvaluated += OnStaticGestureFrameEvaluated;
    }

    private void OnDisable()
    {
        if (detectGesture != null)
            detectGesture.StaticGestureFrameEvaluated -= OnStaticGestureFrameEvaluated;
    }

    private void OnStaticGestureFrameEvaluated(
        XRHandShape topGestureShape,
        string topGestureName,
        float topGestureScore,
        float topScoreMargin,
        bool isTracked)
    {
        if (!isTracked || topGestureScore < minStaticPoseConfidence || topScoreMargin < minTopScoreMargin)
        {
            currentShapeCandidate = null;
            currentPoseCandidate = "";
            return;
        }

        bool changedShape = currentShapeCandidate != topGestureShape;
        bool changedPose = currentPoseCandidate != topGestureName;
        if (changedShape || changedPose)
        {
            currentShapeCandidate = topGestureShape;
            currentPoseCandidate = topGestureName;
            currentPoseStartTime = Time.time;
            return;
        }

        if (Time.time - currentPoseStartTime < minPoseHoldTime)
            return;

        if (lastCommittedShape == currentShapeCandidate && lastCommittedPose == currentPoseCandidate)
            return;

        lastCommittedShape = currentShapeCandidate;
        lastCommittedPose = currentPoseCandidate;
        AdvanceAllSequences(lastCommittedShape, lastCommittedPose, Time.time);
    }

    private void AdvanceAllSequences(XRHandShape committedShape, string committedPose, float now)
    {
        foreach (var sign in dynamicSigns)
        {
            if (sign == null)
                continue;

            if (!sequenceStates.TryGetValue(sign, out SequenceState state))
                continue;

            if (now - state.lastRecognizedTime < sign.cooldown)
                continue;

            bool matchesStart = SequenceCheckpointMatches(sign, 0, committedShape, committedPose);

            if (sign.requireRearmBetweenRepeats && state.awaitingRearm)
            {
                if (now < state.rearmReadyTime)
                    continue;

                if (!matchesStart)
                {
                    state.leftStartSinceRecognition = true;
                    continue;
                }

                if (!state.leftStartSinceRecognition)
                    continue;

                state.awaitingRearm = false;
                state.leftStartSinceRecognition = false;

                if (debugSequenceProgress)
                    Debug.Log($"[DynamicGestureRecognizer] {sign.signName}: re-armed for next repetition.");
            }

            if (state.nextIndex > 0 && now - state.lastStepTime > sign.maxGapBetweenSteps)
            {
                if (debugSequenceProgress)
                    Debug.Log($"[DynamicGestureRecognizer] {sign.signName}: timed out between steps, resetting sequence.");
                state.nextIndex = 0;
            }

            if (SequenceCheckpointMatches(sign, state.nextIndex, committedShape, committedPose))
            {
                int sequenceLength = GetSequenceLength(sign);
                if (sequenceLength <= 0)
                    continue;

                if (state.nextIndex > 0 && now - state.lastStepTime < sign.minGapBetweenSteps)
                    continue;

                if (state.nextIndex == 0)
                    state.sequenceStartTime = now;

                state.nextIndex++;
                state.lastStepTime = now;

                if (debugSequenceProgress)
                    Debug.Log($"[DynamicGestureRecognizer] {sign.signName}: matched step {state.nextIndex}/{sequenceLength}");

                if (state.nextIndex >= sequenceLength)
                {
                    float sequenceDuration = now - state.sequenceStartTime;
                    bool aboveMinDuration = sequenceDuration >= sign.minSequenceDuration;
                    bool belowMaxDuration = sign.maxSequenceDuration <= 0f || sequenceDuration <= sign.maxSequenceDuration;

                    if (aboveMinDuration && belowMaxDuration)
                    {
                        Debug.Log($"[DynamicGestureRecognizer] Recognized dynamic sign: {sign.signName}");
                        DynamicSignRecognized?.Invoke(sign.signName);
                        state.lastRecognizedTime = now;
                        state.awaitingRearm = sign.requireRearmBetweenRepeats;
                        state.leftStartSinceRecognition = false;
                        state.rearmReadyTime = now + Mathf.Max(0f, sign.rearmDelay);
                    }
                    else if (debugSequenceProgress)
                    {
                        Debug.Log($"[DynamicGestureRecognizer] {sign.signName}: invalid sequence duration ({sequenceDuration:F2}s), ignored.");
                    }

                    state.nextIndex = 0;
                }

                continue;
            }

            // If already in-progress, keep progress and wait for the expected next checkpoint
            // until timeout instead of resetting on noisy/misclassified intermediate frames.
            if (state.nextIndex > 0)
                continue;

            state.nextIndex = SequenceCheckpointMatches(sign, 0, committedShape, committedPose) ? 1 : 0;
            if (state.nextIndex == 1)
            {
                state.sequenceStartTime = now;
                state.lastStepTime = now;
                if (debugSequenceProgress)
                    Debug.Log($"[DynamicGestureRecognizer] {sign.signName}: restarted at step 1/{GetSequenceLength(sign)}");
            }
        }
    }

    private int GetSequenceLength(DynamicSignEntry sign)
    {
        if (sign.requiredHandShapes != null && sign.requiredHandShapes.Length > 0)
            return sign.requiredHandShapes.Length;
        if (sign.requiredPoses != null)
            return sign.requiredPoses.Length;
        return 0;
    }

    private bool SequenceCheckpointMatches(DynamicSignEntry sign, int index, XRHandShape shape, string pose)
    {
        if (sign.requiredHandShapes != null && sign.requiredHandShapes.Length > 0)
        {
            if (index >= sign.requiredHandShapes.Length)
                return false;
            return sign.requiredHandShapes[index] == shape;
        }

        if (sign.requiredPoses != null && sign.requiredPoses.Length > 0)
        {
            if (index >= sign.requiredPoses.Length)
                return false;
            return sign.requiredPoses[index] == pose;
        }

        return false;
    }
}
