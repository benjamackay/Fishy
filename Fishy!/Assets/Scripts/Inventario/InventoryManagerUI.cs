using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibuja el contenido de <see cref="InventoryManager"/> dentro de la página de
/// inventario del panel del Tab.
///
/// Va en el GameObject "InventoryPage". Se refresca al abrirse (OnEnable) y
/// cada vez que la mochila cambia, así que un objeto recogido con el panel
/// abierto aparece al instante.
///
/// Funciona sin montar nada: si no hay <see cref="itemSlotPrefab"/> arma una
/// casilla básica (fondo + icono + cantidad), y si no hay
/// <see cref="inventoryContainer"/> crea una grilla propia. Si sí hay prefab,
/// se busca dentro suyo el icono (hijo llamado "Icon", o el primer Image que no
/// sea el fondo) y el primer texto TMP.
/// </summary>
public class InventoryManagerUI : MonoBehaviour
{
    [Header("Montaje (opcional)")]
    [Tooltip("Casilla a instanciar por objeto. Si se deja vacío se arma una básica.")]
    public GameObject itemSlotPrefab;

    [Tooltip("Dónde se cuelgan las casillas. Si se deja vacío se crea una grilla aquí mismo.")]
    public Transform inventoryContainer;

    [Header("Casilla por defecto")]
    public Vector2 slotSize = new Vector2(96f, 96f);
    public Vector2 slotSpacing = new Vector2(8f, 8f);
    public Color slotBackgroundColor = MenuTabsTheme.Colores.FondoRanura;

    [Header("Textos")]
    [Tooltip("Mostrar 'x2', 'x3'... cuando hay más de una unidad.")]
    public bool showQuantity = true;

    [Tooltip("Qué decir cuando la mochila está vacía. Vacío = no mostrar nada.")]
    public string emptyMessage = MenuTabsTheme.Textos.MochilaVacia;

    [Header("Diagnóstico")]
    [Tooltip("Escribir en consola cada refresco. Sirve para saber si esta UI está viva: " +
             "si abres la pestaña y no aparece nada en el Console, el componente no está montado.")]
    public bool verboseLogs = true;

    private readonly List<GameObject> slots = new List<GameObject>();
    private TMP_Text emptyLabel;
    private InventoryManager suscritoA;
    [HideInInspector] public int minimumVisibleSlots;

    /// <summary>itemId ya avisados por falta de ícono, para no repetir el warning en
    /// cada refresco: la mochila se repinta entera cada vez que algo cambia.</summary>
    private readonly HashSet<string> avisadosSinIcono = new HashSet<string>();

    private void Awake()
    {
        if (inventoryContainer == null) inventoryContainer = CrearContenedor();
    }

    private void OnEnable()
    {
        suscritoA = InventoryManager.Instance;
        suscritoA.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (suscritoA != null) suscritoA.OnInventoryChanged -= Refresh;
        suscritoA = null;
    }

    /// <summary>Vuelve a pintar todas las casillas desde la mochila.</summary>
    public void Refresh()
    {
        // Los pines de recompensa (HDU-11) no se dibujan acá: siguen en el
        // inventario de verdad (InventoryManager.AddItem/GetQuantity los sigue
        // viendo, así que DetectiveCaseManager no repite el pin al rejugar un
        // caso), pero visualmente sólo aparecen dentro del álbum
        // (AlbumEvidenciasUI), con su ícono. El álbum en sí SÍ se sigue viendo acá:
        // es la puerta de entrada para abrirlo.
        List<Item> items = new List<Item>(InventoryManager.Instance.inventory.Count);
        foreach (var item in InventoryManager.Instance.inventory)
        {
            bool esPin = item.itemData != null && !string.IsNullOrEmpty(item.itemData.itemId) &&
                         item.itemData.itemId.StartsWith(AlbumEvidenciasUI.PrefijoPin);
            if (!esPin) items.Add(item);
        }

        // Las casillas se reciclan en vez de destruirse y volverse a crear:
        // Destroy es diferido, así que las viejas seguirían ocupando la grilla
        // durante el frame en que se pintan las nuevas.
        int visibleCount = Mathf.Max(items.Count, minimumVisibleSlots);
        while (slots.Count < visibleCount) slots.Add(CrearSlot());

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            bool enUso = i < items.Count;
            slots[i].SetActive(i < visibleCount);
            if (enUso) PintarSlot(slots[i], items[i]);
            else
            {
                var icon = BuscarIcono(slots[i]);
                if (icon != null) icon.enabled = false;
                var label = slots[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "";
            }
        }

        MostrarMensajeVacio(items.Count == 0);

        if (verboseLogs)
            Debug.Log($"[Inventario] UI refrescada: {items.Count} objeto(s) en la mochila.", this);
    }

    // ── Pintado ──────────────────────────────────────────────────────────────

    private void PintarSlot(GameObject slot, Item item)
    {
        string nombre = item.itemData != null && !string.IsNullOrEmpty(item.itemData.itemName)
            ? item.itemData.itemName
            : "(sin nombre)";

        Image icono = BuscarIcono(slot);
        bool hayIcono = false;
        if (icono != null)
        {
            icono.sprite = item.itemData != null ? item.itemData.itemIcon : null;
            icono.preserveAspect = true;
            hayIcono = icono.sprite != null;
            icono.enabled = hayIcono;
        }

        TMP_Text texto = slot.GetComponentInChildren<TMP_Text>(true);
        if (texto != null)
        {
            string cantidad = showQuantity && item.itemQuantity > 1 ? $"x{item.itemQuantity}" : "";
            if (hayIcono)
            {
                // Con icono el dibujo ya identifica el objeto; el texto sólo cuenta.
                texto.text = cantidad;
            }
            else if (string.IsNullOrEmpty(cantidad))
            {
                texto.text = nombre;
            }
            else
            {
                texto.text = $"{nombre} {cantidad}";
            }
        }

        // Sin ícono Y sin dónde poner el nombre (esta casilla no tiene texto), el
        // objeto queda en la mochila pero invisible en pantalla: ni dibujo ni letra,
        // indistinguible de una casilla vacía. Es un fallo silencioso que ya pasó
        // (el Álbum de Evidencias y varios ítems más no tienen 'Item Icon' asignado
        // en su asset) y sin este aviso nadie se entera por qué "no aparece".
        if (!hayIcono && texto == null && item.itemData != null &&
            avisadosSinIcono.Add(item.itemData.itemId))
        {
            Debug.LogWarning(
                $"[Inventario] '{item.itemData.itemId}' no tiene Item Icon asignado y esta " +
                "casilla no tiene texto de respaldo: se ve como una casilla vacía. Asígnale " +
                "un sprite en Resources/Items/{nombre del asset}.asset.", this);
        }

        ConfigurarInteraccion(slot, item);
    }

    /// <summary>
    /// HDU-11 (adelanto mínimo de HDU-12) — el único ítem interactivo hoy es el
    /// Álbum de Evidencias. No es un sistema genérico de "usar ítem": solo este
    /// caso puntual. Los slots se reciclan (<see cref="Refresh"/>), así que el
    /// listener se rehace en cada pintado en vez de engancharse una sola vez en
    /// <see cref="CrearSlot"/> -si no, un slot que fue el álbum seguiría
    /// abriéndolo aunque ahora tenga otro objeto.
    /// </summary>
    private void ConfigurarInteraccion(GameObject slot, Item item)
    {
        bool esAlbum = item.itemData != null && item.itemData.itemId == AlbumEvidenciasUI.ItemIdAlbum;

        Button boton = slot.GetComponent<Button>();
        if (!esAlbum)
        {
            if (boton != null) boton.onClick.RemoveAllListeners();
            return;
        }

        if (boton == null) boton = slot.AddComponent<Button>();
        boton.onClick.RemoveAllListeners();
        boton.onClick.AddListener(AlbumEvidenciasUI.Show);
    }

    /// <summary>
    /// Icono de la casilla: el hijo "Icon" si existe, si no el primer Image que
    /// no sea el propio fondo del slot.
    /// </summary>
    private static Image BuscarIcono(GameObject slot)
    {
        Transform porNombre = slot.transform.Find("Icon");
        if (porNombre != null && porNombre.TryGetComponent(out Image imagen)) return imagen;

        foreach (Image candidato in slot.GetComponentsInChildren<Image>(true))
        {
            if (candidato.gameObject != slot) return candidato;
        }
        return null;
    }

    private void MostrarMensajeVacio(bool visible)
    {
        if (string.IsNullOrEmpty(emptyMessage)) return;

        if (emptyLabel == null)
        {
            var go = new GameObject("EmptyMessage", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            EstirarSobreElPadre(rt);

            emptyLabel = go.AddComponent<TextMeshProUGUI>();
            emptyLabel.alignment = TextAlignmentOptions.Center;
            emptyLabel.text = emptyMessage;
        }
        emptyLabel.gameObject.SetActive(visible);
    }

    // ── Montaje automático ───────────────────────────────────────────────────

    private Transform CrearContenedor()
    {
        var go = new GameObject("InventoryContainer", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        EstirarSobreElPadre(rt, 16f);

        var grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = slotSize;
        grid.spacing = slotSpacing;
        grid.childAlignment = TextAnchor.UpperLeft;
        return rt;
    }

    private GameObject CrearSlot()
    {
        if (itemSlotPrefab != null)
        {
            GameObject desdePrefab = Instantiate(itemSlotPrefab, inventoryContainer);
            desdePrefab.SetActive(true);
            return desdePrefab;
        }
        return CrearSlotPorDefecto();
    }

    private GameObject CrearSlotPorDefecto()
    {
        var slot = new GameObject("ItemSlot", typeof(RectTransform), typeof(Image));
        slot.transform.SetParent(inventoryContainer, false);
        slot.GetComponent<Image>().color = slotBackgroundColor;

        var icono = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var iconoRt = (RectTransform)icono.transform;
        iconoRt.SetParent(slot.transform, false);
        EstirarSobreElPadre(iconoRt, 8f);
        Image imagen = icono.GetComponent<Image>();
        imagen.preserveAspect = true;
        imagen.enabled = false;   // hasta que haya un sprite que mostrar

        var texto = new GameObject("Quantity", typeof(RectTransform));
        var textoRt = (RectTransform)texto.transform;
        textoRt.SetParent(slot.transform, false);
        EstirarSobreElPadre(textoRt, 4f);
        var tmp = texto.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.BottomRight;
        tmp.fontSize = 20f;

        return slot;
    }

    private static void EstirarSobreElPadre(RectTransform rt, float margen = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margen, margen);
        rt.offsetMax = new Vector2(-margen, -margen);
    }
}
