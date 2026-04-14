using System;
using UnityEngine;
using UnityEngine.XR.Hands.Gestures;

public class ExpectedSignMatcher : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private DetectGesture detectGesture;

    [Header("Validation")]
    [Tooltip("Minimum confidence score required to accept the expected sign.")]
    [SerializeField] private float minExpectedSignScore = 0.72f;
    [Tooltip("Expected sign must beat the second-best score by at least this margin.")]
    [SerializeField] private float minExpectedScoreMargin = 0.08f;
    [Tooltip("If true, margin check is skipped when the expected sign is already the top detected sign.")]
    [SerializeField] private bool ignoreMarginWhenExpectedIsTop = true;
    [Tooltip("If true, margin check is skipped when the expected sign is currently the active override gesture.")]
    [SerializeField] private bool ignoreMarginWhenExpectedIsActiveOverride = true;
    [Tooltip("How long the expected sign must be held before it is accepted.")]
    [SerializeField] private float requiredHoldDuration = 0.20f;

    public event Action<string> ExpectedSignMatched;

    public bool IsWaitingForSign { get; private set; }
    public string CurrentExpectedSign => expectedSign;

    private string expectedSign = "";
    private float expectedSignHoldTimer = 0f;

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
        IsWaitingForSign = !string.IsNullOrEmpty(expectedSign);
    }

    public void StopWaiting()
    {
        expectedSign = "";
        expectedSignHoldTimer = 0f;
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
            return;
        }

        string detectedFromGestureKey = NormalizeSignName(topGestureName);
        string detectedFromShapeName = topGestureShape != null ? NormalizeSignName(topGestureShape.name) : "";
        string activeOverrideSign = detectGesture != null ? detectGesture.CurrentActiveGestureSign : "";
        float activeOverrideScore = detectGesture != null ? detectGesture.CurrentActiveGestureScore : 0f;

        bool expectedIsTop = detectedFromGestureKey == expectedSign || detectedFromShapeName == expectedSign;
        bool expectedIsActiveOverride = activeOverrideSign == expectedSign;
        bool isExpected = expectedIsTop || expectedIsActiveOverride;

        float scoreForValidation = expectedIsTop
            ? topGestureScore
            : (expectedIsActiveOverride ? activeOverrideScore : topGestureScore);

        bool passesScore = scoreForValidation >= minExpectedSignScore;
        bool passesMargin = topScoreMargin >= minExpectedScoreMargin
            || (ignoreMarginWhenExpectedIsTop && expectedIsTop)
            || (ignoreMarginWhenExpectedIsActiveOverride && expectedIsActiveOverride);

        if (!isExpected || !passesScore || !passesMargin)
        {
            expectedSignHoldTimer = 0f;
            return;
        }

        expectedSignHoldTimer += Time.deltaTime;
        if (expectedSignHoldTimer < requiredHoldDuration)
            return;

        string matched = expectedSign;
        StopWaiting();
        ExpectedSignMatched?.Invoke(matched);
    }

    private static string NormalizeSignName(string signName)
    {
        if (string.IsNullOrEmpty(signName))
            return "";

        string normalized = signName.Trim().ToUpperInvariant();

        // DetectGesture can append an index suffix when it builds fallback keys (e.g. "A - RIGHT_0").
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

        // Support hand-shape labels like "A - RIGHT" or "B - LEFT".
        int sideSeparator = normalized.IndexOf(" - ");
        if (sideSeparator > 0)
            normalized = normalized.Substring(0, sideSeparator);

        return normalized.Trim();
    }
}
