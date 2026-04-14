using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using System.Collections;
using System.Collections.Generic;
using TMPro;

public class Rupert : MonoBehaviour
{
    private bool playerDetection = false;
    private bool startedDialogue = false;
    public AudioManagerMain audioManager;
    public InputActionReference leftTrigger;
    public InputActionReference rightTrigger;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor leftRay;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rightRay;
    public GameObject npcPanel;
    public string npcName;
    public TMP_Text dialogueText;
    public TMP_Text nameText;
    public string[] dialogue;
    private int index = 0;
    private Coroutine typingCoroutine;

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
        leftTrigger.action.performed += OnTriggerPressed;
        rightTrigger.action.performed += OnTriggerPressed;
        leftTrigger.action.Enable();
        rightTrigger.action.Enable();
    }

    // Unsubscribe to when trigger is pressed
    private void OnDisable()
    {
        leftTrigger.action.performed -= OnTriggerPressed;
        rightTrigger.action.performed -= OnTriggerPressed;
        leftTrigger.action.Disable();
        rightTrigger.action.Disable();
    }

    // When trigger is pressed
    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        if (!startedDialogue)
        {
            RaycastHit hit;
            bool leftHit = leftRay.TryGetCurrent3DRaycastHit(out hit) && hit.collider.CompareTag("NPC");
            bool rightHit = rightRay.TryGetCurrent3DRaycastHit(out hit) && hit.collider.CompareTag("NPC");
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
                    nameText.text = npcName;
                    dialogueText.text = "";
                    typingCoroutine = StartCoroutine(Typing());
                    audioManager.StartTypingSound();
                    startedDialogue = true;
                }
            }
        }
        else
        {
            // Text has finished typing
            // TODO: don't allow user to go to the next line until animation has finished
            if (dialogueText.text == dialogue[index])
            {
                NextLine();
            }
        }
    }

    // Reset text
    public void ZeroText()
    {
        if (typingCoroutine != null) {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        dialogueText.text = "";
        nameText.text = "";
        index = 0;
        npcPanel.SetActive(false);
    }

    // Typing animation
    IEnumerator Typing()
    {
        foreach (char letter in dialogue[index].ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(0.03f);
        }
        audioManager.StopTypingSound();
    }

    // Continue to next dialogue line
    public void NextLine()
    {
        if (index < dialogue.Length - 1)
        {
            index++;
            dialogueText.text = "";
            
            npcPanel.SetActive(true);
            typingCoroutine = StartCoroutine(Typing());
            audioManager.StartTypingSound();
            
        }
        else
        {
            ZeroText();
            startedDialogue = false;
        }
    }
}
