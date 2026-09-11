using System.Collections.Generic;
using Fishy.Mision;
using Fishy.UI;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HDU-16 CA1 y CA6 — Cartel permanente con la misión activa y el resumen de sus
/// objetivos, arriba a la izquierda mientras el niño/a explora.
///
/// Es la contraparte visible de <see cref="MissionManager.Activa"/>: no elige
/// misión ni decide cuándo cambiarla, sólo pinta la que haya y le pasa la zona de
/// destino al <see cref="ZoneMarker"/>. Cuando no queda ninguna disponible enseña
/// el mensaje de "no hay nuevas misiones" (CA6) y apaga el marcador (CA4).
///
/// <b>Es permanente, no un desplegable.</b> Es lo que separa esta HDU de la HDU-1:
/// aquella dejó un panel con botón "Misiones" que había que abrir, y la lista
/// completa —con las ya terminadas— sigue estando en la pestaña Misión del Tab
/// (<see cref="QuestPageUI"/>). Lo que faltaba era saber sin abrir nada qué toca
/// ahora y hacia dónde ir.
///
/// <b>Por qué vive en Assembly-CSharp y no en el assembly Fishy.Mision:</b> el
/// resumen de objetivos lo escribe <see cref="ObjetivoMision.Describir"/>, que
/// depende de NPC, ItemData y PhoneChatLauncher, y Unity no deja que un assembly
/// con .asmdef vea el ensamblado por defecto. La parte que sí se puede probar sin
/// escena —cuál es la misión activa y qué zona señalar— está del otro lado, en
/// <see cref="MissionManager"/>.
///
/// Se crea solo al cargar una escena que tenga a Otto; no hay que montar nada.
/// </summary>
[DisallowMultipleComponent]
public class MissionUIController : MonoBehaviour
{
    public static MissionUIController Instance { get; private set; }

    [Header("Diagnóstico")]
    [Tooltip("Escribir en consola cada refresco del cartel.")]
    public bool verboseLogs = false;

    private GameObject _panel;
    private TextMeshProUGUI _etiqueta;
    private TextMeshProUGUI _titulo;
    private TextMeshProUGUI _guia;
    private readonly List<TextMeshProUGUI> _lineasObjetivo = new List<TextMeshProUGUI>();

    /// <summary>Canvas del menú del Tab. Con él abierto el cartel se esconde: los dos
    /// hablan de misiones y verlos a la vez, uno encima del otro, confunde.</summary>
    private GameObject _menuTab;
    private bool _menuBuscado;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    /// <summary>
    /// Se crea solo, pero <b>sólo donde hay mundo que explorar</b>: el guardia de
    /// OttoController deja fuera el arranque, el login y los menús, donde un cartel
    /// de misión activa no significa nada.
    ///
    /// Tampoco es DontDestroyOnLoad, al revés que SaveManager o MenuPausa: pertenece
    /// a la escena del mundo, y sobrevivir a la vuelta al menú sería justo el
    /// problema que el guardia evita.
    ///
    /// <b>Se mira en cada carga de escena, no sólo en la primera.</b> AfterSceneLoad
    /// corre una sola vez, con la escena con que arranca el juego, y entrando por el
    /// menú esa escena no tiene a Otto: el cartel no aparecía nunca.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear()
    {
        // Quitar antes de poner: sin recarga de dominio al entrar en Play la
        // suscripción de la sesión anterior sigue viva.
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        CrearSiHayOtto();
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo) => CrearSiHayOtto();

    private static void CrearSiHayOtto()
    {
        if (Instance != null) return;
        if (FindAnyObjectByType<OttoController>() == null) return;
        GetOrCreate();
    }

    public static MissionUIController GetOrCreate()
    {
        if (Instance != null) return Instance;

        var encontrado = FindAnyObjectByType<MissionUIController>();
        if (encontrado != null) return encontrado;

        return new GameObject("MissionUIController").AddComponent<MissionUIController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ConstruirUI();

        // El cartel es el único que necesita las dos piezas siempre encendidas: sin
        // ZonaActual no hay "llegué a la zona" y sin MissionTracker no hay objetivos
        // que resumir. Crearlas aquí ahorra tener que acordarse de montarlas a mano
        // en cada escena de mundo.
        ZonaActual.GetOrCreate();
        MissionTracker.GetOrCreate();
    }

    private void OnEnable()
    {
        MissionManager manager = MissionManager.GetOrCreate();
        manager.onMisionActivaCambiada.AddListener(AlCambiarMisionActiva);
        manager.onPanelActualizado.AddListener(Refrescar);

        if (MissionTracker.Instance != null)
            MissionTracker.Instance.OnProgresoCambiado += Refrescar;

        // Entrar en una zona no cambia la misión, pero sí el cartel: "Ve al Bosque"
        // desaparece al llegar.
        ZonaActual.OnZonaCambiada += AlCambiarDeZona;

        Refrescar();
    }

    private void OnDisable()
    {
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.onMisionActivaCambiada.RemoveListener(AlCambiarMisionActiva);
            MissionManager.Instance.onPanelActualizado.RemoveListener(Refrescar);
        }

        if (MissionTracker.Instance != null)
            MissionTracker.Instance.OnProgresoCambiado -= Refrescar;

        ZonaActual.OnZonaCambiada -= AlCambiarDeZona;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void AlCambiarMisionActiva(DesafioRuntime _) => Refrescar();
    private void AlCambiarDeZona(string anterior, string nueva) => Refrescar();

    private void Update()
    {
        bool tapado = MenuAbierto();
        if (_panel != null && _panel.activeSelf == tapado) _panel.SetActive(!tapado);
    }

    /// <summary>
    /// Hay algo a pantalla completa por encima. El menú de pausa se detecta por el
    /// tiempo congelado, que es su efecto y no depende de que exista todavía; el
    /// menú del Tab, preguntándole a su controlador.
    ///
    /// Los diálogos, el chat y el Modo Detective no hace falta mirarlos: sus canvas
    /// van de 950 para arriba y este de 710, así que ya tapan el cartel solos.
    /// </summary>
    private bool MenuAbierto()
    {
        if (Time.timeScale == 0f) return true;

        if (!_menuBuscado)
        {
            _menuBuscado = true;
            var controlador = FindAnyObjectByType<MenuController>();
            if (controlador != null) _menuTab = controlador.menuCanvas;
        }

        return _menuTab != null && _menuTab.activeInHierarchy;
    }

    // ── Pintado ──────────────────────────────────────────────────────────────

    /// <summary>Vuelve a escribir el cartel desde el estado actual y pone al día el
    /// indicador de zona.</summary>
    public void Refrescar()
    {
        if (_titulo == null) return;

        DesafioRuntime activa = MissionManager.GetOrCreate().Activa;
        string zona = ZonaDeLaMision(activa);

        // CA2 y CA4 en una línea: hay zona que señalar, o no la hay.
        ZoneMarker.GetOrCreate().Apuntar(zona);

        if (activa == null) { PintarSinMisiones(); return; }

        _etiqueta.gameObject.SetActive(true);
        _etiqueta.text = MisionHudTheme.Textos.Etiqueta;

        _titulo.color = MisionHudTheme.Colores.Titulo;
        _titulo.font = FishyUIKit.FuentePara(activa.Titulo);
        _titulo.text = activa.Titulo;

        PintarObjetivos(activa);
        PintarGuia(zona);

        if (verboseLogs)
            Debug.Log($"[MisiónHUD] Activa: '{activa.Titulo}'" +
                      (zona != null ? $", zona '{zona}'." : ", sin zona."), this);
    }

    private void PintarSinMisiones()
    {
        _etiqueta.gameObject.SetActive(false);

        _titulo.color = MisionHudTheme.Colores.SinMisiones;
        _titulo.font = MisionHudTheme.Fuente.Objetivos;   // es un párrafo, no un rótulo
        _titulo.text = MisionHudTheme.Textos.SinMisiones;

        foreach (TextMeshProUGUI linea in _lineasObjetivo) linea.gameObject.SetActive(false);
        _guia.gameObject.SetActive(false);

        if (verboseLogs) Debug.Log("[MisiónHUD] Sin misiones disponibles.", this);
    }

    private void PintarObjetivos(DesafioRuntime activa)
    {
        IReadOnlyList<ObjetivoMision> objetivos = MissionTracker.Instance != null
            ? MissionTracker.Instance.Objetivos(activa.Id)
            : new List<ObjetivoMision>();

        int usadas = 0;
        int caben = _lineasObjetivo.Count;

        // Con más objetivos que huecos, el último hueco cuenta cuántos quedan en vez
        // de mostrar uno más: cortar la lista sin avisar hace pensar que ya está.
        bool desborda = caben > 0 && objetivos.Count > caben;
        int aMostrar = desborda ? caben - 1 : Mathf.Min(objetivos.Count, caben);

        for (int i = 0; i < aMostrar; i++)
        {
            ObjetivoMision objetivo = objetivos[i];
            TextMeshProUGUI linea = _lineasObjetivo[usadas++];

            string vinneta = MisionHudTheme.Textos.Vinneta;
            linea.text = objetivo.cumplido
                ? $"{vinneta}  {objetivo.Describir()}  {MisionHudTheme.Textos.Cumplido}"
                : $"{vinneta}  {objetivo.Describir()}";
            linea.color = objetivo.cumplido
                ? MisionHudTheme.Colores.ObjetivoCumplido
                : MisionHudTheme.Colores.ObjetivoPendiente;
            linea.gameObject.SetActive(true);
        }

        if (desborda)
        {
            TextMeshProUGUI linea = _lineasObjetivo[usadas++];
            int restantes = objetivos.Count - aMostrar;
            // Sin puntos suspensivos tipográficos (U+2026): no están en Latin-1.
            linea.text = $"y {restantes} objetivo(s) más en la pestaña Misión";
            linea.color = MisionHudTheme.Colores.SinMisiones;
            linea.gameObject.SetActive(true);
        }

        // Una misión sin objetivos seguidos es informativa: en vez de dejar el hueco
        // vacío se aprovecha la descripción de la ficha, que es la pista escrita.
        if (objetivos.Count == 0 && activa.data != null &&
            !string.IsNullOrWhiteSpace(activa.data.descripcion) && caben > 0)
        {
            TextMeshProUGUI linea = _lineasObjetivo[usadas++];
            linea.text = activa.data.descripcion.Trim();
            linea.color = MisionHudTheme.Colores.ObjetivoPendiente;
            linea.gameObject.SetActive(true);
        }

        for (int i = usadas; i < caben; i++) _lineasObjetivo[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Línea que dice a dónde ir. Sólo aparece con Otto fuera de la zona: ya dentro,
    /// lo que toca hacer lo dicen los objetivos, y un "Ya estás en…" les quitaba el
    /// sitio justo cuando el niño/a buscaba qué hacer.
    /// </summary>
    private void PintarGuia(string zona)
    {
        ZonaActual zonas = ZonaActual.Instance;
        bool dentro = zonas != null && zonas.Actual == zona;

        if (zona == null || dentro) { _guia.gameObject.SetActive(false); return; }

        string texto = string.Format(MisionHudTheme.Textos.IrA, ZonaMundo.NombreDe(zona));

        _guia.font = FishyUIKit.FuentePara(texto);
        _guia.text = texto;
        _guia.color = MisionHudTheme.Colores.Guia;
        _guia.gameObject.SetActive(true);
    }

    /// <summary>
    /// Zona de destino de una misión. Manda lo que diga la ficha; si está vacía se
    /// mira si algún objetivo pendiente es "llegar a una zona", para no tener que
    /// escribir lo mismo dos veces y arriesgarse a que un día no coincidan.
    /// </summary>
    private static string ZonaDeLaMision(DesafioRuntime mision)
    {
        if (mision == null) return null;

        string deLaFicha = MissionManager.Instance != null
            ? MissionManager.Instance.ZonaObjetivoActiva
            : null;
        if (!string.IsNullOrEmpty(deLaFicha)) return deLaFicha;

        return MissionTracker.Instance != null
            ? MissionTracker.Instance.ZonaPendiente(mision.Id)
            : null;
    }

    // ── Construcción de la UI ────────────────────────────────────────────────

    private void ConstruirUI()
    {
        var canvasGO = new GameObject("MisionHudCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 710;   // por encima de la flecha, por debajo de todo lo modal

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _panel = new GameObject("CartelMision",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        _panel.transform.SetParent(canvasGO.transform, false);

        var rt = _panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = MisionHudTheme.Medidas.MargenPanel;
        rt.sizeDelta = new Vector2(MisionHudTheme.Medidas.AnchoPanel, 0f);

        var fondo = _panel.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(fondo, MisionHudTheme.Colores.FondoPanel);
        fondo.raycastTarget = false;   // no debe robarle el clic a nada del mundo

        var vlg = _panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(22, 22, 16, 18);
        vlg.spacing = 6f;
        vlg.childControlWidth  = true; vlg.childControlHeight  = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        // Sólo el alto se ajusta al contenido: el ancho lo fija el diseño, o el cartel
        // se encogería al largo de la frase más corta y bailaría en cada refresco.
        var fitter = _panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _etiqueta = Linea("Etiqueta", MisionHudTheme.Textos.Etiqueta,
            MisionHudTheme.Fuente.Etiqueta, MisionHudTheme.Colores.Etiqueta,
            MisionHudTheme.Fuente.Titulos);

        _titulo = Linea("Titulo", "", MisionHudTheme.Fuente.Titulo,
            MisionHudTheme.Colores.Titulo, MisionHudTheme.Fuente.Titulos);

        for (int i = 0; i < MisionHudTheme.Medidas.MaxObjetivos; i++)
        {
            TextMeshProUGUI linea = Linea($"Objetivo{i}", "", MisionHudTheme.Fuente.Objetivo,
                MisionHudTheme.Colores.ObjetivoPendiente, MisionHudTheme.Fuente.Objetivos);
            linea.gameObject.SetActive(false);
            _lineasObjetivo.Add(linea);
        }

        _guia = Linea("Guia", "", MisionHudTheme.Fuente.Guia,
            MisionHudTheme.Colores.Guia, MisionHudTheme.Fuente.Titulos);
        _guia.gameObject.SetActive(false);
    }

    /// <summary>
    /// Una línea del cartel. Las líneas se crean una vez y se reutilizan escribiendo
    /// encima, en vez de destruirlas y rehacerlas en cada refresco como hace la
    /// página del Tab: aquélla se pinta al abrirla, ésta se repinta cada vez que
    /// cambia el inventario o la zona, y estar creando y destruyendo objetos de UI
    /// durante toda la partida se paga en basura.
    /// </summary>
    private TextMeshProUGUI Linea(string nombre, string contenido, float tamano,
        Color color, TMP_FontAsset fuente)
    {
        TextMeshProUGUI texto = FishyUIKit.Texto(_panel.transform, nombre, contenido,
            tamano, color, TextAlignmentOptions.TopLeft);

        if (fuente != null) texto.font = fuente;
        texto.raycastTarget = false;

        // Sin un alto mínimo el layout aplasta la línea antes de que TMP la mida, que
        // es el mismo detalle que hacía desaparecer filas en la página de misiones.
        texto.gameObject.AddComponent<LayoutElement>().minHeight = tamano * 1.35f;
        return texto;
    }
}
