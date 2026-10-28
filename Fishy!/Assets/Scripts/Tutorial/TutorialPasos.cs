using System;
using System.Collections.Generic;
using Fishy.Chat;
using Fishy.Detective;
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
/// <b>Cada paso se cierra con un hecho, no con un clic.</b> Moverse se comprueba
/// con la distancia recorrida; hablar, chatear y el caso detective, con el mismo
/// evento que usa el sistema de misiones para dar un objetivo por cumplido; y el
/// paso del panel de jugador, mirando si su página de misiones está a la vista.
/// Así el tutorial no se puede pasar pulsando una tecla sin haber aprendido nada.
///
/// <b>No modifica nada del juego.</b> De lo que ya existía solo lee
/// (<c>MenuController.EstaAbierto</c>, si la página de misiones está activa) y
/// enciende o apaga el botón del Tab de ESTA escena, que es una copia propia del
/// tutorial. Ningún script ni escena del juego cambia por esto.
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
    [Tooltip("El controlador del panel. Solo se le consulta si está abierto.")]
    public MenuController panelDeJugador;

    [Tooltip("La página de misiones del panel (QuestPage). El paso se da por hecho " +
             "cuando esta página está a la vista.")]
    public GameObject paginaDeMisiones;

    [Tooltip("El botón que anuncia el Tab (BotonCelular). Arranca apagado y se " +
             "enciende cuando el guía habla del panel: antes no se ha explicado " +
             "para qué sirve, y un botón sin explicar es ruido.")]
    public GameObject botonDelTab;

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
    [TextArea(2, 5)] public string textoPanel =
        "Con TAB se abre tu panel de jugador: ahí está la sección Misiones, con todo lo que llevas.\n\n" +
        "Échale un vistazo y cierra el panel para seguir.";
    [TextArea(2, 5)] public string textoOpciones =
        "¡Buen trabajo! Ese panel lo puedes abrir cuando quieras.\n\n" +
        "Ahora Rosita, la flamenca. Al hablar con ella vas a elegir una respuesta entre " +
        "varias: toca la que quieras, aquí ninguna está mal.";
    [TextArea(2, 5)] public string textoDetective =
        "Falta la foca, que abre el Modo Detective: ahí se revisan conversaciones " +
        "para buscar señales de riesgo. Ella te explica el resto.";
    [TextArea(2, 5)] public string textoFinal =
        "¡Listo! Ya sabes moverte, hablar, elegir respuestas y usar el Modo Detective.\n\n" +
        "Pulsa Espacio para empezar tu aventura.";

    [Header("Título de arriba")]
    [Tooltip("Lo que va delante del contador: \"TUTORIAL 3/7\".")]
    public string rotulo = "TUTORIAL";

    [Header("Al terminar")]
    [Tooltip("Escena del juego. Tiene que estar en Build Settings.")]
    public string escenaDelJuego = "MainScene";
    [Tooltip("Tecla que entra al juego en la última línea. Espacio a propósito: con la " +
             "E, estando al lado de la foca, la misma pulsación podría reabrirle el caso.")]
    public Key teclaParaEmpezar = Key.Space;

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

    private readonly List<Paso> pasos = new List<Paso>();
    private int indice = -1;          // -1 = todavía no empezó ninguno

    private CelularDelGuia celular;

    // Hechos que llegan por evento. Se guardan en vez de consultarse porque el
    // evento pasa una sola vez y el paso puede no estar mirando en ese frame.
    private bool habloConElNpc;
    private bool cerroElChat;
    private bool cerroElCaso;
    private bool vioLasMisiones;

    private float distanciaRecorrida;
    private Vector2? ultimaPosicion;

    /// <summary>El objetoId de la lupa, copiado al empezar: al recogerla el WorldItem
    /// se destruye, y para entonces ya no habría a quién preguntárselo.</summary>
    private string idDeLaLupa;

    private bool movimientoQuitadoPorElChat;
    private bool despedido;
    private bool entrando;

    // ── El "solo la primera vez" ─────────────────────────────────────────────

    /// <summary>
    /// Marca local de tutorial visto, SIEMPRE bajo el jugador que lo vio.
    ///
    /// Sin jugador identificado no se guarda nada, y es a propósito: una clave
    /// compartida le daría el tutorial por visto al siguiente niño/a que entrara en
    /// el mismo equipo. Es la misma lección que dejó <c>DetectiveLauncher</c> con su
    /// clave "local". Jugando la escena suelta en el editor tampoco hay jugador, así
    /// que ahí el tutorial sale siempre: para probarlo es justo lo que se quiere.
    /// </summary>
    private const string PrefijoClave = "Fishy.Tutorial.Visto.";

    private static string ClaveDelJugador()
    {
        int? jugador = ApiManager.Instance?.JugadorId;
        return jugador.HasValue ? PrefijoClave + jugador.Value : null;
    }

    /// <summary>True si este jugador ya vio el tutorial. Lo consulta quien decide a
    /// qué escena entrar al pulsar Jugar.</summary>
    public static bool YaSeVio()
    {
        string clave = ClaveDelJugador();
        return clave != null && PlayerPrefs.GetInt(clave, 0) == 1;
    }

    /// <summary>Deja el tutorial por visto. Idempotente.</summary>
    public static void MarcarVisto()
    {
        string clave = ClaveDelJugador();
        if (clave == null)
        {
            Debug.Log("[Tutorial] Terminado sin jugador identificado: no se marca como " +
                      "visto y volverá a salir.");
            return;
        }

        PlayerPrefs.SetInt(clave, 1);
        PlayerPrefs.Save();
    }

    // ── Arranque ─────────────────────────────────────────────────────────────

    private void Start()
    {
        ResolverReferencias();

        // El botón del Tab arranca apagado: el panel de jugador todavía no se ha
        // explicado. Lo enciende el paso que habla de él.
        if (botonDelTab != null) botonDelTab.SetActive(false);

        if (lupa != null) idDeLaLupa = lupa.objetoId;

        celular = CelularDelGuia.GetOrCreate();

        ArmarPasos();
        Suscribir();

        if (pasos.Count == 0)
        {
            Debug.LogWarning("[Tutorial] No hay ningún paso que enseñar: faltan todas las " +
                             "piezas de la escena. El guía se queda callado.", this);
            enabled = false;
            return;
        }

        EntrarEnPaso(0);
    }

    private void ResolverReferencias()
    {
        if (otto == null) otto = FindAnyObjectByType<OttoController>();
        if (npcInteractuar == null) npcInteractuar = FindAnyObjectByType<DialogoNeutroNPC>();
        if (lupa == null) lupa = FindAnyObjectByType<WorldItem>();
        if (npcOpciones == null) npcOpciones = FindAnyObjectByType<ChatModuleLauncher>();
        if (npcDetective == null) npcDetective = FindAnyObjectByType<DetectiveLauncher>();
        if (panelDeJugador == null) panelDeJugador = FindAnyObjectByType<MenuController>();

        // La página de misiones vive apagada dentro del panel, así que hay que
        // pedirla incluyendo los objetos inactivos o no aparece.
        if (paginaDeMisiones == null)
        {
            QuestPageUI pagina = FindAnyObjectByType<QuestPageUI>(FindObjectsInactive.Include);
            if (pagina != null) paginaDeMisiones = pagina.gameObject;
        }
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
            Agregar(textoInteractuar, () => habloConElNpc);

        // La lupa se da por recogida cuando desaparece del mapa: WorldItem se destruye
        // al recogerlo, y si alguien le desmarca 'Destroy On Pickup' deja de poder
        // interactuarse. Las dos cosas se miran, así que la casilla no rompe el paso.
        if (lupa != null)
            Agregar(textoLupa, () => lupa == null || !lupa.CanInteract());

        // El panel de jugador: se da por aprendido cuando el jugador pasó por la
        // sección Misiones Y volvió al mundo. Las dos condiciones, y en un solo paso,
        // porque el celular no se ve con el panel abierto: no hay forma de darle las
        // gracias ahí dentro, así que la felicitación va en el paso siguiente.
        //
        // Si cierra el panel sin haber entrado en Misiones no pasa nada malo: al
        // cerrarlo reaparece el celular con la instrucción todavía puesta.
        if (paginaDeMisiones != null)
        {
            Agregar(textoPanel,
                () => vioLasMisiones && (panelDeJugador == null || !panelDeJugador.EstaAbierto),
                alEmpezar: () => { if (botonDelTab != null) botonDelTab.SetActive(true); });
        }
        else
        {
            Debug.LogWarning("[Tutorial] No encuentro la página de misiones del panel " +
                             "(QuestPage): me salto el paso del Tab.", this);
        }

        if (npcOpciones != null)
            Agregar(textoOpciones, () => cerroElChat);

        if (npcDetective != null)
            Agregar(textoDetective, () => cerroElCaso);
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
        MedirElCamino();
        VigilarElChat();
        MirarSiVioLasMisiones();
        VigilarElCelular();

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

    /// <summary>Anota que el jugador llegó a ver la sección Misiones del panel.</summary>
    private void MirarSiVioLasMisiones()
    {
        if (!vioLasMisiones && paginaDeMisiones != null && paginaDeMisiones.activeInHierarchy)
            vioLasMisiones = true;
    }

    /// <summary>
    /// Esconde el celular del guía cuando hay algo que mirar por delante: un
    /// diálogo, el chat, el Modo Detective, el panel del Tab o una cinemática.
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
    private void VigilarElCelular()
    {
        if (celular == null) return;

        bool algoPorDelante =
            (otto != null && !otto.movementEnabled && !despedido) ||
            (panelDeJugador != null && panelDeJugador.EstaAbierto);

        celular.Mostrar(!algoPorDelante);
    }

    // ── Final ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Última burbuja y entrada al juego. A Otto se le quita el control aquí: no
    /// queda nada que practicar, y así la tecla de entrar no se mezcla con el salto.
    /// </summary>
    private void Despedida()
    {
        if (entrando) return;

        if (!despedido)
        {
            despedido = true;
            if (otto != null && !movimientoQuitadoPorElChat) otto.DisableMovement();
            celular.Decir(textoFinal);
        }

        Keyboard teclado = Keyboard.current;
        if (teclado == null || !teclado[teclaParaEmpezar].wasPressedThisFrame) return;

        Entrar();
    }

    /// <summary>
    /// Entra al juego. Sin pantalla de carga: esa vive en el Canvas del menú, así
    /// que aquí se vería a Otto colocarse cuando llegue su posición guardada. Si
    /// algún día molesta, se monta una <c>PantallaDeCarga</c> en esta escena y se
    /// llama a su <c>Cargar</c> en vez de esto.
    /// </summary>
    private void Entrar()
    {
        entrando = true;
        MarcarVisto();

        if (!Application.CanStreamedLevelBeLoaded(escenaDelJuego))
        {
            Debug.LogError($"[Tutorial] La escena '{escenaDelJuego}' no está en Build Settings, " +
                            "así que no se puede entrar al juego.", this);
            entrando = false;
            return;
        }

        if (limpiarLaColaAlSalir) LimpiarLaCola();

        SceneManager.LoadScene(escenaDelJuego);
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

        MissionGiver dador = npcInteractuar != null
            ? npcInteractuar.GetComponent<MissionGiver>()
            : null;
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
