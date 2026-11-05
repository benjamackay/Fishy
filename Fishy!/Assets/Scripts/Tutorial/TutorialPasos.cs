using System;
using System.Collections;
using System.Collections.Generic;
using Fishy.Chat;
using Fishy.Detective;
using Fishy.Mision;
using Fishy.Net;
using Fishy.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// El guía del tutorial: suelta por el celular de la izquierda
/// (<see cref="CelularDelGuia"/>) lo que hay que hacer, y pasa al paso siguiente
/// cuando el jugador lo ha hecho de verdad.
///
/// <b>Nada empieza sin elegir.</b> Al cargar la escena sale la oferta
/// (<see cref="OfertaDelTutorial"/>) con Otto quieto; la guía arranca solo con Ver
/// tutorial. Saltar tutorial, ahí o desde el botón del celular en cualquier paso,
/// lo registra como omitido y entra al juego (ver <see cref="Saltar"/>).
///
/// <b>Cada paso se cierra con un hecho, no con un clic.</b> Moverse se comprueba
/// con la distancia recorrida; recoger la lupa, con que ya no esté en el suelo; y
/// hablar, chatear y el caso detective, con el mismo evento que usa el sistema de
/// misiones para dar un objetivo por cumplido.
/// Así el tutorial no se puede pasar pulsando una tecla sin haber aprendido nada.
///
/// <b>No modifica nada del juego.</b> Solo mantiene apagados el panel del Tab y su
/// botón en ESTA escena, que son una copia propia del tutorial: el tutorial ya no
/// los enseña. Ningún script ni escena del juego cambia por esto.
///
/// Las piezas se buscan solas si se dejan vacías: en esta escena hay una de cada.
/// Un paso cuya pieza no esté se salta avisando, para que quitar un NPC no deje el
/// tutorial trabado pidiendo algo que no existe.
/// </summary>
public class TutorialPasos : MonoBehaviour
{
    [Header("Piezas de la escena (se buscan si quedan vacías)")]
    public OttoController otto;
    [Tooltip("El NPC neutro con el que se practica la tecla de interacción.")]
    public DialogoNeutroNPC npcInteractuar;
    [Tooltip("La lupa del suelo, primer objetivo de la misión.")]
    public WorldItem lupa;
    [Tooltip("El NPC cuya conversación tiene opciones de respuesta.")]
    public ChatModuleLauncher npcOpciones;
    [Tooltip("El NPC que abre el Modo Detective.")]
    public DetectiveLauncher npcDetective;

    [Header("Panel de jugador (el del Tab)")]
    [Tooltip("El controlador del panel. Se deja apagado: el tutorial no lo enseña.")]
    public MenuController panelDeJugador;

    [Tooltip("El botón que anuncia el Tab (BotonCelular). Se deja apagado: un botón " +
             "sin explicar es ruido.")]
    public GameObject botonDelTab;

    [Tooltip("La pantalla de carga de ESTA escena: la copia pegada del menú, apagada. " +
             "Se busca sola. Si no está, se entra al juego de golpe como antes.")]
    public Fishy.UI.PantallaDeCarga pantallaDeCarga;

    [Header("Paso 1 — moverse")]
    [Tooltip("Unidades que tiene que recorrer Otto para dar el paso por aprendido. " +
             "Se mide el camino andado, no las teclas pulsadas.")]
    public float distanciaParaMoverse = 4f;

    [Header("Textos (un renglón en blanco = dos burbujas)")]
    [TextArea(2, 5)] public string textoMover =
        "Hola, yo seré tu guía.\n\n" +
        "Prueba a moverte con WASD/flechas, también puedes correr con Shift.";
    [TextArea(2, 5)] public string textoInteractuar =
        "Acércate al huemul. Cuando aparezca el cartel con la E, púlsala para hablarle; " +
        "con la misma tecla vas pasando las líneas.";
    [TextArea(2, 5)] public string textoLupa =
        "El huemul te dejó una misión: arriba a la izquierda sale el cartel con lo que falta.\n\n" +
        "Lo primero es la lupa del suelo, recógela con la E.";
    [TextArea(2, 5)] public string textoOpciones =
        "¡Buen trabajo!\n\n" +
        "Ahora Rosita, la flamenca. Al hablar con ella vas a elegir una respuesta entre " +
        "varias: toca la que quieras, aquí ninguna está mal.";
    [TextArea(2, 5)] public string textoPanel =
        "Ese fue tu primer chat. Ahora abre tu celular con Tab: ahí llevas tus " +
        "misiones, tu mochila y tus conversaciones.\n\n" +
        "Ábrelo y échale un vistazo.";
    [TextArea(2, 5)] public string textoDetective =
        "Falta la foca, que abre el Modo Detective: ahí se revisan conversaciones " +
        "para buscar señales de riesgo. Ella te explica el resto.";
    [TextArea(2, 5)] public string textoFinal =
        "¡Listo! Ya sabes moverte, hablar, elegir respuestas y usar el Modo Detective.\n\n" +
        "Pulsa Espacio para empezar tu aventura.";

    [Header("Imágenes que pueden salir en los mensajes")]
    [Tooltip("Un renglón suelto que diga [img:nombre] se convierte en una burbuja con " +
             "esa imagen. Arrastra aquí el sprite y ponle el nombre que uses en el texto; " +
             "por ejemplo \"misiones\" para IconoMisiones, y en el texto [img:misiones].")]
    public CelularDelGuia.ImagenDelGuia[] imagenesDelGuia = new CelularDelGuia.ImagenDelGuia[0];

    [Header("Título de arriba")]
    [Tooltip("Lo que va delante del contador: \"TUTORIAL 3/7\".")]
    public string rotulo = "TUTORIAL";

    [Header("Al terminar")]
    [Tooltip("Escena del juego. Tiene que estar en Build Settings.")]
    public string escenaDelJuego = "MainScene";
    [Tooltip("Tecla que entra al juego en la última línea. Espacio a propósito: con la " +
             "E, estando al lado de la foca, la misma pulsación podría reabrirle el caso.")]
    public Key teclaParaEmpezar = Key.Space;

    [Tooltip("Los dos renglones de la celebración final, antes de la despedida.")]
    public string tituloFinal = "¡FELICIDADES!";
    public string subtituloFinal = "Terminaste el tutorial";

    [Tooltip("Sacar de la cola de guardado el progreso que generó el tutorial antes de " +
             "entrar al juego. Desmárcalo si algún día el backend conoce estos ids " +
             "(DC_TUTORIAL, el desafío del tutorial y la lupa) y quieres que se guarden.")]
    public bool limpiarLaColaAlSalir = true;

    // ── Un paso: lo que se lee, lo que lo cierra y lo que enciende al empezar ─
    private class Paso
    {
        public string Texto;
        public Func<bool> Cumplido;
        public Action AlEmpezar;
    }

    /// <summary>Por delante de todo lo que dibuja esta escena, celebración incluida
    /// (1400). Ver <see cref="TaparConLaPantallaDeCarga"/>.</summary>
    private const int OrdenDeLaPantallaDeCarga = 1500;

    private readonly List<Paso> pasos = new List<Paso>();
    private int indice = -1;          // -1 = todavía no empezó ninguno

    private CelularDelGuia celular;

    // Hechos que llegan por evento. Se guardan en vez de consultarse porque el
    // evento pasa una sola vez y el paso puede no estar mirando en ese frame.
    private bool habloConElNpc;
    private bool cerroElChat;
    private bool cerroElCaso;

    /// <summary>Si el panel del Tab llegó a abrirse. Se guarda en vez de mirarse en
    /// el momento porque el paso se cierra con haberlo abierto, no con tenerlo
    /// abierto: cerrarlo de inmediato cuenta igual.</summary>
    private bool abrioElPanel;

    private float distanciaRecorrida;
    private Vector2? ultimaPosicion;

    /// <summary>El objetoId de la lupa, copiado al empezar: al recogerla el WorldItem
    /// se destruye, y para entonces ya no habría a quién preguntárselo.</summary>
    private string idDeLaLupa;

    private bool movimientoQuitadoPorElChat;
    private bool eligiendo;           // la oferta está en pantalla
    private bool despedido;
    private bool celebrando;
    private bool entrando;

    // ── El "solo la primera vez" ─────────────────────────────────────────────

    /// <summary>Qué pasó con el tutorial en una partida. Se guarda como entero, así
    /// que los valores no se renumeran.</summary>
    public enum EstadoTutorial { SinRegistrar = 0, Completado = 1, Omitido = 2 }

    /// <summary>
    /// Estado local del tutorial, SIEMPRE bajo la partida en la que pasó: cada partida
    /// nueva tiene su propia oferta, y una partida ya resuelta no vuelve a ofrecerlo.
    ///
    /// Sin partida no se guarda nada, y es a propósito: una clave compartida le daría
    /// el tutorial por resuelto al siguiente niño/a que entrara en el mismo equipo. Es
    /// la misma lección que dejó <c>DetectiveLauncher</c> con su clave "local". Jugando
    /// la escena suelta en el editor tampoco hay partida, así que ahí la oferta sale
    /// siempre: para probarlo es justo lo que se quiere.
    ///
    /// Vive en PlayerPrefs porque el backend no tiene dónde guardarlo: en otro equipo
    /// la partida vuelve a ofrecer el tutorial, sin más consecuencia que esa.
    /// </summary>
    private const string PrefijoPartida = "Fishy.Tutorial.Partida.";

    /// <summary>La marca de antes, que iba por jugador. Solo se lee, para no volver a
    /// ofrecerles el tutorial a las partidas que ya existían (ver
    /// <see cref="HeredarLoVistoPorElJugador"/>).</summary>
    private const string PrefijoJugador = "Fishy.Tutorial.Visto.";

    private static string ClaveDeLaPartida()
    {
        int? partida = ApiManager.Instance?.PartidaId;
        return partida.HasValue ? PrefijoPartida + partida.Value : null;
    }

    /// <summary>Lo registrado para la partida en curso.</summary>
    public static EstadoTutorial EstadoDeLaPartida()
    {
        string clave = ClaveDeLaPartida();
        return clave != null
            ? (EstadoTutorial)PlayerPrefs.GetInt(clave, (int)EstadoTutorial.SinRegistrar)
            : EstadoTutorial.SinRegistrar;
    }

    /// <summary>True si esta partida ya completó u omitió el tutorial. Lo consulta
    /// quien decide a qué escena entrar.</summary>
    public static bool YaSeVio() => EstadoDeLaPartida() != EstadoTutorial.SinRegistrar;

    /// <summary>Anota cómo terminó el tutorial en la partida en curso.</summary>
    public static void Registrar(EstadoTutorial estado)
    {
        string clave = ClaveDeLaPartida();
        if (clave == null)
        {
            Debug.Log($"[Tutorial] {estado} sin partida: no se registra y volverá a salir.");
            return;
        }

        PlayerPrefs.SetInt(clave, (int)estado);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Pasa a la partida que se retoma la marca que el jugador tenía de antes, cuando
    /// el tutorial se contaba por jugador. Solo es para retomar: una partida nueva
    /// tiene que recibir su oferta aunque el jugador ya haya visto el tutorial.
    /// </summary>
    public static void HeredarLoVistoPorElJugador()
    {
        int? jugador = ApiManager.Instance?.JugadorId;
        if (jugador.HasValue && !YaSeVio() &&
            PlayerPrefs.GetInt(PrefijoJugador + jugador.Value, 0) == 1)
            Registrar(EstadoTutorial.Completado);
    }

    // ── Arranque ─────────────────────────────────────────────────────────────

    private void Start()
    {
        ResolverReferencias();

        // El botón del Tab arranca apagado: el panel de jugador todavía no se ha
        // explicado. Lo enciende el paso que habla de él.
        if (botonDelTab != null) botonDelTab.SetActive(false);

        if (lupa != null) idDeLaLupa = lupa.objetoId;

        // Todo bajo llave desde el principio: cada paso abre lo suyo y lo deja
        // abierto para siempre (ver Candado).
        Candado(npcInteractuar, false);
        Candado(lupa, false);
        Candado(npcOpciones, false);
        Candado(npcDetective, false);
        StartCoroutine(CerrarElPanelTrasUnFrame());

        ArmarPasos();
        Suscribir();

        if (pasos.Count == 0)
        {
            Debug.LogWarning("[Tutorial] No hay ningún paso que enseñar: faltan todas las " +
                             "piezas de la escena. El guía se queda callado.", this);
            enabled = false;
            return;
        }

        eligiendo = true;
        OfertaDelTutorial.Mostrar(EmpezarLaGuia, Saltar);
    }

    /// <summary>
    /// Ver tutorial. El celular nace aquí y no en <see cref="Start"/>: al crearse
    /// entra a la pantalla con su animación, y durante la oferta la guía no existe.
    /// </summary>
    private void EmpezarLaGuia()
    {
        eligiendo = false;
        if (otto != null) otto.EnableMovement();

        celular = CelularDelGuia.GetOrCreate();
        celular.AlSaltar = Saltar;

        // El celular se crea en código y no vive en la escena, así que no tiene
        // Inspector donde arrastrarle nada: las imágenes se le pasan desde aquí, que
        // es donde ya están los textos que las nombran.
        if (imagenesDelGuia != null && imagenesDelGuia.Length > 0)
            celular.imagenes = imagenesDelGuia;

        EntrarEnPaso(0);
    }

    /// <summary>
    /// Saltar tutorial, desde la oferta o desde cualquier paso. La partida queda como
    /// omitida y se entra al juego por el mismo camino que al terminar, así que no se
    /// toca nada de lo que ya llevara.
    ///
    /// Si el huemul ya había entregado su misión, se cierra aquí: viaja con la partida
    /// y, con la lupa fuera del juego, se quedaría en el cartel para siempre.
    /// </summary>
    private void Saltar()
    {
        if (entrando || despedido) return;

        eligiendo = false;
        CerrarLaMision();
        Entrar(EstadoTutorial.Omitido);

        if (entrando)
        {
            if (otto != null) otto.DisableMovement();
            return;
        }

        // No se pudo entrar al juego. Saltando desde la oferta no habría nada en
        // pantalla, y es mejor seguir con la guía que dejar a Otto quieto sin salida.
        if (celular == null) EmpezarLaGuia();
    }

    private void CerrarLaMision()
    {
        MissionManager misiones = MissionManager.Instance;
        MissionGiver dador = MisionDelHuemul();
        string mision = dador != null && dador.desafio != null ? dador.desafio.desafioId : null;

        if (misiones != null && !string.IsNullOrEmpty(mision) && misiones.EstaDisponible(mision))
            misiones.CompletarDesafio(mision, "se saltó el tutorial");
    }

    private MissionGiver MisionDelHuemul() =>
        npcInteractuar != null ? npcInteractuar.GetComponent<MissionGiver>() : null;

    private void ResolverReferencias()
    {
        if (otto == null) otto = FindAnyObjectByType<OttoController>();
        if (npcInteractuar == null) npcInteractuar = FindAnyObjectByType<DialogoNeutroNPC>();
        if (lupa == null) lupa = FindAnyObjectByType<WorldItem>();
        if (npcOpciones == null) npcOpciones = FindAnyObjectByType<ChatModuleLauncher>();
        if (npcDetective == null) npcDetective = FindAnyObjectByType<DetectiveLauncher>();
        if (panelDeJugador == null) panelDeJugador = FindAnyObjectByType<MenuController>();

        // La pantalla de carga también vive apagada —la enciende ella misma al
        // empezar a cargar—, así que va con la misma búsqueda que incluye inactivos.
        if (pantallaDeCarga == null)
            pantallaDeCarga = FindAnyObjectByType<Fishy.UI.PantallaDeCarga>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Los pasos, en orden, cada uno con el hecho que lo cierra. El que no tenga su
    /// pieza en la escena no entra en la lista: es mejor un tutorial más corto que
    /// uno atascado pidiendo algo que no existe.
    /// </summary>
    private void ArmarPasos()
    {
        if (otto != null)
            Agregar(textoMover, () => distanciaRecorrida >= distanciaParaMoverse);
        else
            Debug.LogWarning("[Tutorial] Sin Otto en la escena: me salto el paso de moverse.", this);

        if (npcInteractuar != null)
            Agregar(textoInteractuar, () => habloConElNpc,
                alEmpezar: () => Candado(npcInteractuar, true));

        // La lupa se da por recogida cuando desaparece del mapa: WorldItem se destruye
        // al recogerlo, y si alguien le desmarca 'Destroy On Pickup' deja de poder
        // interactuarse. Las dos cosas se miran, así que la casilla no rompe el paso.
        if (lupa != null)
            Agregar(textoLupa, () => lupa == null || !lupa.CanInteract(),
                alEmpezar: () => Candado(lupa, true));

        if (npcOpciones != null)
            Agregar(textoOpciones, () => cerroElChat,
                alEmpezar: () => Candado(npcOpciones, true));

        // El paso del Tab vuelve a estar, y con él la misión que lo pide: hasta aquí
        // el botón y el panel estaban apagados porque nadie los había explicado (ver
        // Start), y este paso es el que los explica y los enciende.
        if (panelDeJugador != null)
            Agregar(textoPanel, () => abrioElPanel, alEmpezar: AbrirElPanelDeJugador);

        if (npcDetective != null)
            Agregar(textoDetective, () => cerroElCaso,
                alEmpezar: () => Candado(npcDetective, true));
    }

    /// <summary>
    /// Devuelve el panel del Tab y su botón, que arrancan apagados.
    ///
    /// El componente se vuelve a habilitar —su Update es el que escucha la tecla— y
    /// el botón reaparece: a partir de aquí el celular es del niño/a. No se vuelve a
    /// apagar, por lo mismo que los candados: un paso ya enseñado no se desenseña.
    /// </summary>
    private void AbrirElPanelDeJugador()
    {
        if (panelDeJugador != null) panelDeJugador.enabled = true;
        if (botonDelTab != null) botonDelTab.SetActive(true);
    }

    // ── El candado: qué se puede tocar en cada paso ──────────────────────────

    /// <summary>
    /// Abre o cierra el candado de algo interactuable, apagándole los colliders.
    ///
    /// <b>Por qué los colliders y no el componente.</b> El <c>InteractionDetector</c>
    /// de Otto se queda con todo lo que entre en su trigger y tenga un
    /// <c>IInteractable</c>, y no mira si el componente está habilitado. Sin collider
    /// no entra en el radio: ni se le puede hablar, ni le sale la manito encima — que
    /// es justo lo que se busca, un paso sin carteles invitando a otra cosa.
    ///
    /// El precio: mientras está cerrado, Otto lo atraviesa. Sale más barato que
    /// inventar un filtro dentro de un script que usa todo el equipo.
    ///
    /// Se abre y no se vuelve a cerrar: quien quiera volver a hablar con Huemulcito
    /// en el paso 4 puede, y así un paso que se dé por cumplido antes de tiempo no
    /// deja nada inalcanzable.
    /// </summary>
    private static void Candado(Component quien, bool abierto)
    {
        if (quien == null) return;

        foreach (Collider2D collider in quien.GetComponentsInChildren<Collider2D>(true))
            collider.enabled = abierto;
    }

    /// <summary>
    /// Apaga el panel de jugador, que el tutorial no enseña. <c>MenuController.Update</c>
    /// hace una sola cosa —mirar si se pulsó Tab—, así que apagar el componente deja la
    /// tecla muda sin tocar nada más.
    ///
    /// Espera un frame a propósito: apagarlo dentro de nuestro <c>Start</c> podría
    /// adelantarse al suyo, y así el panel termina de montarse como en cualquier escena.
    /// </summary>
    private IEnumerator CerrarElPanelTrasUnFrame()
    {
        yield return null;
        if (panelDeJugador != null) panelDeJugador.enabled = false;
    }

    private void Agregar(string texto, Func<bool> cumplido, Action alEmpezar = null)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;
        pasos.Add(new Paso { Texto = texto.Trim(), Cumplido = cumplido, AlEmpezar = alEmpezar });
    }

    /// <summary>
    /// Se escuchan los mismos eventos que mira el sistema de misiones para cumplir
    /// un objetivo: el fin del diálogo, el cierre de la sesión de chat y el cierre
    /// del caso. Varios oyentes en el mismo evento no molestan, así que esto
    /// convive con el MissionGiver del huemul.
    /// </summary>
    private void Suscribir()
    {
        if (npcInteractuar != null) npcInteractuar.onDialogueEnded.AddListener(AlTerminarElDialogo);
        if (npcOpciones != null) npcOpciones.OnSesionFinalizada += AlCerrarElChat;
        if (npcDetective != null) npcDetective.onCasoResuelto.AddListener(AlCerrarElCaso);
    }

    private void OnDestroy()
    {
        if (npcInteractuar != null) npcInteractuar.onDialogueEnded.RemoveListener(AlTerminarElDialogo);
        if (npcOpciones != null) npcOpciones.OnSesionFinalizada -= AlCerrarElChat;
        if (npcDetective != null) npcDetective.onCasoResuelto.RemoveListener(AlCerrarElCaso);
    }

    private void AlTerminarElDialogo() => habloConElNpc = true;
    private void AlCerrarElChat(float porcentajeSeguro) => cerroElChat = true;
    private void AlCerrarElCaso() => cerroElCaso = true;

    // ── Ciclo ────────────────────────────────────────────────────────────────

    private void Update()
    {
        // En cada frame y no una vez: Otto se da el control a sí mismo en su Start, y
        // no hay garantía de que ese Start corra antes que el nuestro.
        if (eligiendo)
        {
            if (otto != null && otto.movementEnabled) otto.DisableMovement();
            return;
        }

        MedirElCamino();
        VigilarElChat();
        VigilarElCelular();
        VigilarElPanel();

        if (entrando) return;

        if (indice >= pasos.Count)
        {
            Despedida();
            return;
        }

        if (indice >= 0 && pasos[indice].Cumplido()) EntrarEnPaso(indice + 1);
    }

    /// <summary>
    /// Empieza un paso: enciende lo que ese paso necesite, suelta sus burbujas y
    /// pone el contador. Las burbujas se escriben <b>una sola vez</b>, al entrar; el
    /// celular es una conversación que se acumula, no un cartel que se repinta.
    /// </summary>
    private void EntrarEnPaso(int nuevo)
    {
        indice = nuevo;
        if (indice >= pasos.Count)
        {
            celular.PonerTitulo("");
            return;
        }

        Paso paso = pasos[indice];
        paso.AlEmpezar?.Invoke();
        celular.PonerTitulo($"{rotulo} {indice + 1}/{pasos.Count}");
        celular.Decir(paso.Texto);
    }

    /// <summary>
    /// Suma el camino andado. Se mide la distancia y no las teclas a propósito: con
    /// Otto frenado contra algo, pulsar una flecha no es haber aprendido a moverse.
    /// </summary>
    private void MedirElCamino()
    {
        if (otto == null) return;

        Vector2 ahora = otto.transform.position;
        if (ultimaPosicion.HasValue)
            distanciaRecorrida += Vector2.Distance(ultimaPosicion.Value, ahora);
        ultimaPosicion = ahora;
    }

    /// <summary>
    /// Le quita el control a Otto mientras el chat está abierto y se lo devuelve al
    /// cerrarse.
    ///
    /// Lo hace el tutorial porque <see cref="ChatModuleLauncher"/> no lo hace: de los
    /// tres lanzadores de conversación, el del celular y el del caso detective paran
    /// a Otto, y este no. Sin esto, Otto sigue caminando por detrás de la pantalla
    /// del chat mientras el niño/a elige su respuesta.
    /// </summary>
    private void VigilarElChat()
    {
        if (otto == null) return;

        bool abierto = ChatModuleController.Instance != null && ChatModuleController.Instance.IsActive;
        if (abierto == movimientoQuitadoPorElChat) return;

        movimientoQuitadoPorElChat = abierto;
        if (abierto) otto.DisableMovement();
        else otto.EnableMovement();
    }

    /// <summary>
    /// Esconde el celular del guía cuando hay algo que mirar por delante: un
    /// diálogo, el chat, el Modo Detective o una cinemática. También al entrar al
    /// juego, que es lo que cierra la guía al saltarla.
    ///
    /// <b>La señal es que Otto no se puede mover.</b> Todas esas pantallas le quitan
    /// el control con <c>DisableMovement</c>, así que "Otto camina" significa "no
    /// hay nada en medio". No es un criterio inventado aquí: es exactamente el que
    /// usa <c>MenuController.PuedeAbrir</c> para decidir si el panel se puede abrir,
    /// y preguntarlo igual evita tener que enumerar —y mantener— la lista de
    /// pantallas que existen en el juego.
    ///
    /// La excepción es la despedida: ahí el movimiento se lo quitamos nosotros, y
    /// el celular tiene que quedarse para que se lea la última línea.
    /// </summary>
    /// <summary>El panel del Tab no avisa cuando se abre, así que se le pregunta.
    /// Una vez abierto no hace falta seguir mirando.</summary>
    private void VigilarElPanel()
    {
        if (abrioElPanel || panelDeJugador == null) return;
        if (panelDeJugador.EstaAbierto) abrioElPanel = true;
    }

    private void VigilarElCelular()
    {
        if (celular == null) return;

        bool algoPorDelante = entrando || (otto != null && !otto.movementEnabled && !despedido);

        celular.Mostrar(!algoPorDelante);
    }

    // ── Final ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Celebración, última burbuja y entrada al juego. A Otto se le quita el control
    /// aquí: no queda nada que practicar, y así la tecla de entrar no se mezcla con
    /// el salto.
    ///
    /// <b>La celebración va antes que la despedida</b>, no a la vez: el mensaje del
    /// celular se leería a medias por detrás del fundido. Y mientras dura, la tecla
    /// de entrar no responde — pulsarla sin querer durante la animación se llevaría
    /// por delante tanto la felicitación como el último mensaje.
    /// </summary>
    private void Despedida()
    {
        if (!despedido)
        {
            despedido = true;
            celular.OcultarSaltar();
            if (otto != null && !movimientoQuitadoPorElChat) otto.DisableMovement();

            celebrando = true;
            FinalDelTutorial.Mostrar(tituloFinal, subtituloFinal, () =>
            {
                celebrando = false;
                celular.Decir(textoFinal);
            });
        }

        if (celebrando) return;

        Keyboard teclado = Keyboard.current;
        if (teclado == null || !teclado[teclaParaEmpezar].wasPressedThisFrame) return;

        Entrar(EstadoTutorial.Completado);
    }

    /// <summary>
    /// Entra al juego, por la misma pantalla de carga con la que se entra desde el
    /// menú: tapa el arranque hasta que se sabe dónde quedó Otto, en vez de dejar
    /// que se le vea colocarse cuando llegue su posición guardada.
    ///
    /// <b>Si no hay pantalla en la escena se entra igual, de golpe.</b> Es una copia
    /// que se pega a mano, y un tutorial que no sepa terminar porque falta un objeto
    /// de adorno sería mucho peor que un salto feo.
    /// </summary>
    private void Entrar(EstadoTutorial estado)
    {
        entrando = true;

        if (!Application.CanStreamedLevelBeLoaded(escenaDelJuego))
        {
            Debug.LogError($"[Tutorial] La escena '{escenaDelJuego}' no está en Build Settings, " +
                            "así que no se puede entrar al juego.", this);
            entrando = false;
            return;
        }

        Registrar(estado);

        // Antes de arrancar la carga, no después: lo que se saca de la cola tiene que
        // estar fuera ya, y la escena nueva puede activarse en cualquier frame.
        if (limpiarLaColaAlSalir) LimpiarLaCola();

        if (TaparConLaPantallaDeCarga()) return;

        SceneManager.LoadScene(escenaDelJuego);
    }

    /// <summary>
    /// Enciende la pantalla de carga y le deja a ella la entrada al juego. Devuelve
    /// false si no hay ninguna o si no pudo con la escena, y entonces manda quien
    /// llama.
    ///
    /// <b>El orden de dibujo se le pone aquí.</b> La copia viene del menú con
    /// sortingOrder 100, que allí sobra porque no hay nada más arriba; en esta escena
    /// está el celular del guía (1250), el panel del Tab (1200), el cartel de misión
    /// (710) y la celebración (1400), y con 100 la carga saldría por debajo de todos.
    /// Se cambia desde el código y no en el Inspector a propósito: es el tutorial
    /// quien sabe lo que tiene encima, y así pegar la copia no pide acordarse de un
    /// campo. No se toca nada del menú — este Canvas vive y muere en esta escena.
    /// </summary>
    private bool TaparConLaPantallaDeCarga()
    {
        if (pantallaDeCarga == null) return false;

        Canvas canvas = pantallaDeCarga.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = OrdenDeLaPantallaDeCarga;

        return pantallaDeCarga.Cargar(escenaDelJuego);
    }

    /// <summary>
    /// Saca de la cola de guardado lo que generó el tutorial.
    ///
    /// <b>Por qué.</b> El tutorial produce progreso con ids que el backend no
    /// conoce: su caso detective, su misión, sus objetivos y su lupa del suelo. Un
    /// cambio que el servidor rechaza vuelve a la cola <b>sin tope de intentos</b>
    /// (ver <c>ColaDeCambios.Vaciar</c>), así que cada guardado posterior se pasaría
    /// su plazo entero reintentando algo que nunca va a entrar, y el cierre del
    /// juego esperaría por ello. Se quitan aquí, cuando ya hicieron su trabajo.
    ///
    /// Nada de esto se pierde para el jugador: la misión del tutorial, el caso y el
    /// objeto recogido siguen anotados en memoria y en PlayerPrefs. Lo único que se
    /// cancela es el viaje al servidor.
    ///
    /// <b>Lo que esto NO arregla.</b> La lupa sigue dentro de la mochila, y la
    /// mochila sube como un único snapshot con todo su contenido. Si el backend
    /// rechaza el id <c>ITEM_LUPA</c>, lo que falla es el inventario entero, y eso
    /// solo se arregla con una fila en su tabla de objetos.
    /// </summary>
    private void LimpiarLaCola()
    {
        var claves = new List<string>();

        if (npcDetective != null && !string.IsNullOrEmpty(npcDetective.CasoId))
            claves.Add($"detective:{npcDetective.CasoId}");

        if (!string.IsNullOrEmpty(idDeLaLupa))
            claves.Add($"objeto:{idDeLaLupa}");

        MissionGiver dador = MisionDelHuemul();
        string mision = dador != null && dador.desafio != null ? dador.desafio.desafioId : null;

        if (!string.IsNullOrEmpty(mision))
        {
            claves.Add($"mision:{mision}");

            // Los objetivos armados en el Inspector se numeran por su posición y
            // desde 1 (MissionTracker.NumerarLosDelInspector), y ese número es la
            // otra mitad de su clave.
            int cuantos = dador.objetivos != null ? dador.objetivos.Count : 0;
            for (int i = 1; i <= cuantos; i++) claves.Add($"objetivo:{mision}:{i}");
        }

        if (claves.Count == 0) return;

        foreach (string clave in claves) ColaDeCambios.Olvidar(clave);
        Debug.Log("[Tutorial] Fuera de la cola de guardado: " + string.Join(", ", claves));
    }
}
