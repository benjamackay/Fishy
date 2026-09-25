using Fishy.UI;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// El minimapa del HUD y el mapa grande que se abre con la tecla <b>M</b>.
///
/// <b>Minimapa:</b> un círculo con aro naranja arriba a la izquierda que enseña, ampliada, la parte
/// del mapa que rodea a Otto, con su flecha en el centro y los mismos marcadores que la página
/// "Mapa" del celular («!» azul, «?» morado y estrellas de objetos). Junto a él, sobre el aro, una
/// «M» dice qué tecla lo agranda (sólo si hay teclado, igual que el «TAB» del botón del celular).
///
/// <b>Mapa grande:</b> con M el mismo círculo crece al centro de la pantalla y el juego se
/// oscurece con un velo (#523224 al 50 %, el marrón del fondo borroso del panel del celular).
/// <b>Otto no se detiene:</b> el mapa se sigue moviendo con él y el juego sigue corriendo debajo.
/// Se cierra con M otra vez, o solo si algo ocupa la escena: el panel del celular, un diálogo, un
/// chat, el Modo Detective o la pausa. Por lo mismo tampoco se abre en esos momentos: sólo se
/// abre caminando por el mundo, igual que el panel del celular.
///
/// Las dos ventanas son <see cref="VistaCircular"/>, con distinto tamaño, y comparten imagen,
/// suelo y conversión mundo → mapa. Por eso tienen el mismo aspecto.
///
/// <b>El círculo es una máscara de stencil</b> (<see cref="Mask"/>) sobre un círculo dibujado por
/// código; un RectMask2D sólo recorta rectángulos.
///
/// <b>La imagen es una copia suavizada.</b> El PNG del mapa no tiene mipmaps y usa filtro de
/// píxeles —lo pide el mundo, que se ve ampliado—, pero en el minimapa se muestra a menos de la
/// mitad de su tamaño y así se vería dentado y parpadeante al moverse. Se copia una vez a una
/// textura con mipmaps en la GPU y se muestra con filtro trilineal. El original no se toca.
///
/// Los marcadores no se calculan una sola vez, como en la página del celular: aquí el mapa está
/// siempre a la vista, y un objeto recogido o una misión nueva tienen que notarse. Se rehacen cada
/// <see cref="intervaloMarcadores"/> segundos.
///
/// Se crea solo al cargar una escena que tenga a Otto; no hay que montar nada. Orden de dibujo:
/// minimapa 708 (con el resto del HUD), velo 720 y mapa grande 725, todo por debajo de los
/// diálogos, el chat y el panel del celular (950 en adelante).
/// </summary>
[DisallowMultipleComponent]
public class MinimapaUI : MonoBehaviour
{
    public static MinimapaUI Instance { get; private set; }

    [Header("Mapa")]
    [Tooltip("Nombre del sprite del suelo del mundo, del que salen el dibujo y el tamaño real.")]
    public string nombreDelSpriteDelSuelo = "mapa_fishy";

    [Tooltip("Cuántas unidades de mundo caben de lado a lado del minimapa. Más chico = más cerca. " +
             "El mapa entero mide 96.")]
    [Min(5f)] public float unidadesVisibles = MapaTheme.Minimapa.UnidadesVisibles;

    [Tooltip("Lo mismo para el mapa grande de la tecla M.")]
    [Min(5f)] public float unidadesVisiblesGrande = MapaTheme.Minimapa.UnidadesVisiblesGrande;

    [Header("Marcadores")]
    [Tooltip("Marcar también los objetos por recoger, con una estrella.")]
    public bool mostrarEstrellas = true;

    [Tooltip("Cada cuántos segundos se vuelven a colocar los marcadores.")]
    [Min(0.2f)] public float intervaloMarcadores = 1f;

    [Header("Diagnóstico")]
    public bool verboseLogs = false;

    private VistaCircular _pequena, _grande;
    private RectTransform _ventanaPequena, _ventanaGrande;
    private RectTransform _aroGrande;
    private CanvasGroup _grupoGrande;
    private GameObject _canvasGrande, _canvasVelo;
    private Image _velo;
    private TextMeshProUGUI _letraM;

    private SpriteRenderer _suelo;
    private OttoController _otto;
    private MenuController _menu;
    private RenderTexture _copiaSuave;

    private bool _mapasConstruidos;
    private bool _abierto;
    private float _t;   // 0 = mapa grande cerrado, 1 = abierto del todo

    private float _proximosMarcadores, _proximoIntento, _proximaRevisionTeclado;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    /// <summary>
    /// Se crea solo, pero sólo donde hay mundo que explorar: el guardia deja fuera el arranque, el
    /// login y los menús. Se mira en cada carga de escena, no sólo en la primera, por lo mismo que
    /// <see cref="MissionUIController"/>: entrando por el menú la primera escena no tiene a Otto.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;   // quitar antes de poner: ver MissionUIController
        SceneManager.sceneLoaded += AlCargarEscena;
        CrearSiHayOtto();
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo) => CrearSiHayOtto();

    private static void CrearSiHayOtto()
    {
        if (Instance != null) return;
        if (FindAnyObjectByType<OttoController>() == null) return;
        new GameObject("MinimapaUI").AddComponent<MinimapaUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ConstruirMinimapa();
        ConstruirMapaGrande();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_copiaSuave != null) { _copiaSuave.Release(); Destroy(_copiaSuave); }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            if (_abierto) Cerrar();
            else if (_mapasConstruidos && EnElMundo()) Abrir();
        }

        // Si algo ocupa la escena mientras el mapa grande está abierto, se va sin animación.
        if (_abierto && !EnElMundo()) CerrarYa();
    }

    private void LateUpdate()
    {
        // El suelo puede no estar todavía cuando nace este componente: se reintenta, sin llenar la
        // consola de avisos.
        if (!_mapasConstruidos)
        {
            if (Time.unscaledTime < _proximoIntento) return;
            _proximoIntento = Time.unscaledTime + 1f;
            if (!ConstruirMapas()) return;
        }

        if (_otto == null) _otto = FindAnyObjectByType<OttoController>();

        _pequena.Actualizar(_otto);
        AnimarMapaGrande();

        if (_t > 0f) _grande.Actualizar(_otto);

        if (Time.unscaledTime >= _proximosMarcadores)
        {
            _proximosMarcadores = Time.unscaledTime + intervaloMarcadores;
            _pequena.RefrescarMarcadores();
            if (_t > 0f) _grande.RefrescarMarcadores();
        }

        if (Time.unscaledTime >= _proximaRevisionTeclado)
        {
            _proximaRevisionTeclado = Time.unscaledTime + 0.5f;
            // Sin teclado (pantalla táctil) no hay tecla que apretar: la letra sobra.
            if (_letraM != null && _letraM.gameObject.activeSelf != (Keyboard.current != null))
                _letraM.gameObject.SetActive(Keyboard.current != null);
        }
    }

    // ── Abrir y cerrar ───────────────────────────────────────────────────────

    /// <summary>
    /// Sólo se abre caminando por el mundo: con el juego corriendo, sin el panel del celular y con
    /// Otto libre para moverse. Los diálogos con un NPC, el chat, el Modo Detective y las
    /// cinemáticas le quitan el movimiento a Otto, así que "Otto puede moverse" es la señal de que
    /// no hay nada en medio (ver <see cref="MenuController.PuedeAbrir"/>).
    /// </summary>
    private bool EnElMundo()
    {
        if (Time.timeScale <= 0f) return false;

        if (_menu == null) _menu = FindAnyObjectByType<MenuController>();
        return _menu == null || (!_menu.EstaAbierto && _menu.PuedeAbrir);
    }

    private void Abrir()
    {
        _abierto = true;
        _grande.RefrescarMarcadores();   // ya, no dentro de un segundo
        _proximosMarcadores = Time.unscaledTime + intervaloMarcadores;
    }

    private void Cerrar() => _abierto = false;

    private void CerrarYa()
    {
        _abierto = false;
        _t = 0f;
        AplicarAnimacion();
    }

    // ── Animación del mapa grande ────────────────────────────────────────────

    private void AnimarMapaGrande()
    {
        float meta = _abierto ? 1f : 0f;
        if (!Mathf.Approximately(_t, meta))
            _t = Mathf.MoveTowards(_t, meta, Time.unscaledDeltaTime / MapaTheme.Minimapa.DuracionApertura);

        AplicarAnimacion();
    }

    /// <summary>Coloca el velo y el círculo grande según cuánto camino llevan de abrirse.</summary>
    private void AplicarAnimacion()
    {
        bool visible = _t > 0.001f;
        if (_canvasGrande.activeSelf != visible) _canvasGrande.SetActive(visible);
        if (_canvasVelo.activeSelf != visible) _canvasVelo.SetActive(visible);
        if (!visible) return;

        // Al abrir crece con un pequeño rebote (el mismo que el panel del celular); al cerrar se
        // encoge sin rebotar, más discreto.
        float e = _abierto ? SalidaConRebote(_t) : _t;
        float escala = Mathf.LerpUnclamped(0.6f, 1f, e);
        _aroGrande.localScale = new Vector3(escala, escala, 1f);
        _grupoGrande.alpha = Mathf.Clamp01(_t * 1.6f);

        Color velo = MapaTheme.Minimapa.Velo;
        velo.a *= _t;
        _velo.color = velo;
    }

    /// <summary>Sube rápido, se pasa de largo y se asienta: el "easeOutBack" de siempre.</summary>
    private static float SalidaConRebote(float k)
    {
        const float c1 = 1.4f;
        float x = k - 1f;
        return 1f + (c1 + 1f) * x * x * x + c1 * x * x;
    }

    // ── Construcción ─────────────────────────────────────────────────────────

    /// <summary>El minimapa del HUD: canvas, aro naranja, ventana circular y la letra M.</summary>
    private void ConstruirMinimapa()
    {
        Canvas canvas = CrearCanvas("MinimapaCanvas", 708);   // con el resto del HUD (700 a 710)

        RectTransform aro;
        _ventanaPequena = CrearCirculo(canvas.transform, MapaTheme.Minimapa.Diametro,
            MapaTheme.Minimapa.GrosorAro, out aro);
        aro.anchorMin = aro.anchorMax = aro.pivot = new Vector2(0f, 1f);   // esquina superior izquierda
        aro.anchoredPosition = new Vector2(MapaTheme.Minimapa.Margen.x, -MapaTheme.Minimapa.Margen.y);

        // La «M»: sobre el borde del aro, abajo a la derecha, como en la referencia.
        _letraM = FishyUIKit.Texto(aro, "TeclaM", "M", MapaTheme.Minimapa.TamanoLetraM,
            MapaTheme.Minimapa.ColorLetraM, TextAlignmentOptions.Center);
        _letraM.font = FishyUIKit.FuentePara("M");
        _letraM.raycastTarget = false;

        float d = MapaTheme.Minimapa.DistanciaLetraM / Mathf.Sqrt(2f);
        var rt = _letraM.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(100f, 90f);
        rt.anchoredPosition = new Vector2(d, -d);
    }

    /// <summary>El velo y el círculo grande. Empiezan apagados: sólo existen mientras se ven.</summary>
    private void ConstruirMapaGrande()
    {
        // Velo: toda la pantalla, por encima del HUD (708 a 710) y por debajo de lo modal. No recibe
        // clics: el juego sigue funcionando debajo.
        Canvas canvasVelo = CrearCanvas("MapaGrandeVelo", 720);
        _canvasVelo = canvasVelo.gameObject;

        var veloGO = new GameObject("Velo", typeof(RectTransform), typeof(Image));
        veloGO.transform.SetParent(canvasVelo.transform, false);
        var vrt = (RectTransform)veloGO.transform;
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        _velo = veloGO.GetComponent<Image>();
        _velo.raycastTarget = false;
        _velo.color = Color.clear;
        _canvasVelo.SetActive(false);

        // Círculo grande, centrado, por encima del velo.
        Canvas canvasGrande = CrearCanvas("MapaGrandeCanvas", 725);
        _canvasGrande = canvasGrande.gameObject;
        _grupoGrande = _canvasGrande.AddComponent<CanvasGroup>();
        _grupoGrande.blocksRaycasts = false;
        _grupoGrande.interactable = false;

        _ventanaGrande = CrearCirculo(canvasGrande.transform, MapaTheme.Minimapa.DiametroGrande,
            MapaTheme.Minimapa.GrosorAroGrande, out _aroGrande);
        _aroGrande.anchorMin = _aroGrande.anchorMax = _aroGrande.pivot = new Vector2(0.5f, 0.5f);
        _aroGrande.anchoredPosition = Vector2.zero;
        _canvasGrande.SetActive(false);
    }

    private Canvas CrearCanvas(string nombre, int orden)
    {
        var go = new GameObject(nombre, typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;

        var escalador = go.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>
    /// Un círculo con aro naranja y, dentro, la ventana redonda (una máscara que no se dibuja).
    /// Devuelve la ventana y, por <paramref name="aro"/>, el círculo exterior, para que quien lo
    /// pide lo coloque donde quiera.
    /// </summary>
    private RectTransform CrearCirculo(Transform padre, float diametro, float grosorAro, out RectTransform aro)
    {
        var aroGO = new GameObject("Aro", typeof(RectTransform), typeof(Image));
        aroGO.transform.SetParent(padre, false);
        aro = (RectTransform)aroGO.transform;
        aro.sizeDelta = new Vector2(diametro, diametro);

        var aroImg = aroGO.GetComponent<Image>();
        aroImg.sprite = FishyUIKit.SpriteRedondeado(256, 128);   // radio = mitad del lado: círculo
        aroImg.color = MapaTheme.Minimapa.ColorAro;
        aroImg.raycastTarget = false;

        var ventanaGO = new GameObject("Ventana", typeof(RectTransform), typeof(Image), typeof(Mask));
        ventanaGO.transform.SetParent(aroGO.transform, false);
        var ventana = (RectTransform)ventanaGO.transform;
        ventana.anchorMin = ventana.anchorMax = ventana.pivot = new Vector2(0.5f, 0.5f);
        ventana.sizeDelta = new Vector2(diametro - grosorAro * 2f, diametro - grosorAro * 2f);
        ventana.anchoredPosition = Vector2.zero;

        var img = ventanaGO.GetComponent<Image>();
        img.sprite = FishyUIKit.SpriteRedondeado(256, 128);
        img.raycastTarget = false;
        ventanaGO.GetComponent<Mask>().showMaskGraphic = false;
        return ventana;
    }

    /// <summary>Monta el mapa dentro de las dos ventanas. Necesita el suelo; false si aún no está.</summary>
    private bool ConstruirMapas()
    {
        _suelo = BuscarSuelo();
        Sprite dibujo = _suelo != null ? _suelo.sprite : null;
        if (dibujo == null) return false;

        Texture textura = CrearCopiaSuave(dibujo);

        _pequena = new VistaCircular(_ventanaPequena, _suelo);
        _pequena.Construir(textura, dibujo, unidadesVisibles, MapaTheme.Minimapa.FactorMarcadores,
            mostrarEstrellas, MapaTheme.Minimapa.TamanoFlecha, MapaTheme.Minimapa.GrosorBordeFlecha);

        _grande = new VistaCircular(_ventanaGrande, _suelo);
        _grande.Construir(textura, dibujo, unidadesVisiblesGrande, MapaTheme.Minimapa.FactorMarcadoresGrande,
            mostrarEstrellas, MapaTheme.Minimapa.TamanoFlechaGrande, MapaTheme.Minimapa.GrosorBordeFlechaGrande);

        _mapasConstruidos = true;
        _proximosMarcadores = 0f;   // colocarlos en cuanto se pueda
        if (verboseLogs) Debug.Log($"[Minimapa] Montado con '{dibujo.name}', mundo {_suelo.bounds.size}.", this);
        return true;
    }

    /// <summary>
    /// La copia con mipmaps que se muestra en las dos ventanas, o null si el sprite es sólo un trozo
    /// de una textura más grande (un atlas): entonces se usa el sprite tal cual, con su filtro.
    /// </summary>
    private Texture CrearCopiaSuave(Sprite dibujo)
    {
        Texture2D textura = dibujo.texture;
        bool esLaTexturaEntera = textura != null &&
                                 Mathf.Approximately(dibujo.rect.width, textura.width) &&
                                 Mathf.Approximately(dibujo.rect.height, textura.height);
        if (!esLaTexturaEntera) return null;

        _copiaSuave = new RenderTexture(textura.width, textura.height, 0,
                                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            useMipMap = true,
            autoGenerateMips = false,   // se generan a mano, una vez, tras copiar
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "MinimapaCopiaSuave",
        };
        _copiaSuave.Create();
        Graphics.Blit(textura, _copiaSuave);
        _copiaSuave.GenerateMips();
        return _copiaSuave;
    }

    private SpriteRenderer BuscarSuelo()
    {
        foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            if (sr != null && sr.sprite != null && sr.sprite.name == nombreDelSpriteDelSuelo)
                return sr;
        return null;
    }
}
