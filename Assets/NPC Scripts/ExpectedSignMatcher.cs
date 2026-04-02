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
    [Tooltip("Lower threshold for recognizing a wrong sign.")]
    [SerializeField] private float minWrongSignScore = 0.55f;
    [Tooltip("Initializing a lower margin so wrong signs can be easier to catch.")]
    [SerializeField] private float minWrongScoreMargin = 0.03f;
    [Tooltip("Initialized cool down so the npc doesn't repeat the wrong sign dialogue in every frame.")]
    [SerializeField] private float wrongSignCooldown = 1.0f;
    [Tooltip("How long to wait before the npc tells the player to try again when nothing correct is happening.")]
    [SerializeField] private float retryPromptDelay = 1.75f;         

    public event Action<string> ExpectedSignMatched;
    public event Action<string> WrongSignDetected;

    public bool IsWaitingForSign { get; private set; }

    private string expectedSign = "";
    private float expectedSignHoldTimer = 0f;
    //prevents spam for the wrong sign
    private float wrongSignCooldownTimer = 0f;
    //measures how long the player has been signing the current sign attempt without suceeding
    private float retryPromptTimer = 0f;

    private void Update()
    {
        if (!IsWaitingForSign)
            return; 

        if (wrongSignCooldownTimer > 0f)
            //counts cooldown towards 0
            wrongSignCooldownTimer -= Time.deltaTime; 

        //keeping count of how long the player has been attempting
        retryPromptTimer += Time.deltaTime; 

        //if enough time passes without success, give retry feedback
        if (retryPromptTimer >= retryPromptDelay && wrongSignCooldownTimer <= 0f)
        {   
            //restart the retry timer
            retryPromptTimer = 0f;                    
            wrongSignCooldownTimer = wrongSignCooldown; 
            //generic failure feedback
            WrongSignDetected?.Invoke("");           
        }
    }
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
        //storing sign npc wants
        expectedSign = NormalizeSignName(sign);  
        //resetting correct sign hold timer
        expectedSignHoldTimer = 0f;   
        //resetting cool down from any previous wrong sign
        wrongSignCooldownTimer = 0f;          
        //reset retry timer for new sign
        retryPromptTimer = 0f;                       
        IsWaitingForSign = !string.IsNullOrEmpty(expectedSign);
    }

    public void StopWaiting()
    {
        //clearing variables
        expectedSign = "";                 
        expectedSignHoldTimer = 0f;       
        wrongSignCooldownTimer = 0f;      
        retryPromptTimer = 0f;            
        IsWaitingForSign = false;    
    }

    private void OnStaticGestureFrameEvaluated(
        XRHandShape topGestureShape,
        string topGestureName,
        float topGestureScore,
        float topScoreMargin,
        bool isTracked)
    {   
        //if npc is not waiting for a sign we ignore the user input
        if (!IsWaitingForSign)
            return;

        //checks if hand is not tracked, if its not the player cannot be signing correctly
        if (!isTracked)
        {
            expectedSignHoldTimer = 0f;
            return;
        }
        //cleaning up raw name from "sign_A" to "A" for example
        string detected = NormalizeSignName(topGestureName);
        //checking if detected sign is the one npc asked for
        bool isExpected = detected == expectedSign;
        //stricter checks for accepting correct sign
        bool passesExpectedScore = topGestureScore >= minExpectedSignScore;
        bool passesExpectedMargin = topScoreMargin >= minExpectedScoreMargin;

        //looser checks for noticing the wrong sign
        bool passesWrongScore = topGestureScore >= minWrongSignScore;
        bool passesWrongMargin = topScoreMargin >= minWrongScoreMargin;

        //If the signed sign by the player is correct
        if (isExpected && passesExpectedScore && passesExpectedMargin)
        {
            //player is doing the correct sign, so do not trigger retry feedback
            retryPromptTimer = 0f;

            //counts how long correct sign ahs been held
            expectedSignHoldTimer += Time.deltaTime;

            //once the sign has has been held by the player long enough, accept it
            if (expectedSignHoldTimer >= requiredHoldDuration)
            {
                string matched = expectedSign;
                StopWaiting();
                ExpectedSignMatched?.Invoke(matched);
            }

            //returning to not run wrong sign logic in below if statement
            return;
        }
        //any frame that is not a valid correct sign frame should reset the time
        expectedSignHoldTimer = 0f;

        //if the player signed other recognizable sign strongly, we give them a failure feedback
        //player did not sign expected sign
        //recognizer identified some sign name
        //wrong sign only needs to pass loser score threshold and confidence margin
        //only allow feedback if cooldown expired
        if (!isExpected && !string.IsNullOrEmpty(detected) && passesWrongScore && passesWrongMargin && wrongSignCooldownTimer <= 0f)
        {
            //start cooldown so npc does not repeat the wrong sign frame every frame
            wrongSignCooldownTimer = wrongSignCooldown;

            //restart retry timer since we already gave failure feedback
            retryPromptTimer = 0f;

            //tell npc which wrong sign was detected
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
