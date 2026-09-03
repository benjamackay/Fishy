using System;
using System.Collections;
using Fishy.World;
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

    [Header("Movimiento")]
    [Tooltip("Le quita el control a Otto mientras dura la conversación y se lo " +
             "devuelve al cerrarse, incluso si el diálogo se corta a medias.")]
    public bool bloquearMovimiento = true;

    [Tooltip("Otto, para quitarle el control. Se busca solo si se deja vacío.")]
    public OttoController otto;

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
    private bool movimientoBloqueado;

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

        // Otto no debería poder caminarse la conversación. DisableMovement le pone
        // además la velocidad a cero, así que no queda deslizándose al soltarlo.
        if (bloquearMovimiento)
        {
            if (otto == null) otto = FindAnyObjectByType<OttoController>();
            if (otto != null)
            {
                otto.DisableMovement();
                movimientoBloqueado = true;
            }
        }

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
        // Se marca al terminar y no al empezar: si la conversación se corta a medias,
        // el jugador no se queda sin haberla leído nunca.
        yaSeConto = true;

        // Se devuelve el control ANTES de avisar, no después: de onDialogueEnded cuelga
        // MissionGiver, que puede lanzar la cinemática de desbloqueo, y esa vuelve a
        // quitarle el movimiento a Otto. Restaurarlo al final se lo pisaría.
        CerrarDialogo();

        //pausa
        onDialogueEnded?.Invoke();
    }

    /// <summary>
    /// Corta la conversación porque el jugador dejó de estar al alcance. No dispara
    /// onDialogueEnded —no llegó a escucharla— ni la da por contada, así que el NPC
    /// podrá retomarla cuando vuelva.
    /// </summary>
    public void AbandonarDialogo()
    {
        if (isDialogueActive) CerrarDialogo();
    }

    private void OnDisable()
    {
        // Un NPC puede apagarse a media charla —el del bosque lo hace al alejarse— o
        // desaparecer con un cambio de escena. Sin esto isDialogueActive se quedaba en
        // true, su CanInteract() no volvía a ser true nunca y dejaba de poder hablarse;
        // y ahora, además, Otto se quedaría sin poder moverse.
        if (isDialogueActive) CerrarDialogo();
    }

    /// <summary>
    /// Deja al NPC y a Otto como estaban antes de la charla, sin avisar de que la
    /// conversación terminó. <see cref="EndDialogue"/> es esto más el onDialogueEnded.
    /// </summary>
    private void CerrarDialogo()
    {
        StopAllCoroutines();
        isTyping = false;
        isDialogueActive = false;

        // Se protegen las dos referencias porque de esto depende que el NPC vuelva a
        // quedar interactuable: si reventaba, isDialogueActive se quedaba en true.
        if (dialogueText != null) dialogueText.SetText("");
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        if (movimientoBloqueado)
        {
            movimientoBloqueado = false;
            if (otto != null) otto.EnableMovement();
        }
    }
}
