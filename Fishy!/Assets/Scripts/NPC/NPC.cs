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

    [Header("Backend (opcional)")]
    [Tooltip("dialogo_id en la tabla DialogoNPC (ej: NPC_FLAMENCO_SEC). Si hay backend " +
             "disponible, reemplaza a 'Dialogue Data' con lo que traiga de la BD; si no, " +
             "se usa 'Dialogue Data' tal cual quedó en el Inspector.")]
    public string dialogoId;

    private void Awake()
    {
        DialogoNpcLoader.LoadAsync(dialogoId, dialogo => dialogueData = dialogo);
    }

    [Header("Repetición")]
    [Tooltip("Cuenta el diálogo completo una sola vez. Al volver a interactuar no se " +
             "reabre el panel, pero la conversación igual cuenta: se dispara " +
             "onDialogueEnded para que MissionGiver entregue la misión o recuerde lo " +
             "que falta. Desactívalo si este NPC debe repetir su discurso siempre.")]
    public bool soloLaPrimeraVez = true;

    [Header("Eventos")]
    [Tooltip("Se dispara al cerrar el diálogo. MissionGiver lo usa para entregar la misión " +
             "cuando la conversación termina, y MissionTracker para los objetivos de 'hablar con'.")]
    public UnityEvent onDialogueEnded = new UnityEvent();
    private int dialogueIndex;
    private bool isTyping, isDialogueActive;
    private bool yaSeConto;

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
            return;
        }

        // Volver a hablarle no repite el discurso, pero sigue contando como
        // conversación: MissionGiver escucha onDialogueEnded para entregar la misión
        // al volver y para avisar de lo que falta, así que tragarse el evento dejaría
        // la misión sin poder completarse y la zona sin abrirse.
        if (soloLaPrimeraVez && yaSeConto)
        {
            onDialogueEnded?.Invoke();
            return;
        }

        StartDialogue();
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

        // dialogueText y dialoguePanel son imprescindibles: sin ellos no hay dónde
        // escribir ni qué abrir. Se comprueban ANTES de tocar isDialogueActive porque
        // si StartDialogue reventaba a medio camino, CanInteract() se quedaba en false
        // y el NPC no volvía a responder en toda la partida.
        if (dialogueText == null || dialoguePanel == null)
        {
            string falta = dialogueText == null ? "Dialogue Text" : "Dialogue Panel";
            Debug.LogError(
                $"[{name}] Falta asignar '{falta}' en el inspector: este NPC no puede hablar.",
                this);
            return;
        }

        isDialogueActive = true;
        dialogueIndex = 0;

        // El nombre y el retrato son decorativos, así que si faltan se conversa igual.
        // Con portraitImage sin asignar esta línea tiraba NullReference y el panel no
        // llegaba a abrirse nunca: el NPC parecía mudo.
        if (nameText != null)
        {
            nameText.SetText(string.IsNullOrEmpty(dialogueData.npcName)
                ? dialogueData.name
                : dialogueData.npcName);
        }

        if (portraitImage != null)
            portraitImage.sprite = dialogueData.npcPortrait;

        dialoguePanel.SetActive(true);
        StartCoroutine(Typeline());
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

        // Se marca al terminar y no al empezar: si la conversación se corta a medias,
        // el jugador no se queda sin haberla leído nunca.
        yaSeConto = true;

        // Se protegen las dos referencias porque onDialogueEnded es lo que entrega la
        // misión: si esto reventaba antes del Invoke, el MissionGiver no se enteraba de
        // que la conversación había terminado.
        if (dialogueText != null) dialogueText.SetText("");
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        //pausa
        onDialogueEnded?.Invoke();
    }
}
