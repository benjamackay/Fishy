using Fishy.Album;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Col = AlbumUITheme.Colores;
using Med = AlbumUITheme.Medidas;
using Fnt = AlbumUITheme.Fuente;
using Txt = AlbumUITheme.Textos;

/// <summary>
/// Lo que comparten las dos páginas de álbum del celular: el fondo y el borde de
/// una pantalla del teléfono, el área con scroll donde va el contenido, y la
/// tarjeta de detalle que se abre al elegir algo.
///
/// <b>Por qué una página del celular y no una ventana aparte.</b> El álbum se abría
/// antes en su propio canvas (sortingOrder 1300), por encima del menú del celular
/// (1200): el niño/a pulsaba un botón del teléfono y lo que aparecía era otra cosa,
/// de otro tamaño y con otro marco, tapando el teléfono. Ahora las dos páginas viven
/// dentro de <c>Pages</c>, como Misiones o Inventario, así que heredan el marco, la
/// barra de estado con la hora, el título del encabezado y el botón de volver — el
/// álbum deja de ser una ventana encima del teléfono y pasa a ser una pantalla DEL
/// teléfono.
///
/// <b>El aspecto se copia, no se escribe.</b> El fondo y el borde se leen de una
/// página que ya exista en el prefab (ver <see cref="TomarAspectoDe"/>), igual que el
/// botón del inicio se clona de sus vecinos. Así, el día que alguien cambie el arte
/// del teléfono, estas dos páginas lo siguen sin que nadie se acuerde de venir aquí.
/// Si no hubiera de dónde copiar, se cae a los colores del tema y se ve digno igual.
/// </summary>
public abstract class PaginaDeAlbum : MonoBehaviour
{
    /// <summary>Margen del contenido contra el borde de la página. Es el mismo que
    /// usan las páginas del prefab (MisionesScroll va a -32).</summary>
    protected const float MargenPagina = 32f;

    private static Sprite _spriteFondo;
    private static Sprite _spriteBorde;
    private static Color _colorFondo = Col.Fondo;
    private static Color _colorBorde = Col.PestanaActiva;

    private ScrollRect _scroll;
    private TMP_Text _pistaTmp;

    private GameObject _detalle;
    private RectTransform _detalleCuerpo;
    private TMP_Text _detalleTitulo;
    private TMP_Text _detalleSubtitulo;
    private RectTransform _detalleChips;
    private ScrollRect _detalleScroll;

    /// <summary>Donde cada página mete sus cosas. Es el contenido del scroll: tiene
    /// un VerticalLayoutGroup y un ContentSizeFitter, así que basta con colgarle
    /// hijos y crecen hacia abajo.</summary>
    protected RectTransform Contenido { get; private set; }

    /// <summary>Qué escribe la franja del pie cuando no hay nada que decir. Cada
    /// página pone la suya; vacío = sin franja.</summary>
    protected virtual string PistaPorDefecto => "";

    /// <summary>
    /// Alto que la página se reserva arriba, por encima del scroll, para controles
    /// que no deben irse con el contenido. 0 = el scroll ocupa hasta el margen.
    ///
    /// Lo usa la página de evidencias para sus dos pestañas: metidas dentro del
    /// scroll se irían hacia arriba al bajar por la lista, y cambiar de agrupación
    /// obligaría a volver a subir del todo para encontrarlas.
    /// </summary>
    protected virtual float AltoCabeceraPropia => 0f;

    /// <summary>Monta esos controles de arriba. Solo la llama la base si
    /// <see cref="AltoCabeceraPropia"/> es mayor que 0.</summary>
    protected virtual void ConstruirCabecera(RectTransform padre) { }

    /// <summary>Rehace el contenido desde cero. La llama la base al encenderse la
    /// página y cada vez que el álbum cambia.</summary>
    protected abstract void Repoblar();

    // ── Ciclo de vida ─────────────────────────────────────────────────────────

    /// <summary>
    /// Copia el fondo y el borde de una página ya montada en el prefab del celular.
    /// Lo llama <see cref="AccesoAlbumEnElCelular"/> antes de crear las páginas
    /// nuevas; si nunca se llama, se usan los colores del tema.
    /// </summary>
    public static void TomarAspectoDe(GameObject paginaModelo)
    {
        if (paginaModelo == null) return;

        var fondo = paginaModelo.GetComponent<Image>();
        if (fondo != null)
        {
            _spriteFondo = fondo.sprite;
            _colorFondo = fondo.color;
        }

        // El borde es un hijo que va POR ENCIMA del contenido (último en la
        // jerarquía) y solo pinta el marco; se busca por nombre porque es como se
        // llama en todas las páginas del prefab.
        Transform borde = paginaModelo.transform.Find("Borde");
        if (borde == null) return;

        var img = borde.GetComponent<Image>();
        if (img == null) return;

        _spriteBorde = img.sprite;
        _colorBorde = img.color;
    }

    private void Awake()
    {
        Construir();
    }

    /// <summary>
    /// Se repuebla al encenderse, no una sola vez al construirse: el celular apaga y
    /// enciende las páginas al navegar (<c>PhoneMenuNavigation.ShowPage</c>), así que
    /// esto corre cada vez que el niño/a entra, y lo que ve siempre está al día.
    /// </summary>
    private void OnEnable()
    {
        AlbumDeEvidencias.AlCambiar += AlCambiarElAlbum;

        CerrarDetalle();
        Repoblar();
    }

    private void OnDisable()
    {
        AlbumDeEvidencias.AlCambiar -= AlCambiarElAlbum;
    }

    /// <summary>Una evidencia nueva con la página abierta se ve al instante; con la
    /// página apagada no hay nada que repintar —y además OnDisable ya soltó la
    /// suscripción, así que esto ni se llama—.</summary>
    private void AlCambiarElAlbum()
    {
        if (!isActiveAndEnabled) return;
        CerrarDetalle();
        Repoblar();
    }

    // ── Piezas que usan las dos páginas ───────────────────────────────────────

    protected void LimpiarContenido()
    {
        for (int i = Contenido.childCount - 1; i >= 0; i--)
        {
            GameObject hijo = Contenido.GetChild(i).gameObject;
            // Apagarlo y sacarlo del contenedor antes de destruirlo: Destroy es
            // diferido hasta el final del frame y, si no, la fila vieja seguiría
            // contando para el alto del scroll durante este repintado.
            hijo.SetActive(false);
            hijo.transform.SetParent(null, false);
            Destroy(hijo);
        }
    }

    protected void Seccion(string texto)
    {
        TextMeshProUGUI t = FishyUIKit.Texto(Contenido, "Seccion", texto,
            Fnt.Seccion, Col.Texto, TextAlignmentOptions.Left);
        t.font = Fnt.Titulo;
        t.gameObject.AddComponent<LayoutElement>().minHeight = Fnt.Seccion * 1.4f;
    }

    protected void TextoVacio(string texto)
    {
        TextMeshProUGUI t = FishyUIKit.Texto(Contenido, "Vacio", texto,
            Fnt.Detalle, Col.TextoVacio, TextAlignmentOptions.Center);
        t.font = Fnt.Cuerpo;
        t.gameObject.AddComponent<LayoutElement>().minHeight = Fnt.Detalle * 3f;
    }

    protected void MostrarPista(string texto)
    {
        if (_pistaTmp != null) _pistaTmp.text = texto;
    }

    // ── La tarjeta de detalle (HDU-12 CA3) ────────────────────────────────────

    /// <summary>
    /// Enseña el detalle de lo elegido, superpuesto a la página y con su propio
    /// scroll. Sirve igual para una evidencia y para un pin.
    /// </summary>
    protected void MostrarTarjeta(string titulo, string subtitulo,
        System.Collections.Generic.List<string> tematicas, string cita, string cuerpo)
    {
        _detalleTitulo.text = titulo;

        _detalleSubtitulo.text = subtitulo ?? "";
        _detalleSubtitulo.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitulo));

        PintarChips(tematicas);
        PintarCuerpoDelDetalle(cita, cuerpo);

        _detalle.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (_detalleScroll != null) _detalleScroll.verticalNormalizedPosition = 1f;
    }

    protected void CerrarDetalle()
    {
        if (_detalle != null) _detalle.SetActive(false);
    }

    private void PintarChips(System.Collections.Generic.List<string> tematicas)
    {
        for (int i = _detalleChips.childCount - 1; i >= 0; i--)
        {
            GameObject hijo = _detalleChips.GetChild(i).gameObject;
            hijo.SetActive(false);
            hijo.transform.SetParent(null, false);
            Destroy(hijo);
        }

        bool hay = false;
        if (tematicas != null)
        {
            foreach (string etiqueta in tematicas)
            {
                if (string.IsNullOrWhiteSpace(etiqueta)) continue;
                Chip(_detalleChips, Txt.NombreTematica(etiqueta));
                hay = true;
            }
        }

        _detalleChips.gameObject.SetActive(hay);
    }

    private static void Chip(Transform padre, string texto)
    {
        var go = new GameObject("Chip", typeof(RectTransform), typeof(Image),
            typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(padre, false);

        FishyUIKit.FondoRedondeado(go.GetComponent<Image>(), Col.Chip, Med.LadoSprite, 14);

        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 4, 4);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI t = FishyUIKit.Texto(go.transform, "Texto", texto,
            Fnt.Chip, Col.Texto, TextAlignmentOptions.Center);
        t.font = Fnt.Cuerpo;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
    }

    private void PintarCuerpoDelDetalle(string cita, string cuerpo)
    {
        for (int i = _detalleCuerpo.childCount - 1; i >= 0; i--)
        {
            GameObject hijo = _detalleCuerpo.GetChild(i).gameObject;
            hijo.SetActive(false);
            hijo.transform.SetParent(null, false);
            Destroy(hijo);
        }

        if (!string.IsNullOrWhiteSpace(cita))
        {
            TextMeshProUGUI citaTmp = FishyUIKit.Texto(_detalleCuerpo, "Cita", cita,
                Fnt.Detalle, Col.TextoSuave, TextAlignmentOptions.TopLeft);
            citaTmp.font = Fnt.Cuerpo;
            citaTmp.fontStyle = FontStyles.Italic;
        }

        if (!string.IsNullOrWhiteSpace(cuerpo))
        {
            TextMeshProUGUI cuerpoTmp = FishyUIKit.Texto(_detalleCuerpo, "Explicacion", cuerpo,
                Fnt.Detalle, Col.Texto, TextAlignmentOptions.TopLeft);
            cuerpoTmp.font = Fnt.Cuerpo;
        }
    }

    // ── Construcción ──────────────────────────────────────────────────────────

    private void Construir()
    {
        var propio = (RectTransform)transform;

        // Fondo de la pantalla. Con el sprite copiado del prefab si lo hay; si no,
        // el redondeado que arma FishyUIKit, que es de donde salen todos los paneles
        // del juego.
        var fondo = gameObject.GetComponent<Image>();
        if (fondo == null) fondo = gameObject.AddComponent<Image>();
        if (_spriteFondo != null)
        {
            fondo.sprite = _spriteFondo;
            fondo.type = Image.Type.Sliced;
            fondo.color = _colorFondo;
        }
        else
        {
            FishyUIKit.FondoRedondeado(fondo, _colorFondo, Med.LadoSprite, Med.Radio);
        }

        if (AltoCabeceraPropia > 0f) ConstruirCabecera(propio);
        ConstruirScroll(propio);
        ConstruirPista(propio);
        ConstruirDetalle(propio);
        ConstruirBorde(propio);
    }

    /// <summary>
    /// El área con scroll, con el mismo margen que las páginas del prefab.
    ///
    /// Si la página lleva franja de pista al pie, el scroll le deja sitio; si no,
    /// baja hasta el margen. Así la de evidencias aprovecha toda la pantalla y la de
    /// recompensas conserva su línea de "qué pin es este".
    /// </summary>
    private void ConstruirScroll(RectTransform padre)
    {
        var go = new GameObject("Scroll",
            typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        go.transform.SetParent(padre, false);

        float abajo = string.IsNullOrEmpty(PistaPorDefecto)
            ? MargenPagina
            : MargenPagina + Med.AltoPista + 10f;

        float arriba = MargenPagina + AltoCabeceraPropia;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(MargenPagina, abajo);
        rt.offsetMax = new Vector2(-MargenPagina, -arriba);

        FishyUIKit.FondoRedondeado(go.GetComponent<Image>(), Col.Scroll, Med.LadoSprite, Med.Radio);

        _scroll = go.GetComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 30f;

        var contenidoGO = new GameObject("Contenido",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contenidoGO.transform.SetParent(go.transform, false);
        Contenido = (RectTransform)contenidoGO.transform;
        Contenido.anchorMin = new Vector2(0f, 1f);
        Contenido.anchorMax = new Vector2(1f, 1f);
        Contenido.pivot = new Vector2(0.5f, 1f);
        Contenido.offsetMin = Vector2.zero;
        Contenido.offsetMax = Vector2.zero;

        var vlg = contenidoGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = Med.PaddingContenido;
        vlg.spacing = Med.Espaciado;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        contenidoGO.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        _scroll.content = Contenido;
        ConstruirBarra(go.transform, _scroll);
    }

    /// <summary>Barra fina a la derecha, que se esconde sola si todo cabe.</summary>
    protected static void ConstruirBarra(Transform padre, ScrollRect scroll)
    {
        var barraGO = new GameObject("Barra", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barraGO.transform.SetParent(padre, false);
        var barraRT = (RectTransform)barraGO.transform;
        barraRT.anchorMin = new Vector2(1f, 0f);
        barraRT.anchorMax = new Vector2(1f, 1f);
        barraRT.pivot = new Vector2(1f, 0.5f);
        barraRT.sizeDelta = new Vector2(Med.AnchoBarra, 0f);
        FishyUIKit.FondoRedondeado(barraGO.GetComponent<Image>(),
            new Color(Col.Texto.r, Col.Texto.g, Col.Texto.b, 0.15f), Med.LadoSprite, Med.Radio);

        var asaGO = new GameObject("Asa", typeof(RectTransform), typeof(Image));
        asaGO.transform.SetParent(barraGO.transform, false);
        var asaRT = (RectTransform)asaGO.transform;
        asaRT.offsetMin = Vector2.zero;
        asaRT.offsetMax = Vector2.zero;
        var asa = asaGO.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(asa,
            new Color(Col.Texto.r, Col.Texto.g, Col.Texto.b, 0.6f), Med.LadoSprite, Med.Radio);

        var barra = barraGO.GetComponent<Scrollbar>();
        barra.direction = Scrollbar.Direction.BottomToTop;
        barra.handleRect = asaRT;
        barra.targetGraphic = asa;

        scroll.verticalScrollbar = barra;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    /// <summary>Franja fija al pie. Solo la montan las páginas que declaran una
    /// pista por defecto.</summary>
    private void ConstruirPista(RectTransform padre)
    {
        if (string.IsNullOrEmpty(PistaPorDefecto)) return;

        var go = new GameObject("Pista", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(-2f * MargenPagina, Med.AltoPista);
        rt.anchoredPosition = new Vector2(0f, MargenPagina * 0.5f);

        FishyUIKit.FondoRedondeado(go.GetComponent<Image>(),
            new Color(Col.Texto.r, Col.Texto.g, Col.Texto.b, 0.08f), Med.LadoSprite, Med.Radio);

        _pistaTmp = FishyUIKit.Texto(go.transform, "Texto", PistaPorDefecto,
            Fnt.Pista, Col.TextoSuave, TextAlignmentOptions.Center);
        _pistaTmp.font = Fnt.Cuerpo;
        FishyUIKit.Estirar(_pistaTmp.rectTransform, 16f, 8f);
    }

    /// <summary>
    /// La tarjeta de detalle, encima de la página y apagada hasta que se elija algo.
    ///
    /// Va superpuesta y no en una segunda columna: una explicación guiada son dos o
    /// tres frases, más el título del caso, la zona, quién escribió el mensaje y el
    /// mensaje citado; en una columna estrecha y a un cuerpo que un niño/a pueda leer,
    /// eso desborda. Acá el cuerpo tiene su propio scroll y la tarjeta no crece.
    /// </summary>
    private void ConstruirDetalle(RectTransform padre)
    {
        var go = new GameObject("Detalle",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(padre, false);
        _detalle = go;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Med.AnchoDetalle, Med.AltoDetalle);

        FishyUIKit.FondoRedondeado(go.GetComponent<Image>(), Col.Tarjeta, Med.LadoSprite, Med.Radio);

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.padding = Med.PaddingDetalle;
        vlg.spacing = 10f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        _detalleTitulo = FishyUIKit.Texto(go.transform, "Titulo", "", Fnt.TituloGrupo,
            Col.Texto, TextAlignmentOptions.Left);
        _detalleTitulo.font = Fnt.Titulo;

        _detalleSubtitulo = FishyUIKit.Texto(go.transform, "Subtitulo", "", Fnt.Subtitulo,
            Col.TextoSuave, TextAlignmentOptions.Left);
        _detalleSubtitulo.font = Fnt.Cuerpo;

        var chipsGO = new GameObject("Chips", typeof(RectTransform),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        chipsGO.transform.SetParent(go.transform, false);
        _detalleChips = (RectTransform)chipsGO.transform;
        var chipsHlg = chipsGO.GetComponent<HorizontalLayoutGroup>();
        chipsHlg.spacing = 8f;
        chipsHlg.childAlignment = TextAnchor.MiddleLeft;
        chipsHlg.childControlWidth = true;
        chipsHlg.childControlHeight = true;
        chipsHlg.childForceExpandWidth = false;
        chipsHlg.childForceExpandHeight = false;
        chipsGO.GetComponent<LayoutElement>().minHeight = Fnt.Chip * 1.8f;

        var scrollGO = new GameObject("DetalleScroll",
            typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(LayoutElement));
        scrollGO.transform.SetParent(go.transform, false);
        // flexibleHeight = 1: se queda con todo el alto que sobre entre la cabecera de
        // la tarjeta y el botón, sin empujar a ninguno de los dos.
        var hueco = scrollGO.GetComponent<LayoutElement>();
        hueco.flexibleHeight = 1f;
        hueco.minHeight = 120f;

        _detalleScroll = scrollGO.GetComponent<ScrollRect>();
        _detalleScroll.horizontal = false;
        _detalleScroll.vertical = true;
        _detalleScroll.movementType = ScrollRect.MovementType.Clamped;
        _detalleScroll.scrollSensitivity = 30f;

        var cuerpoGO = new GameObject("Cuerpo",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        cuerpoGO.transform.SetParent(scrollGO.transform, false);
        _detalleCuerpo = (RectTransform)cuerpoGO.transform;
        _detalleCuerpo.anchorMin = new Vector2(0f, 1f);
        _detalleCuerpo.anchorMax = new Vector2(1f, 1f);
        _detalleCuerpo.pivot = new Vector2(0.5f, 1f);
        _detalleCuerpo.offsetMin = Vector2.zero;
        _detalleCuerpo.offsetMax = Vector2.zero;

        var cuerpoVlg = cuerpoGO.GetComponent<VerticalLayoutGroup>();
        cuerpoVlg.spacing = 10f;
        cuerpoVlg.childControlWidth = true;
        cuerpoVlg.childControlHeight = true;
        cuerpoVlg.childForceExpandWidth = true;
        cuerpoVlg.childForceExpandHeight = false;
        cuerpoGO.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        _detalleScroll.content = _detalleCuerpo;
        ConstruirBarra(scrollGO.transform, _detalleScroll);

        FishyUIKit.Boton(go.transform, Txt.Volver, Col.PestanaActiva, Fnt.Boton,
            Med.AlturaBoton, CerrarDetalle);

        go.SetActive(false);
    }

    /// <summary>
    /// El marco de la pantalla, copiado del prefab. Va el ÚLTIMO para quedar por
    /// encima de todo lo demás, igual que el "Borde" de las páginas del celular, y
    /// sin recibir clics: es decoración, no un botón.
    /// </summary>
    private void ConstruirBorde(RectTransform padre)
    {
        if (_spriteBorde == null) return;

        var go = new GameObject("Borde", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        FishyUIKit.Estirar((RectTransform)go.transform);

        var img = go.GetComponent<Image>();
        img.sprite = _spriteBorde;
        img.type = Image.Type.Sliced;
        img.color = _colorBorde;
        img.raycastTarget = false;

        go.transform.SetAsLastSibling();
    }
}
