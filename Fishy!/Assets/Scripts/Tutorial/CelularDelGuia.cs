using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// El celular por el que habla el guía del tutorial: una pantalla de chat pegada
/// al borde derecho, con las instrucciones cayendo como burbujas.
///
/// <b>Es todo nuevo y no toca nada del juego.</b> Se construye en código, en un
/// Canvas propio, y lo único que usa de fuera son herramientas de dibujo y colores
/// que no modifica: <see cref="FishyUIKit"/> para las esquinas redondeadas y la
/// fuente, y <see cref="Paleta"/> para los tonos. Se parece al celular del Modo
/// Detective, pero no es el mismo: aquel se monta con <c>MarcoTelefono</c> en el
/// centro de la pantalla y este va al lado. Los colores sí son los mismos, los de
/// la paleta café del juego.
///
/// <b>Por qué a la derecha y de ese tamaño.</b> La banda derecha es la única que no
/// comparte con nada: todo el HUD del juego se amontona a la izquierda —el cartel
/// de misiones activas arriba (440 de ancho, 28 de margen), el botón de pausa en
/// esa misma esquina y el de interactuar abajo—, y Otto anda por el centro. Así que
/// el celular la ocupa entera, y sus medidas no son de gusto: están atadas a los
/// dos vecinos que tiene, en píxeles de 1920×1080.
///
/// El botón del TAB (<c>BotonCelular</c>) está anclado arriba a la derecha, mide
/// 160×160 y tiene 28 de margen, así que ocupa de 28 a 188 por ambos lados. De ahí
/// salen los dos números de <see cref="margen"/>: 28 a la derecha, el mismo que el
/// botón, para que los dos aparatos compartan borde; y 204 arriba, que es donde
/// acaba el botón (188) más el mismo aire que el celular usa por dentro (16). Hacia
/// abajo y hacia la izquierda no hay nada, y el tamaño llena ese hueco dejando 48
/// de suelo. El mockup REFE_TUTORIAL.png, donde el celular era más chico y estaba
/// al otro lado, sigue valiendo para las proporciones de dentro.
///
/// <b>Ojo si mueves esos números:</b> el botón del TAB vive en otro Canvas, con el
/// escalador en "match width" en vez del 0,5 de este. A 16:9 los dos miden igual y
/// el borde compartido cuadra al píxel; en una pantalla muy distinta se separan un
/// poco. Es una esquina, no una costura: se nota como más o menos aire, no como un
/// desencaje.
///
/// <b>Orden de dibujo.</b> 1250, que lo deja por encima de lo único que comparte
/// pantalla con él: el cartel de misiones (710) y los avisos de zona (1000). Las
/// pantallas que se dibujan más arriba —el panel del Tab (1200), el álbum (1300),
/// la pausa (9500)— no compiten con el celular, porque con cualquiera de ellas
/// abierta el tutorial ya lo apagó.
///
/// Lo único que recibe el puntero es la conversación, su barra de desplazamiento
/// y el botón de saltar el tutorial; el bisel y las barras lo dejan pasar.
///
/// <b>Un grupo de mensajes a la vez.</b> En el piloto los niños/as se perdían
/// subiendo y bajando por una conversación acumulada. Ahora cada grupo limpia la
/// pantalla, sale con su aviso arriba y la vista se queda ahí mientras llegan los
/// mensajes: el que no cabe asoma cortado, que es lo que invita a bajar a leerlo.
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
    [Tooltip("Distancia a la esquina de arriba a la derecha, en píxeles de 1920×1080. " +
             "La x es la del botón del TAB, para que compartan borde; la y lo deja " +
             "justo debajo de ese botón.")]
    public Vector2 margen = new Vector2(28f, 204f);

    [Tooltip("Tamaño de la pantalla (sin contar el bisel). Con el bisel de 20 el " +
             "aparato mide 560×828, que es la banda derecha entera: desde debajo del " +
             "botón del TAB hasta 48 del suelo.")]
    public Vector2 pantalla = new Vector2(520f, 788f);

    [Tooltip("Grosor del bisel negro alrededor de la pantalla.")]
    public float borde = 20f;

    [Tooltip("Orden de dibujo del Canvas. Por encima del cartel de misiones (710) y de " +
             "los avisos de zona (1000), que es lo único que comparte pantalla con él.")]
    public int ordenDeDibujo = 1250;

    [Header("Colores (la paleta café del juego)")]
    // Todos salen de Fishy.UI.Paleta, igual que el panel del Tab (MenuTabsTheme) y el
    // chat del juego (ChatUITheme). El mockup azul está traducido pieza por pieza con
    // la correspondencia que ya usa el chat, que es la misma pantalla: una ventana con
    // burbujas cayendo. Hex a mano aquí no: serían una cuarta copia de la paleta que
    // se queda atrás en cuanto alguien retoque las otras tres.

    /// <summary>
    /// El aparato visto desde fuera: negro, como el bisel del mockup y como un celular
    /// de verdad.
    ///
    /// Es el único color que no sale de la paleta, y a propósito: el negro no es un
    /// tono de Fishy, es la carcasa. Contra la pantalla se separa poco (1,4:1, porque
    /// la pantalla también es oscura), así que la silueta del aparato la da el borde
    /// contra el mundo del juego que hay detrás, no contra su propia pantalla.
    /// </summary>
    public Color colorBisel = new Color32(0x00, 0x00, 0x00, 255);

    /// <summary>La pantalla sobre la que caen las burbujas. El mismo marrón oscuro con
    /// el que el panel del Tab pinta sus páginas y el chat su historial.</summary>
    public Color colorPantalla = Paleta.MarronOscuro;

    /// <summary>Barra de estado de arriba. En el tono del header del chat.</summary>
    public Color colorBarras = Paleta.MarronMedio;

    /// <summary>Burbuja del guía. Es la del NPC en el chat del juego: quien habla aquí
    /// es un personaje, así que le toca su mismo color.</summary>
    public Color colorBurbuja = Paleta.MarronClaro;

    /// <summary>Texto de las burbujas. En crema, como todo el texto del chat: sobre la
    /// burbuja da 6,2:1, mientras que el blanco que tenía sobre el azul claro del
    /// mockup se quedaba en 1,7:1 — por debajo del mínimo legible de 4,5:1.</summary>
    public Color colorTextoBurbuja = Paleta.Crema;

    /// <summary>La barra de abajo. Deliberadamente discreta: en la referencia está
    /// vacía y solo sirve para que el aparato se lea como un celular.</summary>
    public Color colorBarraInferior = Paleta.MarronMedio;

    [Header("Saltar tutorial")]
    public string textoSaltar = "Saltar tutorial";
    public float tamanoTextoSaltar = 24f;

    /// <summary>Lo que pasa al pulsar el botón de saltar. Lo pone el tutorial.</summary>
    public System.Action AlSaltar;

    [Header("Medidas finas")]
    public float altoBarras = 44f;
    [Tooltip("Hasta dónde sube la barra de abajo, medido desde el borde de la pantalla. " +
             "Es más alta que la de arriba porque lleva un botón que un niño/a tiene " +
             "que poder pinchar sin apuntar.")]
    public float altoBarraInferior = 76f;
    public float margenInterior = 16f;
    [Tooltip("Cuánto del ancho libre ocupa una burbuja, de 0 a 1. Es una porción y no " +
             "un ancho en píxeles a propósito: así el celular se puede agrandar o " +
             "estrechar sin que haya que recalcular esto a mano, que es justo lo que " +
             "se olvida y deja las burbujas recortadas o nadando en un hueco. Menos de " +
             "1 deja ver el fondo a su derecha, que es lo que las hace leerse como " +
             "burbujas y no como bloques de texto.")]
    [Range(0.5f, 1f)] public float porcionDeBurbuja = 0.95f;
    public float separacionBurbujas = 20f;
    public float tamanoTextoBurbuja = 30f;
    public float tamanoReloj = 26f;

    [Header("Separador de mensajes nuevos")]
    [Tooltip("La píldora centrada que anuncia un grupo nuevo, como en separador.png. " +
             "En arena, el único tono claro de la paleta que el panel del Tab también " +
             "usa como acento (el tirador de sus barras): la píldora tiene que cantar " +
             "sobre la pantalla —5,5:1— porque es la alerta de que llegó algo.")]
    public Color colorSeparador = Paleta.Arena;

    [Tooltip("Sobre la arena el texto va oscuro, al revés que en las burbujas (4,9:1).")]
    public Color colorTextoSeparador = Paleta.Marron;

    [Tooltip("Más chico que el de las burbujas: en el mockup la píldora no compite " +
             "con los mensajes, solo los ordena.")]
    public float tamanoTextoSeparador = 22f;

    [Header("Lo importante, destacado")]
    [Tooltip("Color de las palabras que hay que mirar o pulsar. El amarillo cálido del " +
             "juego: el mismo del aro del minimapa y del celular del TAB en " +
             "botoninteractuar.png. Se queda tal cual al pasar el celular a café — " +
             "sobre la burbuja da 4,6:1 y sigue siendo lo más brillante de la " +
             "pantalla, que es justo su trabajo.")]
    public Color colorDestacado = new Color32(0xF4, 0xA7, 0x23, 255);

    // Qué se destaca lo marca el propio texto con *asteriscos*; ver Destacar().

    [Header("Imágenes que puede mandar el guía")]
    [Tooltip("Las imágenes que los textos llaman con [img:nombre]. No se rellena aquí: " +
             "este celular se crea en código y no está en la escena, así que se las pasa " +
             "TutorialPasos, que es donde viven los textos.")]
    public ImagenDelGuia[] imagenes = new ImagenDelGuia[0];

    [Tooltip("Ancho de la imagen dentro de la burbuja. El alto sale solo, respetando " +
             "la proporción del sprite. Subió con la pantalla: en una burbuja de 448 " +
             "los 140 de antes se veían como un sello.")]
    public float anchoImagen = 184f;

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

    [Header("Barra de desplazamiento")]
    [Tooltip("Grosor de lo que SE VE: el carril y su tirador, en píxeles de 1920×1080. " +
             "Fina a propósito, que es como se lee una barra de chat.")]
    [Min(2f)] public float anchoBarraScroll = 5f;

    [Tooltip("Ancho de lo que SE PUEDE AGARRAR, que es mayor que lo que se ve. Una " +
             "barra de 5 px es bonita y casi imposible de pinchar con el ratón, menos " +
             "aún para un niño/a; esta franja invisible alrededor es la que recibe el " +
             "clic y el arrastre. Es el espacio que se le quita a las burbujas, porque " +
             "si no, pinchar sobre un mensaje pegado a la barra daría un salto de " +
             "página en vez de seleccionar nada.")]
    [Min(4f)] public float anchoAgarreScroll = 22f;

    [Tooltip("Aire entre las burbujas y la barra, para que el texto no quede pegado a ella.")]
    [Min(0f)] public float separacionBarraScroll = 8f;

    [Tooltip("Carril de la barra. El mismo marrón medio que usa el panel del Tab en las " +
             "suyas: queda discreto sobre la pantalla y es el tirador el que se ve.")]
    public Color colorCarrilScroll = Paleta.MarronMedio;

    [Tooltip("Tirador de la barra. En arena, el acento claro de la paleta: sobre el " +
             "carril da 4,2:1, así que se ve de un vistazo que hay algo que arrastrar.")]
    public Color colorTiradorScroll = Paleta.Arena;

    [Header("Entrada")]
    [Tooltip("Segundos que tarda en entrar deslizándose desde el borde derecho.")]
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
    private ScrollRect scroll;
    private Button botonSaltar;
    private TextMeshProUGUI reloj;
    private TextMeshProUGUI titulo, tituloSombra;
    private float proximoRefrescoDelReloj;

    /// <summary>
    /// Dónde se queda quieto el celular: pegado al borde derecho, a <see cref="margen"/>
    /// de él y de la parte de arriba.
    ///
    /// La x es negativa porque el marco está anclado a la derecha, así que restar lo
    /// mete en pantalla. Vive en una sola propiedad porque este punto hace falta en
    /// tres sitios —al construirlo, al animar la entrada y al rescatarlo si se apaga
    /// a media animación—, y tenerlo escrito tres veces es justo lo que hace que
    /// cambiar el lado deje el celular medio fuera de la pantalla.
    /// </summary>
    private Vector2 Reposo => new Vector2(-margen.x, -margen.y);

    /// <summary>Todo lo dibujado en la conversación: burbujas y separadores. Van
    /// juntos porque al llegar un grupo nuevo se borran todos por igual.</summary>
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
        marco.anchoredPosition = Reposo;
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

        // Todos los grupos llevan su aviso, el primero también: no separa nada, es
        // la alerta de que llegaron mensajes y el punto desde el que se lee.
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
    /// El separador gasta su turno como uno más: borra lo anterior, sale solo y, tras
    /// la misma pausa, su grupo empieza a caer debajo.
    /// </summary>
    private IEnumerator SoltarPendientes()
    {
        while (pendientes.Count > 0)
        {
            while (!EstaVisible) yield return null;

            Pendiente siguiente = pendientes.Dequeue();
            if      (siguiente.EsSeparador) EmpezarGrupo(siguiente.Cuantos);
            else if (siguiente.EsImagen)    CrearImagen(siguiente.Imagen);
            else                            CrearBurbuja(siguiente.Texto);

            if (pendientes.Count > 0) yield return new WaitForSecondsRealtime(segundosEntreMensajes);
        }
        rutina = null;
    }

    /// <summary>
    /// Deja la pantalla limpia con el aviso arriba y la vista en él. Los mensajes del
    /// grupo se añaden debajo sin mover nada: el contenido crece hacia abajo desde su
    /// borde superior, así que el que no quepa queda cortado por el borde del visor.
    /// </summary>
    private void EmpezarGrupo(int cuantos)
    {
        BorrarDibujados();
        CrearSeparador(cuantos);

        if (scroll != null) scroll.StopMovement();
        if (contenido != null) contenido.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Saca de la pantalla lo dibujado. Se apaga además de destruirse porque Destroy
    /// espera al final del frame, y hasta entonces lo viejo seguiría ocupando sitio
    /// en la lista cuando se mida el aviso nuevo.
    /// </summary>
    private void BorrarDibujados()
    {
        foreach (GameObject go in dibujados)
        {
            if (go == null) continue;
            go.SetActive(false);
            Destroy(go);
        }
        dibujados.Clear();
    }

    /// <summary>Quita el botón de saltar: ya no queda nada que saltarse.</summary>
    public void OcultarSaltar()
    {
        if (botonSaltar != null) botonSaltar.gameObject.SetActive(false);
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
    /// Deja el celular fuera de pantalla por la derecha y lo trae a su sitio.
    ///
    /// Es la misma curva con la que sube el panel del Tab —entra rápido, se pasa un
    /// poco de largo y se asienta— y el mismo arranque en pequeño, para que los dos
    /// aparatos se sientan del mismo juego. Lo que cambia es de dónde viene: el
    /// panel sube desde abajo y este entra desde el borde derecho, que es el suyo.
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
        Vector2 reposo = Reposo;
        Vector2 fuera = new Vector2(marco.rect.width + 40f, reposo.y);

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
        BorrarDibujados();
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
        // Bisel: el aparato visto desde fuera. Anclado arriba a la derecha, que es el
        // lado libre: el cartel de misiones, el botón de pausa y el de interactuar
        // están todos a la izquierda.
        celular = new GameObject("Celular", typeof(RectTransform), typeof(Image));
        celular.transform.SetParent(padre, false);

        marco = (RectTransform)celular.transform;
        marco.anchorMin = marco.anchorMax = new Vector2(1f, 1f);
        marco.pivot = new Vector2(1f, 1f);
        marco.anchoredPosition = Reposo;
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

        // El reloj en crema y no en blanco: es parte del aparato, así que va con el
        // mismo texto que las burbujas. Sobre la barra da 7,9:1.
        reloj = FishyUIKit.Texto(barra, "Reloj", System.DateTime.Now.ToString("HH:mm"),
            tamanoReloj, Paleta.Crema, TextAlignmentOptions.MidlineLeft);

        var rt = (RectTransform)reloj.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = new Vector2(14f, 0f);
        rt.offsetMax = new Vector2(-14f, 0f);
        reloj.raycastTarget = false;
    }

    /// <summary>
    /// La barra de abajo, con el botón de saltar el tutorial. Va aquí porque es lo
    /// único del celular que no se mueve con los mensajes y queda a mano en todos los
    /// pasos.
    ///
    /// Sin navegación: un botón seleccionable recibiría WASD y las flechas, las
    /// mismas teclas con las que camina Otto (ver <see cref="SoltarLaSeleccion"/>).
    /// </summary>
    private void ConstruirBarraInferior(Transform pant)
    {
        Transform barra = Barra(pant, "BarraInferior", colorBarraInferior, arriba: false,
            alto: altoBarraInferior);

        botonSaltar = FishyUIKit.Boton(barra, textoSaltar, Paleta.Madera, tamanoTextoSaltar, 0f,
            () => AlSaltar?.Invoke());
        Estirar((RectTransform)botonSaltar.transform, Vector2.zero, 6f);
        botonSaltar.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private Transform Barra(Transform pant, string nombre, Color color, bool arriba, float alto = -1f)
    {
        if (alto < 0f) alto = altoBarras;

        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pant, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, arriba ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, arriba ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, arriba ? 1f : 0f);
        rt.offsetMin = new Vector2(margenInterior, arriba ? -alto : margenInterior);
        rt.offsetMax = new Vector2(-margenInterior, arriba ? -margenInterior : alto);

        var img = go.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(img, color, 64, 12);
        img.raycastTarget = false;

        return go.transform;
    }

    /// <summary>
    /// La zona de las burbujas: una lista que crece hacia abajo dentro de un
    /// ScrollRect. Hace falta el scroll porque un grupo largo no cabe entero: el
    /// último mensaje asoma cortado y se baja para terminar de leerlo.
    /// </summary>
    private void ConstruirConversacion(Transform pant)
    {
        float huecoArriba = altoBarras + margenInterior * 2f;
        float huecoAbajo = altoBarraInferior + margenInterior;

        var visorGO = new GameObject("Conversacion",
            typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        visorGO.transform.SetParent(pant, false);

        var visor = (RectTransform)visorGO.transform;
        visor.anchorMin = Vector2.zero;
        visor.anchorMax = Vector2.one;
        visor.offsetMin = new Vector2(margenInterior, huecoAbajo);
        visor.offsetMax = new Vector2(-(margenInterior + anchoAgarreScroll + separacionBarraScroll),
                                      -huecoArriba);

        // Transparente, pero sí recibe el puntero: es lo que permite arrastrar o usar
        // la rueda para bajar a leer el mensaje cortado.
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

        // La barra: refuerza lo que ya insinúa el mensaje cortado, que hay más abajo.
        scroll.verticalScrollbar = ConstruirBarraDeScroll(pant, huecoArriba, huecoAbajo);

        // AutoHide y no Permanent: la barra aparece en cuanto hay más mensajes de los
        // que caben, que es exactamente cuando hay algo que bajar. Un carril visible
        // desde el primer mensaje, con el tirador ocupándolo entero, diría lo contrario
        // de lo que queremos —"esto no se mueve"— justo cuando el niño/a lo mira por
        // primera vez.
        //
        // Y no AutoHideAndExpandViewport, que además le devolvería el ancho al visor
        // al esconderse: las burbujas cambiarían de tamaño al llegar el mensaje que
        // llena la pantalla, y el texto se recolocaría solo delante de quien lo lee.
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    /// <summary>
    /// El carril con su tirador, pegado al borde derecho de la pantalla y a la altura
    /// del visor.
    ///
    /// Va hermano del visor y no dentro, porque un hijo del visor lo recortaría su
    /// <c>RectMask2D</c> y, con AutoHide, Unity además necesita poder apagarlo sin
    /// apagar la conversación.
    ///
    /// La jerarquía es la que espera <see cref="Scrollbar"/>: el componente con su
    /// zona deslizable estirada dentro y el tirador dentro de ella. Del tirador, el
    /// Scrollbar maneja los ANCLAJES —cuánto ocupa y dónde va a lo largo del
    /// recorrido—, pero no los márgenes, así que estrecharlo por los lados con
    /// <c>offsetMin/offsetMax</c> es lo que aguanta: ahí es donde se separa lo que se
    /// ve de lo que se puede agarrar.
    /// </summary>
    private Scrollbar ConstruirBarraDeScroll(Transform pant, float huecoArriba, float huecoAbajo)
    {
        var barraGO = new GameObject("BarraDeScroll",
            typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barraGO.transform.SetParent(pant, false);

        var rt = (RectTransform)barraGO.transform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(anchoAgarreScroll, -(huecoArriba + huecoAbajo));
        rt.anchoredPosition = new Vector2(-margenInterior, (huecoAbajo - huecoArriba) * 0.5f);

        // La franja de agarre: invisible, pero es la que recibe el puntero. Un Image
        // transparente sigue recibiendo raycasts, que es lo que la hace funcionar —el
        // mismo truco que el fondo del visor para poder arrastrar la conversación.
        var zonaDeAgarre = barraGO.GetComponent<Image>();
        zonaDeAgarre.color = new Color(0f, 0f, 0f, 0f);
        zonaDeAgarre.raycastTarget = true;

        // Radio igual a la mitad del grosor: el carril y el tirador salen en cápsula,
        // que es la forma con la que se leen como "barra" y no como dos rectángulos.
        int radio = Mathf.Max(1, Mathf.RoundToInt(anchoBarraScroll * 0.5f));

        // El carril visible, centrado en la franja. Va en su propio hijo y no en el
        // Image de arriba porque ese tiene que quedarse transparente para hacer de
        // zona de agarre: son dos cosas distintas, una que se ve y otra que se toca.
        var carrilGO = new GameObject("Carril", typeof(RectTransform), typeof(Image));
        carrilGO.transform.SetParent(barraGO.transform, false);

        var carrilRT = (RectTransform)carrilGO.transform;
        carrilRT.anchorMin = new Vector2(0.5f, 0f);
        carrilRT.anchorMax = new Vector2(0.5f, 1f);
        carrilRT.pivot = new Vector2(0.5f, 0.5f);
        carrilRT.sizeDelta = new Vector2(anchoBarraScroll, 0f);
        carrilRT.anchoredPosition = Vector2.zero;

        var imgCarril = carrilGO.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(imgCarril, colorCarrilScroll, 64, radio);
        imgCarril.raycastTarget = false;

        var deslizable = new GameObject("ZonaDeslizable", typeof(RectTransform));
        deslizable.transform.SetParent(barraGO.transform, false);
        Estirar((RectTransform)deslizable.transform, Vector2.zero);

        var tiradorGO = new GameObject("Tirador", typeof(RectTransform), typeof(Image));
        tiradorGO.transform.SetParent(deslizable.transform, false);

        // Estrechado a los lados hasta el grosor visible, y sin márgenes arriba ni
        // abajo: esos dos se los deja al Scrollbar, que es quien mueve el tirador.
        float sobra = Mathf.Max(0f, (anchoAgarreScroll - anchoBarraScroll) * 0.5f);
        var tiradorRT = (RectTransform)tiradorGO.transform;
        tiradorRT.offsetMin = new Vector2(sobra, 0f);
        tiradorRT.offsetMax = new Vector2(-sobra, 0f);

        var imgTirador = tiradorGO.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(imgTirador, colorTiradorScroll, 64, radio);

        var barra = barraGO.GetComponent<Scrollbar>();
        barra.direction = Scrollbar.Direction.BottomToTop;
        barra.handleRect = tiradorRT;
        barra.targetGraphic = imgTirador;

        SoltarLaSeleccion(barraGO);
        return barra;
    }

    /// <summary>
    /// Deja la barra sin seleccionar en cuanto el jugador suelta el ratón.
    ///
    /// <b>Por qué.</b> Un <see cref="Scrollbar"/> es un <c>Selectable</c>, así que al
    /// pincharlo queda seleccionado, y el EventSystem le manda a lo seleccionado los
    /// eventos de navegación. Las teclas de esa navegación son, en el asset por
    /// defecto del InputSystem, WASD y las cuatro flechas: las ocho con las que
    /// camina Otto. Una barra seleccionada desplaza la conversación cada vez que el
    /// jugador se mueve, y encima en el primer paso del tutorial, que es justo el que
    /// enseña esas teclas. Como Otto lee su propia acción, la tecla no se consume: se
    /// hacen las dos cosas a la vez.
    ///
    /// <b>Por qué al soltar y no al recibir la selección.</b> Desde el evento
    /// <c>Select</c> no se puede: <c>EventSystem.SetSelectedGameObject</c> lleva un
    /// cerrojo mientras reparte la selección (<c>alreadySelecting</c>), y llamarla
    /// desde ahí solo saca un error en la consola y deja la barra igual de
    /// seleccionada. Al soltar el botón ya estamos fuera de ese reparto. Quedan las
    /// décimas que dure el arrastre con el botón apretado, que es tiempo en el que el
    /// jugador no está caminando.
    ///
    /// <b>Qué no se rompe.</b> El arrastre y la rueda, que van por eventos de puntero
    /// y no miran quién está seleccionado. Lo único que se pierde es desplazar la
    /// conversación con el teclado; entre eso y que caminar mueva el chat, se pierde
    /// esto.
    /// </summary>
    private static void SoltarLaSeleccion(GameObject barraGO)
    {
        var alSoltar = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        alSoltar.callback.AddListener(_ =>
        {
            EventSystem sistema = EventSystem.current;
            if (sistema != null && !sistema.alreadySelecting)
                sistema.SetSelectedGameObject(null);
        });

        barraGO.AddComponent<EventTrigger>().triggers.Add(alSoltar);
    }

    /// <summary>
    /// Ancho libre dentro de la pantalla, ya descontada la barra.
    ///
    /// Se descuenta la franja de agarre y no el grosor visible: lo que hay que
    /// respetar es lo que se puede pinchar, no lo que se pinta.
    /// </summary>
    private float AnchoDelVisor =>
        pantalla.x - margenInterior * 2f - anchoAgarreScroll - separacionBarraScroll;

    /// <summary>
    /// Ancho de una burbuja: su porción del espacio libre.
    ///
    /// Se calcula y no se escribe porque los dos números de los que depende —el ancho
    /// de la pantalla y el de la barra— ya cambiaron una vez cada uno. Con un ancho
    /// fijo, agrandar el celular dejaba las burbujas nadando en un hueco y meter la
    /// barra las dejaba recortadas por el borde, que parece un fallo de texto y no de
    /// medidas.
    /// </summary>
    private float AnchoDeBurbuja => AnchoDelVisor * porcionDeBurbuja;

    private void CrearBurbuja(string texto)
    {
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
        le.preferredWidth = AnchoDeBurbuja;
        le.flexibleWidth = 0f;

        TextMeshProUGUI t = FishyUIKit.Texto(burbuja.transform, "Texto", Destacar(texto),
            tamanoTextoBurbuja, colorTextoBurbuja, TextAlignmentOptions.TopLeft);
        t.raycastTarget = false;

        dibujados.Add(burbuja);
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
    /// La píldora centrada que anuncia un grupo nuevo: «Tienes 2 mensajes nuevos».
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
        var fila = new GameObject("Separador",
            typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        fila.transform.SetParent(contenido, false);

        var filaHlg = fila.GetComponent<HorizontalLayoutGroup>();
        filaHlg.childAlignment = TextAnchor.MiddleCenter;
        filaHlg.childControlWidth = true; filaHlg.childControlHeight = true;
        filaHlg.childForceExpandWidth = false; filaHlg.childForceExpandHeight = false;

        var filaLe = fila.GetComponent<LayoutElement>();
        filaLe.preferredWidth = AnchoDeBurbuja;
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

        // El singular importa: «1 mensajes nuevos» en un juego que además enseña a
        // leer se nota, y mucho.
        string texto = cuantos == 1
            ? "Tienes 1 mensaje nuevo"
            : $"Tienes {cuantos} mensajes nuevos";

        TextMeshProUGUI t = FishyUIKit.Texto(pildora.transform, "Texto", texto,
            tamanoTextoSeparador, colorTextoSeparador, TextAlignmentOptions.Center);
        t.raycastTarget = false;

        dibujados.Add(fila);
    }

    private static void Estirar(RectTransform rt, Vector2 desplazamiento, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding + desplazamiento.x, padding + desplazamiento.y);
        rt.offsetMax = new Vector2(-padding + desplazamiento.x, -padding + desplazamiento.y);
    }
}
