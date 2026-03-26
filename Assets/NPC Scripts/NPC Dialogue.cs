using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Events;
using UnityEngine.XR.Hands.Gestures;

public class NPCDialogue : MonoBehaviour
{
    private bool playerDetection = false;
    private bool startedDialogue = false;
    public AnchorGate requiredAnchor;
    public InputActionReference leftTrigger;
    public InputActionReference rightTrigger;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor leftRay;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rightRay;
    public GameObject npcPanel;
    // TODO: implement aslPanel animations
    public string npcName;
    public TMP_Text dialogueText;
    public TMP_Text nameText;
    public string[] dialogue;
    [Header("Sign Lesson")]
    public DetectGesture detectGesture;
    public string[] lessonSigns = { "A", "B", "C", "D", "E" };
    [Tooltip("Automatically invoke hand-tracking mode when a SIGNING step starts.")]
    public bool autoSwitchToHandsForSigning = true;
    public UnityEvent onEnterSigningMode;
    public UnityEvent onExitSigningMode;
    [Tooltip("Shown after a correct sign before moving to the next dialogue line.")]
    public string successFormat = "Good job! That was {0}.";
    public float successMessageDuration = 1.2f;
    [Header("Sign Validation Tuning")]
    [Tooltip("Minimum confidence score required to accept the expected sign.")]
    public float minExpectedSignScore = 0.72f;
    [Tooltip("Expected sign must beat the second-best score by at least this margin.")]
    public float minExpectedScoreMargin = 0.08f;
    [Tooltip("How long the expected sign must be held before it is accepted.")]
    public float requiredHoldDuration = 0.20f;
    private int index = 0;
    private int lessonSignIndex = 0;
    private bool waitingForExpectedSign = false;
    private bool processingSignSuccess = false;
    private string currentExpectedSign = "";
    private float expectedSignHoldTimer = 0f;
    private Coroutine typingCoroutine;
    private Coroutine signSuccessCoroutine;

    private void Awake() {
        if (detectGesture == null) {
            detectGesture = FindObjectOfType<DetectGesture>();
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.name == "Player") {
            playerDetection = true;
        }
    }

    private void OnTriggerExit(Collider other) {
        playerDetection = false;
        ZeroText();
        startedDialogue = false;
    }

    // Subscribe to when trigger is pressed
    private void OnEnable() {
        if (leftTrigger != null && leftTrigger.action != null) {
            leftTrigger.action.performed += OnTriggerPressed;
            leftTrigger.action.Enable();
        }

        if (rightTrigger != null && rightTrigger.action != null) {
            rightTrigger.action.performed += OnTriggerPressed;
            rightTrigger.action.Enable();
        }

        if (detectGesture != null) {
            detectGesture.StaticGestureFrameEvaluated += OnStaticGestureFrameEvaluated;
        }
    }

    // Unsubscribe to when trigger is pressed
    private void OnDisable() {
        if (leftTrigger != null && leftTrigger.action != null) {
            leftTrigger.action.performed -= OnTriggerPressed;
            leftTrigger.action.Disable();
        }

        if (rightTrigger != null && rightTrigger.action != null) {
            rightTrigger.action.performed -= OnTriggerPressed;
            rightTrigger.action.Disable();
        }

        if (detectGesture != null) {
            detectGesture.StaticGestureFrameEvaluated -= OnStaticGestureFrameEvaluated;
        }
    }

    // When trigger is pressed
    private void OnTriggerPressed(InputAction.CallbackContext context) {
        if (requiredAnchor != null && !requiredAnchor.IsPlayerOnAnchor) {
            return;
        }

        if (!startedDialogue) {
            RaycastHit hit;
            bool leftHit = leftRay != null
                && leftRay.TryGetCurrent3DRaycastHit(out hit)
                && hit.collider != null
                && hit.collider.CompareTag("NPC");
            bool rightHit = rightRay != null
                && rightRay.TryGetCurrent3DRaycastHit(out hit)
                && hit.collider != null
                && hit.collider.CompareTag("NPC");
            // When player is in range, controller is aimed at NPC, and trigger has been pressed
            if (playerDetection && (leftHit || rightHit)) {
                if (npcPanel.activeInHierarchy) {
                    ZeroText();
                } 
                else {
                    npcPanel.SetActive(true);
                    nameText.text = npcName;
                    dialogueText.text = "";
                    typingCoroutine = StartCoroutine(Typing());
                    startedDialogue = true;
                }
            }
        } else {
            // Text has finished typing
            // TODO: don't allow user to go to the next line until animation has finished
            if (dialogue[index] == "SIGNING" && waitingForExpectedSign) {
                return;
            }

            if (dialogueText.text == dialogue[index] || dialogue[index] == "DEMONSTRATION" || dialogue[index] == "SIGNING") {
                NextLine();
            }

            // TODO: Note handing at the end of the lesson
        }
    }

    // Reset text
    public void ZeroText() {
        if (typingCoroutine != null) {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (signSuccessCoroutine != null) {
            StopCoroutine(signSuccessCoroutine);
            signSuccessCoroutine = null;
        }

        dialogueText.text = "";
        nameText.text = "";
        index = 0;
        lessonSignIndex = 0;
        waitingForExpectedSign = false;
        processingSignSuccess = false;
        currentExpectedSign = "";
        expectedSignHoldTimer = 0f;
        npcPanel.SetActive(false); 
    }

    private void BeginSignStep() {
        if (lessonSigns == null || lessonSignIndex >= lessonSigns.Length) {
            waitingForExpectedSign = false;
            currentExpectedSign = "";
            NextLine();
            return;
        }

        if (autoSwitchToHandsForSigning) {
            onEnterSigningMode?.Invoke();
        }

        waitingForExpectedSign = true;
        currentExpectedSign = lessonSigns[lessonSignIndex];
        expectedSignHoldTimer = 0f;
        npcPanel.SetActive(true);
        dialogueText.text = "Show me sign: " + currentExpectedSign;
    }

    private string NormalizeSignName(string signName) {
        if (string.IsNullOrEmpty(signName)) {
            return "";
        }

        string normalized = signName.Trim().ToUpperInvariant();

        // DetectGesture can append an index suffix when it builds fallback keys (e.g. "A - RIGHT_0").
        int trailingUnderscore = normalized.LastIndexOf('_');
        if (trailingUnderscore >= 0 && trailingUnderscore < normalized.Length - 1) {
            bool suffixIsDigits = true;
            for (int i = trailingUnderscore + 1; i < normalized.Length; i++) {
                if (!char.IsDigit(normalized[i])) {
                    suffixIsDigits = false;
                    break;
                }
            }

            if (suffixIsDigits) {
                normalized = normalized.Substring(0, trailingUnderscore);
            }
        }

        if (normalized.StartsWith("SIGN_")) {
            normalized = normalized.Substring(5);
        }

        // Support hand-shape labels like "A - RIGHT" or "B - LEFT".
        int sideSeparator = normalized.IndexOf(" - ");
        if (sideSeparator > 0) {
            normalized = normalized.Substring(0, sideSeparator);
        }

        normalized = normalized.Trim();
        return normalized;
    }

    private void OnStaticGestureFrameEvaluated(
        XRHandShape topGestureShape,
        string topGestureName,
        float topGestureScore,
        float topScoreMargin,
        bool isTracked)
    {
        if (!startedDialogue || !waitingForExpectedSign || processingSignSuccess) {
            return;
        }

        if (!isTracked) {
            expectedSignHoldTimer = 0f;
            return;
        }

        string detected = NormalizeSignName(topGestureName);
        string expected = NormalizeSignName(currentExpectedSign);

        bool isExpected = detected == expected;
        bool passesScore = topGestureScore >= minExpectedSignScore;
        bool passesMargin = topScoreMargin >= minExpectedScoreMargin;

        if (!isExpected || !passesScore || !passesMargin) {
            expectedSignHoldTimer = 0f;
            return;
        }

        expectedSignHoldTimer += Time.deltaTime;
        if (expectedSignHoldTimer < requiredHoldDuration) {
            return;
        }

        expectedSignHoldTimer = 0f;

        waitingForExpectedSign = false;
        processingSignSuccess = true;
        lessonSignIndex++;

        if (signSuccessCoroutine != null) {
            StopCoroutine(signSuccessCoroutine);
        }
        signSuccessCoroutine = StartCoroutine(ShowSignSuccessAndContinue(expected));
    }

    private IEnumerator ShowSignSuccessAndContinue(string expectedSign) {
        npcPanel.SetActive(true);
        dialogueText.text = string.Format(successFormat, expectedSign);
        yield return new WaitForSeconds(Mathf.Max(0f, successMessageDuration));
        processingSignSuccess = false;
        signSuccessCoroutine = null;
        NextLine();
    }

    // Typing animation
    IEnumerator Typing() {
        foreach(char letter in dialogue[index].ToCharArray()) {
            dialogueText.text += letter;
            yield return new WaitForSeconds(0.03f);
        }
    }

    // Continue to next dialogue line
    public void NextLine() {
        if (index < dialogue.Length - 1) {
            index++;
            dialogueText.text = "";
            if (dialogue[index] == "DEMONSTRATION") {
                Debug.Log("Demonstration");
                // TODO: show animation for signing
            } else if (dialogue[index] == "SIGNING") {
                BeginSignStep();
                Debug.Log("Signing");
                // TODO: implement asl testing here
            } else {
                if (autoSwitchToHandsForSigning && waitingForExpectedSign == false && processingSignSuccess == false) {
                    onExitSigningMode?.Invoke();
                }
                npcPanel.SetActive(true);
                typingCoroutine = StartCoroutine(Typing());
            }
        } else {
            ZeroText();
            startedDialogue = false;
        }
    }
}
