using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    private static AlbumEvidenciasUI _instance;

    private CanvasGroup _panel;
    private RectTransform _contenedorEntradas;

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

        var pines = new List<Item>();
        foreach (var item in InventoryManager.Instance.inventory)
        {
            if (item.itemData != null && !string.IsNullOrEmpty(item.itemData.itemId) &&
                item.itemData.itemId.StartsWith(PrefijoPin))
                pines.Add(item);
        }

        if (pines.Count == 0)
        {
            CrearEntradaTexto(MensajeVacio);
            return;
        }

        foreach (var item in pines)
            CrearEntradaPin(item);
    }

    private void CrearEntradaTexto(string texto)
    {
        var entrada = new GameObject("Entrada", typeof(RectTransform), typeof(LayoutElement));
        entrada.transform.SetParent(_contenedorEntradas, false);

        var tmp = entrada.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 28f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.TopLeft;
    }

    /// <summary>Ícono a la izquierda + nombre/descripción a la derecha. Es la única
    /// forma en la que un pin es visible en el juego (ver InventoryManagerUI, que
    /// los excluye de la mochila general a propósito).</summary>
    private void CrearEntradaPin(Item item)
    {
        var entrada = new GameObject("Entrada", typeof(RectTransform), typeof(LayoutElement),
            typeof(HorizontalLayoutGroup));
        entrada.transform.SetParent(_contenedorEntradas, false);

        var layout = entrada.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var iconoGO = new GameObject("Icono", typeof(RectTransform), typeof(Image),
            typeof(LayoutElement));
        iconoGO.transform.SetParent(entrada.transform, false);
        var iconoLE = iconoGO.GetComponent<LayoutElement>();
        iconoLE.preferredWidth = 72f;
        iconoLE.preferredHeight = 72f;
        var iconoImg = iconoGO.GetComponent<Image>();
        iconoImg.sprite = item.itemData.itemIcon;
        iconoImg.preserveAspect = true;
        iconoImg.enabled = item.itemData.itemIcon != null;

        var textoGO = new GameObject("Texto", typeof(RectTransform), typeof(LayoutElement));
        textoGO.transform.SetParent(entrada.transform, false);
        textoGO.GetComponent<LayoutElement>().flexibleWidth = 1f;

        string texto = item.itemData.itemName;
        if (!string.IsNullOrEmpty(item.itemData.itemDescription))
            texto += $"\n{item.itemData.itemDescription}";

        var tmp = textoGO.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 28f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
    }

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

        var contenidoGO = new GameObject("Contenido",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contenidoGO.transform.SetParent(panelGO.transform, false);
        var contenidoRT = (RectTransform)contenidoGO.transform;
        contenidoRT.anchorMin = new Vector2(0f, 0f);
        contenidoRT.anchorMax = new Vector2(1f, 1f);
        contenidoRT.offsetMin = new Vector2(40f, 100f);
        contenidoRT.offsetMax = new Vector2(-40f, -110f);
        var vlg = contenidoGO.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 16f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        contenidoGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _contenedorEntradas = contenidoRT;

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
