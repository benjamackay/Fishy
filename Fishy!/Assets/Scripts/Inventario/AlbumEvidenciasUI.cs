using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// HDU-11 (adelanto mínimo de HDU-12) — Panel simple que lista los pines de
/// recompensa ya obtenidos en el Modo Detective. Se abre al hacer click en el
/// ítem "Álbum de Evidencias" de la mochila (ver
/// <see cref="InventoryManagerUI"/>).
///
/// Es un placeholder a propósito: no agrupa por caso ni registra señales
/// individuales -eso es la HDU-12 completa, con su propio EvidenceAlbumManager-,
/// solo reusa el inventario como fuente de datos, filtrando por el prefijo
/// "PIN_" que ya comparten los 3 pines de HDU-11.
///
/// Los pines se ven como mosaico (sólo el ícono, en una grilla): el nombre y la
/// descripción no se dibujan en la casilla —no entran sin desbordarse— sino en
/// una franja fija al pie del panel, que se llena al pasar el mouse por encima
/// de un pin y se vacía al salir.
/// </summary>
public class AlbumEvidenciasUI : MonoBehaviour
{
    /// <summary>itemId del ítem "Álbum" en Resources/Items. Compartido con
    /// InventoryManagerUI (para saber qué slot es clicable) y con
    /// DetectiveCaseManager (para saber qué ItemData entregar).</summary>
    public const string ItemIdAlbum = "ITEM_ALBUM_EVIDENCIAS";

    /// <summary>Prefijo que comparten los 3 pines de recompensa. Público: lo usa
    /// también <see cref="InventoryManagerUI"/> para que los pines no se dibujen
    /// en la mochila general — sólo viven aquí, en el álbum.</summary>
    public const string PrefijoPin = "PIN_";
    private const string MensajeVacio =
        "Todavía no hay evidencias.\nResuelve un caso del Modo Detective para conseguir tu primer pin.";
    private const string MensajePorDefecto = "Pasa el mouse sobre un pin para ver de qué se trata.";

    private static AlbumEvidenciasUI _instance;

    private CanvasGroup _panel;
    private RectTransform _contenedorEntradas;
    private GameObject _vacioGO;
    private TMP_Text _vacioTmp;
    private TMP_Text _tooltipTmp;

    public static void Show()
    {
        if (_instance == null)
        {
            var go = new GameObject("AlbumEvidenciasUI");
            _instance = go.AddComponent<AlbumEvidenciasUI>();
        }
        _instance.Abrir();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        BuildRuntimeUI();
        _panel.gameObject.SetActive(false);
    }

    private void Abrir()
    {
        Repoblar();
        _panel.gameObject.SetActive(true);
    }

    private void Cerrar() => _panel.gameObject.SetActive(false);

    private void Repoblar()
    {
        for (int i = _contenedorEntradas.childCount - 1; i >= 0; i--)
            Destroy(_contenedorEntradas.GetChild(i).gameObject);
        MostrarTooltip(MensajePorDefecto);

        var pines = new List<Item>();
        foreach (var item in InventoryManager.Instance.inventory)
        {
            if (item.itemData != null && !string.IsNullOrEmpty(item.itemData.itemId) &&
                item.itemData.itemId.StartsWith(PrefijoPin))
                pines.Add(item);
        }

        _vacioGO.SetActive(pines.Count == 0);
        if (pines.Count == 0)
        {
            _vacioTmp.text = MensajeVacio;
            return;
        }

        foreach (var item in pines)
            CrearCasillaPin(item);
    }

    /// <summary>Sólo el ícono: el nombre y la descripción se leen al pasar el mouse
    /// por encima (ver <see cref="PinHover"/>), no ocupan espacio en la grilla.</summary>
    private void CrearCasillaPin(Item item)
    {
        var casilla = new GameObject("Pin", typeof(RectTransform), typeof(Image));
        casilla.transform.SetParent(_contenedorEntradas, false);

        var img = casilla.GetComponent<Image>();
        img.sprite = item.itemData.itemIcon;
        img.preserveAspect = true;
        // Sin sprite, se deja un cuadro tenue en vez de nada: así se ve que hay un
        // pin ahí (y se puede seguir pasando el mouse para leer cuál es) aunque el
        // ItemData todavía no tenga el ícono asignado.
        img.color = item.itemData.itemIcon != null ? Color.white : new Color(1f, 1f, 1f, 0.18f);

        string texto = item.itemData.itemName;
        if (!string.IsNullOrEmpty(item.itemData.itemDescription))
            texto += $"\n{item.itemData.itemDescription}";

        var hover = casilla.AddComponent<PinHover>();
        hover.album = this;
        hover.texto = texto;
    }

    /// <summary>Enganche de mouse de una casilla del mosaico. Clase aparte (y no la
    /// UI entera) porque sólo el ícono necesita reaccionar al hover.</summary>
    private class PinHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public AlbumEvidenciasUI album;
        public string texto;

        public void OnPointerEnter(PointerEventData eventData) => album.MostrarTooltip(texto);
        public void OnPointerExit(PointerEventData eventData) => album.MostrarTooltip(MensajePorDefecto);
    }

    private void MostrarTooltip(string texto) => _tooltipTmp.text = texto;

    // ── Construcción de UI en runtime (mismo criterio que ZonePopupUI) ──────
    private void BuildRuntimeUI()
    {
        var canvasGO = new GameObject("AlbumCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelRT = (RectTransform)panelGO.transform;
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(900f, 700f);
        panelGO.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.15f, 0.95f);
        _panel = panelGO.GetComponent<CanvasGroup>();

        var tituloGO = new GameObject("Titulo", typeof(RectTransform));
        tituloGO.transform.SetParent(panelGO.transform, false);
        var tituloRT = (RectTransform)tituloGO.transform;
        tituloRT.anchorMin = new Vector2(0f, 1f);
        tituloRT.anchorMax = new Vector2(1f, 1f);
        tituloRT.pivot = new Vector2(0.5f, 1f);
        tituloRT.sizeDelta = new Vector2(0f, 80f);
        tituloRT.anchoredPosition = new Vector2(0f, -20f);
        var tituloTmp = tituloGO.AddComponent<TextMeshProUGUI>();
        tituloTmp.text = "Álbum de Evidencias";
        tituloTmp.fontSize = 40f;
        tituloTmp.color = Color.white;
        tituloTmp.alignment = TextAlignmentOptions.Center;

        // Franja fija al pie: aquí se lee el nombre/descripción del pin bajo el
        // mouse. Va antes que la grilla para reservarle su alto (100px + margen).
        var tooltipGO = new GameObject("Tooltip", typeof(RectTransform), typeof(Image));
        tooltipGO.transform.SetParent(panelGO.transform, false);
        var tooltipRT = (RectTransform)tooltipGO.transform;
        tooltipRT.anchorMin = new Vector2(0f, 0f);
        tooltipRT.anchorMax = new Vector2(1f, 0f);
        tooltipRT.pivot = new Vector2(0.5f, 0f);
        tooltipRT.sizeDelta = new Vector2(-80f, 100f);
        tooltipRT.anchoredPosition = new Vector2(0f, 20f);
        tooltipGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

        var tooltipTxtGO = new GameObject("Texto", typeof(RectTransform));
        tooltipTxtGO.transform.SetParent(tooltipGO.transform, false);
        var tooltipTxtRT = (RectTransform)tooltipTxtGO.transform;
        tooltipTxtRT.anchorMin = Vector2.zero;
        tooltipTxtRT.anchorMax = Vector2.one;
        tooltipTxtRT.offsetMin = new Vector2(20f, 10f);
        tooltipTxtRT.offsetMax = new Vector2(-20f, -10f);
        _tooltipTmp = tooltipTxtGO.AddComponent<TextMeshProUGUI>();
        _tooltipTmp.fontSize = 26f;
        _tooltipTmp.color = Color.white;
        _tooltipTmp.alignment = TextAlignmentOptions.Center;

        // Grilla del mosaico. Sin ContentSizeFitter ni scroll a propósito: por ahora
        // caben los 3 pines de HDU-11 en el área fija; si el álbum crece (HDU-12),
        // esto es lo primero que habría que envolver en un ScrollRect.
        var contenidoGO = new GameObject("Contenido", typeof(RectTransform), typeof(GridLayoutGroup));
        contenidoGO.transform.SetParent(panelGO.transform, false);
        var contenidoRT = (RectTransform)contenidoGO.transform;
        contenidoRT.anchorMin = new Vector2(0f, 0f);
        contenidoRT.anchorMax = new Vector2(1f, 1f);
        contenidoRT.offsetMin = new Vector2(40f, 140f);
        contenidoRT.offsetMax = new Vector2(-40f, -110f);
        var grid = contenidoGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(96f, 96f);
        grid.spacing = new Vector2(16f, 16f);
        grid.childAlignment = TextAnchor.UpperLeft;
        _contenedorEntradas = contenidoRT;

        // Mensaje de "sin evidencias todavía": ocupa la misma área que la grilla,
        // pero aparte de ella —dentro del grid quedaría metido en una sola celda de
        // 96px y se cortaría igual que pasaba antes con el texto de cada pin.
        _vacioGO = new GameObject("Vacio", typeof(RectTransform));
        _vacioGO.transform.SetParent(panelGO.transform, false);
        var vacioRT = (RectTransform)_vacioGO.transform;
        vacioRT.anchorMin = contenidoRT.anchorMin;
        vacioRT.anchorMax = contenidoRT.anchorMax;
        vacioRT.offsetMin = contenidoRT.offsetMin;
        vacioRT.offsetMax = contenidoRT.offsetMax;
        _vacioTmp = _vacioGO.AddComponent<TextMeshProUGUI>();
        _vacioTmp.fontSize = 28f;
        _vacioTmp.color = Color.white;
        _vacioTmp.alignment = TextAlignmentOptions.Center;
        _vacioGO.SetActive(false);

        var cerrarGO = new GameObject("Cerrar", typeof(RectTransform), typeof(Image), typeof(Button));
        cerrarGO.transform.SetParent(panelGO.transform, false);
        var cerrarRT = (RectTransform)cerrarGO.transform;
        cerrarRT.anchorMin = new Vector2(1f, 1f);
        cerrarRT.anchorMax = new Vector2(1f, 1f);
        cerrarRT.pivot = new Vector2(1f, 1f);
        cerrarRT.sizeDelta = new Vector2(60f, 60f);
        cerrarRT.anchoredPosition = new Vector2(-10f, -10f);
        cerrarGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        var cerrarTxtGO = new GameObject("X", typeof(RectTransform));
        cerrarTxtGO.transform.SetParent(cerrarGO.transform, false);
        var cerrarTxtRT = (RectTransform)cerrarTxtGO.transform;
        cerrarTxtRT.anchorMin = Vector2.zero;
        cerrarTxtRT.anchorMax = Vector2.one;
        cerrarTxtRT.offsetMin = Vector2.zero;
        cerrarTxtRT.offsetMax = Vector2.zero;
        var cerrarTmp = cerrarTxtGO.AddComponent<TextMeshProUGUI>();
        cerrarTmp.text = "X";
        cerrarTmp.fontSize = 32f;
        cerrarTmp.color = Color.white;
        cerrarTmp.alignment = TextAlignmentOptions.Center;

        cerrarGO.GetComponent<Button>().onClick.AddListener(Cerrar);
    }
}
