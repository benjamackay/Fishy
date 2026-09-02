using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NPC : MonoBehaviour, IInteractable
{
    public NPCDialogue dialogueData;
    public GameObject dialoguePanel;
    public TMP_Text dialogueText, nameText;
    public Image portraitImage;

    [Header("Eventos")]
    [Tooltip("Se dispara al cerrar el diálogo. MissionGiver lo usa para entregar la misión " +
             "cuando la conversación termina, y MissionTracker para los objetivos de 'hablar con'.")]
    public UnityEvent onDialogueEnded = new UnityEvent();
    private int dialogueIndex;
    private bool isTyping, isDialogueActive;

    public bool CanInteract()
    {
        return !isDialogueActive;
    }

    public void Interact()
    {
        if(dialogueData == null) //pausa falta
        {
            return;
        }
        if (isDialogueActive)
        {
            NextLine();
        }
        else
        {
            StartDialogue();
        }
    }

    void StartDialogue()
    {
        // Sin esto, Typeline reventaba con IndexOutOfRange al indexar un array vacío y
        // el NPC quedaba colgado con isDialogueActive en true: no se cerraba el diálogo
        // y por tanto nunca se entregaba la misión.
        if (dialogueData.dialogueLines == null || dialogueData.dialogueLines.Length == 0)
        {
            Debug.LogWarning($"[{name}] El NPCDialogue '{dialogueData.name}' no tiene líneas.", this);
            return;
        }

        isDialogueActive = true;
        dialogueIndex = 0;

        nameText.SetText(dialogueData.name);
        portraitImage.sprite = dialogueData.npcPortrait;

        dialoguePanel.SetActive(true);
        StartCoroutine(Typeline());
        //typeline
    }

    void NextLine()
    {
        if (isTyping)
        {
            StopAllCoroutines();
            dialogueText.SetText(dialogueData.dialogueLines[dialogueIndex]);
            isTyping = false;
        }
        else if(++dialogueIndex < dialogueData.dialogueLines.Length)
        {
            StartCoroutine(Typeline());
        }
        else
        {
            EndDialogue();
        }
    }

    IEnumerator Typeline()
    {
        isTyping = true;
        dialogueText.SetText("");

        foreach(char letter in dialogueData.dialogueLines[dialogueIndex]){
            dialogueText.text += letter;
             yield return new WaitForSeconds(dialogueData.typingSpeed);
        }
        isTyping = false;

        if(dialogueData.autoProgressLine.Length > dialogueIndex && dialogueData.autoProgressLine[dialogueIndex])
        {
            yield return new WaitForSeconds(dialogueData.autoProgressDelay);
            NextLine();
        }
    }

    public void EndDialogue()
    {
        StopAllCoroutines();
        isDialogueActive = false;
        dialogueText.SetText("");
        dialoguePanel.SetActive(false);
        //pausa
        onDialogueEnded?.Invoke();
    }
}
