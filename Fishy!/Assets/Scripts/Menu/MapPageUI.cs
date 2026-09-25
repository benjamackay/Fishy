using Fishy.UI;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La página "Mapa" del celular: el mapa del mundo con una flecha que marca dónde está
/// Otto y hacia dónde mira.
///
/// Va en el GameObject "MapPage". Lo monta <see cref="MenuController"/> solo, igual que
/// hace <see cref="TabsController"/> con "QuestPage", así que no hay que tocar el prefab.
///
/// <b>El mapa es el mismo dibujo que el suelo del juego</b> (<c>mapa_fishy</c>, el sprite de
/// <c>map_ground</c>), y de ahí sale también la conversión: el rectángulo que ocupa ese
/// sprite en el mundo (<see cref="SpriteRenderer.bounds"/>) es exactamente el rectángulo
/// que ocupa la imagen, así que posición del mundo → posición en el mapa es una regla de
/// tres, sin números escritos a mano que se desactualicen si alguien mueve o reescala el suelo.
///
/// <b>Se ve una ventana ampliada, no el mapa entero.</b> El mapa se dibuja grande dentro de
/// un visor y se desplaza para dejar a Otto en el centro; en los bordes del mapa se detiene,
/// para no enseñar nunca fuera de la imagen. La flecha va pegada al mapa (se mueve con él),
/// por eso en los bordes se aparta del centro. Cuánto se acerca lo decide
/// <see cref="unidadesVisiblesAncho"/>.
///
/// <b>El visor es más grande que la página.</b> La página vive dentro de "Pages", que deja
/// un hueco para el título y los botones; el mapa se ve a pantalla completa por debajo de
/// ellos, así que el visor se ajusta a la "Pantalla" del celular menos la barra de estado.
/// </summary>
[DisallowMultipleComponent]
public class MapPageUI : MonoBehaviour, IVistaDeMapa
{
    [Header("Mapa")]
    [Tooltip("Dibujo del mapa. Si se deja vacío se toma el del suelo del mundo.")]
    public Sprite spriteDelMapa;

    [Tooltip("Nombre del sprite del suelo del mundo, del que salen el dibujo y el tamaño real.")]
    public string nombreDelSpriteDelSuelo = "mapa_fishy";

    [Tooltip("Cuántas unidades de mundo caben a lo ancho de la pantalla del mapa. Más chico = " +
             "más cerca. El mapa entero mide 96 unidades de ancho.")]
    [Min(5f)] public float unidadesVisiblesAncho = 52f;

    [Header("Visor")]
    [Tooltip("Espacio libre entre la barra de estado y el mapa, en unidades del panel. 0 = el " +
             "mapa empieza justo debajo de la barra.")]
    public float margenBajoLaBarra = 0f;

    [Header("Botones y título sobre el mapa")]
    [Tooltip("Color del título «Mapa», la flecha de volver y el engranaje mientras el mapa está " +
             "abierto. Medido en la referencia: el azul casi negro de la barra de la hora.")]
    public Color colorSobreElMapa = new Color32(0x0E, 0x16, 0x1C, 255);

    [Tooltip("Color con el que vienen dibujados los íconos de volver y del engranaje " +
             "(volver.png y config.png son de un solo color). Con él se calcula el tinte que da " +
             "exactamente el color de arriba.")]
    public Color colorBaseDeLosIconos = new Color32(190, 145, 113, 255);

    [Header("Flecha de Otto")]
    public Color colorFlecha = Color.white;
    public Color colorBordeFlecha = new Color32(0x1B, 0x14, 0x12, 255);
    [Tooltip("Largo (hacia donde apunta) y ancho de la flecha, en unidades del panel.")]
    public Vector2 tamanoFlecha = new Vector2(70f, 58f);
    [Tooltip("Grosor del contorno oscuro de la flecha, por lado.")]
    public float grosorBordeFlecha = 6f;

    [Header("Diagnóstico")]
    public bool verboseLogs = false;

    private RectTransform _pagina;
    private RectTransform _visor;
    private RectTransform _mapa;
    private RectTransform _flecha;
    private MapaMarcadoresUI _marcadores;

    private SpriteRenderer _suelo;
    private OttoController _otto;

    private bool _construido;
    private bool _visorPendiente;
    private bool _avisado;
    private Vector2 _tamanoMapa;

    /// <summary>Dónde se cuelgan las cosas que van sobre el mapa (marcadores). Se desplaza
    /// junto con él, así que basta con ponerles su posición con <see cref="PosicionEnMapa"/>.</summary>
    public RectTransform Mapa => _mapa;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    private void Awake()
    {
        _pagina = (RectTransform)transform;
    }

    private void OnEnable()
    {
        // Antes de construir nada: los colores no dependen de que el mapa se encuentre.
        PintarBotonesParaElMapa();

        if (!Construir()) return;
        AjustarVisor();
        _visorPendiente = true;   // el diseño de la página aún no está calculado en este frame
        Actualizar();
        if (_marcadores != null) _marcadores.Refrescar();
    }

    private void OnDisable() => DevolverLosColores();

    // ── Colores de los botones ───────────────────────────────────────────────

    // Estos tres objetos son de TODAS las páginas del celular, no del mapa. Sobre las otras
    // páginas (fondo marrón) se ven claros y así deben seguir; sobre el mapa la referencia
    // los quiere oscuros. Por eso se pintan al abrir el mapa y se devuelven al cerrarlo.
    private TMP_Text _titulo;
    private Image _botonVolver, _botonOpciones;
    private Color _colorTituloOriginal, _colorVolverOriginal, _colorOpcionesOriginal;
    private bool _coloresCambiados;

    private void PintarBotonesParaElMapa()
    {
        if (_coloresCambiados) return;

        RectTransform pantalla = BuscarPantalla();
        _titulo        = pantalla.Find("Encabezado/Titulo")?.GetComponent<TMP_Text>();
        _botonVolver   = pantalla.Find("Encabezado/BotonVolver")?.GetComponent<Image>();
        _botonOpciones = pantalla.Find("BotonOpciones")?.GetComponent<Image>();

        // Un Image multiplica su color por el del dibujo; para que un dibujo de un solo color
        // acabe exactamente en el color deseado, el tinte es el cociente de los dos.
        Color tinte = new Color(
            colorSobreElMapa.r / Mathf.Max(0.001f, colorBaseDeLosIconos.r),
            colorSobreElMapa.g / Mathf.Max(0.001f, colorBaseDeLosIconos.g),
            colorSobreElMapa.b / Mathf.Max(0.001f, colorBaseDeLosIconos.b), 1f);

        if (_titulo != null)        { _colorTituloOriginal = _titulo.color;          _titulo.color = colorSobreElMapa; }
        if (_botonVolver != null)   { _colorVolverOriginal = _botonVolver.color;     _botonVolver.color = tinte; }
        if (_botonOpciones != null) { _colorOpcionesOriginal = _botonOpciones.color; _botonOpciones.color = tinte; }

        _coloresCambiados = true;
    }

    private void DevolverLosColores()
    {
        if (!_coloresCambiados) return;
        _coloresCambiados = false;

        if (_titulo != null)        _titulo.color = _colorTituloOriginal;
        if (_botonVolver != null)   _botonVolver.color = _colorVolverOriginal;
        if (_botonOpciones != null) _botonOpciones.color = _colorOpcionesOriginal;
    }

    // Va en LateUpdate: al abrir el panel, la página se enciende en el mismo frame en que
    // recibe su tamaño, y el visor se mide sobre ese tamaño.
    private void LateUpdate()
    {
        if (!_construido) return;

        if (_visorPendiente)
        {
            _visorPendiente = false;
            AjustarVisor();
            // El mapa acaba de tomar su tamaño de verdad: los marcadores se recolocan sobre él.
            if (_marcadores != null) _marcadores.Refrescar();
        }
        Actualizar();
    }

    // ── Conversión mundo → mapa ──────────────────────────────────────────────

    /// <summary>
    /// Posición en el mapa (en su espacio local, con el centro del dibujo como origen) de un
    /// punto del mundo. Sirve para colocar cualquier cosa sobre el mapa: Otto, NPC, objetos.
    /// </summary>
    public Vector2 PosicionEnMapa(Vector2 mundo)
    {
        if (_suelo == null) return Vector2.zero;

        Bounds b = _suelo.bounds;
        float u = (mundo.x - b.min.x) / Mathf.Max(0.0001f, b.size.x);
        float v = (mundo.y - b.min.y) / Mathf.Max(0.0001f, b.size.y);
        return new Vector2((u - 0.5f) * _tamanoMapa.x, (v - 0.5f) * _tamanoMapa.y);
    }

    // ── Colocación ───────────────────────────────────────────────────────────

    private void Actualizar()
    {
        if (_visor == null || _visor.rect.width < 1f) return;

        if (_otto == null) _otto = FindAnyObjectByType<OttoController>();
        if (_otto == null) return;

        Vector2 enElMapa = PosicionEnMapa(_otto.transform.position);
        _flecha.anchoredPosition = enElMapa;

        Vector2 f = _otto.Facing;
        _flecha.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg);

        // Se desplaza el mapa para que Otto quede en el centro del visor, sin dejar que se
        // vea fuera de la imagen: el desplazamiento máximo es lo que sobra del mapa a cada lado.
        Vector2 sobra = (_tamanoMapa - _visor.rect.size) * 0.5f;
        Vector2 desplazamiento = -enElMapa;
        desplazamiento.x = sobra.x > 0f ? Mathf.Clamp(desplazamiento.x, -sobra.x, sobra.x) : 0f;
        desplazamiento.y = sobra.y > 0f ? Mathf.Clamp(desplazamiento.y, -sobra.y, sobra.y) : 0f;
        _mapa.anchoredPosition = desplazamiento;
    }

    /// <summary>
    /// Pone el visor sobre toda la pantalla del celular, por debajo de la barra de estado, y
    /// da al mapa el tamaño que le toca según el zoom. Las esquinas se pasan por el mundo
    /// (GetWorldCorners) para no depender de cuánto valgan los márgenes de "Pages": si
    /// alguien los cambia en el prefab, el mapa sigue cubriendo la pantalla.
    /// </summary>
    private void AjustarVisor()
    {
        if (_visor == null) return;

        RectTransform pantalla = BuscarPantalla();
        var c = new Vector3[4];
        pantalla.GetWorldCorners(c);   // 0 abajo-izq, 1 arriba-izq, 2 arriba-der, 3 abajo-der

        Vector3 abajoIzq = _pagina.InverseTransformPoint(c[0]);
        Vector3 arribaDer = _pagina.InverseTransformPoint(c[2]);
        float arriba = arribaDer.y;

        var barra = pantalla.Find("BarraEstado") as RectTransform;
        if (barra != null)
        {
            var cb = new Vector3[4];
            barra.GetWorldCorners(cb);
            arriba = Mathf.Min(arriba, _pagina.InverseTransformPoint(cb[0]).y - margenBajoLaBarra);
        }

        Rect area = Rect.MinMaxRect(abajoIzq.x, abajoIzq.y, arribaDer.x, arriba);

        _visor.anchorMin = _visor.anchorMax = _visor.pivot = new Vector2(0.5f, 0.5f);
        _visor.sizeDelta = area.size;
        // El punto de anclaje es el centro del rectángulo de la página, no su pivote.
        _visor.anchoredPosition = area.center - _pagina.rect.center;

        if (_suelo == null || area.width < 1f) return;

        float pxPorUnidad = area.width / unidadesVisiblesAncho;
        Bounds b = _suelo.bounds;
        _tamanoMapa = new Vector2(b.size.x, b.size.y) * pxPorUnidad;
        _mapa.sizeDelta = _tamanoMapa;
    }

    /// <summary>La "Pantalla" del celular: el ancestro que se llama así, o la propia página
    /// si por algún motivo no aparece.</summary>
    private RectTransform BuscarPantalla()
    {
        for (Transform t = transform.parent; t != null; t = t.parent)
            if (t.name == "Pantalla") return (RectTransform)t;
        return _pagina;
    }

    // ── Construcción ─────────────────────────────────────────────────────────

    private bool Construir()
    {
        if (_construido) return true;

        _suelo = BuscarSuelo();
        Sprite dibujo = spriteDelMapa != null ? spriteDelMapa : (_suelo != null ? _suelo.sprite : null);

        if (_suelo == null || dibujo == null)
        {
            // Se reintenta en el siguiente OnEnable; con un solo aviso para no llenar la consola.
            if (!_avisado)
            {
                _avisado = true;
                Debug.LogWarning($"[MapPageUI] No encuentro el suelo del mundo (un SpriteRenderer cuyo " +
                                 $"sprite se llame '{nombreDelSpriteDelSuelo}'), así que no sé qué mapa " +
                                 "dibujar ni a qué escala.", this);
            }
            return false;
        }

        // Visor: recorta lo que se sale de la pantalla del celular.
        var visorGO = new GameObject("VisorMapa", typeof(RectTransform), typeof(RectMask2D));
        visorGO.transform.SetParent(transform, false);
        _visor = (RectTransform)visorGO.transform;

        // El mapa, centrado en el visor y con el tamaño que le da AjustarVisor.
        var mapaGO = new GameObject("Mapa", typeof(RectTransform), typeof(Image));
        mapaGO.transform.SetParent(_visor, false);
        _mapa = (RectTransform)mapaGO.transform;
        _mapa.anchorMin = _mapa.anchorMax = _mapa.pivot = new Vector2(0.5f, 0.5f);

        var img = mapaGO.GetComponent<Image>();
        img.sprite = dibujo;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;   // el tamaño ya respeta la proporción del mundo
        img.raycastTarget = false;

        // Los marcadores se crean antes que la flecha de Otto: quedan por debajo de ella.
        _marcadores = MapaMarcadoresUI.Crear(this, _mapa);
        ConstruirFlecha();

        // La tarjeta va después del mapa: hermana suya dentro del visor, así se dibuja encima
        // y no se desplaza con él.
        MapaTarjetaUI.Crear(_visor);

        _construido = true;
        if (verboseLogs) Debug.Log($"[MapPageUI] Mapa montado con '{dibujo.name}', mundo {_suelo.bounds.size}.", this);
        return true;
    }

    private void ConstruirFlecha()
    {
        var go = new GameObject("FlechaOtto", typeof(RectTransform));
        go.transform.SetParent(_mapa, false);
        _flecha = (RectTransform)go.transform;
        _flecha.anchorMin = _flecha.anchorMax = _flecha.pivot = new Vector2(0.5f, 0.5f);
        _flecha.sizeDelta = Vector2.zero;

        // El contorno es la misma flecha, más grande y oscura, detrás: así se despega del fondo.
        Triangulo("Borde", tamanoFlecha + Vector2.one * (grosorBordeFlecha * 2f), colorBordeFlecha);
        Triangulo("Relleno", tamanoFlecha, colorFlecha);
    }

    private void Triangulo(string nombre, Vector2 tamano, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_flecha, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamano;

        var img = go.GetComponent<Image>();
        img.sprite = DibujoFlecha.Obtener();
        img.color = color;
        img.raycastTarget = false;
    }

    /// <summary>El SpriteRenderer del suelo del mundo. Se miran también los inactivos.</summary>
    private SpriteRenderer BuscarSuelo()
    {
        foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            if (sr != null && sr.sprite != null && sr.sprite.name == nombreDelSpriteDelSuelo)
                return sr;
        return null;
    }
}
