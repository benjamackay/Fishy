using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fishy.Net;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controlador de la pantalla "Ingresar": toma usuario + contrasena, autentica
/// contra el backend Django y muestra los perfiles. Tras elegir perfil y partida, carga el menu.
///
/// Los datos terminan en Supabase, pero por la via que fijo el equipo en
/// Backend/MIGRACION_SUPABASE.md: "Django adelante, Supabase solo como Postgres".
/// Unity no usa la API REST ni la anon key de Supabase; pega a /api/auth/login/ y
/// /api/auth/registro/ a traves de <see cref="ApiManager"/>, y Django escribe en el
/// Postgres administrado.
///
/// El campo de arriba es el NOMBRE DE USUARIO, no el email: el modelo
/// AdultoResponsable define USERNAME_FIELD = "nombre" (Backend/backend/api/models.py),
/// asi que /auth/login/ autentica por nombre. El email solo se pide al crear la
/// cuenta, donde el backend lo exige y es unico.
///
/// La clase se sigue llamando "iniciar" y conserva <see cref="MenuDos"/> porque el
/// boton "Ingresar" de la escena los tiene cableados en su onClick persistente
/// (m_TargetAssemblyTypeName: iniciar, Assembly-CSharp / m_MethodName: MenuDos).
/// Renombrarlos romperia ese wiring guardado en Ingresar.unity.
///
/// El resto de la pantalla se cablea sola en Awake() buscando los objetos por
/// nombre, asi que no hace falta arrastrar nada en el inspector.
/// </summary>
public class iniciar : MonoBehaviour
{
    public enum Modo { Login, Registro }

    [Header("Pruebas con perfiles automaticos")]
    [Tooltip("Activado: asegura los perfiles de prueba y elige uno sin pasar por el panel. " +
             "Desactivado: muestra el panel para elegir entre los perfiles del backend.")]
    public bool usarPerfilesDePrueba = false;
    [Tooltip("Estos perfiles se crean en la cuenta del backend si todavia no existen.")]
    public string[] perfiles = { "Perfil 1", "Perfil 2" };
    [Tooltip("Perfil de prueba a usar (1 = primero). perfil.txt junto al ejecutable tiene prioridad.")]
    [Min(1)] public int perfilActivo = 1;

    [Header("Panel de perfiles")]
    [Tooltip("Panel creado en la escena Ingresar. Si falta, se busca dentro del Canvas.")]
    [SerializeField] private PanelPerfilesUI panelPerfiles;
    [SerializeField] private Button botonAceptarPerfil;

    [Header("Panel de crear perfil")]
    [Tooltip("Duplicado del cartel del login con el formulario de perfil nuevo. " +
             "Si falta, el boton 'Crear perfil' avisa en consola y no hace nada.")]
    [SerializeField] private PanelCrearPerfilUI panelCrearPerfil;

    [Header("Panel de partidas")]
    [Tooltip("Panel creado en la escena Ingresar. Si falta, se busca dentro del Canvas. " +
             "Sin el, la eleccion de partida cae al cartel del login, que sigue funcionando.")]
    [SerializeField] private PanelPartidasUI panelPartidas;

    private bool esperandoPartidas;
    private int solicitudPartida;
    private float tamanoTituloOriginal;
    private bool autoSizeTituloOriginal;
    private TextWrappingModes ajusteTituloOriginal;

    [Header("Continuar partida")]
    [Tooltip("Cuantas sesiones se ofrecen para continuar. El backend las manda de la mas " +
             "reciente a la mas antigua, asi que se muestran las N ultimas. Con cero " +
             "partidas guardadas no se muestra nada: se empieza una y se entra directo.")]
    [Min(1)] public int maxPartidasEnLista = 3;

    [Header("Destino")]
    [Tooltip("Escena que se carga despues de autenticarse. Debe estar en Build Settings.")]
    public string escenaDestino = "MenuDos";

    [Header("Referencias (opcionales: si faltan se buscan por nombre en la escena)")]
    public TMP_InputField usuarioInput;
    public TMP_InputField passwordInput;
    public Button ingresarButton;
    public Button registerButton;
    public TMP_Text tituloLabel;
    public TMP_Text cuentaLabel;
    public TMP_Text registerLabel;

    [Header("Aspecto")]
    public Color colorError = new Color(0.94f, 0.38f, 0.38f);
    public Color colorInfo = new Color(0.85f, 0.85f, 0.85f);
    public Color colorOk = new Color(0.42f, 0.85f, 0.48f);

    /// <summary>Separacion vertical entre campos, tomada del layout de la escena.</summary>
    private const float AltoFila = 51f;

    /// <summary>Tamano de cada boton de la lista de partidas. Caben dos lineas de texto:
    /// "Seguir donde quedaste" y la fecha en que se guardo.</summary>
    private const float AnchoBoton = 380f;
    private const float AltoBoton = 70f;
    private const float TamanoTextoBoton = 21f;

    /// <summary>Aire entre dos botones de la lista de partidas, para que no se toquen.</summary>
    private const float SeparacionBotones = 10f;

    /// <summary>Aire entre el titulo y el primer boton, y entre el ultimo boton y el
    /// borde de abajo del cartel (que tiene marco y sombra).</summary>
    private const float MargenBajoTitulo = 14f;
    private const float MargenInferior = 30f;

    private Modo modo = Modo.Login;
    private TMP_InputField emailInput;   // solo existe en modo registro
    private TMP_Text estadoLabel;        // se crea en runtime: la escena no trae uno
    private RectTransform cartel;
    private Vector2 cartelPosOriginal, cartelSizeOriginal;
    private readonly Dictionary<RectTransform, Vector2> posOriginal = new Dictionary<RectTransform, Vector2>();
    private bool ocupado;
    private bool backendListo;

    /// <summary>Perfil de menor con el que se va a jugar, elegido desde el panel de perfiles.</summary>
    private UsuarioJugadorDto perfilElegido;
    /// <summary>Guarda contra el doble toque en la lista de partidas: sin el, dos toques
    /// seguidos en "Empezar una partida nueva" crean dos partidas.</summary>
    private bool seleccionando;
    private readonly List<Button> botonesPartida = new List<Button>();

    // -- Ciclo de vida ---------------------------------------------------------
    private void Awake()
    {
        UiBootstrap.EnsureEventSystem();
        AsegurarApiManager();
        Cablear();
        GuardarLayout();
        if (tituloLabel != null)
        {
            tamanoTituloOriginal = tituloLabel.fontSize;
            autoSizeTituloOriginal = tituloLabel.enableAutoSizing;
            ajusteTituloOriginal = tituloLabel.textWrappingMode;
        }

        AplicarPlaceholder(usuarioInput, "Usuario");

        // La escena guarda el campo de contrasena con m_ContentType: 0 (Standard),
        // o sea que se veia en texto plano mientras se escribia. Se enmascara aca
        // para no tener que editar el .unity a mano.
        if (passwordInput != null)
        {
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();
        }

        estadoLabel = CrearEtiquetaEstado();

        // Al boton "Ingresar" NO se le agrega listener: ya llama a MenuDos() por su
        // onClick persistente y se enviaria el formulario dos veces.
        if (registerButton != null) registerButton.onClick.AddListener(AlternarModo);
        if (usuarioInput != null) usuarioInput.onSubmit.AddListener(_ => Enviar());
        if (passwordInput != null) passwordInput.onSubmit.AddListener(_ => Enviar());

        AplicarModo();
        if (panelPerfiles != null)
        {
            panelPerfiles.gameObject.SetActive(false);
            panelPerfiles.Configurar(crearPerfil: MostrarCrearPerfil);
        }
        if (botonAceptarPerfil != null)
            botonAceptarPerfil.onClick.AddListener(AceptarPerfil);

        if (panelCrearPerfil != null)
        {
            panelCrearPerfil.gameObject.SetActive(false);
            panelCrearPerfil.Configurar(
                crear: PerfilCreado,
                cancelar: MostrarPerfiles);
        }

        // El panel monta la lista y deja elegir; entrar al juego lo sigue haciendo
        // esta clase, que es la unica que sabe atar el progreso y cargar la escena.
        if (panelPartidas != null)
        {
            panelPartidas.gameObject.SetActive(false);
            panelPartidas.Configurar(
                jugar: ContinuarPartida,
                crearNueva: CrearPartidaNueva,
                volver: MostrarPerfiles);
        }
    }

    private void OnDestroy()
    {
        ++solicitudPartida;
        if (botonAceptarPerfil != null)
            botonAceptarPerfil.onClick.RemoveListener(AceptarPerfil);
    }

    private void Start()
    {
        // ApiManager sobrevive entre escenas: si ya hay sesion no volvemos a pedir
        // credenciales (p. ej. al volver del menu).
        if (ApiManager.Instance.IsLoggedIn) { OnAuthOk(); return; }
        VerificarBackend();
    }

    // -- Entrada desde la UI ---------------------------------------------------

    /// <summary>
    /// Lo llama el boton "Ingresar" (onClick persistente de la escena). Antes cargaba
    /// la escena 2 directamente; ahora primero autentica y solo avanza si el backend
    /// confirmo las credenciales.
    /// </summary>
    public void MenuDos() => Enviar();

    /// <summary>Alterna entre "Iniciar Sesion" y "Crear cuenta". Lo llama el boton "Registrarse".</summary>
    public void AlternarModo()
    {
        if (ocupado) return;
        modo = modo == Modo.Login ? Modo.Registro : Modo.Login;
        AplicarModo();
        SetEstado(string.Empty, colorInfo);
    }

    public void Enviar()
    {
        if (ocupado) return;

        string usuario = usuarioInput != null ? usuarioInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;
        string email = emailInput != null ? emailInput.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(usuario)) { SetEstado("Escribi tu usuario.", colorError); return; }
        if (string.IsNullOrEmpty(password)) { SetEstado("Escribi tu contrasena.", colorError); return; }

        if (modo == Modo.Registro && (string.IsNullOrEmpty(email) || !email.Contains("@")))
        {
            SetEstado("Escribi un email valido: el backend lo exige para crear la cuenta.", colorError);
            return;
        }

        // Sin backend NO se entra. En modo local ApiManager guardaria la cuenta en
        // PlayerPrefs y no llegaria nada a Supabase, pero la pantalla pareceria haber
        // funcionado. Preferimos el error explicito y reintentar la conexion.
        if (!backendListo || ApiManager.Instance.IsLocalMode)
        {
            SetEstado("Sin conexion con el servidor: no se puede guardar. Reintentando...", colorError);
            VerificarBackend();
            return;
        }

        SetOcupado(true);

        if (modo == Modo.Login)
        {
            SetEstado("Ingresando...", colorInfo);
            ApiManager.Instance.Login(usuario, password, OnAuthOk, OnAuthError);
        }
        else
        {
            SetEstado("Creando cuenta...", colorInfo);
            ApiManager.Instance.Registro(usuario, email, password,
                onSuccess: OnAuthOk, onError: OnAuthError);
        }
    }

    // -- Backend ---------------------------------------------------------------
    private void VerificarBackend()
    {
        SetOcupado(true);
        SetEstado("Conectando con el servidor...", colorInfo);

        // ReintentarConexion en vez de CheckHealth: este ultimo corta en seco si
        // useLocalMode ya se prendio, y entonces nunca se recuperaria la conexion.
        ApiManager.Instance.ReintentarConexion(ok =>
        {
            backendListo = ok;
            SetOcupado(false);
            SetEstado(ok ? string.Empty
                         : "Sin conexion con el servidor. Verifica que el backend Django este corriendo.",
                      ok ? colorInfo : colorError);
        });
    }

    private void OnAuthOk()
    {
        if (usarPerfilesDePrueba)
        {
            SetOcupado(true);
            SetEstado("Preparando perfiles de prueba...", colorInfo);
            AsegurarPerfil(0, null);
            return;
        }
        MostrarPerfiles();
    }

    // Se conserva el atajo original para probar el progreso separado por perfil.
    // Solo se ejecuta cuando el modo de pruebas esta activado en el Inspector.
    private void AsegurarPerfil(int indice, List<UsuarioJugadorDto> existentes)
    {
        if (perfiles == null || perfiles.Length == 0 || perfiles.Any(string.IsNullOrWhiteSpace))
        {
            MostrarErrorPartida("Configura los nombres de los perfiles de prueba.");
            return;
        }

        ApiManager api = ApiManager.Instance;
        if (api == null || !api.IsLoggedIn || api.IsLocalMode)
        {
            MostrarErrorPartida("Se necesita una sesion conectada para probar los perfiles.");
            return;
        }

        int actual = solicitudPartida;
        string sesion = api.Token;
        if (existentes == null)
        {
            api.ListarJugadores(
                onSuccess: lista =>
                {
                    if (!RespuestaPartidaVigente(actual, api, sesion)) return;
                    if (lista == null || lista.Any(j => j == null || j.id <= 0))
                    {
                        MostrarErrorPartida("No se recibio una lista de perfiles valida.");
                        return;
                    }
                    AsegurarPerfil(0, lista);
                },
                onError: error =>
                {
                    if (RespuestaPartidaVigente(actual, api, sesion))
                        MostrarErrorPartida(TraducirError(error));
                });
            return;
        }

        if (indice >= perfiles.Length)
        {
            int elegido = Mathf.Clamp(LeerPerfilActivo(), 1, perfiles.Length) - 1;
            UsuarioJugadorDto perfil = existentes.FirstOrDefault(j => j.nombre == perfiles[elegido]);
            if (perfil == null) { MostrarErrorPartida("No se encontro el perfil de prueba."); return; }
            AbrirPartida(perfil);
            return;
        }

        string nombre = perfiles[indice];
        if (existentes.Any(j => j.nombre == nombre))
        {
            AsegurarPerfil(indice + 1, existentes);
            return;
        }

        api.CrearJugador(nombre, null,
            onSuccess: creado =>
            {
                if (!RespuestaPartidaVigente(actual, api, sesion)) return;
                if (creado == null || creado.id <= 0)
                {
                    MostrarErrorPartida("No se recibio un perfil de prueba valido.");
                    return;
                }
                existentes.Add(creado);
                AsegurarPerfil(indice + 1, existentes);
            },
            onError: error =>
            {
                if (RespuestaPartidaVigente(actual, api, sesion))
                    MostrarErrorPartida(TraducirError(error));
            });
    }

    private int LeerPerfilActivo()
    {
        try
        {
            string ruta = Path.Combine(Application.dataPath, "..", "perfil.txt");
            if (File.Exists(ruta) && int.TryParse(File.ReadAllText(ruta).Trim(), out int numero))
                return numero;
        }
        catch (Exception error)
        {
            Debug.LogWarning($"[Ingresar] No se pudo leer perfil.txt: {error.Message}");
        }
        return perfilActivo;
    }

    public void MostrarPerfiles()
    {
        if (panelPerfiles == null || !panelPerfiles.enabled || cartel == null ||
            botonAceptarPerfil == null)
        {
            SetOcupado(false);
            SetEstado("Falta configurar el panel de perfiles en el Inspector.", colorError);
            return;
        }

        ++solicitudPartida;
        esperandoPartidas = false;
        seleccionando = false;
        perfilElegido = null;
        LimpiarBotonesPartida();
        SetOcupado(true);
        // Login tambien contiene el fondo de pantalla. Solo ocultamos su cartel.
        cartel.gameObject.SetActive(false);
        if (estadoLabel != null) estadoLabel.gameObject.SetActive(false);
        if (panelPartidas != null) panelPartidas.gameObject.SetActive(false);
        if (panelCrearPerfil != null) panelCrearPerfil.gameObject.SetActive(false);
        panelPerfiles.Mostrar();
    }

    /// <summary>Abre el formulario de perfil nuevo. Lo pide el boton "Crear perfil"
    /// del panel de perfiles, que no conoce a este panel a proposito.</summary>
    public void MostrarCrearPerfil()
    {
        if (panelCrearPerfil == null)
        {
            Debug.LogError("[Ingresar] Falta el panel de crear perfil en el Inspector.", this);
            return;
        }

        if (panelPerfiles != null) panelPerfiles.gameObject.SetActive(false);
        panelCrearPerfil.Mostrar();
    }

    /// <summary>
    /// El perfil ya existe en el servidor. Se vuelve a la lista en vez de entrar
    /// directo con el recien creado: al adulto que esta dando de alta a dos hijos
    /// seguidos le sirve ver la lista, y el niño que va a jugar tiene que elegir
    /// igualmente. Recargar ademas confirma contra el backend que quedo guardado.
    /// </summary>
    private void PerfilCreado(UsuarioJugadorDto perfil)
    {
        if (perfil != null)
            Debug.Log($"[Ingresar] Perfil '{perfil.nombre}' creado (id {perfil.id}).");

        MostrarPerfiles();
    }

    public void AceptarPerfil()
    {
        if (esperandoPartidas || seleccionando || panelPerfiles == null ||
            !panelPerfiles.isActiveAndEnabled) return;

        UsuarioJugadorDto elegido = panelPerfiles.PerfilSeleccionado;
        ApiManager api = ApiManager.Instance;
        if (elegido == null || api == null || !api.IsLoggedIn || api.IsLocalMode) return;

        AbrirPartida(elegido);
    }

    private void AbrirPartida(UsuarioJugadorDto elegido)
    {
        // Camino nuevo: el panel pide la lista y deja elegir. El cartel de abajo se
        // conserva como respaldo para cuando el panel no esta montado en la escena,
        // que es tambien el caso del atajo de perfiles de prueba.
        if (panelPartidas != null)
        {
            ++solicitudPartida;
            esperandoPartidas = false;
            seleccionando = false;
            perfilElegido = elegido;
            LimpiarBotonesPartida();
            if (panelPerfiles != null) panelPerfiles.gameObject.SetActive(false);
            if (cartel != null) cartel.gameObject.SetActive(false);
            if (estadoLabel != null) estadoLabel.gameObject.SetActive(false);
            ApiManager.Instance.SeleccionarJugador(elegido.id);
            panelPartidas.Mostrar(elegido);
            return;
        }

        if (!PrepararCartelPartidas()) return;
        ApiManager api = ApiManager.Instance;
        perfilElegido = elegido;
        esperandoPartidas = true;
        int actual = ++solicitudPartida;
        string sesion = api.Token;

        if (panelPerfiles != null) panelPerfiles.gameObject.SetActive(false);
        if (tituloLabel != null) tituloLabel.text = "Preparando tu aventura";
        SetEstado("Buscando tus partidas...", colorInfo);

        api.SeleccionarJugador(elegido.id);
        api.ObtenerPartidasJugador(elegido.id,
            onSuccess: partidas =>
            {
                if (!RespuestaPartidaVigente(actual, api, sesion)) return;
                esperandoPartidas = false;
                if (partidas == null)
                {
                    MostrarErrorPartida("El servidor no devolvio una lista de partidas valida.");
                    return;
                }
                if (partidas.Count == 0) { CrearPartidaNueva(); return; }
                MostrarSelectorDePartidas(partidas);
            },
            onError: error =>
            {
                if (!RespuestaPartidaVigente(actual, api, sesion)) return;
                MostrarErrorPartida(TraducirError(error));
            });
    }

    private bool RespuestaPartidaVigente(int numero, ApiManager api, string sesion)
    {
        return this != null && isActiveAndEnabled && numero == solicitudPartida &&
            api != null && api == ApiManager.Instance && api.IsLoggedIn && api.Token == sesion;
    }

    private bool PrepararCartelPartidas()
    {
        if (cartel == null)
        {
            esperandoPartidas = false;
            seleccionando = false;
            SetOcupado(false);
            const string mensaje = "No se encontro Cartel. Revisa Login > Cartel y el campo Usuario Input de iniciar.";
            Debug.LogError("[Ingresar] " + mensaje, this);
            SetEstado(mensaje, colorError);
            return false;
        }
        LimpiarBotonesPartida();
        // El cartel y el panel ocupan el mismo sitio: si el panel se queda puesto,
        // el error queda debajo y el nino ve una lista que ya no responde.
        if (panelPartidas != null) panelPartidas.gameObject.SetActive(false);
        cartel.gameObject.SetActive(true);
        cartel.sizeDelta = cartelSizeOriginal;
        cartel.anchoredPosition = cartelPosOriginal;
        Mover(tituloLabel, 0f);
        foreach (Component c in new Component[] { usuarioInput, passwordInput, emailInput,
                                                  ingresarButton, registerButton, cuentaLabel })
            if (c != null) c.gameObject.SetActive(false);
        if (estadoLabel != null) estadoLabel.gameObject.SetActive(true);
        ReubicarEstado();
        return true;
    }

    private string TextoVolver => usarPerfilesDePrueba ? "Volver al login" : "Volver a perfiles";

    private void VolverDesdePartidas()
    {
        if (!usarPerfilesDePrueba)
        {
            MostrarPerfiles();
            return;
        }

        // El atajo de pruebas no depende de que PanelPerfiles este montado.
        // Volver al formulario no cierra la sesion ni borra progreso.
        ++solicitudPartida;
        esperandoPartidas = false;
        seleccionando = false;
        perfilElegido = null;
        LimpiarBotonesPartida();
        if (panelPerfiles != null) panelPerfiles.gameObject.SetActive(false);
        if (cartel != null) cartel.gameObject.SetActive(true);
        if (estadoLabel != null) estadoLabel.gameObject.SetActive(true);
        foreach (Component c in new Component[] { tituloLabel, usuarioInput, passwordInput,
                                                  ingresarButton, registerButton, cuentaLabel })
            if (c != null) c.gameObject.SetActive(true);
        if (tituloLabel != null)
        {
            tituloLabel.enableAutoSizing = autoSizeTituloOriginal;
            tituloLabel.fontSize = tamanoTituloOriginal;
            tituloLabel.textWrappingMode = ajusteTituloOriginal;
        }
        if (passwordInput != null) passwordInput.text = string.Empty;
        modo = Modo.Login;
        AplicarModo();
        VerificarBackend();
    }

    private void LimpiarBotonesPartida()
    {
        foreach (Button boton in botonesPartida)
        {
            if (boton == null) continue;
            boton.gameObject.SetActive(false);
            Destroy(boton.gameObject);
        }
        botonesPartida.Clear();
    }

    private void MostrarErrorPartida(string mensaje)
    {
        esperandoPartidas = false;
        seleccionando = false;
        if (!PrepararCartelPartidas()) return;
        if (tituloLabel != null) tituloLabel.text = "No se pudo continuar";
        SetEstado(mensaje, colorError);
        Button volver = CrearBotonPartida(TextoVolver, Paleta.MarronSuave,
            new Vector2(0f, -50f));
        volver.onClick.AddListener(VolverDesdePartidas);
    }

    // -- Seleccion de partida (HDU-15) -----------------------------------------
    //
    // Esta pantalla no tiene panel propio: reaprovecha el cartel del login. Los
    // botones se CLONAN del boton "Ingresar" para heredar tipografia, colores y
    // tamano del diseno que hizo el equipo, igual que hace CrearCampoEmail con los
    // campos de texto.

    private void MostrarSelectorDePartidas(List<PartidaDto> partidas)
    {
        // Sin cartel donde montarla no hay lista. Antes de HDU-15 esta puerta retomaba la
        // mas reciente sin preguntar: se vuelve a eso en vez de dejar al nino/a en una
        // pantalla vacia.
        if (cartel == null || usuarioInput == null)
        {
            Debug.LogWarning("[Ingresar] No se pudo montar la lista de partidas " +
                             "(falta el cartel). Se retoma la mas reciente.");
            ContinuarPartida(partidas[0]);
            return;
        }

        int cuantas = Mathf.Min(partidas.Count, Mathf.Max(1, maxPartidasEnLista));
        int filas = cuantas + 2;   // nueva partida y volver a perfiles

        // El titulo en UNA linea: "¿Seguimos tu aventura?" no cabe a 55 en los 403 px del
        // diseno, y la segunda linea ("aventura?") quedaba escondida detras del primer
        // boton. Se achica solo lo justo para caber.
        if (tituloLabel != null)
        {
            tituloLabel.text = "¿Seguimos tu aventura?";
            tituloLabel.textWrappingMode = TextWrappingModes.NoWrap;
            tituloLabel.fontSizeMax = tituloLabel.fontSize;
            tituloLabel.fontSizeMin = Mathf.Min(28f, tituloLabel.fontSize);
            tituloLabel.enableAutoSizing = true;
        }

        // Los botones van en el hueco entre el titulo y el borde de abajo del cartel,
        // medido en el cartel SIN crecer. Si no caben, el cartel crece justo lo que falta,
        // hacia abajo igual que en modo registro.
        float bloque = filas * AltoBoton + (filas - 1) * SeparacionBotones;
        float techo = BordeInferiorDelTitulo() - MargenBajoTitulo;
        float piso = -cartelSizeOriginal.y * 0.5f + MargenInferior;
        float crecer = Mathf.Max(0f, bloque - (techo - piso));
        cartel.sizeDelta = cartelSizeOriginal + new Vector2(0f, crecer);
        cartel.anchoredPosition = cartelPosOriginal - new Vector2(0f, crecer * 0.5f);

        foreach (Component c in new Component[] { usuarioInput, passwordInput, emailInput,
                                                  ingresarButton, registerButton, cuentaLabel })
            if (c != null) c.gameObject.SetActive(false);

        // El titulo sube con el borde de arriba, para quedar donde estaba.
        Mover(tituloLabel, crecer * 0.5f);

        // Al crecer, el techo sube y el piso baja la mitad cada uno: el centro del hueco
        // no se mueve, y ahi va el centro del bloque.
        float centro = (techo + piso) * 0.5f;
        float primera = centro + bloque * 0.5f - AltoBoton * 0.5f;
        float paso = AltoBoton + SeparacionBotones;

        for (int i = 0; i < cuantas; i++)
        {
            var partida = partidas[i];       // copia local: sin ella todos los botones
            bool masReciente = i == 0;       // usarian la ultima partida del bucle

            var boton = CrearBotonPartida(TextoDePartida.Etiqueta(partida, masReciente),
                masReciente ? Paleta.Verde : Paleta.MarronSuave,
                new Vector2(0f, primera - i * paso));
            boton.onClick.AddListener(() => ContinuarPartida(partida));
        }

        var nueva = CrearBotonPartida("Empezar una partida nueva", Paleta.Madera,
            new Vector2(0f, primera - cuantas * paso));
        nueva.onClick.AddListener(CrearPartidaNueva);

        var volver = CrearBotonPartida(TextoVolver, Paleta.MarronSuave,
            new Vector2(0f, primera - (cuantas + 1) * paso));
        volver.onClick.AddListener(VolverDesdePartidas);

        ReubicarEstado();
        SetEstado(partidas.Count > cuantas
            ? $"Partidas de {perfilElegido.nombre} (las {cuantas} mas recientes)."
            : $"Partidas de {perfilElegido.nombre}.", colorInfo);
    }

    /// <summary>
    /// Crea una fila de la lista de partidas.
    ///
    /// Antes se clonaba el boton "Ingresar" para heredar el diseno, pero ese boton es
    /// una imagen con la palabra INGRESAR dibujada (images/log_in_button2.png) y no
    /// trae ningun texto adentro. El clon no tenia donde escribir, asi que todas las
    /// filas salian diciendo "Ingresar" y no habia forma de saber cual era "Empezar
    /// una partida nueva": el nino/a apretaba una y se le creaba otra partida.
    /// </summary>
    private Button CrearBotonPartida(string texto, Color fondo, Vector2 posicion)
    {
        var boton = FishyUIKit.Boton(cartel, texto, fondo, TamanoTextoBoton, AltoBoton, null);

        // El cartel no tiene layout, asi que el LayoutElement que trae el boton no manda:
        // tamano y posicion van a mano, anclados al centro del cartel igual que los
        // campos del formulario.
        var rt = (RectTransform)boton.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(AnchoBoton, AltoBoton);
        rt.anchoredPosition = posicion;

        botonesPartida.Add(boton);
        return boton;
    }

    /// <summary>Altura del borde de abajo del titulo en el cartel sin crecer, que es
    /// donde empieza el hueco para la lista. Sin titulo, se deja la mitad de arriba
    /// del cartel libre.</summary>
    private float BordeInferiorDelTitulo()
    {
        if (tituloLabel != null && posOriginal.TryGetValue(tituloLabel.rectTransform, out var pos))
            return pos.y - tituloLabel.rectTransform.rect.height * tituloLabel.rectTransform.pivot.y;
        return cartelSizeOriginal.y * 0.25f;
    }

    /// <summary>Entra al juego con una partida que ya existia.</summary>
    private void ContinuarPartida(PartidaDto partida)
    {
        if (seleccionando) return;
        seleccionando = true;
        SetBotonesPartidaActivos(false);
        SetEstado("Cargando tu partida...", colorInfo);

        if (!ApiManager.Instance.RetomarPartida(partida))
        {
            seleccionando = false;
            SetBotonesPartidaActivos(true);
            // Viniendo del panel no hay botones de cartel que reactivar ni etiqueta de
            // estado a la vista: hay que devolverle el mando al panel o la lista queda
            // muerta, con todo apagado y sin explicacion.
            if (panelPartidas != null && panelPartidas.isActiveAndEnabled)
            {
                panelPartidas.Liberar("Esa partida no se pudo abrir. Prueba con otra.");
                return;
            }
            SetEstado("Esa partida no se pudo abrir. Prueba con otra.", colorError);
            return;
        }

        EntrarConPartida(partida, "retomada");
    }

    /// <summary>Empieza de cero. Lo llama el boton "Empezar una partida nueva" y tambien
    /// AceptarPerfil cuando el perfil todavia no tiene ninguna partida.</summary>
    private void CrearPartidaNueva()
    {
        if (seleccionando || esperandoPartidas) return;
        if (perfilElegido == null) { MostrarErrorPartida("Elige un perfil para continuar."); return; }

        ApiManager api = ApiManager.Instance;
        if (api == null || !api.IsLoggedIn || api.IsLocalMode)
        {
            MostrarErrorPartida("No hay una sesion conectada al servidor.");
            return;
        }

        seleccionando = true;
        int actual = ++solicitudPartida;
        string sesion = api.Token;
        SetBotonesPartidaActivos(false);
        SetEstado("Preparando una partida nueva...", colorInfo);

        api.CrearPartida(perfilElegido.id, 0f, null,
            onSuccess: partida =>
            {
                if (!RespuestaPartidaVigente(actual, api, sesion)) return;
                if (partida == null || partida.id <= 0)
                {
                    MostrarErrorPartida("No se recibio una partida valida del servidor.");
                    return;
                }
                EntrarConPartida(partida, "creada");
            },
            onError: error =>
            {
                if (!RespuestaPartidaVigente(actual, api, sesion)) return;
                MostrarErrorPartida(TraducirError(error));
            });
    }

    private void EntrarConPartida(PartidaDto partida, string verbo)
    {
        // Sin esto, entrar por aquí dejaba el progreso sin atar a la partida:
        // los desafios completados no se guardaban ni en PlayerPrefs y la mochila
        // del perfil anterior seguia puesta. Es la pantalla que usa la feria.
        MisionBackendSync.AtarProgresoALaPartida(partida.id);

        Debug.Log($"[Ingresar] Perfil '{perfilElegido.nombre}' (id {perfilElegido.id}), " +
                  $"partida {partida.id} {verbo}.");
        SetEstado("Listo. Entrando...", colorOk);
        Continuar();
    }

    private void SetBotonesPartidaActivos(bool valor)
    {
        foreach (var boton in botonesPartida)
            if (boton != null) boton.interactable = valor;
    }



    private void OnAuthError(string error)
    {
        SetOcupado(false);
        SetEstado(TraducirError(error), colorError);
    }

    private void Continuar()
    {
        if (Application.CanStreamedLevelBeLoaded(escenaDestino))
        {
            SceneManager.LoadScene(escenaDestino);
            return;
        }

        Debug.LogError($"[Ingresar] La escena '{escenaDestino}' no esta en Build Settings. " +
                       "El login fue correcto, pero no es seguro continuar sin una escena valida.");
        MostrarErrorPartida($"No se encontro la escena '{escenaDestino}'. Revisa Build Settings.");
    }

    /// <summary>
    /// Convierte la respuesta cruda del backend en algo legible. ApiManager entrega el
    /// body tal cual: {"error": "..."} en los 401 y {"campo": ["..."]} en los 400 de
    /// los serializers.
    /// </summary>
    private static string TraducirError(string error)
    {
        if (string.IsNullOrEmpty(error)) return "No se pudo completar la operacion.";

        // Desenvolver el body es igual en todas las pantallas y vive en TextoDeError;
        // las frases de abajo son las de ESTA pantalla y se quedan aqui.
        string detalle = TextoDeError.Detalle(error);

        // Comparacion en minusculas: el backend responde "Ya existe ..." con Y
        // mayuscula, asi que un Contains("ya existe") tal cual nunca coincidia.
        // Los textos dependen del locale (LANGUAGE_CODE = "es-cl"), por eso se
        // cubren las dos variantes; si no coincide ninguna cae el mensaje crudo,
        // que igual llega en castellano y es legible.
        string comparable = detalle.ToLowerInvariant();

        if (comparable.Contains("credenciales") || comparable.Contains("invalid credentials"))
            return "Usuario o contrasena incorrectos.";
        if (comparable.Contains("ya existe") || comparable.Contains("already exists"))
            return "Ese usuario o email ya esta registrado.";
        if (comparable.Contains("correo electr") || comparable.Contains("valid email"))
            return "El email no es valido.";

        return TextoDeError.Recortar(detalle);
    }

    // -- Cableado y layout -----------------------------------------------------
    private void AsegurarApiManager()
    {
        if (ApiManager.Instance != null) return;
        var go = new GameObject("ApiManager");
        go.AddComponent<ApiManager>();   // hace DontDestroyOnLoad solo
    }

    private void Cablear()
    {
        if (panelPerfiles == null) panelPerfiles = GetComponentInChildren<PanelPerfilesUI>(true);
        if (panelPartidas == null) panelPartidas = GetComponentInChildren<PanelPartidasUI>(true);
        if (panelCrearPerfil == null) panelCrearPerfil = GetComponentInChildren<PanelCrearPerfilUI>(true);
        if (botonAceptarPerfil == null && panelPerfiles != null)
            botonAceptarPerfil = panelPerfiles.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.name == "BotonAceptar");

        // Ambos paneles tienen un Titulo: buscar dentro de Login evita cambiar
        // accidentalmente el encabezado "Quien va a jugar".
        Transform raizLogin = transform.Find("Login");
        if (raizLogin == null) raizLogin = transform;
        var inputs = raizLogin.GetComponentsInChildren<TMP_InputField>(true);
        if (passwordInput == null)
            passwordInput = inputs.FirstOrDefault(f => f.name == "Password");
        if (usuarioInput == null)
            usuarioInput = inputs.FirstOrDefault(f => f.name == "Email" || f.name == "Usuario")
                        ?? inputs.FirstOrDefault(f => f != passwordInput);

        var botones = raizLogin.GetComponentsInChildren<Button>(true);
        if (ingresarButton == null) ingresarButton = botones.FirstOrDefault(b => b.name == "Ingresar");
        if (registerButton == null)
            registerButton = botones.FirstOrDefault(b => b.name == "Register" || b.name == "Registrarse");

        var textos = raizLogin.GetComponentsInChildren<TMP_Text>(true);
        if (tituloLabel == null) tituloLabel = textos.FirstOrDefault(t => t.name == "Titulo");
        if (cuentaLabel == null) cuentaLabel = textos.FirstOrDefault(t => t.name == "Cuenta");
        if (registerLabel == null && registerButton != null)
            registerLabel = registerButton.GetComponentInChildren<TMP_Text>(true);

        if (usuarioInput == null || passwordInput == null)
            Debug.LogError("[Ingresar] No se encontraron los campos de usuario/contrasena. " +
                           "Asignalos a mano en el inspector del componente 'iniciar'.");
    }

    private void GuardarLayout()
    {
        if (usuarioInput != null) cartel = usuarioInput.transform.parent as RectTransform;
        if (cartel != null)
        {
            cartelPosOriginal = cartel.anchoredPosition;
            cartelSizeOriginal = cartel.sizeDelta;
        }

        foreach (Component c in new Component[]
                 { tituloLabel, usuarioInput, passwordInput, ingresarButton, registerButton, cuentaLabel })
        {
            if (c != null && c.transform is RectTransform rt) posOriginal[rt] = rt.anchoredPosition;
        }
    }

    private void AplicarModo()
    {
        bool registro = modo == Modo.Registro;

        if (registro && emailInput == null) emailInput = CrearCampoEmail();
        if (emailInput != null) emailInput.gameObject.SetActive(registro);

        AjustarLayout(registro);

        if (tituloLabel != null) tituloLabel.text = registro ? "Crear cuenta" : "Iniciar Sesion";
        if (cuentaLabel != null) cuentaLabel.text = registro ? "¿Ya tienes cuenta?" : "¿No tienes cuenta?";
        if (registerLabel != null) registerLabel.text = registro ? "Volver" : "Registrarse";
    }

    /// <summary>
    /// En modo registro hace falta una fila mas (el email). El cartel crece hacia
    /// abajo: se agranda AltoFila y se corre medio AltoFila para que el borde de
    /// arriba no se mueva. Como el centro bajo esa mitad, lo de arriba se compensa
    /// subiendo y lo de abajo termina bajando la fila completa.
    /// </summary>
    private void AjustarLayout(bool registro)
    {
        if (cartel == null) return;

        float delta = registro ? AltoFila : 0f;
        cartel.sizeDelta = cartelSizeOriginal + new Vector2(0f, delta);
        cartel.anchoredPosition = cartelPosOriginal - new Vector2(0f, delta * 0.5f);

        Mover(tituloLabel, delta * 0.5f);
        Mover(usuarioInput, delta * 0.5f);
        Mover(passwordInput, delta * 0.5f);
        Mover(ingresarButton, -delta * 0.5f);
        Mover(registerButton, -delta * 0.5f);
        Mover(cuentaLabel, -delta * 0.5f);

        if (emailInput != null && passwordInput != null)
        {
            var pass = (RectTransform)passwordInput.transform;
            ((RectTransform)emailInput.transform).anchoredPosition =
                pass.anchoredPosition - new Vector2(0f, AltoFila);
        }

        ReubicarEstado();
    }

    /// <summary>Deja la etiqueta de estado pegada al borde de abajo del cartel. La usan
    /// los dos sitios que cambian el alto del cartel: el modo registro y la lista de
    /// partidas.</summary>
    private void ReubicarEstado()
    {
        if (estadoLabel == null || cartel == null) return;

        // El alto VISIBLE, no el del rect: el cartel puede estar escalado para
        // agrandar la pantalla entera, y la etiqueta cuelga de Login (sin escalar),
        // asi que con sizeDelta a secas se metia dentro del cartel en vez de debajo.
        float altoVisible = cartel.sizeDelta.y * cartel.localScale.y;

        ((RectTransform)estadoLabel.transform).anchoredPosition = new Vector2(
            cartel.anchoredPosition.x,
            cartel.anchoredPosition.y - altoVisible * 0.5f - 26f);
    }

    private void Mover(Component objetivo, float dy)
    {
        if (objetivo == null) return;
        if (objetivo.transform is RectTransform rt && posOriginal.TryGetValue(rt, out var origen))
            rt.anchoredPosition = origen + new Vector2(0f, dy);
    }

    /// <summary>
    /// Clona el campo de usuario para que el de email herede tipografia, colores y
    /// tamano exactos del que hizo el equipo, en vez de construir uno a mano.
    /// </summary>
    private TMP_InputField CrearCampoEmail()
    {
        if (usuarioInput == null) return null;

        var campo = Instantiate(usuarioInput, usuarioInput.transform.parent);
        campo.name = "EmailRegistro";
        campo.onSubmit = new TMP_InputField.SubmitEvent();
        campo.onValueChanged = new TMP_InputField.OnChangeEvent();
        campo.text = string.Empty;
        campo.contentType = TMP_InputField.ContentType.EmailAddress;
        campo.onSubmit.AddListener(_ => Enviar());
        AplicarPlaceholder(campo, "Email");
        return campo;
    }

    private TMP_Text CrearEtiquetaEstado()
    {
        var modelo = cuentaLabel != null ? cuentaLabel : tituloLabel;
        if (modelo == null || cartel == null) return null;

        // Cuelga del padre del cartel, justo debajo: adentro no hay hueco libre sin
        // reacomodar el diseno que ya existe.
        var etiqueta = Instantiate(modelo, cartel.parent);
        etiqueta.name = "EstadoIngreso";
        etiqueta.text = string.Empty;
        etiqueta.fontSize = Mathf.Max(16f, modelo.fontSize * 0.9f);
        etiqueta.alignment = TextAlignmentOptions.Center;
        etiqueta.textWrappingMode = TextWrappingModes.Normal;
        etiqueta.raycastTarget = false;

        var rt = (RectTransform)etiqueta.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(cartelSizeOriginal.x, 56f);
        return etiqueta;
    }

    private static void AplicarPlaceholder(TMP_InputField campo, string texto)
    {
        if (campo != null && campo.placeholder is TMP_Text t) t.text = texto;
    }

    private void SetEstado(string mensaje, Color color)
    {
        if (estadoLabel == null)
        {
            if (!string.IsNullOrEmpty(mensaje)) Debug.Log($"[Ingresar] {mensaje}");
            return;
        }
        estadoLabel.text = mensaje;
        estadoLabel.color = color;
    }

    private void SetOcupado(bool valor)
    {
        ocupado = valor;
        if (ingresarButton != null) ingresarButton.interactable = !valor;
        if (registerButton != null) registerButton.interactable = !valor;
        if (usuarioInput != null) usuarioInput.interactable = !valor;
        if (passwordInput != null) passwordInput.interactable = !valor;
        if (emailInput != null) emailInput.interactable = !valor;
    }
}
