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
    [Tooltip("How long the expected sign must be held before it is accepted.")]
    [SerializeField] private float requiredHoldDuration = 0.20f;

    public event Action<string> ExpectedSignMatched;
    public event Action<string> WrongSignDetected;

    public bool IsWaitingForSign { get; private set; }

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

        string detected = NormalizeSignName(topGestureName);
        bool isExpected = detected == expectedSign;
        bool passesScore = topGestureScore >= minExpectedSignScore;
        bool passesMargin = topScoreMargin >= minExpectedScoreMargin;

        //If the signed sign by the player is correct
        if (isExpected && passesScore && passesMargin)
        {
            expectedSignHoldTimer += Time.deltaTime;
            if (expectedSignHoldTimer >= requiredHoldDuration)
            {
                string matched = expectedSign;
                StopWaiting();
                ExpectedSignMatched?.Invoke(matched);
            }
        }
        else if (!isExpected && passesScore && passesMargin && !string.IsNullOrEmpty(detected))
        {

            expectedSignHoldTimer = 0f;
            WrongSignDetected?.Invoke(detected);
        }
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

        // Support hand-shape labels like "A - RIGHT" or "B - LEFT".
        int sideSeparator = normalized.IndexOf(" - ");
        if (sideSeparator > 0)
            normalized = normalized.Substring(0, sideSeparator);

        return normalized.Trim();
    }
}
