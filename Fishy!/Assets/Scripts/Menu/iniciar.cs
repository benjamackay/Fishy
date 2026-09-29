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

    [Header("Panel de crear cuenta")]
    [Tooltip("Cartel propio con el formulario de cuenta nueva. Reemplaza al viejo modo " +
             "registro, que estiraba el cartel del login. Si falta, 'Registrarse' avisa " +
             "en consola y no hace nada.")]
    [SerializeField] private PanelCrearCuentaUI panelCrearCuenta;

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

    [Tooltip("Escena a la que vuelve el boton Volver: la pantalla anterior al login. " +
             "Debe estar en Build Settings.")]
    public string escenaAnterior = "MenuUno";

    [Header("Referencias (opcionales: si faltan se buscan por nombre en la escena)")]
    public TMP_InputField usuarioInput;
    public TMP_InputField passwordInput;
    public Button ingresarButton;
    public Button registerButton;
    [Tooltip("Boton 'Volver' del cartel del login. Se busca por nombre (Volver o Cancelar).")]
    public Button botonVolver;
    public TMP_Text tituloLabel;
    public TMP_Text cuentaLabel;

    [Tooltip("Etiqueta del mensaje de estado y de error. Si se asigna una de la escena, se usa " +
             "tal cual y no se le toca la posicion: se asume puesta a mano donde corresponde. " +
             "Si se deja vacia se crea una en runtime colgando bajo el cartel, como antes.")]
    public TMP_Text estadoEnEscena;

    [Header("Aspecto")]
    public Color colorError = new Color(0.94f, 0.38f, 0.38f);
    public Color colorInfo = new Color(0.85f, 0.85f, 0.85f);
    public Color colorOk = new Color(0.42f, 0.85f, 0.48f);

    // Las medidas de abajo estan en unidades locales del cartel, que es donde se
    // montan a mano el campo de email del registro y la lista de partidas.
    //
    // Antes el cartel se dibujaba a escala 1.5 y estas cifras eran 1.5 veces mas
    // chicas: lo que se escribia aqui se veia multiplicado. Al dejar el cartel a su
    // tamano real (escala 1, 1090x801) ese factor desaparecio y se horneo en los
    // numeros, que por eso ya no son redondos. Si alguna vuelve a verse a dos tercios
    // de lo que deberia, lo que paso es que a la pantalla le volvieron a poner escala.

    /// <summary>Tamano de cada boton de la lista de partidas. Caben dos lineas de texto:
    /// "Seguir donde quedaste" y la fecha en que se guardo.</summary>
    private const float AnchoBoton = 570f;
    private const float AltoBoton = 105f;
    private const float TamanoTextoBoton = 31.5f;

    /// <summary>Aire entre dos botones de la lista de partidas, para que no se toquen.</summary>
    private const float SeparacionBotones = 15f;

    /// <summary>Aire entre el titulo y el primer boton, y entre el ultimo boton y el
    /// borde de abajo del cartel (que tiene marco y sombra).</summary>
    private const float MargenBajoTitulo = 21f;
    private const float MargenInferior = 45f;

    private TMP_Text estadoLabel;        // la de la escena, o una creada en runtime
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

        // La de la escena manda: esta puesta dentro del cartel, en el sitio del diseno.
        // La de runtime es el respaldo para las pantallas que todavia no la tienen.
        estadoLabel = estadoEnEscena != null ? estadoEnEscena : CrearEtiquetaEstado();

        // Al boton "Ingresar" NO se le agrega listener: ya llama a MenuDos() por su
        // onClick persistente y se enviaria el formulario dos veces.
        if (registerButton != null) registerButton.onClick.AddListener(MostrarCrearCuenta);
        if (botonVolver != null) botonVolver.onClick.AddListener(Volver);
        if (usuarioInput != null) usuarioInput.onSubmit.AddListener(_ => Enviar());
        if (passwordInput != null) passwordInput.onSubmit.AddListener(_ => Enviar());

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

        if (panelCrearCuenta != null)
        {
            panelCrearCuenta.gameObject.SetActive(false);
            panelCrearCuenta.Configurar(
                crear: CuentaCreada,
                iniciarSesion: VolverAlLogin,
                volver: Volver);
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

    /// <summary>
    /// Vuelve a la pantalla anterior al login. Lo llama el boton "Volver" del cartel.
    ///
    /// No cierra la sesion a proposito: desde aqui todavia no hay ninguna abierta, y
    /// si la hubiera (se volvio atras despues de entrar) borrarla obligaria a escribir
    /// la contrasena otra vez para nada.
    /// </summary>
    public void Volver()
    {
        if (ocupado) return;

        if (string.IsNullOrEmpty(escenaAnterior))
        {
            Debug.LogWarning("[Ingresar] No hay escena anterior configurada: el boton " +
                             "Volver no hace nada. Rellena 'Escena Anterior' en el inspector.", this);
            return;
        }

        SceneManager.LoadScene(escenaAnterior);
    }

    public void Enviar()
    {
        if (ocupado) return;

        string usuario = usuarioInput != null ? usuarioInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;

        if (string.IsNullOrEmpty(usuario)) { SetEstado("Escribe tu usuario.", colorError); return; }
        if (string.IsNullOrEmpty(password)) { SetEstado("Escribe tu contraseña.", colorError); return; }

        // Sin backend NO se entra. En modo local ApiManager guardaria la cuenta en
        // PlayerPrefs y no llegaria nada a Supabase, pero la pantalla pareceria haber
        // funcionado. Preferimos el error explicito y reintentar la conexion.
        if (!backendListo || ApiManager.Instance.IsLocalMode)
        {
            SetEstado("Sin conexión con el servidor: no se puede guardar. Reintentando...", colorError);
            VerificarBackend();
            return;
        }

        SetOcupado(true);
        SetEstado("Ingresando...", colorInfo);
        ApiManager.Instance.Login(usuario, password, OnAuthOk, OnAuthError);
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
                         : "Sin conexión con el servidor. Verifica que el backend Django esté corriendo.",
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
            MostrarErrorPartida("Se necesita una sesión conectada para probar los perfiles.");
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
                        MostrarErrorPartida("No se recibió una lista de perfiles válida.");
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
            if (perfil == null) { MostrarErrorPartida("No se encontró el perfil de prueba."); return; }
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
                    MostrarErrorPartida("No se recibió un perfil de prueba válido.");
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
        if (panelCrearCuenta != null) panelCrearCuenta.gameObject.SetActive(false);
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

    /// <summary>Abre el formulario de cuenta nueva. Lo llama el boton "Registrarse"
    /// del cartel del login.</summary>
    public void MostrarCrearCuenta()
    {
        if (ocupado) return;

        if (panelCrearCuenta == null)
        {
            Debug.LogError("[Ingresar] Falta el panel de crear cuenta en el Inspector.", this);
            return;
        }

        // El cartel del login y este panel ocupan el mismo sitio de la pantalla.
        if (cartel != null) cartel.gameObject.SetActive(false);
        panelCrearCuenta.Mostrar();
    }

    /// <summary>
    /// La cuenta quedo creada y /auth/registro/ ya devolvio token, asi que se sigue
    /// derecho a los perfiles en vez de mandar al login a escribir lo mismo otra vez.
    /// </summary>
    private void CuentaCreada()
    {
        if (panelCrearCuenta != null) panelCrearCuenta.gameObject.SetActive(false);
        if (cartel != null) cartel.gameObject.SetActive(true);

        OnAuthOk();
    }

    /// <summary>Cierra el formulario de cuenta y devuelve al cartel del login. Lo
    /// llama el enlace "Iniciar sesión" del panel.</summary>
    private void VolverAlLogin()
    {
        if (panelCrearCuenta != null) panelCrearCuenta.gameObject.SetActive(false);
        if (cartel != null) cartel.gameObject.SetActive(true);
        SetEstado(string.Empty, colorInfo);
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
        // Elegir perfil ya no elige partida: se fija el perfil y se pasa a MenuDos, donde
        // el boton Jugar ofrece "Nueva partida" o "Continuar" segun lo que tenga guardado
        // (ver Segundomenu). El resto del flujo viejo (selector de partidas sobre el cartel
        // del login, CrearPartidaNueva, ContinuarPartida) queda sin llamadas; se puede
        // borrar en una limpieza aparte.
        ++solicitudPartida;
        esperandoPartidas = false;
        seleccionando = true;   // evita un segundo toque en "Aceptar" mientras carga la escena
        perfilElegido = elegido;
        ApiManager.Instance.SeleccionarJugador(elegido.id);
        SetOcupado(true);
        Continuar();
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
            const string mensaje = "No se encontró Cartel. Revisa Login > Cartel y el campo Usuario Input de iniciar.";
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
        foreach (Component c in new Component[] { usuarioInput, passwordInput,
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
        RestaurarCartelLogin();
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
    // La lista de verdad la monta PanelPartidas, que tiene su propio cartel. Lo de
    // aqui abajo es el RESPALDO: solo corre si ese panel no esta en la escena
    // (ver AbrirPartida), y entonces reaprovecha el cartel del login agrandandolo.
    //
    // Lo que si se sigue usando con el panel puesto son las pantallas de ERROR:
    // MostrarErrorPartida pasa por PrepararCartelPartidas, que enciende el cartel del
    // login y escribe encima. Eso es lo que mantiene vivos a Mover, posOriginal y
    // RestaurarCartelLogin; el dia que los errores tengan donde mostrarse sin pedirle
    // prestado el cartel al login, los tres se pueden borrar.
    //
    // Los botones los construye FishyUIKit.Boton, no se clonan del boton "Ingresar":
    // ese es una imagen con la palabra dibujada y el clon salia sin texto donde
    // escribir, asi que todas las filas decian "Ingresar".

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

        foreach (Component c in new Component[] { usuarioInput, passwordInput,
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
            ? $"Partidas de {perfilElegido.nombre} (las {cuantas} más recientes)."
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
            MostrarErrorPartida("No hay una sesión conectada al servidor.");
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
                    MostrarErrorPartida("No se recibió una partida válida del servidor.");
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
        MostrarErrorPartida($"No se encontró la escena '{escenaDestino}'. Revisa Build Settings.");
    }

    /// <summary>
    /// Convierte la respuesta cruda del backend en algo legible. ApiManager entrega el
    /// body tal cual: {"error": "..."} en los 401 y {"campo": ["..."]} en los 400 de
    /// los serializers.
    /// </summary>
    private static string TraducirError(string error)
    {
        if (string.IsNullOrEmpty(error)) return "No se pudo completar la operación.";

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
            return "Usuario o contraseña incorrectos.";
        if (comparable.Contains("ya existe") || comparable.Contains("already exists"))
            return "Ese usuario o email ya está registrado.";
        if (comparable.Contains("correo electr") || comparable.Contains("valid email"))
            return "El email no es válido.";

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
        if (panelCrearCuenta == null) panelCrearCuenta = GetComponentInChildren<PanelCrearCuentaUI>(true);
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
        // Se busca dentro de Login, asi que no choca con el BotonVolver de PanelPartidas
        // ni con los botones "Volver a perfiles" que la lista de partidas crea en runtime:
        // esos nacen despues de Awake, que es cuando corre este metodo.
        if (botonVolver == null)
            botonVolver = botones.FirstOrDefault(b => b.name == "Volver" || b.name == "Cancelar");

        var textos = raizLogin.GetComponentsInChildren<TMP_Text>(true);
        if (tituloLabel == null) tituloLabel = textos.FirstOrDefault(t => t.name == "Titulo");
        if (cuentaLabel == null) cuentaLabel = textos.FirstOrDefault(t => t.name == "Cuenta");

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

    /// <summary>
    /// Devuelve el cartel del login y sus piezas al sitio que tienen en la escena.
    ///
    /// Hace falta porque la lista de partidas se monta encima de este mismo cartel y
    /// lo agranda para que quepan las filas. Al volver de ahi hay que deshacerlo, o el
    /// login se queda con el cartel estirado y el titulo corrido.
    /// </summary>
    private void RestaurarCartelLogin()
    {
        if (cartel != null)
        {
            cartel.sizeDelta = cartelSizeOriginal;
            cartel.anchoredPosition = cartelPosOriginal;
        }

        foreach (Component c in new Component[] { tituloLabel, usuarioInput, passwordInput,
                                                  ingresarButton, registerButton, cuentaLabel })
            Mover(c, 0f);

        if (tituloLabel != null) tituloLabel.text = "Iniciar sesión";

        ReubicarEstado();
    }

    /// <summary>Deja la etiqueta de estado pegada al borde de abajo del cartel. La usan
    /// los dos sitios que cambian el alto del cartel: el modo registro y la lista de
    /// partidas.</summary>
    private void ReubicarEstado()
    {
        if (estadoLabel == null || cartel == null) return;

        // La etiqueta de la escena va DENTRO del cartel, asi que sus coordenadas son
        // relativas al cartel y no al padre. La cuenta de abajo la mandaria lejos.
        if (estadoEnEscena != null) return;

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
        if (botonVolver != null) botonVolver.interactable = !valor;
        if (usuarioInput != null) usuarioInput.interactable = !valor;
        if (passwordInput != null) passwordInput.interactable = !valor;
    }
}
