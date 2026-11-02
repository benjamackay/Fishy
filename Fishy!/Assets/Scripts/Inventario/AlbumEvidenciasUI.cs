using System.Collections.Generic;
using Fishy.Album;
using Fishy.Detective;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Col = AlbumUITheme.Colores;
using Med = AlbumUITheme.Medidas;
using Fnt = AlbumUITheme.Fuente;
using Txt = AlbumUITheme.Textos;

/// <summary>
/// HDU-12 — El álbum de evidencias: lo que el niño/a fue descubriendo en el Modo
/// Detective. Se abre haciendo clic en el ítem "Álbum de Evidencias" de la mochila
/// (ver <see cref="InventoryManagerUI"/>), que es su única puerta de entrada.
///
/// Tiene dos secciones. Arriba, los <b>pines</b> de recompensa de cada caso
/// superado (HDU-11), en mosaico, con su nombre al pasar el mouse. Debajo, las
/// <b>evidencias</b>: cada señal de riesgo que se marcó bien, agrupada por caso o
/// por temática (CA2), y con su explicación y el contexto del caso al elegirla
/// (CA3). Los datos no salen de acá: los guarda
/// <see cref="AlbumDeEvidencias"/>, que es quien sabe acumularlos entre intentos.
///
/// Esta clase <b>era</b> el adelanto mínimo de HDU-11: un mosaico de pines sin
/// scroll, sin agrupar y con la paleta propia. De eso quedan el nombre, las dos
/// constantes que usa el resto del juego y la idea de la franja de pistas al pie.
/// </summary>
public class AlbumEvidenciasUI : MonoBehaviour
{
    /// <summary>itemId del ítem "Álbum" en Resources/Items. Compartido con
    /// InventoryManagerUI (para saber qué slot es clicable) y con
    /// DetectiveCaseManager (para saber qué ItemData entregar).</summary>
    public const string ItemIdAlbum = "ITEM_ALBUM_EVIDENCIAS";

    /// <summary>Prefijo que comparten los pines de recompensa. Público: lo usa
    /// también <see cref="InventoryManagerUI"/> para que los pines no se dibujen
    /// en la mochila general — sólo viven aquí, en el álbum.</summary>
    public const string PrefijoPin = "PIN_";

    private static AlbumEvidenciasUI _instance;

    /// <summary>El canvas entero. Es lo que se apaga y se prende al cerrar y abrir:
    /// así se va también el telón, y con el álbum cerrado no queda ni un raycaster
    /// escuchando clics.</summary>
    private GameObject _raiz;

    private ScrollRect _scroll;
    private RectTransform _contenido;
    private TMP_Text _pistaTmp;

    private Image _pestanaCaso;
    private Image _pestanaTematica;

    private GameObject _detalle;
    private RectTransform _detalleCuerpo;
    private TMP_Text _detalleTitulo;
    private TMP_Text _detalleSubtitulo;
    private RectTransform _detalleChips;
    private ScrollRect _detalleScroll;

    /// <summary>Cómo están agrupadas las evidencias ahora mismo (CA2).</summary>
    private bool _porTematica;

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
        _raiz.SetActive(false);

        // Se escucha en Awake y se suelta en OnDestroy, y NO en OnEnable/OnDisable
        // como hacen las páginas del menú (QuestPageUI). Acá lo que se apaga al
        // cerrar el álbum es el canvas, que es un hijo: este componente nunca se
        // desactiva, así que esos dos callbacks solo se dispararían una vez y la
        // suscripción quedaría muerta.
        AlbumDeEvidencias.AlCambiar += AlCambiarElAlbum;
    }

    private void OnDestroy()
    {
        AlbumDeEvidencias.AlCambiar -= AlCambiarElAlbum;
        if (_instance == this) _instance = null;
    }

    /// <summary>Una evidencia nueva con el álbum abierto se ve al instante; con el
    /// álbum cerrado no hay nada que repintar.</summary>
    private void AlCambiarElAlbum()
    {
        if (_raiz == null || !_raiz.activeSelf) return;
        Repoblar();
    }

    private void Abrir()
    {
        // Las evidencias que vinieron del servidor traen solo ids: el backend no
        // guarda el texto del mensaje ni su explicación. Se completan con los casos
        // que viajan empaquetados en el juego, sin pedirle nada a la red.
        if (AlbumDeEvidencias.CantidadIncompletas > 0)
            AlbumDetectivePuente.ConciliarLoQueFalte();

        CerrarDetalle();
        Repoblar();
        _raiz.SetActive(true);

        // Siempre se abre arriba, como las páginas del celular.
        if (_scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = 1f;
        }
    }

    private void Cerrar() => _raiz.SetActive(false);

    // ── Contenido ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Rehace la lista entera desde el estado del álbum y de la mochila.
    ///
    /// <b>Los tres estados vacíos</b> (CA4), que no son el mismo mensaje:
    /// sin pines y sin evidencias, el álbum está recién empezado y hay que decir
    /// cómo se llena; con pines y sin evidencias, es alguien que cerró casos antes
    /// de que existiera el registro de señales y sus evidencias de entonces no se
    /// pueden reconstruir; y con evidencias pero sin pines —que pasa de verdad,
    /// porque una señal de cuatro deja evidencia y no alcanza para el pin— lo que
    /// falta es solo el mosaico.
    /// </summary>
    private void Repoblar()
    {
        LimpiarContenido();
        MostrarPista(Txt.PistaDefecto);

        List<Item> pines = PinesDeLaMochila();
        List<GrupoAlbum> grupos = _porTematica
            ? AlbumDeEvidencias.PorTematica()
            : AlbumDeEvidencias.PorCaso();

        PintarPestanas();

        // Cuál de los tres estados vacíos es lo decide el núcleo del álbum, que es
        // donde se puede comprobar con NUnit en PlayMode; acá solo se eligen las
        // palabras. Ver EstadoVacioAlbum.
        EstadoVacio estado = EstadoVacioAlbum.Decidir(pines.Count, grupos.Count);

        if (!EstadoVacioAlbum.MuestraSecciones(estado))
        {
            TextoVacio(Txt.VacioTotal);
            return;
        }

        Seccion(Txt.SeccionPines);
        if (estado == EstadoVacio.FaltanPines) TextoVacio(Txt.SinPines);
        else MosaicoDePines(pines);

        Seccion(Txt.SeccionEvidencias);
        if (estado == EstadoVacio.FaltanEvidencias) TextoVacio(Txt.VacioSoloPines);
        else foreach (GrupoAlbum grupo in grupos) TarjetaDeGrupo(grupo);
    }

    private void LimpiarContenido()
    {
        for (int i = _contenido.childCount - 1; i >= 0; i--)
        {
            GameObject hijo = _contenido.GetChild(i).gameObject;
            // Apagarlo y sacarlo del contenedor antes de destruirlo: Destroy es
            // diferido hasta el final del frame y, si no, la fila vieja seguiría
            // contando para el alto del scroll durante este repintado.
            hijo.SetActive(false);
            hijo.transform.SetParent(null, false);
            Destroy(hijo);
        }
    }

    private static List<Item> PinesDeLaMochila()
    {
        var pines = new List<Item>();
        foreach (Item item in InventoryManager.Instance.inventory)
        {
            if (item.itemData != null && !string.IsNullOrEmpty(item.itemData.itemId) &&
                item.itemData.itemId.StartsWith(PrefijoPin))
                pines.Add(item);
        }
        return pines;
    }

    private void CambiarAgrupacion(bool porTematica)
    {
        if (_porTematica == porTematica) return;

        _porTematica = porTematica;
        CerrarDetalle();
        Repoblar();
    }

    private void PintarPestanas()
    {
        if (_pestanaCaso != null)
            _pestanaCaso.color = _porTematica ? Col.Pestana : Col.PestanaActiva;
        if (_pestanaTematica != null)
            _pestanaTematica.color = _porTematica ? Col.PestanaActiva : Col.Pestana;
    }

    // ── Piezas de la lista ────────────────────────────────────────────────────

    private void Seccion(string texto)
    {
        TextMeshProUGUI t = FishyUIKit.Texto(_contenido, "Seccion", texto,
            Fnt.Seccion, Col.Texto, TextAlignmentOptions.Left);
        t.font = Fnt.Titulo;
        t.gameObject.AddComponent<LayoutElement>().minHeight = Fnt.Seccion * 1.4f;
    }

    private void TextoVacio(string texto)
    {
        TextMeshProUGUI t = FishyUIKit.Texto(_contenido, "Vacio", texto,
            Fnt.Detalle, Col.TextoVacio, TextAlignmentOptions.Center);
        t.font = Fnt.Cuerpo;
        t.gameObject.AddComponent<LayoutElement>().minHeight = Fnt.Detalle * 3f;
    }

    /// <summary>El mosaico de pines: solo el ícono. El nombre y la descripción no
    /// entran en la casilla sin desbordarse, así que se leen en la franja del pie al
    /// pasar el mouse, o en el detalle al hacer clic — que es la única forma de
    /// consultarlos en una pantalla táctil.</summary>
    private void MosaicoDePines(List<Item> pines)
    {
        var go = new GameObject("Pines", typeof(RectTransform), typeof(GridLayoutGroup));
        go.transform.SetParent(_contenido, false);

        var grid = go.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(Med.LadoPin, Med.LadoPin);
        grid.spacing = new Vector2(Med.EspacioPin, Med.EspacioPin);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Med.ColumnasPines;

        foreach (Item item in pines) CasillaDePin(go.transform, item);
    }

    private void CasillaDePin(Transform padre, Item item)
    {
        var casilla = new GameObject("Pin", typeof(RectTransform), typeof(Image), typeof(Button));
        casilla.transform.SetParent(padre, false);

        var fondo = casilla.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(fondo, Col.RanuraPin, Med.LadoSprite, Med.Radio);

        var iconoGO = new GameObject("Icono", typeof(RectTransform), typeof(Image));
        iconoGO.transform.SetParent(casilla.transform, false);
        FishyUIKit.Estirar((RectTransform)iconoGO.transform, 8f, 8f);
        var icono = iconoGO.GetComponent<Image>();
        icono.sprite = item.itemData.itemIcon;
        icono.preserveAspect = true;
        icono.raycastTarget = false;
        // Sin sprite se deja un cuadro tenue en vez de nada: así se ve que hay un pin
        // ahí —y se puede seguir pasando el mouse o pulsarlo para saber cuál es—
        // aunque el ItemData todavía no tenga el ícono asignado.
        icono.color = item.itemData.itemIcon != null ? Color.white : new Color(1f, 1f, 1f, 0.18f);

        string nombre = item.itemData.itemName;
        string descripcion = item.itemData.itemDescription;

        string pista = nombre;
        if (!string.IsNullOrEmpty(descripcion)) pista += $" — {descripcion}";

        var hover = casilla.AddComponent<PinHover>();
        hover.album = this;
        hover.texto = pista;

        casilla.GetComponent<Button>().onClick.AddListener(
            () => MostrarTarjeta(nombre, Txt.SeccionPines, null, null, descripcion));
    }

    /// <summary>Una tarjeta por grupo: el caso (o la temática), y debajo una fila por
    /// evidencia.</summary>
    private void TarjetaDeGrupo(GrupoAlbum grupo)
    {
        var tarjeta = new GameObject($"Grupo_{grupo.clave}",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        tarjeta.transform.SetParent(_contenido, false);

        FishyUIKit.FondoRedondeado(tarjeta.GetComponent<Image>(), Col.Tarjeta,
            Med.LadoSprite, Med.Radio);

        var vlg = tarjeta.GetComponent<VerticalLayoutGroup>();
        vlg.padding = Med.PaddingTarjeta;
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Agrupando por temática el rótulo viene con la etiqueta cruda del banco
        // ("retos_virales"): el núcleo del álbum agrupa, pero no puede nombrar —el
        // diccionario de nombres vive de este lado—. Ver GrupoAlbum.
        string titulo = _porTematica ? Txt.NombreTematica(grupo.clave) : grupo.titulo;

        TextMeshProUGUI tituloTmp = FishyUIKit.Texto(tarjeta.transform, "Titulo", titulo,
            Fnt.TituloGrupo, Col.Texto, TextAlignmentOptions.Left);
        tituloTmp.font = Fnt.Titulo;
        tituloTmp.gameObject.AddComponent<LayoutElement>().minHeight = Fnt.TituloGrupo * 1.3f;

        if (!string.IsNullOrWhiteSpace(grupo.subtitulo))
        {
            TextMeshProUGUI sub = FishyUIKit.Texto(tarjeta.transform, "Subtitulo", grupo.subtitulo,
                Fnt.Subtitulo, Col.TextoSuave, TextAlignmentOptions.Left);
            sub.font = Fnt.Cuerpo;
            sub.gameObject.AddComponent<LayoutElement>().minHeight = Fnt.Subtitulo * 1.3f;
        }

        foreach (EvidenciaAlbum evidencia in grupo.evidencias)
            FilaDeEvidencia(tarjeta.transform, evidencia);
    }

    private void FilaDeEvidencia(Transform padre, EvidenciaAlbum evidencia)
    {
        var fila = new GameObject($"Evidencia_{evidencia.mensajeId}",
            typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement), typeof(VerticalLayoutGroup));
        fila.transform.SetParent(padre, false);

        FishyUIKit.FondoRedondeado(fila.GetComponent<Image>(), Col.Fila, Med.LadoSprite, Med.Radio);
        fila.GetComponent<LayoutElement>().minHeight = Med.AlturaFila;

        // El grupo mide el texto y le pide ese alto a la tarjeta: una señal es una
        // frase entera y a este ancho envuelve a dos líneas. Con alto fijo se
        // desbordaba por encima de la fila siguiente.
        var vlg = fila.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        TextMeshProUGUI t = FishyUIKit.Texto(fila.transform, "Texto", ResumenDe(evidencia),
            Fnt.Fila, Col.Texto, TextAlignmentOptions.Left);
        t.font = Fnt.Cuerpo;
        t.raycastTarget = false;

        fila.GetComponent<Button>().onClick.AddListener(() => MostrarEvidencia(evidencia));
    }

    /// <summary>Lo que se lee en la fila. De una evidencia que todavía es un esqueleto
    /// —vino del servidor y no se pudo completar— se enseña el id del mensaje: es
    /// poco, pero deja ver que la evidencia está ahí.</summary>
    private static string ResumenDe(EvidenciaAlbum evidencia)
    {
        if (!evidencia.EstaCompleta) return evidencia.mensajeId;

        string texto = Recortar(evidencia.texto, 90);
        return string.IsNullOrWhiteSpace(evidencia.autor) ? texto : $"{evidencia.autor}: {texto}";
    }

    private static string Recortar(string texto, int largo)
    {
        if (string.IsNullOrEmpty(texto) || texto.Length <= largo) return texto;
        return texto.Substring(0, largo).TrimEnd() + "…";
    }

    // ── Detalle (CA3) ─────────────────────────────────────────────────────────

    /// <summary>
    /// CA3 — El detalle de una evidencia: la explicación de por qué ese mensaje era
    /// una señal, y el contexto del caso (de qué caso es, en qué zona pasó, quién lo
    /// escribió y qué decía).
    /// </summary>
    private void MostrarEvidencia(EvidenciaAlbum evidencia)
    {
        string subtitulo = evidencia.casoZona;
        if (!string.IsNullOrWhiteSpace(evidencia.autor))
            subtitulo = string.IsNullOrWhiteSpace(subtitulo)
                ? $"Lo escribió {evidencia.autor}"
                : $"{evidencia.casoZona} · lo escribió {evidencia.autor}";

        if (!evidencia.EstaCompleta)
        {
            MostrarTarjeta(evidencia.TituloVisible, subtitulo, evidencia.etiquetas,
                null, Txt.EvidenciaSinDatos);
            return;
        }

        MostrarTarjeta(evidencia.TituloVisible, subtitulo, evidencia.etiquetas,
            $"«{evidencia.texto}»", evidencia.explicacion);
    }

    /// <summary>La tarjeta de detalle, que sirve igual para una evidencia y para un
    /// pin. Se dibuja encima del panel y se cierra con "Volver".</summary>
    private void MostrarTarjeta(string titulo, string subtitulo, List<string> tematicas,
        string cita, string cuerpo)
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

    private void CerrarDetalle()
    {
        if (_detalle != null) _detalle.SetActive(false);
    }

    private void PintarChips(List<string> tematicas)
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

    /// <summary>Enganche de mouse de una casilla del mosaico. Clase aparte (y no la
    /// UI entera) porque sólo el ícono necesita reaccionar al hover.</summary>
    private class PinHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public AlbumEvidenciasUI album;
        public string texto;

        public void OnPointerEnter(PointerEventData eventData) => album.MostrarPista(texto);
        public void OnPointerExit(PointerEventData eventData) => album.MostrarPista(Txt.PistaDefecto);
    }

    private void MostrarPista(string texto)
    {
        if (_pistaTmp != null) _pistaTmp.text = texto;
    }

    // ── Construcción de UI en runtime (mismo criterio que DetectiveUI) ────────

    private void BuildRuntimeUI()
    {
        var canvasGO = new GameObject("AlbumCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        _raiz = canvasGO;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Por encima del panel del menú (1200): el álbum se abre desde la mochila y
        // tiene que verse delante de ella. MenuController ya documenta este 1300.
        canvas.sortingOrder = 1300;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        ConstruirTelon(canvasGO.transform);
        Transform panel = ConstruirPanel(canvasGO.transform);
        ConstruirCabecera(panel);
        ConstruirScroll(panel);
        ConstruirPista(panel);
        ConstruirDetalle(panel);
    }

    /// <summary>Telón de fondo. Además de oscurecer lo de atrás, se come los clics de
    /// fuera del panel para cerrar el álbum sin tener que apuntar a la aspa.</summary>
    private void ConstruirTelon(Transform padre)
    {
        var go = new GameObject("Telon", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        FishyUIKit.Estirar((RectTransform)go.transform);

        go.GetComponent<Image>().color = Col.Telon;
        go.GetComponent<Button>().onClick.AddListener(Cerrar);
    }

    private Transform ConstruirPanel(Transform padre)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(padre, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Med.AnchoPanel, Med.AltoPanel);

        FishyUIKit.FondoRedondeado(go.GetComponent<Image>(), Col.Fondo, Med.LadoSprite, Med.Radio);

        return go.transform;
    }

    private void ConstruirCabecera(Transform panel)
    {
        TextMeshProUGUI titulo = FishyUIKit.Texto(panel, "Titulo", Txt.Titulo,
            Fnt.TituloPanel, Col.Texto, TextAlignmentOptions.Center);
        titulo.font = Fnt.Titulo;
        var tituloRT = titulo.rectTransform;
        tituloRT.anchorMin = new Vector2(0f, 1f);
        tituloRT.anchorMax = new Vector2(1f, 1f);
        tituloRT.pivot = new Vector2(0.5f, 1f);
        tituloRT.sizeDelta = new Vector2(-2f * (Med.LadoCerrar + 16f),
            Med.AltoCabecera - Med.MargenCabecera);
        tituloRT.anchoredPosition = new Vector2(0f, -Med.MargenCabecera);

        // Las dos formas de agrupar (CA2), una al lado de la otra. La elegida se
        // distingue por el color del fondo; lo pinta PintarPestanas.
        var pestanasGO = new GameObject("Pestanas", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        pestanasGO.transform.SetParent(panel, false);
        var pestanasRT = (RectTransform)pestanasGO.transform;
        pestanasRT.anchorMin = new Vector2(0.5f, 1f);
        pestanasRT.anchorMax = new Vector2(0.5f, 1f);
        pestanasRT.pivot = new Vector2(0.5f, 1f);
        pestanasRT.sizeDelta = new Vector2(Med.AnchoPestanas, Med.AltoPestanas);
        pestanasRT.anchoredPosition = new Vector2(0f, -Med.AltoCabecera);

        var hlg = pestanasGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        Button porCaso = FishyUIKit.Boton(pestanasGO.transform, Txt.PorCaso, Col.Pestana,
            Fnt.Boton, 40f, () => CambiarAgrupacion(false));
        Button porTematica = FishyUIKit.Boton(pestanasGO.transform, Txt.PorTematica, Col.Pestana,
            Fnt.Boton, 40f, () => CambiarAgrupacion(true));
        _pestanaCaso = porCaso.GetComponent<Image>();
        _pestanaTematica = porTematica.GetComponent<Image>();
        PintarPestanas();

        // Cerrar: el aspa va dibujada y no como carácter, porque Mango no tiene
        // glifo de ✕ y la X de texto sale fina y descentrada. Ver FishyUIKit.Aspa.
        var cerrarGO = new GameObject("Cerrar", typeof(RectTransform), typeof(Image), typeof(Button));
        cerrarGO.transform.SetParent(panel, false);
        var cerrarRT = (RectTransform)cerrarGO.transform;
        cerrarRT.anchorMin = new Vector2(1f, 1f);
        cerrarRT.anchorMax = new Vector2(1f, 1f);
        cerrarRT.pivot = new Vector2(1f, 1f);
        cerrarRT.sizeDelta = new Vector2(Med.LadoCerrar, Med.LadoCerrar);
        cerrarRT.anchoredPosition = new Vector2(-Med.MargenCabecera, -Med.MargenCabecera);

        FishyUIKit.FondoRedondeado(cerrarGO.GetComponent<Image>(), Col.Cerrar,
            Med.LadoSprite, Med.Radio);
        FishyUIKit.Aspa(cerrarGO.transform, Col.Aspa, Med.LargoAspa, Med.GrosorAspa);
        cerrarGO.GetComponent<Button>().onClick.AddListener(Cerrar);
    }

    /// <summary>
    /// El área con scroll, que es lo que esta pantalla no tenía.
    ///
    /// Con tres pines cabían en un área fija; con una evidencia por señal bien
    /// marcada —doce en los tres casos del banco, repartidas en tarjetas por caso— no
    /// caben, y sin scroll lo de abajo quedaba fuera del panel sin ninguna forma de
    /// llegar. La barra y el contenido van como en DetectiveUI.
    /// </summary>
    private void ConstruirScroll(Transform panel)
    {
        var go = new GameObject("Scroll",
            typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        go.transform.SetParent(panel, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        float arriba = Med.AltoCabecera + Med.AltoPestanas + 10f;
        rt.offsetMin = new Vector2(Med.MargenPanel, Med.MargenPanel + Med.AltoPista + 10f);
        rt.offsetMax = new Vector2(-Med.MargenPanel, -arriba);

        FishyUIKit.FondoRedondeado(go.GetComponent<Image>(), Col.Scroll, Med.LadoSprite, Med.Radio);

        _scroll = go.GetComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 30f;

        var contenidoGO = new GameObject("Contenido",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contenidoGO.transform.SetParent(go.transform, false);
        _contenido = (RectTransform)contenidoGO.transform;
        _contenido.anchorMin = new Vector2(0f, 1f);
        _contenido.anchorMax = new Vector2(1f, 1f);
        _contenido.pivot = new Vector2(0.5f, 1f);
        _contenido.offsetMin = Vector2.zero;
        _contenido.offsetMax = Vector2.zero;

        var vlg = contenidoGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = Med.PaddingContenido;
        vlg.spacing = Med.Espaciado;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        contenidoGO.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        _scroll.content = _contenido;
        ConstruirBarra(go.transform, _scroll);
    }

    /// <summary>Barra fina a la derecha, que se esconde sola si todo cabe.</summary>
    private static void ConstruirBarra(Transform padre, ScrollRect scroll)
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

    /// <summary>Franja fija al pie: acá se lee el nombre del pin bajo el mouse.</summary>
    private void ConstruirPista(Transform panel)
    {
        var go = new GameObject("Pista", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panel, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(-2f * Med.MargenPanel, Med.AltoPista);
        rt.anchoredPosition = new Vector2(0f, Med.MargenPanel * 0.5f);

        FishyUIKit.FondoRedondeado(go.GetComponent<Image>(),
            new Color(Col.Texto.r, Col.Texto.g, Col.Texto.b, 0.08f), Med.LadoSprite, Med.Radio);

        _pistaTmp = FishyUIKit.Texto(go.transform, "Texto", Txt.PistaDefecto,
            Fnt.Pista, Col.TextoSuave, TextAlignmentOptions.Center);
        _pistaTmp.font = Fnt.Cuerpo;
        FishyUIKit.Estirar(_pistaTmp.rectTransform, 16f, 8f);
    }

    /// <summary>
    /// La tarjeta de detalle (CA3), encima del panel y apagada hasta que se elija algo.
    ///
    /// Va superpuesta y no en una segunda columna: una explicación guiada son dos o
    /// tres frases, más el título del caso, la zona, quién escribió el mensaje y el
    /// mensaje citado. En la columna de 300 px que quedaría al partir el panel, y a un
    /// cuerpo que un niño/a pueda leer, eso desborda — el mismo problema que tenía la
    /// tarjeta de resultado del Modo Detective antes de ponerle scroll. Acá el cuerpo
    /// tiene su propio scroll y la tarjeta no crece.
    /// </summary>
    private void ConstruirDetalle(Transform panel)
    {
        var go = new GameObject("Detalle",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(panel, false);
        _detalle = go;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
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
}
