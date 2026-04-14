using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard;

public class NPCDialogue : MonoBehaviour
{
    private bool playerDetection = false;
    private bool startedDialogue = false;

    public AnchorGate requiredAnchor;
    public AudioManager audioManager;
    public InputActionReference leftTrigger;
    public InputActionReference rightTrigger;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor leftRay;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rightRay;
    public GameObject inventoryItem;
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

    // reference to video manager
    [Header("Video Settings")]
    public SignVideoManager videoManager;

    [Header("Name Input + Signing")]
    [Tooltip("Dialogue marker that starts XR keyboard name input.")]
    public string nameInputMarker = "NAME_INPUT";
    [Tooltip("Dialogue marker that signs each letter from typed name.")]
    public string nameSigningMarker = "NAME_SIGNING";
    public TMP_InputField nameInputField;
    public Button nameSubmitButton;
    public GameObject nameInputPanel;
    [Tooltip("Assigned world-space XRKeyboard to use near this NPC.")]
    public XRKeyboard worldSpaceKeyboard;
    [Tooltip("If true, uses world-space keyboard first, then falls back to global keyboard.")]
    public bool preferWorldSpaceKeyboard = true;
    [Tooltip("If true, pressing Enter/Submit on the XR keyboard submits the typed name.")]
    public bool autoSubmitOnKeyboardEnter = true;
    public string nameInputPrompt = "Before we finish, type your name on the keyboard, then press submit.";
    public string nameSignInstructionFormat = "Great! Now sign each letter of your name: {0}";
    public string nameLetterPromptFormat = "Show me letter: {0} ({1}/{2})";

    private int index = 0;
    private int lessonSignIndex = 0;
    private int nameSignIndex = 0;

    private bool waitingForExpectedSign = false;
    private bool processingSignSuccess = false;
    private bool waitingForNameInput = false;
    private bool inNameSigningMode = false;

    private string currentExpectedSign = "";
    private string typedName = "";

    private readonly List<string> nameSigns = new List<string>();

    private XRKeyboard activeNameKeyboard;
    private Coroutine typingCoroutine;
    private Coroutine signSuccessCoroutine;
    private bool playerHasItem = false;

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

    private void Awake()
    {
        if (audioManager == null)
        {
            audioManager = FindFirstObjectByType<AudioManager>();
        }

        if (audioManager == null)
        {
            audioManager = AudioManager.GetOrCreate();
        }

        if (signMatcher == null)
        {
            signMatcher = FindFirstObjectByType<ExpectedSignMatcher>();
        }

        if (nameInputPanel == null && nameInputField != null)
        {
            nameInputPanel = nameInputField.gameObject;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name == "Player")
        {
            playerDetection = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        playerDetection = false;
        ZeroText();
        startedDialogue = false;
    }

    // Subscribe to when trigger is pressed
    private void OnEnable()
    {
        if (leftTrigger != null && leftTrigger.action != null)
        {
            leftTrigger.action.performed += OnTriggerPressed;
            leftTrigger.action.Enable();
        }

        if (rightTrigger != null && rightTrigger.action != null)
        {
            rightTrigger.action.performed += OnTriggerPressed;
            rightTrigger.action.Enable();
        }

        if (signMatcher != null)
        {
            signMatcher.ExpectedSignMatched += OnExpectedSignMatched;
        }

        if (nameSubmitButton != null)
        {
            nameSubmitButton.onClick.AddListener(SubmitTypedName);
        }
    }

    // Unsubscribe to when trigger is pressed
    private void OnDisable()
    {
        if (leftTrigger != null && leftTrigger.action != null)
        {
            leftTrigger.action.performed -= OnTriggerPressed;
            leftTrigger.action.Disable();
        }

        if (rightTrigger != null && rightTrigger.action != null)
        {
            rightTrigger.action.performed -= OnTriggerPressed;
            rightTrigger.action.Disable();
        }

        if (signMatcher != null)
        {
            signMatcher.ExpectedSignMatched -= OnExpectedSignMatched;
        }

        if (nameSubmitButton != null)
        {
            nameSubmitButton.onClick.RemoveListener(SubmitTypedName);
        }

        DetachKeyboardSubmitListener();

        if (videoManager != null)
        {
            videoManager.HideTutorial();
        }
    }

    // When trigger is pressed
    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        if (requiredAnchor != null && !requiredAnchor.IsPlayerOnAnchor)
        {
            return;
        }

        if (!startedDialogue)
        {
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
            if (playerDetection && (leftHit || rightHit))
            {
                if (npcPanel.activeInHierarchy)
                {
                    ZeroText();
                }
                else
                {
                    npcPanel.SetActive(true);
                    nameText.text = ActiveNpcName;
                    dialogueText.text = "";
                    typingCoroutine = StartCoroutine(Typing());
                    audioManager?.StartTypingSound();
                    startedDialogue = true;
                }
            }
        }
        else
        {
            // Text has finished typing
            // TODO: don't allow user to go to the next line until animation has finished
            string[] activeDialogue = ActiveDialogue;
            if (activeDialogue == null || activeDialogue.Length == 0 || index >= activeDialogue.Length)
            {
                return;
            }

            // While a sign is expected (or success feedback is being shown),
            // trigger input should not manually advance dialogue.
            if (waitingForExpectedSign || processingSignSuccess)
            {
                return;
            }

            if (waitingForNameInput)
            {
                return;
            }

            if (activeDialogue[index] == "SIGNING" && waitingForExpectedSign)
            {
                return;
            }

            if (dialogueText.text == activeDialogue[index]
                || activeDialogue[index] == "DEMONSTRATION"
                || activeDialogue[index] == "SIGNING"
                || activeDialogue[index] == nameInputMarker
                || activeDialogue[index] == nameSigningMarker)
            {
                NextLine();
            }
        }
    }

    // Reset text
    public void ZeroText()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (signSuccessCoroutine != null)
        {
            StopCoroutine(signSuccessCoroutine);
            signSuccessCoroutine = null;
        }

        dialogueText.text = "";
        nameText.text = "";
        index = 0;
        lessonSignIndex = 0;
        nameSignIndex = 0;
        waitingForExpectedSign = false;
        processingSignSuccess = false;
        waitingForNameInput = false;
        inNameSigningMode = false;
        currentExpectedSign = "";
        typedName = "";
        nameSigns.Clear();

        if (signMatcher != null)
        {
            signMatcher.StopWaiting();
        }

        if (nameInputField != null)
        {
            nameInputField.text = "";
        }

        if (nameInputPanel != null)
        {
            nameInputPanel.SetActive(false);
        }

        HideNameKeyboard();

        if (videoManager != null)
        {
            videoManager.HideTutorial();
        }

        npcPanel.SetActive(false);
    }

    private void BeginSignStep()
    {
        string[] activeLessonSigns = ActiveLessonSigns;
        if (activeLessonSigns == null || lessonSignIndex >= activeLessonSigns.Length)
        {
            waitingForExpectedSign = false;
            currentExpectedSign = "";
            NextLine();
            return;
        }

        if (autoSwitchToHandsForSigning)
        {
            onEnterSigningMode?.Invoke();
        }

        waitingForExpectedSign = true;
        currentExpectedSign = activeLessonSigns[lessonSignIndex];

        if (signMatcher != null)
        {
            signMatcher.BeginWaitingForSign(currentExpectedSign);
        }

        npcPanel.SetActive(true);
        dialogueText.text = "Now, show me: " + currentExpectedSign;

        if (videoManager != null)
        {
            videoManager.ShowTutorial(currentExpectedSign, npcPanel);
        }
    }

    private void BeginNameInputStep()
    {
        waitingForNameInput = true;
        inNameSigningMode = false;
        waitingForExpectedSign = false;
        processingSignSuccess = false;
        currentExpectedSign = "";
        typedName = "";
        nameSigns.Clear();
        nameSignIndex = 0;

        if (signMatcher != null)
        {
            signMatcher.StopWaiting();
        }

        if (videoManager != null)
        {
            videoManager.HideTutorial();
        }

        if (autoSwitchToHandsForSigning)
        {
            onExitSigningMode?.Invoke();
        }

        npcPanel.SetActive(true);
        dialogueText.text = nameInputPrompt;

        if (nameInputPanel != null)
        {
            nameInputPanel.SetActive(true);
        }

        if (nameInputField != null)
        {
            nameInputField.text = "";
            ShowNameKeyboard();
        }
    }

    private void BeginNameSigningStep()
    {
        if (nameSigns.Count == 0)
        {
            BeginNameInputStep();
            return;
        }

        inNameSigningMode = true;
        waitingForNameInput = false;

        if (nameSignIndex >= nameSigns.Count)
        {
            inNameSigningMode = false;

            if (videoManager != null)
            {
                videoManager.HideTutorial();
            }

            NextLine();
            return;
        }

        if (autoSwitchToHandsForSigning)
        {
            onEnterSigningMode?.Invoke();
        }

        waitingForExpectedSign = true;
        currentExpectedSign = nameSigns[nameSignIndex];

        if (signMatcher != null)
        {
            signMatcher.BeginWaitingForSign(currentExpectedSign);
        }

        npcPanel.SetActive(true);
        dialogueText.text = string.Format(nameLetterPromptFormat, currentExpectedSign, nameSignIndex + 1, nameSigns.Count);

        if (videoManager != null)
        {
            videoManager.ShowTutorial(currentExpectedSign, npcPanel);
        }
    }

    public void SubmitTypedName()
    {
        if (!waitingForNameInput)
        {
            return;
        }

        string sourceText = nameInputField != null ? nameInputField.text : "";
        string normalizedName = NormalizeNameToLetters(sourceText);

        if (string.IsNullOrEmpty(normalizedName))
        {
            dialogueText.text = "Please type at least one letter for your name.";
            return;
        }

        typedName = normalizedName;
        nameSigns.Clear();

        for (int i = 0; i < typedName.Length; i++)
        {
            nameSigns.Add(typedName[i].ToString());
        }

        nameSignIndex = 0;
        waitingForNameInput = false;

        if (nameInputPanel != null)
        {
            nameInputPanel.SetActive(false);
        }

        HideNameKeyboard();

        npcPanel.SetActive(true);
        dialogueText.text = string.Format(nameSignInstructionFormat, typedName);
        NextLine();
    }

    private string NormalizeNameToLetters(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            return "";
        }

        StringBuilder cleaned = new StringBuilder(rawText.Length);
        for (int i = 0; i < rawText.Length; i++)
        {
            char c = rawText[i];
            if (char.IsLetter(c))
            {
                cleaned.Append(char.ToUpperInvariant(c));
            }
        }

        return cleaned.ToString();
    }

    private void ShowNameKeyboard()
    {
        if (nameInputField == null)
        {
            return;
        }

        XRKeyboard keyboardToAttach = null;
        bool openedWorldSpace = false;

        if (preferWorldSpaceKeyboard && worldSpaceKeyboard != null)
        {
            worldSpaceKeyboard.Open(nameInputField, true);
            openedWorldSpace = true;
            keyboardToAttach = worldSpaceKeyboard;
        }

        if (!openedWorldSpace && GlobalNonNativeKeyboard.instance != null)
        {
            GlobalNonNativeKeyboard.instance.ShowKeyboard(nameInputField, true);
            keyboardToAttach = GlobalNonNativeKeyboard.instance.keyboard;
        }

        AttachKeyboardSubmitListener(keyboardToAttach);
    }

    private void HideNameKeyboard()
    {
        DetachKeyboardSubmitListener();

        if (worldSpaceKeyboard != null && worldSpaceKeyboard.isOpen)
        {
            worldSpaceKeyboard.Close();
        }

        if (GlobalNonNativeKeyboard.instance != null)
        {
            GlobalNonNativeKeyboard.instance.HideKeyboard();
        }
    }

    private void AttachKeyboardSubmitListener(XRKeyboard keyboard)
    {
        if (!autoSubmitOnKeyboardEnter || keyboard == null)
        {
            return;
        }

        if (activeNameKeyboard == keyboard)
        {
            return;
        }

        DetachKeyboardSubmitListener();
        activeNameKeyboard = keyboard;
        activeNameKeyboard.onTextSubmitted.AddListener(OnKeyboardTextSubmitted);
    }

    private void DetachKeyboardSubmitListener()
    {
        if (activeNameKeyboard == null)
        {
            return;
        }

        activeNameKeyboard.onTextSubmitted.RemoveListener(OnKeyboardTextSubmitted);
        activeNameKeyboard = null;
    }

    private void OnKeyboardTextSubmitted(KeyboardTextEventArgs args)
    {
        if (!autoSubmitOnKeyboardEnter || !waitingForNameInput)
        {
            return;
        }

        SubmitTypedName();
    }

    private void OnExpectedSignMatched(string matchedSign)
    {
        if (!startedDialogue || !waitingForExpectedSign || processingSignSuccess)
        {
            return;
        }

        waitingForExpectedSign = false;
        processingSignSuccess = true;

        if (signMatcher != null)
        {
            signMatcher.StopWaiting();
        }

        if (videoManager != null)
        {
            videoManager.HideTutorial();
        }

        bool continueNameSigning = false;

        if (inNameSigningMode)
        {
            nameSignIndex++;
            continueNameSigning = nameSignIndex < nameSigns.Count;
        }
        else
        {
            lessonSignIndex++;
        }

        if (signSuccessCoroutine != null)
        {
            StopCoroutine(signSuccessCoroutine);
        }

        signSuccessCoroutine = StartCoroutine(ShowSignSuccessAndContinue(matchedSign, continueNameSigning));

        if (audioManager != null && audioManager.confetti != null)
        {
            audioManager.PlaySFXOnce(audioManager.confetti);
        }
    }

    private IEnumerator ShowSignSuccessAndContinue(string expectedSign, bool continueNameSigning)
    {
        npcPanel.SetActive(true);
        dialogueText.text = string.Format(ActiveSuccessFormat, expectedSign);
        onSignSuccess?.Invoke();

        yield return new WaitForSeconds(Mathf.Max(0f, successMessageDuration));

        processingSignSuccess = false;
        signSuccessCoroutine = null;

        if (continueNameSigning)
        {
            BeginNameSigningStep();
        }
        else
        {
            inNameSigningMode = false;

            if (!inNameSigningMode && !waitingForExpectedSign && autoSwitchToHandsForSigning)
            {
                onExitSigningMode?.Invoke();
            }

            NextLine();
        }
    }

    // Typing animation
    IEnumerator Typing()
    {
        string[] activeDialogue = ActiveDialogue;
        if (activeDialogue == null || activeDialogue.Length == 0 || index >= activeDialogue.Length)
        {
            yield break;
        }

        foreach (char letter in activeDialogue[index].ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(0.03f);
        }

        audioManager?.StopTypingSound();
    }

    // Continue to next dialogue line
    public void NextLine()
    {
        string[] activeDialogue = ActiveDialogue;
        if (activeDialogue == null || activeDialogue.Length == 0)
        {
            return;
        }

        if (index < activeDialogue.Length - 1)
        {
            index++;
            dialogueText.text = "";

            if (activeDialogue[index] == "DEMONSTRATION")
            {
                Debug.Log("Demonstration");
                // TODO: show animation for signing
            }
            else if (activeDialogue[index] == nameInputMarker)
            {
                BeginNameInputStep();
            }
            else if (activeDialogue[index] == nameSigningMarker)
            {
                BeginNameSigningStep();
            }
            else if (activeDialogue[index] == "SIGNING")
            {
                BeginSignStep();
                Debug.Log("Signing");
                // TODO: implement asl testing here
            }
            else
            {
                if (videoManager != null)
                {
                    videoManager.HideTutorial();
                }

                if (autoSwitchToHandsForSigning && waitingForExpectedSign == false && processingSignSuccess == false)
                {
                    onExitSigningMode?.Invoke();
                }

                npcPanel.SetActive(true);
                typingCoroutine = StartCoroutine(Typing());
                audioManager?.StartTypingSound();

                // If last line of dialogue
                if (index == activeDialogue.Length - 1 && !playerHasItem)
                {
                    inventoryItem.SetActive(true);

                    if (audioManager != null && audioManager.reward != null)
                    {
                        audioManager.PlaySFXOnce(audioManager.reward);
                    }

                    // Won't give player item in future interactions
                    playerHasItem = true;
                }
            }
        }
        else
        {
            ZeroText();
            startedDialogue = false;
        }
    }
}