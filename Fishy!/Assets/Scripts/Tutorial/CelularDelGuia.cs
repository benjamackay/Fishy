using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El celular por el que habla el guía del tutorial: una pantalla de chat pegada
/// al borde izquierdo, con las instrucciones cayendo como burbujas.
///
/// <b>Es todo nuevo y no toca nada del juego.</b> Se construye en código, en un
/// Canvas propio, y lo único que usa de fuera son herramientas de dibujo que no
/// modifica: <see cref="FishyUIKit"/> para las esquinas redondeadas y la fuente.
/// Se parece al celular del Modo Detective, pero no es el mismo: aquel se monta
/// con <c>MarcoTelefono</c> en el centro de la pantalla, y este va al lado y con
/// otros colores.
///
/// <b>Por qué a la izquierda y a esa altura.</b> Sale de medir REFE_TUTORIAL.png:
/// el cartel de misiones activas ocupa la esquina de arriba a la izquierda (440 de
/// ancho, 28 de margen) y Otto anda por el centro, así que el hueco libre es la
/// banda izquierda por debajo del cartel. Las medidas de abajo son las de esa
/// referencia llevadas a 1920×1080.
///
/// <b>Orden de dibujo.</b> 1250, que lo deja por encima de lo único que comparte
/// pantalla con él: el cartel de misiones (710) y los avisos de zona (1000). Las
/// pantallas que se dibujan más arriba —el panel del Tab (1200), el álbum (1300),
/// la pausa (9500)— no compiten con el celular, porque con cualquiera de ellas
/// abierta el tutorial ya lo apagó.
///
/// Lo único que recibe el puntero es la zona de la conversación, para poder subir
/// a releer un mensaje que ya se fue hacia arriba; el bisel y las barras lo dejan
/// pasar.
///
/// <b>No se queda encima de todo.</b> Quien lo enciende y lo apaga es el tutorial
/// (<see cref="Mostrar"/>): se va mientras hay un diálogo, un chat, un caso
/// detective o el panel del Tab, que son justo las pantallas que el jugador tiene
/// que estar mirando. Y mientras está apagado el guía no habla: la cola de
/// mensajes espera a que vuelva a verse (ver <see cref="SoltarPendientes"/>), para
/// no soltar una instrucción a una pantalla que no está.
/// </summary>
[DisallowMultipleComponent]
public class CelularDelGuia : MonoBehaviour
{
    public static CelularDelGuia Instance { get; private set; }

    [Header("Dónde")]
    [Tooltip("Distancia a la esquina de arriba a la izquierda, en píxeles de 1920×1080. " +
             "El valor por defecto deja el celular justo debajo del cartel de misiones.")]
    public Vector2 margen = new Vector2(105f, 227f);

    [Tooltip("Tamaño de la pantalla (sin contar el bisel).")]
    public Vector2 pantalla = new Vector2(396f, 630f);

    [Tooltip("Grosor del bisel negro alrededor de la pantalla.")]
    public float borde = 20f;

    [Tooltip("Orden de dibujo del Canvas. Por encima del cartel de misiones (710) y de " +
             "los avisos de zona (1000), que es lo único que comparte pantalla con él.")]
    public int ordenDeDibujo = 1250;

    [Header("Colores (los del mockup)")]
    public Color colorBisel = new Color32(0x00, 0x00, 0x00, 255);
    public Color colorPantalla = new Color32(0x7C, 0xA7, 0xFF, 255);
    public Color colorBarras = new Color32(0x0D, 0x16, 0x1C, 255);
    public Color colorBurbuja = new Color32(0xB4, 0xCD, 0xFF, 255);
    public Color colorTextoBurbuja = Color.white;
    public Color colorBarraInferior = new Color32(0xCB, 0xDD, 0xFF, 255);

    [Header("Medidas finas")]
    public float altoBarras = 44f;
    public float margenInterior = 16f;
    public float anchoMaximoBurbuja = 346f;
    public float separacionBurbujas = 20f;
    public float tamanoTextoBurbuja = 30f;
    public float tamanoReloj = 26f;

    [Header("Separador de mensajes nuevos")]
    [Tooltip("La píldora centrada que anuncia un grupo nuevo, como en separador.png. " +
             "Oscura sobre la pantalla clara, igual que las barras del celular.")]
    public Color colorSeparador = new Color32(0x0D, 0x16, 0x1C, 220);
    public Color colorTextoSeparador = Color.white;

    [Tooltip("Más chico que el de las burbujas: en el mockup la píldora no compite " +
             "con los mensajes, solo los ordena.")]
    public float tamanoTextoSeparador = 22f;

    [Header("Lo importante, destacado")]
    [Tooltip("Color de las palabras que hay que mirar o pulsar. El amarillo cálido del " +
             "juego: el mismo del aro del minimapa y del celular del TAB en " +
             "botoninteractuar.png.")]
    public Color colorDestacado = new Color32(0xF4, 0xA7, 0x23, 255);

    // Qué se destaca lo marca el propio texto con *asteriscos*; ver Destacar().

    [Header("Imágenes que puede mandar el guía")]
    [Tooltip("Las imágenes que los textos llaman con [img:nombre]. No se rellena aquí: " +
             "este celular se crea en código y no está en la escena, así que se las pasa " +
             "TutorialPasos, que es donde viven los textos.")]
    public ImagenDelGuia[] imagenes = new ImagenDelGuia[0];

    [Tooltip("Ancho de la imagen dentro de la burbuja. El alto sale solo, respetando " +
             "la proporción del sprite.")]
    public float anchoImagen = 140f;

    /// <summary>Una imagen con el nombre por el que la llaman los textos.</summary>
    [System.Serializable]
    public class ImagenDelGuia
    {
        public string nombre;
        public Sprite imagen;
    }

    [Header("Ritmo y scroll")]
    [Tooltip("Segundos entre un mensaje y el siguiente cuando el guía suelta varios " +
             "de una vez. Cerca del 0,5 que usa el chat del Modo Detective: esperar " +
             "más se siente lento, y el texto no se va a ningún lado — queda en " +
             "pantalla para leerlo con calma.")]
    [Min(0f)] public float segundosEntreMensajes = 0.6f;

    [Tooltip("Cuánto sube la conversación por muesca de la rueda del ratón.")]
    [Min(0f)] public float sensibilidadScroll = 40f;

    [Header("Entrada")]
    [Tooltip("Segundos que tarda en entrar deslizándose desde el borde izquierdo.")]
    [Min(0.05f)] public float duracionEntrada = 0.45f;

    [Tooltip("Tamaño con el que empieza la entrada (1 = el tamaño final).")]
    [Range(0.2f, 1f)] public float escalaDeEntrada = 0.6f;

    [Tooltip("Cuánto se pasa de largo antes de asentarse. 0 = nada; 1,2 es lo que usa " +
             "el panel del Tab al subir.")]
    [Range(0f, 3f)] public float sobrepaso = 1.2f;

    [Header("Título de arriba")]
    [Tooltip("El \"TUTORIAL 1/5\" centrado en el borde superior de la pantalla.")]
    public float tamanoTitulo = 60f;
    public float margenSuperiorTitulo = 18f;
    public Color colorTitulo = Color.white;
    public Color colorSombraTitulo = new Color(0f, 0f, 0f, 0.65f);

    private Canvas canvas;
    private GameObject celular;
    private RectTransform marco;
    private Coroutine entrada;
    private bool yaEntroUnaVez;
    private RectTransform contenido;
    private RectTransform visor;
    private ScrollRect scroll;
    private TextMeshProUGUI reloj;
    private TextMeshProUGUI titulo, tituloSombra;
    private float proximoRefrescoDelReloj;

    /// <summary>Todo lo dibujado en la conversación: burbujas y separadores. Van
    /// juntos porque <see cref="Limpiar"/> los borra igual, y porque para decidir si
    /// un grupo lleva separador hace falta saber si hay algo encima, sea lo que
    /// sea.</summary>
    private readonly List<GameObject> dibujados = new List<GameObject>();

    /// <summary>
    /// Una cosa por dibujar cuando le toque: o una burbuja con su texto, o el
    /// separador de un grupo con cuántos mensajes anuncia.
    ///
    /// Comparten cola porque lo que importa es el orden: el separador tiene que
    /// salir justo antes de su grupo, y con dos colas aparte habría que mantenerlas
    /// sincronizadas a mano.
    /// </summary>
    private readonly struct Pendiente
    {
        public readonly string Texto;     // una burbuja de texto
        public readonly Sprite Imagen;    // o una de imagen
        public readonly int    Cuantos;   // o el separador, con su número

        private Pendiente(string texto, Sprite imagen, int cuantos)
        {
            Texto = texto; Imagen = imagen; Cuantos = cuantos;
        }

        public static Pendiente Burbuja(string texto)  => new Pendiente(texto, null, 0);
        public static Pendiente Foto(Sprite imagen)    => new Pendiente(null, imagen, 0);
        public static Pendiente Separador(int cuantos) => new Pendiente(null, null, cuantos);

        public bool EsImagen    => Imagen != null;
        public bool EsSeparador => Texto == null && Imagen == null;
    }

    /// <summary>Lo que el guía ya dijo pero todavía no se ha dibujado: sale de a uno
    /// (ver <see cref="SoltarPendientes"/>).</summary>
    private readonly Queue<Pendiente> pendientes = new Queue<Pendiente>();
    private Coroutine rutina;

    /// <summary>Crea el celular si no existe. Pensado para que el tutorial no tenga
    /// que montarlo a mano en la escena.</summary>
    public static CelularDelGuia GetOrCreate()
    {
        if (Instance != null) return Instance;

        CelularDelGuia encontrado = FindAnyObjectByType<CelularDelGuia>();
        if (encontrado != null) return encontrado;

        return new GameObject("CelularDelGuia").AddComponent<CelularDelGuia>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Construir();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // La primera entrada. Va aquí y no en Mostrar() porque el celular nace ya
        // montado y a la vista: nadie lo enciende, así que nadie la lanzaría.
        Entrar();
    }

    private void OnEnable()
    {
        // Unity para las corrutinas al desactivar el objeto y no las reanuda sola.
        // Sin esto, lo que quedara en la cola no se dibujaría nunca.
        if (rutina == null && pendientes.Count > 0) rutina = StartCoroutine(SoltarPendientes());
    }

    private void OnDisable()
    {
        // Ya las paró Unity; queda anotado para que OnEnable las vuelva a lanzar.
        rutina = null;

        // Una entrada cortada a medias dejaría el celular fuera de pantalla y
        // encogido para siempre. Se le da el sitio final a mano.
        if (entrada == null || marco == null) return;
        entrada = null;
        marco.anchoredPosition = new Vector2(margen.x, -margen.y);
        marco.localScale = Vector3.one;
    }

    private void Update()
    {
        // El reloj del mockup marca una hora de verdad. Mirarlo cada frame sería
        // tirar trabajo: con dos veces por minuto va sobrado.
        if (reloj == null || Time.unscaledTime < proximoRefrescoDelReloj) return;
        proximoRefrescoDelReloj = Time.unscaledTime + 30f;
        reloj.text = System.DateTime.Now.ToString("HH:mm");
    }

    // ── Lo que usa el tutorial ───────────────────────────────────────────────

    /// <summary>
    /// Suelta lo que dice el guía. Un renglón en blanco parte el texto en dos
    /// burbujas, como en la referencia: el saludo en una y la instrucción en otra.
    ///
    /// Cada llamada es <b>un grupo</b>, y por eso se sabe cuántos mensajes trae antes
    /// de soltar ninguno: ese es el número que anuncia el separador.
    ///
    /// Un trozo que sea solo <c>[img:nombre]</c> no es texto: sale como una burbuja
    /// con esa imagen, buscada en la lista <see cref="imagenes"/>. Cuenta como un
    /// mensaje más, que es lo que es.
    /// </summary>
    public void Decir(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;

        var partes = new List<Pendiente>();
        foreach (string parte in texto.Split(new[] { "\n\n", "\r\n\r\n" },
                                             System.StringSplitOptions.RemoveEmptyEntries))
        {
            string limpio = parte.Trim();
            if (limpio.Length == 0) continue;

            Match marca = MarcaDeImagen.Match(limpio);
            if (!marca.Success) { partes.Add(Pendiente.Burbuja(limpio)); continue; }

            Sprite imagen = BuscarImagen(marca.Groups[1].Value.Trim());
            if (imagen != null) partes.Add(Pendiente.Foto(imagen));
        }

        if (partes.Count == 0) return;

        // El primer grupo no lleva separador: no habría nada encima de lo que
        // separarlo y la píldora anunciaría el vacío. Se mira también la cola, no
        // solo lo dibujado: si el guía habla dos veces seguidas, el segundo grupo sí
        // tiene algo delante aunque todavía no se vea en pantalla.
        if (dibujados.Count > 0 || pendientes.Count > 0)
            pendientes.Enqueue(Pendiente.Separador(partes.Count));

        foreach (Pendiente parte in partes) pendientes.Enqueue(parte);

        if (rutina == null && isActiveAndEnabled) rutina = StartCoroutine(SoltarPendientes());
    }

    /// <summary>Un renglón que sea solo <c>[img:nombre]</c> y nada más.</summary>
    private static readonly Regex MarcaDeImagen = new Regex(@"^\[img:(.+?)\]$");

    /// <summary>
    /// La imagen que los textos llaman por ese nombre.
    ///
    /// Si no está, se avisa en vez de callarlo: una marca mal escrita dejaría el
    /// mensaje sin su imagen y no habría forma de saber por qué.
    /// </summary>
    private Sprite BuscarImagen(string nombre)
    {
        if (imagenes != null)
        {
            foreach (ImagenDelGuia entrada in imagenes)
            {
                if (entrada == null || entrada.imagen == null) continue;
                if (string.Equals(entrada.nombre?.Trim(), nombre,
                                  System.StringComparison.OrdinalIgnoreCase))
                    return entrada.imagen;
            }
        }

        Debug.LogWarning($"[Tutorial] El celular del guía no tiene ninguna imagen llamada " +
                          $"'{nombre}'. Añádela en su lista \"Imágenes que puede mandar el " +
                          "guía\" o corrige la marca del texto.", this);
        return null;
    }

    /// <summary>
    /// Va soltando la cola de a un mensaje. Dos burbujas apareciendo juntas se leen
    /// como un muro de texto; de a una se leen como alguien escribiendo, y da
    /// tiempo a mirar la primera antes de que llegue la segunda.
    ///
    /// El primero sale sin espera: el jugador acaba de hacer algo y la respuesta
    /// tiene que ser inmediata. Y la espera es en tiempo real, para que una pausa
    /// no deje un mensaje a medio camino.
    ///
    /// Con el celular apagado la cola se detiene: si el guía hablara mientras el
    /// jugador está en un diálogo o en el panel, sus mensajes aparecerían ya dichos
    /// al volver, sin que nadie los haya visto llegar.
    ///
    /// El separador gasta su turno como uno más: sale solo y, tras la misma pausa,
    /// su grupo empieza a caer debajo. Es el gesto del celular de verdad, donde la
    /// raya queda marcando dónde te habías quedado.
    /// </summary>
    private IEnumerator SoltarPendientes()
    {
        while (pendientes.Count > 0)
        {
            while (!EstaVisible) yield return null;

            Pendiente siguiente = pendientes.Dequeue();
            if      (siguiente.EsSeparador) CrearSeparador(siguiente.Cuantos);
            else if (siguiente.EsImagen)    CrearImagen(siguiente.Imagen);
            else                            CrearBurbuja(siguiente.Texto);

            if (pendientes.Count > 0) yield return new WaitForSecondsRealtime(segundosEntreMensajes);
        }
        rutina = null;
    }

    /// <summary>El rótulo de arriba. Vacío lo esconde.</summary>
    public void PonerTitulo(string texto)
    {
        if (titulo == null) return;

        bool hay = !string.IsNullOrWhiteSpace(texto);
        titulo.transform.parent.gameObject.SetActive(hay);
        if (!hay) return;

        // La fuente se resuelve AQUÍ y no al construir: el título nace vacío, y
        // FuentePara("") devuelve la de cuerpo. Sin esto el rótulo se quedaba con
        // esa para siempre, en otra tipografía que el resto del juego.
        TMP_FontAsset fuente = FishyUIKit.FuentePara(texto);
        titulo.font = fuente;
        tituloSombra.font = fuente;

        titulo.text = texto;
        tituloSombra.text = texto;
    }

    public void Mostrar(bool visible)
    {
        if (canvas == null || canvas.gameObject.activeSelf == visible) return;

        canvas.gameObject.SetActive(visible);

        // La entrada es la presentación del aparato, y eso pasa una vez. El celular
        // se esconde y vuelve muchas veces durante el tutorial —cada diálogo, cada
        // vez que se abre el panel—, y animarlo en todas convertiría un detalle en
        // un tic. Al volver, simplemente está.
        if (visible && !yaEntroUnaVez) Entrar();
    }

    /// <summary>
    /// Deja el celular fuera de pantalla por la izquierda y lo trae a su sitio.
    ///
    /// Es la misma curva con la que sube el panel del Tab —entra rápido, se pasa un
    /// poco de largo y se asienta— y el mismo arranque en pequeño, para que los dos
    /// aparatos se sientan del mismo juego. Lo que cambia es de dónde viene: el
    /// panel sube desde abajo y este entra desde el borde izquierdo, que es el suyo.
    ///
    /// En tiempo real, porque también tiene que animarse con el juego detenido.
    /// </summary>
    private void Entrar()
    {
        yaEntroUnaVez = true;
        if (marco == null) return;

        if (entrada != null) StopCoroutine(entrada);
        entrada = StartCoroutine(RutinaDeEntrada());
    }

    private IEnumerator RutinaDeEntrada()
    {
        Vector2 reposo = new Vector2(margen.x, -margen.y);
        Vector2 fuera = new Vector2(-(marco.rect.width + 40f), reposo.y);

        // Fuera de pantalla YA, antes de ceder el frame: si esto esperara, el
        // primer dibujo pillaría el celular en su sitio y la animación empezaría
        // con un parpadeo.
        marco.anchoredPosition = fuera;
        marco.localScale = Vector3.one * escalaDeEntrada;

        // Un frame de margen antes de empezar a contar. El primero después de
        // cargar la escena trae como delta todo lo que tardó la carga —medio
        // segundo largo—, y con él la animación se terminaba de una tacada: se veía
        // aparecer el celular ya puesto.
        yield return null;

        for (float t = 0f; t < duracionEntrada; )
        {
            // Mismo recorte que usa BotonCelularMenu con su resorte: un tirón de
            // framerate no debe saltarse el movimiento.
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

            float k = ConSobrepaso(Mathf.Clamp01(t / duracionEntrada));
            marco.anchoredPosition = Vector2.LerpUnclamped(fuera, reposo, k);
            marco.localScale = Vector3.LerpUnclamped(Vector3.one * escalaDeEntrada, Vector3.one, k);
            yield return null;
        }

        marco.anchoredPosition = reposo;
        marco.localScale = Vector3.one;
        entrada = null;
    }

    /// <summary>El "easeOutBack" de siempre, igual que en <c>MenuController</c>.</summary>
    private float ConSobrepaso(float k)
    {
        float c1 = sobrepaso;
        float c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    /// <summary>Si el celular está a la vista. Mientras no lo esté, el guía calla.</summary>
    public bool EstaVisible => canvas != null && canvas.gameObject.activeSelf;

    /// <summary>Vacía la conversación. No se usa en el tutorial, pero deja el
    /// celular reutilizable sin tener que destruirlo y volver a montarlo.</summary>
    public void Limpiar()
    {
        if (rutina != null) { StopCoroutine(rutina); rutina = null; }
        pendientes.Clear();

        foreach (GameObject go in dibujados)
            if (go != null) Destroy(go);
        dibujados.Clear();
    }

    // ── Construcción ─────────────────────────────────────────────────────────

    private void Construir()
    {
        // El GraphicRaycaster es lo que permite arrastrar la conversación y usar la
        // rueda sobre ella. Apagar el celular entero ya le quita los clics, así que
        // no hace falta encenderlo y apagarlo aparte.
        var canvasGO = new GameObject("CelularDelGuiaCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ordenDeDibujo;

        var escalador = canvasGO.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;

        ConstruirTitulo(canvasGO.transform);
        ConstruirCelular(canvasGO.transform);
    }

    private void ConstruirTitulo(Transform padre)
    {
        var cont = new GameObject("Titulo", typeof(RectTransform));
        cont.transform.SetParent(padre, false);

        var rt = (RectTransform)cont.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -margenSuperiorTitulo);
        rt.sizeDelta = new Vector2(900f, tamanoTitulo * 1.4f);

        // La sombra es una copia detrás y desplazada. Da el mismo recorte oscuro de
        // la referencia sin tocar el material de la fuente, que es compartido.
        tituloSombra = FishyUIKit.Texto(cont.transform, "Sombra", "", tamanoTitulo,
            colorSombraTitulo, TextAlignmentOptions.Top);
        Estirar((RectTransform)tituloSombra.transform, new Vector2(3f, -3f));

        titulo = FishyUIKit.Texto(cont.transform, "Texto", "", tamanoTitulo,
            colorTitulo, TextAlignmentOptions.Top);
        Estirar((RectTransform)titulo.transform, Vector2.zero);

        cont.SetActive(false);
    }

    private void ConstruirCelular(Transform padre)
    {
        // Bisel: el aparato visto desde fuera. Anclado arriba a la izquierda, que es
        // donde la referencia lo pone y donde no estorba al cartel de misiones.
        celular = new GameObject("Celular", typeof(RectTransform), typeof(Image));
        celular.transform.SetParent(padre, false);

        marco = (RectTransform)celular.transform;
        marco.anchorMin = marco.anchorMax = new Vector2(0f, 1f);
        marco.pivot = new Vector2(0f, 1f);
        marco.anchoredPosition = new Vector2(margen.x, -margen.y);
        marco.sizeDelta = pantalla + Vector2.one * (borde * 2f);

        var imgMarco = celular.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(imgMarco, colorBisel, 64, 24);
        imgMarco.raycastTarget = false;

        // Pantalla: el fondo azul sobre el que caen las burbujas.
        var pant = new GameObject("Pantalla", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        pant.transform.SetParent(celular.transform, false);
        Estirar((RectTransform)pant.transform, Vector2.zero, borde);

        var imgPant = pant.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(imgPant, colorPantalla, 64, 14);
        imgPant.raycastTarget = false;

        ConstruirBarraEstado(pant.transform);
        ConstruirConversacion(pant.transform);
        ConstruirBarraInferior(pant.transform);
    }

    private void ConstruirBarraEstado(Transform pant)
    {
        var barra = Barra(pant, "BarraEstado", colorBarras, arriba: true);

        reloj = FishyUIKit.Texto(barra, "Reloj", System.DateTime.Now.ToString("HH:mm"),
            tamanoReloj, Color.white, TextAlignmentOptions.MidlineLeft);

        var rt = (RectTransform)reloj.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = new Vector2(14f, 0f);
        rt.offsetMax = new Vector2(-14f, 0f);
        reloj.raycastTarget = false;
    }

    /// <summary>
    /// La barra clara de abajo. En la referencia está vacía: es el sitio donde se
    /// escribiría en un chat de verdad, y aquí solo sirve para que el celular se
    /// lea como un celular. Por eso no tiene texto ni recibe clics.
    /// </summary>
    private void ConstruirBarraInferior(Transform pant)
    {
        Barra(pant, "BarraInferior", colorBarraInferior, arriba: false);
    }

    private Transform Barra(Transform pant, string nombre, Color color, bool arriba)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pant, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, arriba ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, arriba ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, arriba ? 1f : 0f);
        rt.offsetMin = new Vector2(margenInterior, arriba ? -altoBarras : margenInterior);
        rt.offsetMax = new Vector2(-margenInterior, arriba ? -margenInterior : altoBarras);

        var img = go.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(img, color, 64, 12);
        img.raycastTarget = false;

        return go.transform;
    }

    /// <summary>
    /// La zona de las burbujas: una lista que crece hacia abajo dentro de un
    /// ScrollRect. Hace falta el scroll porque en una pantalla de este tamaño caben
    /// dos o tres instrucciones, y el tutorial suelta más: lo viejo sube y se va,
    /// como en un chat.
    /// </summary>
    private void ConstruirConversacion(Transform pant)
    {
        float hueco = altoBarras + margenInterior * 2f;

        var visorGO = new GameObject("Conversacion",
            typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        visorGO.transform.SetParent(pant, false);

        visor = (RectTransform)visorGO.transform;
        visor.anchorMin = Vector2.zero;
        visor.anchorMax = Vector2.one;
        visor.offsetMin = new Vector2(margenInterior, hueco);
        visor.offsetMax = new Vector2(-margenInterior, -hueco);

        // Transparente, pero sí recibe el puntero: es lo único del celular que lo
        // recibe, y es lo que permite arrastrar o usar la rueda para subir a releer.
        // Un ScrollRect sin ningún Graphic debajo no se entera de nada.
        var fondoVisor = visorGO.GetComponent<Image>();
        fondoVisor.color = new Color(0f, 0f, 0f, 0f);
        fondoVisor.raycastTarget = true;

        var cont = new GameObject("Contenido",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        cont.transform.SetParent(visorGO.transform, false);

        contenido = (RectTransform)cont.transform;
        contenido.anchorMin = new Vector2(0f, 1f);
        contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.anchoredPosition = Vector2.zero;
        contenido.sizeDelta = Vector2.zero;

        var vlg = cont.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = separacionBurbujas;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;

        cont.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = visorGO.GetComponent<ScrollRect>();
        scroll.viewport = visor;
        scroll.content = contenido;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = sensibilidadScroll;
    }

    private void CrearBurbuja(string texto)
    {
        // Se mira ANTES de añadir nada: después, el contenido ya habrá crecido.
        bool arrastrarLaVista = EstaAlFinal();

        var burbuja = new GameObject("Burbuja",
            typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        burbuja.transform.SetParent(contenido, false);

        var img = burbuja.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(img, colorBurbuja, 64, 18);
        img.raycastTarget = false;

        var vlg = burbuja.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 18, 12, 14);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var fitter = burbuja.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Ancho fijo y sin estirarse: así todas las burbujas quedan alineadas y el
        // texto largo parte de línea en vez de salirse de la pantalla.
        var le = burbuja.GetComponent<LayoutElement>();
        le.preferredWidth = anchoMaximoBurbuja;
        le.flexibleWidth = 0f;

        TextMeshProUGUI t = FishyUIKit.Texto(burbuja.transform, "Texto", Destacar(texto),
            tamanoTextoBurbuja, colorTextoBurbuja, TextAlignmentOptions.TopLeft);
        t.raycastTarget = false;

        dibujados.Add(burbuja);

        // Medir antes de mover el scroll: el contenido acaba de crecer y sin esto
        // el salto iría al final de la lista vieja.
        Canvas.ForceUpdateCanvases();
        if (arrastrarLaVista && scroll != null) scroll.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// Una burbuja con una imagen dentro, para enseñar algo en vez de describirlo:
    /// el icono que hay que tocar, el objeto que hay que buscar.
    ///
    /// La burbuja se ajusta a la imagen en vez de usar el ancho fijo de las de texto:
    /// un icono pequeño en una burbuja ancha se vería perdido en medio de un hueco.
    /// El alto sale de la proporción del sprite, así que un icono cuadrado sale
    /// cuadrado y uno apaisado no se estira.
    /// </summary>
    private void CrearImagen(Sprite imagen)
    {
        bool arrastrarLaVista = EstaAlFinal();

        var burbuja = new GameObject("BurbujaImagen",
            typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        burbuja.transform.SetParent(contenido, false);

        var fondo = burbuja.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(fondo, colorBurbuja, 64, 18);
        fondo.raycastTarget = false;

        var vlg = burbuja.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 14, 14);
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;

        var fitter = burbuja.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        burbuja.GetComponent<LayoutElement>().flexibleWidth = 0f;

        var fotoGO = new GameObject("Foto",
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        fotoGO.transform.SetParent(burbuja.transform, false);

        var foto = fotoGO.GetComponent<Image>();
        foto.sprite = imagen;
        foto.preserveAspect = true;
        foto.raycastTarget = false;

        var fotoLe = fotoGO.GetComponent<LayoutElement>();
        fotoLe.preferredWidth  = anchoImagen;
        fotoLe.preferredHeight = anchoImagen * (imagen.rect.height / Mathf.Max(1f, imagen.rect.width));

        dibujados.Add(burbuja);

        Canvas.ForceUpdateCanvases();
        if (arrastrarLaVista && scroll != null) scroll.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// Pinta de amarillo lo que el texto marque entre asteriscos:
    /// <c>*WASD/flechas*</c>, <c>*E para hablarle*</c>, <c>*abajo a tu izquierda*</c>.
    ///
    /// <b>Por qué marcas en el texto y no una lista de palabras.</b> Lo que hay que
    /// destacar no siempre es una palabra: muchas veces es una frase entera, y la
    /// misma palabra se destaca en un mensaje y en otro no —la primera vez que sale
    /// la tecla E importa, la tercera ya no—. Con marcas lo decide quien escribe el
    /// texto, en el mismo sitio donde lo escribe y sin tocar código.
    ///
    /// Hacen falta los dos asteriscos: uno suelto se queda tal cual, y las marcas no
    /// cruzan de un renglón a otro, así que no se pueden comer medio mensaje por un
    /// despiste.
    /// </summary>
    private string Destacar(string texto)
    {
        string hex = ColorUtility.ToHtmlStringRGB(colorDestacado);
        return Regex.Replace(texto, @"\*(.+?)\*", $"<color=#{hex}><b>$1</b></color>");
    }

    /// <summary>
    /// La píldora centrada que anuncia un grupo nuevo: «2 mensajes no leídos».
    ///
    /// <b>Por qué va dentro de una fila</b> y no suelta en la conversación: el
    /// contenedor alinea a la izquierda, que es lo que mantiene las burbujas pegadas
    /// a su borde. Un hijo suelto heredaría esa alineación y la píldora se iría al
    /// margen; la fila ocupa todo el ancho y centra lo que lleva dentro.
    ///
    /// Medidas sacadas de <c>referencias/separador.png</c>: la píldora es un
    /// rectángulo bien redondeado —radio en torno a un tercio de su alto, no una
    /// cápsula—, con poco aire a los lados del texto.
    /// </summary>
    private void CrearSeparador(int cuantos)
    {
        bool arrastrarLaVista = EstaAlFinal();

        var fila = new GameObject("Separador",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        fila.transform.SetParent(contenido, false);

        var filaHlg = fila.GetComponent<HorizontalLayoutGroup>();
        filaHlg.childAlignment = TextAnchor.MiddleCenter;
        filaHlg.childControlWidth = true; filaHlg.childControlHeight = true;
        filaHlg.childForceExpandWidth = false; filaHlg.childForceExpandHeight = false;

        var filaLe = fila.GetComponent<LayoutElement>();
        filaLe.preferredWidth = anchoMaximoBurbuja;
        filaLe.flexibleWidth = 0f;

        var pildora = new GameObject("Pildora",
            typeof(RectTransform), typeof(Image),
            typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        pildora.transform.SetParent(fila.transform, false);

        var img = pildora.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(img, colorSeparador, 64, 12);
        img.raycastTarget = false;

        var vlg = pildora.GetComponent<HorizontalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 6, 8);
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;

        // Que se ajuste al texto en los dos sentidos: la píldora es del tamaño de lo
        // que dice, no de un hueco fijo. "1 mensaje" ocupa menos que "12 mensajes".
        var fitter = pildora.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // El singular importa: «1 mensajes no leídos» en un juego que además enseña
        // a leer se nota, y mucho.
        string texto = cuantos == 1
            ? "1 mensaje no leído"
            : $"{cuantos} mensajes no leídos";

        TextMeshProUGUI t = FishyUIKit.Texto(pildora.transform, "Texto", texto,
            tamanoTextoSeparador, colorTextoSeparador, TextAlignmentOptions.Center);
        t.raycastTarget = false;

        dibujados.Add(fila);

        Canvas.ForceUpdateCanvases();
        if (arrastrarLaVista && scroll != null) scroll.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// True si la conversación está abajo del todo, o si cabe entera.
    ///
    /// Decide si un mensaje nuevo arrastra la vista. Si el jugador subió a releer
    /// algo, el celular no debe tirarle de la pantalla justo entonces: se queda
    /// donde estaba y el mensaje nuevo lo espera abajo.
    /// </summary>
    private bool EstaAlFinal()
    {
        if (scroll == null || visor == null || contenido == null) return true;
        if (contenido.rect.height <= visor.rect.height) return true;
        return scroll.verticalNormalizedPosition <= 0.05f;
    }

    private static void Estirar(RectTransform rt, Vector2 desplazamiento, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding + desplazamiento.x, padding + desplazamiento.y);
        rt.offsetMax = new Vector2(-padding + desplazamiento.x, -padding + desplazamiento.y);
    }
}
