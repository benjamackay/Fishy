using Fishy.Pausa;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HDU-11 — El álbum de pines: un libro abierto con un recuadro por cada pin que se
/// puede ganar en el Modo Detective. Los ganados se pegan en su recuadro; los que
/// faltan dejan el recuadro vacío con un «?». Al pulsar un pin se lee qué es.
///
/// <b>Una pantalla aparte y no una página del celular.</b> El dibujo del libro es
/// ancho (3:2) y dentro de la pantalla del teléfono los pines quedaban del tamaño de
/// un botón. Se abre encima del celular (1300, sobre su 1200), desde el ítem «Álbum»
/// de la mochila, y al cerrarlo se vuelve al celular tal como estaba.
///
/// <b>Los pines no los guarda el álbum</b>: son ítems de la mochila que entrega
/// <c>DetectiveCaseManager</c>. Acá solo se miran, así que el álbum siempre dice la
/// verdad aunque se abra con la partida recién cargada.
/// </summary>
public class AlbumDePinesUI : MonoBehaviour
{
    /// <summary>Dónde está el dibujo del libro. En Resources porque se carga desde
    /// código.</summary>
    public const string RutaImagen = "Album/album_interfaz";

    /// <summary>Por encima del celular (1200) y por debajo de la cinemática (8000),
    /// el zoom del teléfono (9000) y la pausa (9500).</summary>
    public const int OrdenDeDibujo = 1300;

    private const string NombreBloqueo = "Álbum de pines";

    /// <summary>
    /// Un recuadro del libro: qué pin va ahí y dónde está, en fracciones del dibujo
    /// (0,0 = arriba a la izquierda), medidas sobre los recuadros punteados de
    /// <c>album_interfaz.png</c>. El orden es el del dibujo de referencia: vigía
    /// arriba a la izquierda, testigo abajo, analista en la página derecha.
    /// </summary>
    private struct Recuadro
    {
        public string PinId;
        public Vector2 Centro;
        public Vector2 Tamano;

        public Recuadro(string pinId, float cx, float cy, float ancho, float alto)
        {
            PinId = pinId;
            Centro = new Vector2(cx, cy);
            Tamano = new Vector2(ancho, alto);
        }
    }

    private static readonly Recuadro[] Recuadros =
    {
        new Recuadro("PIN_VIGIA_SILENCIOSO",    0.252f, 0.383f, 0.120f, 0.223f),
        new Recuadro("PIN_TESTIGO_SOLIDARIO",   0.399f, 0.656f, 0.136f, 0.222f),
        new Recuadro("PIN_ANALISTA_CORRIENTES", 0.592f, 0.400f, 0.130f, 0.196f),
    };

    private const string TituloBloqueado = "Pin por descubrir";
    private const string TextoBloqueado =
        "Todavía no lo tienes. Se gana en el Modo Detective, acertando más de la mitad " +
        "de las señales de un caso.";

    private static AlbumDePinesUI _instancia;

    private GameObject _raiz;
    private readonly Image[] _iconos = new Image[Recuadros.Length];
    private readonly GameObject[] _incognitas = new GameObject[Recuadros.Length];

    private GameObject _detalle;
    private Image _detalleIcono;
    private TMP_Text _detalleTitulo;
    private TMP_Text _detalleTexto;

    private MenuController _celular;
    private InventoryManager _suscritoA;
    private int _frameCerrado = -1;

    public static bool Abierto => _instancia != null && _instancia._raiz != null &&
                                  _instancia._raiz.activeSelf;

    /// <summary>Para las pruebas: la pantalla, si ya se construyó.</summary>
    public static AlbumDePinesUI Instancia => _instancia;

    public bool DetalleAbierto => _detalle != null && _detalle.activeSelf;

    // ── Abrir y cerrar ────────────────────────────────────────────────────────

    public static void Abrir()
    {
        if (_instancia == null)
        {
            _instancia = new GameObject("AlbumDePines").AddComponent<AlbumDePinesUI>();
            _instancia.Construir();
        }

        // Esc cierra el álbum y no tiene que abrir además la pausa en el mismo frame.
        EstadoDePausa.RegistrarBloqueo(NombreBloqueo,
            () => Abierto || (_instancia != null && Time.frameCount == _instancia._frameCerrado));

        _instancia.Mostrar();
    }

    public static void Cerrar()
    {
        if (_instancia != null) _instancia.Ocultar();
    }

    private void Mostrar()
    {
        _celular = FindAnyObjectByType<MenuController>();

        if (_suscritoA == null && InventoryManager.Instance != null)
        {
            _suscritoA = InventoryManager.Instance;
            _suscritoA.OnInventoryChanged += Repintar;
        }

        CerrarDetalle();
        Repintar();
        _raiz.SetActive(true);
    }

    private void Ocultar()
    {
        if (_raiz == null || !_raiz.activeSelf) return;

        CerrarDetalle();
        _raiz.SetActive(false);
        _frameCerrado = Time.frameCount;

        if (_suscritoA != null) _suscritoA.OnInventoryChanged -= Repintar;
        _suscritoA = null;
    }

    private void OnDestroy()
    {
        if (_suscritoA != null) _suscritoA.OnInventoryChanged -= Repintar;
        if (_instancia == this) _instancia = null;
    }

    private void Update()
    {
        if (!Abierto) return;

        // Si el celular se guardó (Tab) el álbum se va con él: quedarse flotando
        // sobre el mundo sin el teléfono debajo no tiene sentido.
        if (_celular != null && !_celular.EstaAbierto) { Ocultar(); return; }

        Keyboard teclado = Keyboard.current;
        if (teclado == null || !teclado.escapeKey.wasPressedThisFrame) return;

        if (DetalleAbierto) CerrarDetalle();
        else Ocultar();
    }

    // ── Contenido ─────────────────────────────────────────────────────────────

    /// <summary>Pone en cada recuadro el pin si se tiene, o el «?» si no.</summary>
    private void Repintar()
    {
        for (int i = 0; i < Recuadros.Length; i++)
        {
            ItemData pin = CatalogoItems.Buscar(Recuadros[i].PinId);
            bool loTiene = Tiene(pin);

            _iconos[i].sprite = loTiene ? pin.itemIcon : null;
            _iconos[i].enabled = loTiene && pin.itemIcon != null;
            _incognitas[i].SetActive(!loTiene);
        }
    }

    private static bool Tiene(ItemData pin)
    {
        return pin != null && InventoryManager.Instance != null &&
               InventoryManager.Instance.GetQuantity(pin) > 0;
    }

    /// <summary>Lo que hace pulsar un recuadro: enseñar el pin, o contar cómo se
    /// consigue si todavía no se tiene.</summary>
    public void Elegir(int indice)
    {
        if (indice < 0 || indice >= Recuadros.Length) return;

        ItemData pin = CatalogoItems.Buscar(Recuadros[indice].PinId);

        if (Tiene(pin))
        {
            _detalleTitulo.text = pin.itemName;
            _detalleTexto.text = pin.itemDescription;
            _detalleIcono.sprite = pin.itemIcon;
            _detalleIcono.gameObject.SetActive(pin.itemIcon != null);
        }
        else
        {
            _detalleTitulo.text = TituloBloqueado;
            _detalleTexto.text = TextoBloqueado;
            _detalleIcono.gameObject.SetActive(false);
        }

        _detalle.SetActive(true);
    }

    private void CerrarDetalle()
    {
        if (_detalle != null) _detalle.SetActive(false);
    }

    // ── Construcción ──────────────────────────────────────────────────────────

    private void Construir()
    {
        UiBootstrap.EnsureEventSystem();

        var canvasGO = new GameObject("AlbumDePinesCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        _raiz = canvasGO;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OrdenDeDibujo;

        var escala = canvasGO.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;

        // El velo: oscurece el celular de debajo, y pulsarlo cierra el álbum.
        var velo = new GameObject("Velo", typeof(RectTransform), typeof(Image), typeof(Button));
        velo.transform.SetParent(canvasGO.transform, false);
        FishyUIKit.Estirar((RectTransform)velo.transform);
        velo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        SinNavegacion(velo.GetComponent<Button>()).onClick.AddListener(Ocultar);

        ConstruirLibro(canvasGO.transform);

        Button volver = FishyUIKit.Boton(canvasGO.transform, "Volver", Paleta.Verde, 34f, 72f, Ocultar);
        SinNavegacion(volver);
        var rtVolver = (RectTransform)volver.transform;
        rtVolver.anchorMin = rtVolver.anchorMax = rtVolver.pivot = new Vector2(0.5f, 0f);
        rtVolver.sizeDelta = new Vector2(280f, 72f);
        rtVolver.anchoredPosition = new Vector2(0f, 28f);

        ConstruirDetalle(canvasGO.transform);

        canvasGO.SetActive(false);
    }

    private void ConstruirLibro(Transform padre)
    {
        Sprite dibujo = CargarDibujo();

        var libroGO = new GameObject("Libro", typeof(RectTransform), typeof(Image));
        libroGO.transform.SetParent(padre, false);

        // Se dimensiona por el alto y el ancho sale de la proporción del dibujo, para
        // que las fracciones de los recuadros caigan donde están los punteados.
        float alto = 880f;
        float proporcion = dibujo != null ? dibujo.rect.width / dibujo.rect.height : 2116f / 1347f;

        var rt = (RectTransform)libroGO.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(alto * proporcion, alto);
        rt.anchoredPosition = new Vector2(0f, 45f);

        // El libro se come los clics: pulsar sobre él no cierra el álbum.
        var imagen = libroGO.GetComponent<Image>();
        imagen.sprite = dibujo;
        imagen.color = dibujo != null ? Color.white : Paleta.Crema;

        for (int i = 0; i < Recuadros.Length; i++)
            ConstruirRecuadro(rt, i);
    }

    private static Sprite CargarDibujo()
    {
        // LoadAll y no Load: la imagen está importada como hoja de sprites (Multiple)
        // con uno solo dentro, recortado al libro.
        Sprite[] sprites = Resources.LoadAll<Sprite>(RutaImagen);
        if (sprites != null && sprites.Length > 0) return sprites[0];

        Debug.LogWarning($"[Álbum] Falta el dibujo del álbum en Assets/Resources/{RutaImagen}.png.");
        return null;
    }

    private void ConstruirRecuadro(RectTransform libro, int indice)
    {
        Recuadro r = Recuadros[indice];

        var go = new GameObject("Recuadro_" + r.PinId, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(libro, false);

        // Anclado en fracciones del libro: se estira y se mueve con él.
        var rt = (RectTransform)go.transform;
        float y = 1f - r.Centro.y;
        rt.anchorMin = new Vector2(r.Centro.x - r.Tamano.x / 2f, y - r.Tamano.y / 2f);
        rt.anchorMax = new Vector2(r.Centro.x + r.Tamano.x / 2f, y + r.Tamano.y / 2f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Transparente: el recuadro punteado ya está dibujado en el libro. Solo
        // está para recibir el clic.
        go.GetComponent<Image>().color = Color.clear;

        Button boton = SinNavegacion(go.GetComponent<Button>());
        boton.transition = Selectable.Transition.None;
        boton.onClick.AddListener(() => Elegir(indice));
        go.AddComponent<CrecerAlPasar>();

        var iconoGO = new GameObject("Icono", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        iconoGO.transform.SetParent(go.transform, false);
        var icono = iconoGO.GetComponent<Image>();
        icono.preserveAspect = true;
        icono.raycastTarget = false;
        var cuadrado = iconoGO.GetComponent<AspectRatioFitter>();
        cuadrado.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        cuadrado.aspectRatio = 1f;
        _iconos[indice] = icono;

        TextMeshProUGUI incognita = FishyUIKit.Texto(go.transform, "Incognita", "?",
            90f, new Color(0.25f, 0.25f, 0.3f, 0.35f), TextAlignmentOptions.Center);
        incognita.font = FishyUIKit.Titulos;
        incognita.enableAutoSizing = true;
        incognita.fontSizeMin = 24f;
        incognita.fontSizeMax = 110f;
        incognita.raycastTarget = false;
        FishyUIKit.Estirar(incognita.rectTransform);
        _incognitas[indice] = incognita.gameObject;
    }

    /// <summary>
    /// La tarjeta del pin elegido, encima del libro. Tapa todo con un velo propio:
    /// pulsar fuera de la tarjeta la cierra y deja el libro como estaba.
    /// </summary>
    private void ConstruirDetalle(Transform padre)
    {
        var go = new GameObject("Detalle", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        FishyUIKit.Estirar((RectTransform)go.transform);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        SinNavegacion(go.GetComponent<Button>()).onClick.AddListener(CerrarDetalle);
        _detalle = go;

        var tarjetaGO = new GameObject("Tarjeta",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        tarjetaGO.transform.SetParent(go.transform, false);
        FishyUIKit.FondoRedondeado(tarjetaGO.GetComponent<Image>(), Paleta.Marron);

        var rt = (RectTransform)tarjetaGO.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720f, 0f);

        var vlg = tarjetaGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(44, 44, 36, 36);
        vlg.spacing = 18f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        tarjetaGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var iconoGO = new GameObject("Icono", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconoGO.transform.SetParent(tarjetaGO.transform, false);
        _detalleIcono = iconoGO.GetComponent<Image>();
        _detalleIcono.preserveAspect = true;
        _detalleIcono.raycastTarget = false;
        var hueco = iconoGO.GetComponent<LayoutElement>();
        hueco.minHeight = hueco.preferredHeight = 160f;

        _detalleTitulo = FishyUIKit.Texto(tarjetaGO.transform, "Titulo", "",
            44f, Paleta.Crema, TextAlignmentOptions.Center);
        _detalleTitulo.font = FishyUIKit.Titulos;

        _detalleTexto = FishyUIKit.Texto(tarjetaGO.transform, "Texto", "",
            30f, Paleta.Arena, TextAlignmentOptions.Center);
        _detalleTexto.font = FishyUIKit.Cuerpo;

        SinNavegacion(FishyUIKit.Boton(tarjetaGO.transform, "Volver", Paleta.Verde, 32f, 72f, CerrarDetalle));

        go.SetActive(false);
    }

    /// <summary>Sin navegación por teclado: WASD y las flechas no tienen que andar
    /// moviendo la selección entre botones.</summary>
    private static Button SinNavegacion(Button boton)
    {
        boton.navigation = new Navigation { mode = Navigation.Mode.None };
        return boton;
    }

    /// <summary>El recuadro crece un poco bajo el mouse, para que se note que se
    /// puede pulsar.</summary>
    private class CrecerAlPasar : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => transform.localScale = Vector3.one * 1.08f;
        public void OnPointerExit(PointerEventData eventData) => transform.localScale = Vector3.one;
        private void OnDisable() => transform.localScale = Vector3.one;
    }
}
