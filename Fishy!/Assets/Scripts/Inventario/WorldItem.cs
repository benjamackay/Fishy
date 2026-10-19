using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Objeto suelto en el mapa que Otto puede recoger con la tecla de interacción.
///
/// Va en el GameObject del objeto, que necesita un Collider2D (no hace falta
/// que sea trigger ni que tenga Rigidbody2D: el que se mueve es Otto y su
/// cuerpo es el que genera el contacto). Al implementar
/// <see cref="IInteractable"/> lo detecta el mismo InteractionDetector que ya
/// usan los NPC, así que hereda gratis el cartelito de "pulsa E".
///
/// Al recogerlo entra en <see cref="InventoryManager"/>, que a su vez avisa a
/// la UI del inventario.
/// </summary>
public class WorldItem : MonoBehaviour, IInteractable
{
    [Header("Identidad")]
    [Tooltip("Identificador único de ESTE objeto del mapa. Se usa para recordar que " +
             "ya fue recogido. Asignalo con Fishy → Asignar ids a los objetos del mapa.")]
    public string objetoId;

    [Header("Qué se recoge")]
    [Tooltip("Plantilla del objeto (Assets > Create > Inventory > New Item).")]
    public ItemData itemData;

    [Min(1)]
    [Tooltip("Cuántas unidades entrega este objeto del mapa.")]
    public int quantity = 1;

    [Header("Al recogerlo")]
    [Tooltip("Destruir el objeto del mapa. Si se desmarca sólo se desactiva, " +
             "y puede volver a encenderse desde otro script.")]
    public bool destroyOnPickup = true;

    [Tooltip("Escribir en consola qué se recogió. Útil mientras se prueba.")]
    public bool logOnPickup = true;

    [Tooltip("Para enganchar sonido, animación o lo que haga falta al recoger.")]
    public UnityEvent onPickup;

    private bool recogido;

    private void Awake()
    {
        if (itemData == null)
            Debug.LogWarning($"[{name}] WorldItem sin 'Item Data': no se puede recoger.", this);

        if (GetComponent<Collider2D>() == null)
            Debug.LogWarning($"[{name}] WorldItem sin Collider2D: Otto no lo va a detectar.", this);
    }

    /// <summary>
    /// Si este objeto ya fue recogido en esta partida, se quita del mapa antes del
    /// primer frame.
    ///
    /// Va en <c>Start</c> y no en <c>Awake</c> a proposito: el registro de lo ya
    /// recogido se pide apenas hay partida —en la pantalla de ingreso, mucho antes
    /// de que cargue el mapa— y para cuando corre Start ya esta en memoria. Asi el
    /// objeto nunca llega a dibujarse. Es el mismo cuidado que con la posicion de
    /// Otto: si se resolviera al llegar la respuesta, se veria desaparecer.
    ///
    /// Si el registro todavia no llego, <see cref="ObjetosRecogidosSync"/> lo apaga
    /// en cuanto llegue; ahi si se alcanza a ver un parpadeo, pero es el caso raro.
    /// </summary>
    private void Start()
    {
        if (ObjetosRecogidosSync.YaFueRecogido(objetoId))
            QuitarDelMapa();
    }

    /// <summary>
    /// Lo saca del mapa sin pasar por <see cref="Interact"/>: no entra al inventario
    /// —ya esta guardado ahi— ni dispara <see cref="onPickup"/>, que podria tener
    /// sonido o animacion. Restaurar no es volver a recoger.
    /// </summary>
    public void QuitarDelMapa()
    {
        recogido = true;
        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    // Con esto en false el detector ni siquiera muestra el cartel, así no se
    // ofrece una interacción que no va a hacer nada.
    public bool CanInteract() => !recogido && itemData != null;

    public void Interact()
    {
        if (!CanInteract()) return;

        // Antes de tocar el mundo: si el objeto se destruye y algo fallara
        // después, el item ya quedó guardado.
        recogido = true;
        InventoryManager.Instance.AddItem(itemData, quantity);

        // Que este objeto ya no esta en el suelo se guarda aparte del inventario:
        // un item consumible sale de la mochila y el objeto NO debe reaparecer.
        ObjetosRecogidosSync.Marcar(objetoId, name);

        if (logOnPickup)
            Debug.Log($"[Inventario] Recogido: {itemData.itemName} x{quantity}", this);

        onPickup?.Invoke();

        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
