using System.Collections.Generic;
using Fishy.UI;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Señales de los objetivos "con quién hay que interactuar" de la misión activa:
/// un "!" (o "?") sobre la cabeza del NPC y, si Otto está en su zona pero no lo ve,
/// una flecha alrededor de Otto que apunta hacia él.
///
/// <b>No decide nada, igual que <see cref="ZoneMarker"/>.</b> Recibe la lista de
/// objetivos de <see cref="MissionUIController"/> y dibuja lo que esté pendiente.
///
/// Qué se señala y con qué aspecto:
/// <list type="bullet">
///   <item>Hablar con un NPC y atender un chat de teléfono: "!" y flecha azules.</item>
///   <item>Completar un caso del Modo Detective: "?" y flecha moradas.</item>
/// </list>
///
/// <b>El "!" siempre se dibuja; la flecha no.</b> La flecha sólo sale con Otto en la
/// misma zona que el objetivo: fuera de ella ya lo guía la flecha de zona y dos flechas
/// a la vez confunden. Las de los más cercanos salen más grandes que las de los lejanos.
///
/// <b>Un objetivo sin resolver se reintenta.</b> Un NPC de una zona todavía sin cargar no
/// existe en la escena cuando se entrega la misión; darlo por perdido dejaría ese NPC sin
/// señal para siempre. Se vuelve a buscar cada pocos segundos.
///
/// Se crea solo; no hay que montar nada en la escena.
/// </summary>
[DisallowMultipleComponent]
public class NpcMarker : MonoBehaviour
{
    public static NpcMarker Instance { get; private set; }

    [Header("Diagnóstico")]
    [Tooltip("Escribir en consola cuando aparece o desaparece una señal.")]
    public bool verboseLogs = false;

    [Header("Ajustes")]
    [Tooltip("Cada cuántos segundos se reintenta encontrar un objetivo que no existía.")]
    [Min(0.5f)] public float intervaloReintento = 2f;

    [Tooltip("Cada cuántos segundos se recalcula en qué zona está cada objetivo.")]
    [Min(0.05f)] public float intervaloZona = 0.25f;

    [Header("Orden de dibujo de los signos (! y ?)")]
    [Tooltip("Sorting Layer de los signos. Tiene que ser la misma que la de Otto, los NPC y " +
             "los árboles: entre capas distintas manda la capa, y un signo en una capa de " +
             "abajo se esconde detrás de cualquier árbol.")]
    public string capaDeLosSignos = "Entity";

    [Tooltip("Order in Layer de los signos. Más alto que el de todo lo que pueda tapar " +
             "(Otto y los árboles están en 0 y 1): así el signo se ve siempre, se ponga " +
             "un árbol delante o no.")]
    public int ordenDeLosSignos = 500;

    /// <summary>Todo lo que se dibuja para un mismo destino.</summary>
    private class Senal
    {
        public Component destino;
        public string signo;
        public Color color;

        public Transform signoTransform;
        public RectTransform flechaRT;
        public GameObject flecha;

        public SpriteRenderer[] sprites;   // para saber dónde está la cabeza
        public bool vista;                 // marcada en este frame; las que no, se retiran

        public bool enZonaDeOtto;
        public float proximaRevisionZona;
    }

    private readonly List<ObjetivoMision> _objetivos = new List<ObjetivoMision>();
    private readonly Dictionary<Component, Senal> _senales = new Dictionary<Component, Senal>();
    private readonly List<Component> _aRetirar = new List<Component>();

    private RectTransform _canvasRect;
    private Camera _camara;
    private Transform _otto;
    private float _proximoReintento;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    public static NpcMarker GetOrCreate()
    {
        if (Instance != null) return Instance;

        var encontrado = FindAnyObjectByType<NpcMarker>();
        if (encontrado != null) return encontrado;

        return new GameObject("NpcMarker").AddComponent<NpcMarker>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ConstruirCanvas();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Lo que le mandan ─────────────────────────────────────────────────────

    /// <summary>
    /// Los objetivos de la misión activa. Pasar null o vacío apaga todas las señales.
    /// Se copia la lista: la del rastreador es suya y puede cambiar por debajo.
    /// </summary>
    public void Actualizar(IReadOnlyList<ObjetivoMision> objetivos)
    {
        _objetivos.Clear();

        if (objetivos != null)
        {
            foreach (ObjetivoMision objetivo in objetivos)
                if (objetivo != null && EsDeInteraccion(objetivo.tipo)) _objetivos.Add(objetivo);
        }

        _proximoReintento = 0f;   // que los que faltan se busquen ya, no dentro de 2 s
    }

    private static bool EsDeInteraccion(TipoObjetivo tipo) =>
        tipo == TipoObjetivo.HablarConNpc ||
        tipo == TipoObjetivo.ChatearPorTelefono ||
        tipo == TipoObjetivo.CompletarCasoDetective;

    /// <summary>Qué objeto del mundo hay que buscar para este objetivo, o null si aún no
    /// está resuelto.</summary>
    private static Component DestinoDe(ObjetivoMision objetivo)
    {
        switch (objetivo.tipo)
        {
            case TipoObjetivo.HablarConNpc:            return objetivo.npc;
            case TipoObjetivo.ChatearPorTelefono:      return objetivo.telefono;
            case TipoObjetivo.CompletarCasoDetective:  return objetivo.detective;
            default:                                   return null;
        }
    }

    private static void EstiloDe(TipoObjetivo tipo, out string signo, out Color color)
    {
        if (tipo == TipoObjetivo.CompletarCasoDetective)
        {
            signo = "?";
            color = MisionHudTheme.Colores.ObjetivoDetective;
        }
        else
        {
            signo = "!";
            color = MisionHudTheme.Colores.ObjetivoNpc;
        }
    }

    // ── Dibujo ───────────────────────────────────────────────────────────────

    // LateUpdate por lo mismo que ZoneMarker: la cámara sigue a Otto con suavizado y
    // calcular antes de que se mueva haría temblar las flechas.
    private void LateUpdate()
    {
        ReintentarPendientes();

        foreach (Senal s in _senales.Values) s.vista = false;

        ZonaActual zonas = ZonaActual.Instance;
        Camera camara = Camara();

        // Primera pasada: qué señales hay que tener y dónde está cada una.
        foreach (ObjetivoMision objetivo in _objetivos)
        {
            if (objetivo.cumplido) continue;

            Component destino = DestinoDe(objetivo);
            if (destino == null || !destino.gameObject.activeInHierarchy) continue;

            if (!_senales.TryGetValue(destino, out Senal senal))
            {
                senal = CrearSenal(destino, objetivo.tipo);
                _senales[destino] = senal;
                if (verboseLogs) Debug.Log($"[NpcMarker] Señalando a '{destino.name}'.", this);
            }
            senal.vista = true;
        }

        // Lo que ya no se pide se retira: objetivo cumplido, misión cambiada o NPC apagado.
        _aRetirar.Clear();
        foreach (var par in _senales)
            if (!par.Value.vista || par.Key == null) _aRetirar.Add(par.Key);
        foreach (Component clave in _aRetirar) Retirar(clave);

        if (camara == null) return;

        // Segunda pasada: colocar. Las flechas necesitan a todas para repartir tamaños.
        foreach (Senal s in _senales.Values)
        {
            Vector3 cabeza = Cabeza(s);
            ColocarSigno(s, cabeza);
            ColocarFlecha(s, cabeza, camara, zonas);
        }
    }

    private void ReintentarPendientes()
    {
        if (Time.unscaledTime < _proximoReintento) return;
        _proximoReintento = Time.unscaledTime + intervaloReintento;

        foreach (ObjetivoMision objetivo in _objetivos)
        {
            if (objetivo.cumplido || DestinoDe(objetivo) != null) continue;
            objetivo.Resolver();
        }
    }

    // ── Signo sobre la cabeza ────────────────────────────────────────────────

    private void ColocarSigno(Senal s, Vector3 cabeza)
    {
        float rebote = Mathf.Sin(Time.unscaledTime * MisionHudTheme.Medidas.VelocidadRebote)
                       * MisionHudTheme.Medidas.AmplitudRebote;
        s.signoTransform.position =
            cabeza + Vector3.up * (MisionHudTheme.Medidas.ElevacionSigno + rebote);
    }

    /// <summary>
    /// Lo más alto del objeto, para poner el signo encima. Con sprites, el techo de sus
    /// límites juntos; sin ellos —un lanzador de chat invisible— el techo de su collider;
    /// y sin nada, su posición.
    /// </summary>
    private static Vector3 Cabeza(Senal s)
    {
        bool hay = false;
        Bounds limites = default;

        foreach (SpriteRenderer sprite in s.sprites)
        {
            if (sprite == null || !sprite.enabled) continue;
            if (!hay) { limites = sprite.bounds; hay = true; }
            else limites.Encapsulate(sprite.bounds);
        }

        if (!hay)
        {
            Collider2D collider = s.destino.GetComponent<Collider2D>();
            if (collider != null) { limites = collider.bounds; hay = true; }
        }

        if (!hay) return s.destino.transform.position;
        return new Vector3(limites.center.x, limites.max.y, s.destino.transform.position.z);
    }

    // ── Flecha alrededor de Otto ─────────────────────────────────────────────

    private void ColocarFlecha(Senal s, Vector3 cabeza, Camera camara, ZonaActual zonas)
    {
        // Sólo con Otto en la misma zona: fuera de ella manda la flecha de zona.
        RefrescarZona(s, zonas);
        if (!s.enZonaDeOtto) { MostrarFlecha(s, false); return; }

        // Si ya está en pantalla basta con el signo.
        Vector3 vp = camara.WorldToViewportPoint(cabeza);
        bool enPantalla = vp.x > 0.03f && vp.x < 0.97f && vp.y > 0.03f && vp.y < 0.97f;
        if (enPantalla) { MostrarFlecha(s, false); return; }

        Transform otto = Otto();
        Vector3 origenMundo = otto != null ? otto.position : camara.transform.position;

        Vector2 origen = camara.WorldToScreenPoint(origenMundo);
        Vector2 direccion = (Vector2)camara.WorldToScreenPoint(s.destino.transform.position) - origen;
        if (direccion.sqrMagnitude < 1f) { MostrarFlecha(s, false); return; }

        Vector2 puntoPantalla = origen + direccion.normalized * MisionHudTheme.Medidas.RadioFlecha;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, puntoPantalla, null, out Vector2 local))
        {
            MostrarFlecha(s, false);
            return;
        }

        float margen = MisionHudTheme.Medidas.MargenPantalla;
        Vector2 media = _canvasRect.rect.size * 0.5f;
        local.x = Mathf.Clamp(local.x, -media.x + margen, media.x - margen);
        local.y = Mathf.Clamp(local.y, -media.y + margen, media.y - margen);
        s.flechaRT.anchoredPosition = local;

        float angulo = Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg;
        s.flechaRT.localRotation = Quaternion.Euler(0f, 0f, angulo);

        // Cuanto más cerca, más grande. La distancia se mide en alturas de pantalla.
        float distancia = direccion.magnitude / Mathf.Max(1f, Screen.height);
        float t = Mathf.InverseLerp(MisionHudTheme.Medidas.DistanciaCerca,
                                    MisionHudTheme.Medidas.DistanciaLejos, distancia);
        float escala = Mathf.Lerp(MisionHudTheme.Medidas.EscalaCerca,
                                  MisionHudTheme.Medidas.EscalaLejos, t);
        float latido = 1f + Mathf.Sin(Time.unscaledTime * MisionHudTheme.Medidas.VelocidadLatido)
                            * MisionHudTheme.Medidas.AmplitudLatido;
        s.flechaRT.localScale = new Vector3(escala * latido, escala * latido, 1f);

        MostrarFlecha(s, true);
    }

    /// <summary>¿El objetivo está en la zona donde está Otto? Se recalcula cada pocas
    /// décimas y no en cada frame: es una prueba punto-en-polígono por zona.</summary>
    private void RefrescarZona(Senal s, ZonaActual zonas)
    {
        if (Time.unscaledTime < s.proximaRevisionZona) return;
        s.proximaRevisionZona = Time.unscaledTime + intervaloZona;

        s.enZonaDeOtto = zonas != null &&
                         zonas.ZonaEn(s.destino.transform.position) == zonas.Actual;
    }

    private static void MostrarFlecha(Senal s, bool mostrar)
    {
        if (s.flecha.activeSelf != mostrar) s.flecha.SetActive(mostrar);
    }

    // ── Altas y bajas ────────────────────────────────────────────────────────

    private Senal CrearSenal(Component destino, TipoObjetivo tipo)
    {
        EstiloDe(tipo, out string signo, out Color color);

        var s = new Senal
        {
            destino = destino,
            signo = signo,
            color = color,
            sprites = destino.GetComponentsInChildren<SpriteRenderer>(),
        };

        // Fuera de la jerarquía del NPC a propósito: si el NPC se voltea con escala
        // negativa, el signo se voltearía con él y saldría al revés.
        var go = new GameObject("Signo_" + destino.name);
        go.transform.SetParent(transform, false);
        s.signoTransform = go.transform;

        Sprite dibujo = tipo == TipoObjetivo.CompletarCasoDetective
            ? Dibujos.Interrogacion : Dibujos.Exclamacion;

        if (dibujo != null) ConstruirSignoConDibujo(go, dibujo);
        else ConstruirSignoConTexto(go, s);

        ConstruirFlecha(s);
        return s;
    }

    /// <summary>El signo como imagen. El dibujo ya trae su color, así que no se tiñe;
    /// se escala para que mida <see cref="MisionHudTheme.Medidas.AlturaSignoDibujo"/>
    /// de alto en el mundo, sea cual sea el tamaño en píxeles del PNG.</summary>
    private void ConstruirSignoConDibujo(GameObject go, Sprite dibujo)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = dibujo;
        sr.sortingLayerID = IdDeLaCapa();
        sr.sortingOrder = ordenDeLosSignos;

        float alto = Mathf.Max(0.0001f, dibujo.bounds.size.y);
        float escala = MisionHudTheme.Medidas.AlturaSignoDibujo / alto;
        go.transform.localScale = new Vector3(escala, escala, 1f);
    }

    /// <summary>Respaldo cuando no hay dibujo: el signo escrito con la fuente.</summary>
    private void ConstruirSignoConTexto(GameObject go, Senal s)
    {
        var texto = go.AddComponent<TextMeshPro>();
        texto.text = s.signo;
        texto.font = FishyUIKit.FuentePara(s.signo);
        texto.fontSize = MisionHudTheme.Medidas.TamanoSigno;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = s.color;
        texto.outlineWidth = 0.3f;
        texto.outlineColor = MisionHudTheme.Colores.FlechaBorde;
        texto.rectTransform.sizeDelta = new Vector2(4f, 4f);
        texto.sortingLayerID = IdDeLaCapa();
        texto.sortingOrder = ordenDeLosSignos;
    }

    /// <summary>
    /// La Sorting Layer de los signos. Si no existe con ese nombre (alguien la renombró o
    /// la quitó) se cae a Default en vez de fallar: peor un signo tapado por un árbol que
    /// un error en consola cada vez que aparece un NPC.
    /// </summary>
    private int IdDeLaCapa()
    {
        int id = SortingLayer.NameToID(capaDeLosSignos);
        return SortingLayer.IsValid(id) ? id : 0;
    }

    /// <summary>
    /// Los dibujos de <c>Assets/Resources/NpcMarker/</c>. Se cargan una vez y se
    /// guardan; cada uno puede faltar, y entonces se usa el dibujado por código.
    /// </summary>
    private static class Dibujos
    {
        private const string Carpeta = "NpcMarker/";

        private static Sprite _flecha, _exclamacion, _interrogacion;
        private static bool _cargados;

        public static Sprite Flecha        { get { Cargar(); return _flecha; } }
        public static Sprite Exclamacion   { get { Cargar(); return _exclamacion; } }
        public static Sprite Interrogacion { get { Cargar(); return _interrogacion; } }

        private static void Cargar()
        {
            if (_cargados) return;
            _cargados = true;

            _flecha        = Resources.Load<Sprite>(Carpeta + "flecha");
            _exclamacion   = Resources.Load<Sprite>(Carpeta + "exclamacion");
            _interrogacion = Resources.Load<Sprite>(Carpeta + "interrogacion");
        }
    }

    private void ConstruirFlecha(Senal s)
    {
        s.flecha = new GameObject("Flecha_" + s.destino.name, typeof(RectTransform));
        s.flecha.transform.SetParent(_canvasRect, false);
        s.flechaRT = s.flecha.GetComponent<RectTransform>();
        s.flechaRT.anchorMin = s.flechaRT.anchorMax = new Vector2(0.5f, 0.5f);
        s.flechaRT.pivot = new Vector2(0.5f, 0.5f);
        s.flechaRT.sizeDelta = Vector2.zero;

        float largo  = MisionHudTheme.Medidas.LargoFlecha;
        float ancho  = MisionHudTheme.Medidas.AnchoFlecha;
        float grosor = MisionHudTheme.Medidas.GrosorBorde;

        Triangulo(s.flecha.transform, "Borde", new Vector2(largo + grosor * 2f, ancho + grosor * 2f),
            MisionHudTheme.Colores.FlechaBorde);
        Triangulo(s.flecha.transform, "Relleno", new Vector2(largo, ancho), s.color);

        s.flecha.SetActive(false);
    }

    private static void Triangulo(Transform padre, string nombre, Vector2 tamano, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamano;

        var img = go.GetComponent<Image>();
        // El contorno usa el mismo dibujo, más grande y oscuro: así vale con cualquier
        // forma de flecha y el PNG no tiene que traer borde.
        img.sprite = Dibujos.Flecha != null ? Dibujos.Flecha : FishyUIKit.SpriteTriangulo();
        img.color = color;
        img.raycastTarget = false;
    }

    private void Retirar(Component clave)
    {
        if (!_senales.TryGetValue(clave, out Senal s)) { _senales.Remove(clave); return; }

        if (s.signoTransform != null) Destroy(s.signoTransform.gameObject);
        if (s.flecha != null) Destroy(s.flecha);
        _senales.Remove(clave);

        if (verboseLogs) Debug.Log("[NpcMarker] Señal retirada.", this);
    }

    private void ConstruirCanvas()
    {
        var canvasGO = new GameObject("NpcMarkerCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 705;   // sobre la flecha de zona (700), bajo el cartel (710)

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasRect = canvasGO.GetComponent<RectTransform>();
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
}
