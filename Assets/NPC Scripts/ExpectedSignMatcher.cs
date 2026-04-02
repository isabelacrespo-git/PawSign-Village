using System;
using UnityEngine;
using UnityEngine.XR.Hands.Gestures;

public class ExpectedSignMatcher : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private DetectGesture detectGesture;

    [Header("Correct Sign Validation")]
    [Tooltip("Minimum confidence score required to accept the expected sign.")]
    [SerializeField] private float minExpectedSignScore = 0.72f;

    [Tooltip("Expected sign must beat the second-best score by at least this margin.")]
    [SerializeField] private float minExpectedScoreMargin = 0.08f;

    [Tooltip("How long the expected sign must be held before it is accepted.")]
    [SerializeField] private float requiredHoldDuration = 0.20f;

    [Header("Wrong Sign Validation")]
    [Tooltip("If another sign is clearly recognized, require at least this score before naming it.")]
    [SerializeField] private float minWrongSignScore = 0.70f;

    [Tooltip("If another sign is clearly recognized, require at least this score margin before naming it.")]
    [SerializeField] private float minWrongScoreMargin = 0.08f;

    [Tooltip("Cooldown after wrong-sign feedback so the NPC does not repeat it immediately.")]
    [SerializeField] private float wrongSignCooldown = 1.0f;

    [Header("Out Of Range Failure Logic")]
    [Tooltip("How long the player gets before we start judging whether they are outside the expected sign.")]
    [SerializeField] private float wrongFeedbackGracePeriod = 2.0f;

    [Tooltip("If the expected sign's current score stays below this, the hand is considered out of range.")]
    [SerializeField] private float minExpectedRangeScore = 0.45f;

    [Tooltip("How long the hand must stay out of range before failure feedback is sent.")]
    [SerializeField] private float outOfRangeHoldDuration = 0.25f;

    public event Action<string> ExpectedSignMatched;
    public event Action<string> WrongSignDetected;

    public bool IsWaitingForSign { get; private set; }

    private string expectedSign = "";
    private float expectedSignHoldTimer = 0f;

    // Time when this sign attempt started
    private float waitStartTime = 0f;

    // If >= 0, this is when we first noticed the hand was out of range
    private float outOfRangeStartTime = -1f;

    // Prevents failure feedback from firing repeatedly too fast
    private float wrongSignBlockedUntil = 0f;

    private void Awake()
    {
        if (detectGesture == null)
            detectGesture = FindFirstObjectByType<DetectGesture>();
    }

    private void OnEnable()
    {
        if (detectGesture != null)
            detectGesture.StaticGestureFrameEvaluated += OnStaticGestureFrameEvaluated;
    }

    private void OnDisable()
    {
        if (detectGesture != null)
            detectGesture.StaticGestureFrameEvaluated -= OnStaticGestureFrameEvaluated;
    }

    public void BeginWaitingForSign(string sign)
    {
        expectedSign = NormalizeSignName(sign);
        expectedSignHoldTimer = 0f;
        waitStartTime = Time.time;
        outOfRangeStartTime = -1f;
        wrongSignBlockedUntil = 0f;
        IsWaitingForSign = !string.IsNullOrEmpty(expectedSign);
    }

    public void StopWaiting()
    {
        expectedSign = "";
        expectedSignHoldTimer = 0f;
        waitStartTime = 0f;
        outOfRangeStartTime = -1f;
        wrongSignBlockedUntil = 0f;
        IsWaitingForSign = false;
    }

    private void OnStaticGestureFrameEvaluated(
        XRHandShape topGestureShape,
        string topGestureName,
        float topGestureScore,
        float topScoreMargin,
        bool isTracked)
    {
        if (!IsWaitingForSign)
            return;

        if (!isTracked)
        {
            expectedSignHoldTimer = 0f;
            outOfRangeStartTime = -1f;
            return;
        }

        string detected = NormalizeSignName(topGestureName);
        bool isExpected = detected == expectedSign;

        bool passesExpectedScore = topGestureScore >= minExpectedSignScore;
        bool passesExpectedMargin = topScoreMargin >= minExpectedScoreMargin;

        bool passesWrongScore = topGestureScore >= minWrongSignScore;
        bool passesWrongMargin = topScoreMargin >= minWrongScoreMargin;

        if (isExpected && passesExpectedScore && passesExpectedMargin)
        {
            expectedSignHoldTimer += Time.deltaTime;
            outOfRangeStartTime = -1f;

            if (expectedSignHoldTimer >= requiredHoldDuration)
            {
                string matched = expectedSign;
                StopWaiting();
                ExpectedSignMatched?.Invoke(matched);
            }

            return;
        }

        // Not currently holding a valid correct sign
        expectedSignHoldTimer = 0f;

        // Give the player a few seconds before deciding they are wrong
        if (Time.time - waitStartTime < wrongFeedbackGracePeriod)
        {
            outOfRangeStartTime = -1f;
            return;
        }

        // Ask DetectGesture how close the current hand is to the EXPECTED sign specifically
        float expectedScore = 0f;
        bool hasExpectedScore = detectGesture != null &&
                                detectGesture.TryGetBestScoreForSign(expectedSign, out expectedScore);

        // If the hand is still close enough to the expected sign, do not fail yet
        if (hasExpectedScore && expectedScore >= minExpectedRangeScore)
        {
            outOfRangeStartTime = -1f;
            return;
        }

        // Hand is outside the expected sign's range
        if (outOfRangeStartTime < 0f)
            outOfRangeStartTime = Time.time;

        // Make sure it stays out of range briefly before we call it wrong
        if (Time.time - outOfRangeStartTime < outOfRangeHoldDuration)
            return;

        // Respect cooldown so feedback is not spammed
        if (Time.time < wrongSignBlockedUntil)
            return;

        wrongSignBlockedUntil = Time.time + wrongSignCooldown;
        outOfRangeStartTime = -1f;

        // If another sign is strongly recognized, report it specifically
        if (!isExpected &&
            !string.IsNullOrEmpty(detected) &&
            passesWrongScore &&
            passesWrongMargin)
        {
            WrongSignDetected?.Invoke(detected);
        }
        else
        {
            // Generic failure: the hand is outside the expected sign,
            // but we do not have a strong enough named wrong sign
            WrongSignDetected?.Invoke("");
        }
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

        int sideSeparator = normalized.IndexOf(" - ");
        if (sideSeparator > 0)
            normalized = normalized.Substring(0, sideSeparator);

        return normalized.Trim();
    }
}