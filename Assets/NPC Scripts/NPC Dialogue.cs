using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Events;

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
    [Header("Lesson Data")]
    public NPCLessonData lessonData;
    public string npcName;
    public TMP_Text dialogueText;
    public TMP_Text nameText;
    public string[] dialogue;
    [Header("Sign Lesson")]
    public ExpectedSignMatcher signMatcher;
    public string[] lessonSigns = { "A", "B", "C", "D", "E" };
    [Tooltip("Automatically invoke hand-tracking mode when a SIGNING step starts.")]
    public bool autoSwitchToHandsForSigning = true;
    public UnityEvent onEnterSigningMode;
    public UnityEvent onExitSigningMode;
    [Tooltip("Shown after a correct sign before moving to the next dialogue line.")]
    public string successFormat = "Good job! That was {0}.";
    public float successMessageDuration = 1.2f;
    [Tooltip("Invoked when the expected sign is matched successfully.")]
    public UnityEvent onSignSuccess;
    private int index = 0;
    private int lessonSignIndex = 0;
    private bool waitingForExpectedSign = false;
    private bool processingSignSuccess = false;
    private string currentExpectedSign = "";
    private Coroutine typingCoroutine;
    private Coroutine signSuccessCoroutine;

    private string ActiveNpcName => lessonData != null && !string.IsNullOrEmpty(lessonData.npcName)
        ? lessonData.npcName
        : npcName;

    private string[] ActiveDialogue => lessonData != null && lessonData.dialogue != null && lessonData.dialogue.Length > 0
        ? lessonData.dialogue
        : dialogue;

    private string[] ActiveLessonSigns => lessonData != null && lessonData.lessonSigns != null && lessonData.lessonSigns.Length > 0
        ? lessonData.lessonSigns
        : lessonSigns;

    private string ActiveSuccessFormat => lessonData != null && !string.IsNullOrEmpty(lessonData.successFormat)
        ? lessonData.successFormat
        : successFormat;

    private void Awake() {
        if (signMatcher == null) {
            signMatcher = FindFirstObjectByType<ExpectedSignMatcher>();
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

        if (signMatcher != null) {
            signMatcher.ExpectedSignMatched += OnExpectedSignMatched;
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

        if (signMatcher != null) {
            signMatcher.ExpectedSignMatched -= OnExpectedSignMatched;
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
                    nameText.text = ActiveNpcName;
                    dialogueText.text = "";
                    typingCoroutine = StartCoroutine(Typing());
                    startedDialogue = true;
                }
            }
        } else {
            // Text has finished typing
            // TODO: don't allow user to go to the next line until animation has finished
            string[] activeDialogue = ActiveDialogue;
            if (activeDialogue == null || activeDialogue.Length == 0 || index >= activeDialogue.Length) {
                return;
            }

            if (activeDialogue[index] == "SIGNING" && waitingForExpectedSign) {
                return;
            }

            if (dialogueText.text == activeDialogue[index] || activeDialogue[index] == "DEMONSTRATION" || activeDialogue[index] == "SIGNING") {
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
        if (signMatcher != null) {
            signMatcher.StopWaiting();
        }
        npcPanel.SetActive(false); 
    }

    private void BeginSignStep() {
        string[] activeLessonSigns = ActiveLessonSigns;
        if (activeLessonSigns == null || lessonSignIndex >= activeLessonSigns.Length) {
            waitingForExpectedSign = false;
            currentExpectedSign = "";
            NextLine();
            return;
        }

        if (autoSwitchToHandsForSigning) {
            onEnterSigningMode?.Invoke();
        }

        waitingForExpectedSign = true;
        currentExpectedSign = activeLessonSigns[lessonSignIndex];
        if (signMatcher != null) {
            signMatcher.BeginWaitingForSign(currentExpectedSign);
        }
        npcPanel.SetActive(true);
        dialogueText.text = "Show me sign: " + currentExpectedSign;
    }
    private void OnExpectedSignMatched(string matchedSign) {
        if (!startedDialogue || !waitingForExpectedSign || processingSignSuccess) {
            return;
        }

        waitingForExpectedSign = false;
        processingSignSuccess = true;
        lessonSignIndex++;

        if (signSuccessCoroutine != null) {
            StopCoroutine(signSuccessCoroutine);
        }
        signSuccessCoroutine = StartCoroutine(ShowSignSuccessAndContinue(matchedSign));
    }

    private IEnumerator ShowSignSuccessAndContinue(string expectedSign) {
        npcPanel.SetActive(true);
        dialogueText.text = string.Format(ActiveSuccessFormat, expectedSign);
        onSignSuccess?.Invoke();

        yield return new WaitForSeconds(Mathf.Max(0f, successMessageDuration));
        processingSignSuccess = false;
        signSuccessCoroutine = null;
        NextLine();
    }

    // Typing animation
    IEnumerator Typing() {
        string[] activeDialogue = ActiveDialogue;
        if (activeDialogue == null || activeDialogue.Length == 0 || index >= activeDialogue.Length) {
            yield break;
        }

        foreach(char letter in activeDialogue[index].ToCharArray()) {
            dialogueText.text += letter;
            yield return new WaitForSeconds(0.03f);
        }
    }

    // Continue to next dialogue line
    public void NextLine() {
        string[] activeDialogue = ActiveDialogue;
        if (activeDialogue == null || activeDialogue.Length == 0) {
            return;
        }

        if (index < activeDialogue.Length - 1) {
            index++;
            dialogueText.text = "";
            if (activeDialogue[index] == "DEMONSTRATION") {
                Debug.Log("Demonstration");
                // TODO: show animation for signing
            } else if (activeDialogue[index] == "SIGNING") {
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
