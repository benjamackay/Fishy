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

        if (logOnPickup)
            Debug.Log($"[Inventario] Recogido: {itemData.itemName} x{quantity}", this);

        onPickup?.Invoke();

        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
