using Fishy.UI;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HDU-16 CA2 — Flecha que señala hacia la zona de destino de la misión activa.
///
/// <b>No decide nada.</b> Sólo dibuja lo que le mandan: quién decide qué zona hay
/// que señalar —y cuándo dejar de señalarla— es
/// <see cref="Fishy.Mision.MissionManager.ZonaObjetivoActiva"/>, y quien se lo
/// pasa es <see cref="MissionUIController"/>. Está partido así para que la
/// decisión se pueda probar sin escena, sin cámara y sin mapa; aquí sólo queda
/// geometría.
///
/// <b>Orbita alrededor de Otto en vez de pegarse al borde de la pantalla.</b> La
/// flecha se coloca en la línea que va de Otto al destino, a la distancia que haya
/// —si el destino está cerca se posa encima de él— con un tope de
/// <see cref="MisionHudTheme.Medidas.RadioFlecha"/> píxeles. Es una sola fórmula
/// que vale igual con el destino dentro y fuera de la pantalla, y deja la flecha
/// siempre cerca de donde el niño/a ya está mirando, que es su propio personaje.
///
/// Mientras Otto está DENTRO de la zona de destino la flecha se esconde, pero el
/// marcador sigue de servicio: apuntar al sitio donde ya estás es ruido, y lo que
/// falta por hacer ahí lo cuenta el cartel de la misión.
///
/// Se crea solo; no hay que montar nada en la escena.
/// </summary>
[DisallowMultipleComponent]
public class ZoneMarker : MonoBehaviour
{
    public static ZoneMarker Instance { get; private set; }

    [Header("Diagnóstico")]
    [Tooltip("Escribir en consola cada vez que cambia la zona señalada.")]
    public bool verboseLogs = true;

    /// <summary>Zona que se está señalando, o null si el marcador está apagado.</summary>
    public string ZonaObjetivo { get; private set; }

    /// <summary>El marcador está de servicio. Es lo que se enciende al empezar la
    /// misión y se apaga al completarla.</summary>
    public bool Visible => _raiz != null && _raiz.activeSelf;

    /// <summary>La flecha se está dibujando ahora mismo. Puede ser false estando de
    /// servicio: Otto ya llegó a la zona, o la zona todavía no está cargada.</summary>
    public bool Apuntando => Visible && _flecha != null && _flecha.activeSelf;

    private RectTransform _canvasRect;
    private GameObject _raiz;
    private RectTransform _raizRT;
    private GameObject _flecha;
    private RectTransform _flechaRT;
    private GameObject _etiqueta;
    private TextMeshProUGUI _textoZona;

    private Camera _camara;
    private Transform _otto;
    private string _zonaAvisada;   // para no repetir el mismo warning cada frame

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    public static ZoneMarker GetOrCreate()
    {
        if (Instance != null) return Instance;

        var encontrado = FindAnyObjectByType<ZoneMarker>();
        if (encontrado != null) return encontrado;

        return new GameObject("ZoneMarker").AddComponent<ZoneMarker>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ConstruirUI();
        Ocultar();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Lo que le mandan ─────────────────────────────────────────────────────

    /// <summary>
    /// Señala esa zona. Pasar null o vacío es lo mismo que <see cref="Ocultar"/>.
    /// Repetir la zona que ya se estaba señalando no hace nada.
    /// </summary>
    public void Apuntar(string zonaId)
    {
        string nueva = string.IsNullOrWhiteSpace(zonaId) ? null : zonaId.Trim();
        if (nueva == ZonaObjetivo) return;

        ZonaObjetivo = nueva;
        _zonaAvisada = null;

        if (_raiz != null) _raiz.SetActive(ZonaObjetivo != null);

        if (verboseLogs)
        {
            Debug.Log(ZonaObjetivo != null
                ? $"[ZoneMarker] Señalando '{ZonaObjetivo}'."
                : "[ZoneMarker] Sin zona que señalar.", this);
        }

        if (ZonaObjetivo != null) Colocar();
    }

    /// <summary>Apaga el marcador. Es lo que toca al completar la misión (CA4).</summary>
    public void Ocultar() => Apuntar(null);

    // ── Dibujo ───────────────────────────────────────────────────────────────

    // En LateUpdate y no en Update porque la cámara sigue a Otto con suavizado: si
    // se calculara antes de que se moviera, la flecha iría un frame por detrás y se
    // notaría como un temblor al caminar.
    private void LateUpdate()
    {
        if (ZonaObjetivo == null) return;
        Colocar();
    }

    private void Colocar()
    {
        Camera camara = Camara();
        if (camara == null || _raizRT == null) return;

        // Estando ya dentro no hay nada que señalar.
        ZonaActual zonas = ZonaActual.Instance;
        if (zonas != null && zonas.Actual == ZonaObjetivo) { MostrarFlecha(false); return; }

        Vector2? destino = DestinoMundo();
        if (destino == null) { MostrarFlecha(false); return; }

        Transform otto = Otto();
        Vector3 origenMundo = otto != null ? otto.position : camara.transform.position;

        Vector2 enPantallaDestino = camara.WorldToScreenPoint(destino.Value);
        Vector2 enPantallaOrigen  = camara.WorldToScreenPoint(origenMundo);
        Vector2 direccion = enPantallaDestino - enPantallaOrigen;

        // Encima de Otto no hay ninguna dirección que dibujar.
        if (direccion.sqrMagnitude < 1f) { MostrarFlecha(false); return; }

        float distancia = Mathf.Min(direccion.magnitude, MisionHudTheme.Medidas.RadioFlecha);
        Vector2 puntoPantalla = enPantallaOrigen + direccion.normalized * distancia;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, puntoPantalla, null, out Vector2 local))
        {
            MostrarFlecha(false);
            return;
        }

        // El nombre de la zona cuelga bajo la flecha, así que el margen se mide sobre
        // el conjunto: sin esto el cartelito se sale por abajo cuando el destino queda
        // justo debajo de Otto.
        float margen = MisionHudTheme.Medidas.MargenPantalla;
        Vector2 media = _canvasRect.rect.size * 0.5f;
        local.x = Mathf.Clamp(local.x, -media.x + margen, media.x - margen);
        local.y = Mathf.Clamp(local.y, -media.y + margen, media.y - margen);

        _raizRT.anchoredPosition = local;

        float angulo = Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg;
        _flechaRT.localRotation = Quaternion.Euler(0f, 0f, angulo);

        float latido = 1f + Mathf.Sin(Time.unscaledTime * MisionHudTheme.Medidas.VelocidadLatido)
                            * MisionHudTheme.Medidas.AmplitudLatido;
        _flechaRT.localScale = new Vector3(latido, latido, 1f);

        string nombre = ZonaMundo.NombreDe(ZonaObjetivo);
        if (_textoZona != null && _textoZona.text != nombre)
        {
            // La fuente se re-decide con el nombre puesto: al construir el cartelito
            // el texto estaba vacío, y Mango no tiene todos los glifos.
            _textoZona.font = FishyUIKit.FuentePara(nombre);
            _textoZona.text = nombre;
        }

        MostrarFlecha(true);
    }

    private void MostrarFlecha(bool mostrar)
    {
        if (_flecha != null && _flecha.activeSelf != mostrar) _flecha.SetActive(mostrar);
        if (_etiqueta != null && _etiqueta.activeSelf != mostrar) _etiqueta.SetActive(mostrar);
    }

    /// <summary>
    /// A qué punto del mapa apuntar. Se prefiere el punto de aparición de la zona
    /// —es un sitio donde se puede estar de pie, puesto a mano— y si no lo hay, el
    /// centro de su polígono.
    ///
    /// Se resuelve en cada frame y no una sola vez al empezar: las zonas se
    /// registran al activarse, así que al señalar una que todavía no cargó la
    /// respuesta llega unos frames después, y cachear el primer null dejaría la
    /// flecha muda para siempre.
    /// </summary>
    private Vector2? DestinoMundo()
    {
        PuntoDeAparicion punto = PuntoDeAparicion.De(ZonaObjetivo);
        if (punto != null) return punto.Posicion;

        ZonaMundo zona = ZonaMundo.De(ZonaObjetivo);
        if (zona != null) return zona.Centro;

        if (_zonaAvisada != ZonaObjetivo)
        {
            _zonaAvisada = ZonaObjetivo;
            Debug.LogWarning(
                $"[ZoneMarker] No sé dónde está '{ZonaObjetivo}': esa zona no tiene " +
                "ni ZonaMundo ni PuntoDeAparicion en la escena, así que no puedo " +
                "dibujar la flecha. El cartel de la misión sí la va a nombrar.", this);
        }
        return null;
    }

    private Camera Camara()
    {
        if (_camara == null) _camara = Camera.main;
        return _camara;
    }

    private Transform Otto()
    {
        if (_otto == null)
        {
            var otto = FindAnyObjectByType<OttoController>();
            if (otto != null) _otto = otto.transform;
        }
        return _otto;
    }

    // ── Construcción de la UI ────────────────────────────────────────────────

    private void ConstruirUI()
    {
        var canvasGO = new GameObject("MarcadorZonaCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Por debajo de los diálogos, el chat y los menús (950 para arriba): la flecha
        // es una guía de fondo, no puede quedarse encima de una conversación.
        canvas.sortingOrder = 700;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasRect = canvasGO.GetComponent<RectTransform>();

        _raiz = new GameObject("Marcador", typeof(RectTransform));
        _raiz.transform.SetParent(canvasGO.transform, false);
        _raizRT = _raiz.GetComponent<RectTransform>();
        _raizRT.anchorMin = _raizRT.anchorMax = new Vector2(0.5f, 0.5f);
        _raizRT.pivot = new Vector2(0.5f, 0.5f);
        _raizRT.sizeDelta = Vector2.zero;

        ConstruirFlecha();
        ConstruirEtiquetaZona();
    }

    private void ConstruirFlecha()
    {
        _flecha = new GameObject("Flecha", typeof(RectTransform));
        _flecha.transform.SetParent(_raiz.transform, false);
        _flechaRT = _flecha.GetComponent<RectTransform>();
        _flechaRT.anchorMin = _flechaRT.anchorMax = new Vector2(0.5f, 0.5f);
        _flechaRT.pivot = new Vector2(0.5f, 0.5f);
        _flechaRT.sizeDelta = Vector2.zero;

        float largo  = MisionHudTheme.Medidas.LargoFlecha;
        float ancho  = MisionHudTheme.Medidas.AnchoFlecha;
        float grosor = MisionHudTheme.Medidas.GrosorBorde;

        // El contorno es el mismo triángulo un poco más grande y detrás. Sale más
        // barato que un shader de borde y, al escalarse entero, no se deforma.
        Triangulo("Borde", new Vector2(largo + grosor * 2f, ancho + grosor * 2f),
            MisionHudTheme.Colores.FlechaBorde);
        Triangulo("Relleno", new Vector2(largo, ancho), MisionHudTheme.Colores.Flecha);
    }

    private void Triangulo(string nombre, Vector2 tamano, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_flecha.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamano;

        var img = go.GetComponent<Image>();
        img.sprite = FishyUIKit.SpriteTriangulo();
        img.color = color;
        img.raycastTarget = false;   // la flecha no se toca, sólo se mira
    }

    private void ConstruirEtiquetaZona()
    {
        _etiqueta = new GameObject("NombreZona",
            typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));
        _etiqueta.transform.SetParent(_raiz.transform, false);

        var rt = _etiqueta.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -MisionHudTheme.Medidas.BajadaEtiquetaZona);

        var img = _etiqueta.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(img, MisionHudTheme.Colores.FondoEtiquetaZona, radio: 16);
        img.raycastTarget = false;

        var hlg = _etiqueta.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 6, 6);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth  = true; hlg.childControlHeight  = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        // Se ajusta a lo que mida el nombre: "El Bosque" y "zona_2" no ocupan igual.
        var fitter = _etiqueta.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        _textoZona = FishyUIKit.Texto(_etiqueta.transform, "Texto", "",
            MisionHudTheme.Fuente.NombreZona, MisionHudTheme.Colores.Guia,
            TextAlignmentOptions.Center);
        _textoZona.raycastTarget = false;
    }
}
