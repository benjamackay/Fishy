using System;
using System.Collections;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// El NPC neutro: el que cuenta su texto línea a línea en el panel de diálogo y no
/// tiene opciones de respuesta. Se habla con él acercándose y pulsando E.
///
/// Es uno de los tres tipos de interacción con un NPC, junto con el NPC sospechoso
/// (<see cref="Fishy.Phone.PhoneChatLauncher"/>, un chat con respuestas) y el de caso
/// detective (<see cref="Fishy.Detective.DetectiveLauncher"/>). No hace falta para
/// entregar misiones: <see cref="MissionGiver"/> funciona con cualquiera de los tres.
/// </summary>
public class DialogoNeutroNPC : MonoBehaviour, IInteractable
{
    // Las secciones y sus nombres son los mismos que en PhoneChatLauncher (el NPC
    // sospechoso), a propósito: quien configura un NPC encuentra lo mismo en el mismo
    // sitio, se llame como se llame el componente.

    [Header("Contenido del diálogo")]
    [Tooltip("id del diálogo en el banco (ej: HDU1_SEC_PUDU_COLLAR). Es la fuente " +
             "principal: se lee de Resources/banco_preguntas.json, y si hay sesión se " +
             "pisa con lo que tenga la tabla DialogoNPC de la base. Déjalo vacío para " +
             "usar solo el NPCDialogue de abajo.")]
    public string dialogoId;

    [Tooltip("Diálogos del banco que este NPC dice DESPUÉS del de 'Dialogo Id', uno por " +
             "conversación y en este orden. Al terminar uno pasa al siguiente; al llegar " +
             "al último se queda en él. Vacío = el NPC dice siempre lo mismo.\n\n" +
             "Ej.: Coipo se presenta (HDU1_NPC_COIPO) y la vez siguiente da su " +
             "testimonio (HDU3_M3_TESTIMONIO_COIPO).")]
    public System.Collections.Generic.List<string> dialogosSiguientes =
        new System.Collections.Generic.List<string>();

    /// <summary>En cuál de sus diálogos va: 0 = <see cref="dialogoId"/>, 1 = el primero
    /// de <see cref="dialogosSiguientes"/>, y así.</summary>
    private int _indiceDialogo;

    /// <summary>Id del diálogo que diría si se le hablara ahora.</summary>
    public string DialogoActualId =>
        _indiceDialogo == 0 || dialogosSiguientes == null || _indiceDialogo > dialogosSiguientes.Count
            ? dialogoId
            : dialogosSiguientes[_indiceDialogo - 1];

    /// <summary>
    /// Id del último diálogo que se terminó de escuchar. Lo lee el objetivo "hablar
    /// con" cuando el NPC tiene varios diálogos: onDialogueEnded se dispara al cerrar
    /// cualquiera, y sin esto terminar la presentación de Coipo contaría también como
    /// haber escuchado su testimonio.
    /// </summary>
    public string UltimoDialogoTerminado { get; private set; }

    /// <summary>True si alguno de sus diálogos es <paramref name="id"/>.</summary>
    public bool DiceDialogo(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (string.Equals(dialogoId, id, StringComparison.Ordinal)) return true;
        return dialogosSiguientes != null && dialogosSiguientes.Contains(id);
    }

    /// <summary>True si dice más de un diálogo a lo largo de la partida.</summary>
    public bool TieneVariosDialogos => dialogosSiguientes != null && dialogosSiguientes.Count > 0;

    [Tooltip("Diálogo local. Se usa si no hay 'Dialogo Id', y de él salen siempre el " +
             "retrato, la velocidad de tecleo y la voz, que el banco no trae.")]
    public NPCDialogue dialogueData;

    [Header("Referencias (se buscan si quedan vacías)")]
    [Tooltip("Panel de diálogo de la escena. Imprescindible: sin él no hay qué abrir.")]
    public GameObject dialoguePanel;
    [Tooltip("Dónde se escribe la línea. Imprescindible.")]
    public TMP_Text dialogueText, nameText;
    [Tooltip("Retrato del NPC. Si se deja vacío se busca un hijo del panel llamado " +
             "'DialoguePortrait'.")]
    public Image portraitImage;

    private void Awake()
    {
        CargarDialogo(dialogoId);
    }

    /// <summary>
    /// Pone las líneas del diálogo <paramref name="id"/>. Lo usa Awake con el primero
    /// y <see cref="EndDialogue"/> al pasar al siguiente de la lista.
    /// </summary>
    private void CargarDialogo(string id)
    {
        // El banco primero, y en el sitio: va empaquetado en Resources, así que el NPC
        // tiene sus líneas desde el primer frame, sin conexión y sin sesión iniciada.
        NPCDialogue delBanco = DialogoNpcLoader.DesdeBanco(id, dialogueData);
        if (delBanco != null) dialogueData = delBanco;

        // El backend puede traer una corrección posterior del mismo texto, pero llega
        // tarde y puede no llegar. Nunca se aplica a media conversación: cambiar las
        // líneas con dialogueIndex a mitad dejaría el diálogo saltándose frases o
        // indexando fuera del array.
        DialogoNpcLoader.LoadAsync(id, dialogueData, dialogo =>
        {
            if (isDialogueActive) return;
            if (DialogoActualId != id) return;   // llegó tarde: el NPC ya pasó a otro
            dialogueData = dialogo;
        });
    }

    [Tooltip("Otto, para quitarle el control. Se busca solo si se deja vacío.")]
    public OttoController otto;

    [Header("Comportamiento")]
    [Tooltip("Le quita el control a Otto mientras dura la conversación y se lo " +
             "devuelve al cerrarse, incluso si el diálogo se corta a medias.")]
    public bool bloquearMovimiento = true;

    [Tooltip("Permite que este NPC repita su discurso completo cada vez que se le " +
             "habla. Desmarcado cuenta el diálogo una sola vez: al volver no se reabre " +
             "el panel, pero la conversación igual cuenta —se dispara onDialogueEnded— " +
             "para que MissionGiver entregue la misión o recuerde lo que falta. " +
             "Misma casilla y mismo significado que en el NPC sospechoso.")]
    public bool repetible;

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
        if (!repetible && yaSeConto)
        {
            DispararFinDeDialogo();
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

        MostrarRetrato();

        // Mismo aspecto que el chat de NPCs y el Modo Detective. Solo pinta: no
        // toca el avance por tecla ni añade opciones, porque este diálogo es de una
        // sola vía y no está preparado para elegir respuestas.
        Fishy.UI.DialogoNeutroSkin.Aplicar(dialoguePanel, nameText, dialogueText, portraitImage);

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

    /// <summary>Nombre del hijo del panel que hace de retrato, si no se asignó a mano.</summary>
    private const string NombreRetrato = "DialoguePortrait";

    /// <summary>
    /// Pone la cara del NPC en el panel.
    ///
    /// Busca el retrato si no está asignado: en la escena los NPCs cablean el panel,
    /// el texto y el nombre, pero nadie asignó 'Portrait Image', así que la línea de
    /// abajo no se ejecutaba nunca y quedaba a la vista el círculo de relleno que
    /// venía del mockup de diseño. Deducirlo del panel evita repetir ese olvido en
    /// cada NPC nuevo.
    ///
    /// Y si el diálogo no trae retrato, el Image se apaga: un círculo vacío parece
    /// un error de carga, y es justo lo que despistó la primera vez.
    /// </summary>
    private void MostrarRetrato()
    {
        if (portraitImage == null && dialoguePanel != null)
        {
            Transform t = dialoguePanel.transform.Find(NombreRetrato);
            if (t != null) portraitImage = t.GetComponent<Image>();
        }

        if (portraitImage == null) return;

        portraitImage.sprite  = dialogueData.npcPortrait;
        portraitImage.enabled = dialogueData.npcPortrait != null;

        if (dialogueData.npcPortrait == null)
        {
            Debug.LogWarning($"[{name}] '{dialogueData.name}' no tiene 'Npc Portrait' " +
                             "asignado, así que el diálogo va sin cara.", this);
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

        UltimoDialogoTerminado = DialogoActualId;

        //pausa
        DispararFinDeDialogo();
    }

    /// <summary>
    /// Da la conversación por terminada: la anota en el historial y avisa.
    ///
    /// Las dos cosas juntas y en un solo sitio a propósito. Hay dos caminos que dan una
    /// conversación por hablada —terminarla, y volver a hablarle a un NPC que ya contó lo
    /// suyo— y si uno anotara y el otro no, un objetivo de "hablar con" se cumpliría o no
    /// según por dónde hubiera pasado el jugador.
    ///
    /// El historial es lo que permite que una misión recibida DESPUÉS de haber hablado
    /// nazca ya cumplida, en vez de obligar a repetir la conversación.
    /// </summary>
    private void DispararFinDeDialogo()
    {
        HistorialDeObjetivos.AnotarDialogo(dialogoId);
        onDialogueEnded?.Invoke();

        // Se avanza DESPUÉS de avisar: quien escucha onDialogueEnded tiene que ver
        // en UltimoDialogoTerminado el que se acaba de oír, no el que viene.
        PasarAlSiguienteDialogo();
    }

    /// <summary>
    /// Si quedan diálogos en <see cref="dialogosSiguientes"/>, la próxima charla será
    /// el siguiente. El nuevo todavía no se ha contado, así que se reabre el panel
    /// aunque el NPC no sea repetible.
    /// </summary>
    private void PasarAlSiguienteDialogo()
    {
        if (dialogosSiguientes == null || _indiceDialogo >= dialogosSiguientes.Count) return;

        _indiceDialogo++;
        yaSeConto = false;
        CargarDialogo(DialogoActualId);
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
