using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Aviso de interacción por cercanía.
///
/// Va en un hijo de Otto que tenga un Collider2D con "Is Trigger" activo: todo
/// <see cref="IInteractable"/> que entre en ese círculo queda "al alcance".
/// Mientras haya alguien al alcance se enciende <see cref="interactionIcon"/>
/// (el cartelito sobre la cabeza) y al pulsar la tecla de interacción —E por
/// defecto— se llama a <see cref="IInteractable.Interact"/> del más cercano.
///
/// La acción de entrada se arma en código, igual que en OttoController: no hace
/// falta un PlayerInput ni cablear nada en el inspector.
///
/// Detalle importante: una vez iniciada la conversación el objetivo NO se suelta
/// aunque su CanInteract() pase a false, porque es la misma tecla la que avanza
/// las líneas de diálogo (ver <see cref="NPC.Interact"/>). Lo que sí se esconde
/// es el aviso, que con el panel abierto ya no aporta nada.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    [Header("Aviso visual")]
    [Tooltip("Objeto que se enciende cuando hay alguien al alcance. Si se deja vacío se busca " +
             "un hijo con el nombre de abajo.")]
    public GameObject interactionIcon;

    [Tooltip("Hijo que se usa como aviso cuando 'Interaction Icon' queda vacío.")]
    public string interactionIconChildName = "InteractionIcon";

    [Tooltip("Dibujar la letra de la tecla encima del aviso.")]
    public bool showKeyLabel = true;

    [Tooltip("Tamaño de esa letra. Ajústalo hasta que calce dentro del cartel.")]
    public float keyLabelFontSize = 5f;

    [Tooltip("Color de la letra. El cartel por defecto es un cuadro blanco, así que negro contrasta.")]
    public Color keyLabelColor = Color.black;

    [Header("Entrada")]
    [Tooltip("Tecla que dispara la interacción.")]
    public Key interactKey = Key.E;

    [Tooltip("Aceptar también el botón inferior del mando (A / X).")]
    public bool alsoGamepadSouth = true;

    [Tooltip("Desactívalo sólo si cableas la interacción con un PlayerInput hacia onInteract(): " +
             "si no, la interacción se dispararía dos veces.")]
    public bool useOwnInputAction = true;

    /// <summary>Interactuable elegido ahora mismo, o null si no hay nadie al alcance.</summary>
    public IInteractable Current => objetivo;

    // Se guardan como Component y no como IInteractable para poder leerles el
    // transform (medir distancias) y para detectar los que se destruyen.
    private readonly List<Component> enRango = new List<Component>();
    private IInteractable objetivo;
    private InputAction interactAction;
    private TextMeshPro keyLabel;

    private void Awake()
    {
        ResolverIcono();
        PrepararEtiqueta();
        RevisarCollider();
        if (useOwnInputAction) ConstruirInput();
    }

    private void OnEnable() => interactAction?.Enable();

    private void OnDisable() => interactAction?.Disable();

    private void OnDestroy() => interactAction?.Dispose();

    private void Update()
    {
        ActualizarObjetivo();

        if (objetivo != null && interactAction != null && interactAction.WasPressedThisFrame())
            objetivo.Interact();
    }

    // ── Detección ────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out IInteractable interactable) &&
            interactable is Component componente && !enRango.Contains(componente))
        {
            enRango.Add(componente);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.TryGetComponent(out IInteractable interactable)) return;

        if (interactable is Component componente) enRango.Remove(componente);

        // Si el que se va era con quien estábamos hablando, se corta aquí: nadie
        // debería seguir avanzando un diálogo desde el otro lado del mapa.
        if (ReferenceEquals(interactable, objetivo))
        {
            // Soltarlo no basta: al NPC hay que cerrarle la conversación o se queda
            // con isDialogueActive en true, su CanInteract() no vuelve a ser true y
            // deja de poder hablarse en toda la partida (además de dejar a Otto sin
            // movimiento, que se le devuelve ahí dentro).
            if (interactable is NPC npc) npc.AbandonarDialogo();
            objetivo = null;
        }
    }

    /// <summary>
    /// Decide con quién se interactúa este frame y enciende o apaga el aviso.
    /// </summary>
    private void ActualizarObjetivo()
    {
        // Un NPC puede desactivarse por su cuenta (p. ej. el del bosque cuando se
        // aleja) sin que llegue a dispararse OnTriggerExit2D.
        enRango.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

        // 'objetivo' es una interfaz, así que su '== null' es comparación normal de
        // C# y NO el operador de Unity: un objeto ya destruido —un WorldItem recién
        // recogido— seguiría pareciendo vivo. Se comprueba contra el Component.
        if (objetivo != null && (objetivo as Component) == null) objetivo = null;

        // El objetivo sólo se cambia cuando el actual ya no está: mientras dura un
        // diálogo su CanInteract() es false y aun así la tecla debe seguir sirviendo.
        if (objetivo == null || !enRango.Contains(objetivo as Component))
            objetivo = MasCercanoDisponible();

        MostrarAviso(objetivo != null && objetivo.CanInteract());
    }

    private IInteractable MasCercanoDisponible()
    {
        IInteractable mejor = null;
        float mejorDistancia = float.MaxValue;

        foreach (Component componente in enRango)
        {
            if (!(componente is IInteractable candidato) || !candidato.CanInteract()) continue;

            float distancia = ((Vector2)(componente.transform.position - transform.position)).sqrMagnitude;
            if (distancia < mejorDistancia)
            {
                mejorDistancia = distancia;
                mejor = candidato;
            }
        }
        return mejor;
    }

    private void MostrarAviso(bool visible)
    {
        if (interactionIcon != null && interactionIcon.activeSelf != visible)
            interactionIcon.SetActive(visible);
    }

    // ── Montaje ──────────────────────────────────────────────────────────────

    private void ResolverIcono()
    {
        if (interactionIcon == null && !string.IsNullOrEmpty(interactionIconChildName))
        {
            Transform hijo = transform.Find(interactionIconChildName);
            if (hijo != null) interactionIcon = hijo.gameObject;
        }

        // Último recurso: cualquier hijo con sprite sirve de cartel.
        if (interactionIcon == null)
        {
            SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null && sprite.gameObject != gameObject) interactionIcon = sprite.gameObject;
        }

        if (interactionIcon == null)
        {
            Debug.LogWarning($"[{name}] No hay aviso visual: asigna 'Interaction Icon' o crea un " +
                             $"hijo llamado '{interactionIconChildName}'. La tecla igual funciona.", this);
            return;
        }
        interactionIcon.SetActive(false);
    }

    private void PrepararEtiqueta()
    {
        if (!showKeyLabel || interactionIcon == null) return;

        keyLabel = interactionIcon.GetComponentInChildren<TextMeshPro>(true);
        if (keyLabel == null)
        {
            var go = new GameObject("KeyLabel");
            go.transform.SetParent(interactionIcon.transform, false);
            keyLabel = go.AddComponent<TextMeshPro>();
            keyLabel.rectTransform.sizeDelta = Vector2.one * 2f;
        }

        keyLabel.text = interactKey.ToString();
        keyLabel.alignment = TextAlignmentOptions.Center;
        keyLabel.fontSize = keyLabelFontSize;
        keyLabel.color = keyLabelColor;

        // Sin esto la letra queda detrás del cuadro del cartel.
        SpriteRenderer fondo = interactionIcon.GetComponent<SpriteRenderer>();
        if (fondo != null)
        {
            keyLabel.sortingLayerID = fondo.sortingLayerID;
            keyLabel.sortingOrder = fondo.sortingOrder + 1;
        }
    }

    private void ConstruirInput()
    {
        interactAction = new InputAction("Interact", InputActionType.Button);
        // El enum Key usa el mismo nombre que la ruta del control: Key.E -> "<Keyboard>/e".
        interactAction.AddBinding($"<Keyboard>/{interactKey.ToString().ToLowerInvariant()}");
        if (alsoGamepadSouth) interactAction.AddBinding("<Gamepad>/buttonSouth");
    }

    private void RevisarCollider()
    {
        Collider2D collider2d = GetComponent<Collider2D>();
        if (collider2d == null)
            Debug.LogWarning($"[{name}] Falta un Collider2D: sin él no se detecta a nadie.", this);
        else if (!collider2d.isTrigger)
            Debug.LogWarning($"[{name}] El Collider2D no tiene 'Is Trigger' activo.", this);
    }

    /// <summary>
    /// Enganche para un PlayerInput cableado por UnityEvents. No hace falta: la
    /// tecla ya funciona sola. Si lo usas, apaga <see cref="useOwnInputAction"/>
    /// o la interacción se disparará dos veces.
    /// </summary>
    public void onInteract(InputAction.CallbackContext context)
    {
        if (context.performed && objetivo != null) objetivo.Interact();
    }
}
