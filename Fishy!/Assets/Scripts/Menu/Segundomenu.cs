using System.Collections.Generic;
using System.Linq;
using Fishy.Net;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controlador de la escena MenuDos: Jugar, Opciones y Salir.
///
/// El boton Jugar cambia segun lo que tenga guardado el perfil que se eligio en
/// Ingresar:
///  - Sin partidas: dice "Nueva partida" y al apretarlo crea una y entra al juego.
///  - Con partidas: dice "Continuar" y al apretarlo abre <see cref="PanelPartidasUI"/>
///    para elegir cual retomar (o empezar una nueva desde ahi).
///
/// La clase conserva el nombre y <see cref="Jugar"/> porque el boton Jugar de la
/// escena los tiene cableados en su onClick persistente (m_TargetAssemblyTypeName:
/// Segundomenu, m_MethodName: Jugar). Renombrarlos romperia ese wiring.
///
/// Si MenuDos se abre sin haber pasado por Ingresar (sin sesion o sin perfil) Jugar
/// conserva su comportamiento de siempre: carga la escena de juego. Sirve para probar
/// esta escena suelta desde el editor.
/// </summary>
public class Segundomenu : MonoBehaviour
{
    private enum Estado { SinSesion, Consultando, Error, SinPartidas, ConPartidas }

    [Header("Boton Jugar (si faltan se buscan por nombre)")]
    [SerializeField] private Button botonJugar;
    [Tooltip("Texto del boton, si el boton tiene uno. Si el boton es solo una imagen con la " +
             "palabra dibujada, deja esto vacio y usa los dos sprites de abajo.")]
    [SerializeField] private TMP_Text textoJugar;
    [SerializeField] private Image imagenJugar;
    [SerializeField] private Sprite spriteNuevaPartida;
    [SerializeField] private Sprite spriteContinuar;

    [Header("Paneles (si faltan se buscan por nombre)")]
    [Tooltip("Objeto que contiene el fondo, el logo y los botones Jugar / Opciones / Salir. " +
             "Sus hijos se ocultan mientras se ve el panel de partidas; el objeto queda " +
             "encendido para no perder el fondo.")]
    [SerializeField] private GameObject menuPrincipal;
    [SerializeField] private PanelPartidasUI panelPartidas;

    [Header("Destino")]
    [Tooltip("Escena a la que se entra con la partida elegida. Debe estar en Build Settings.")]
    public string escenaJuego = "MainScene";

    private Estado estado = Estado.Consultando;
    private List<PartidaDto> partidas;
    private UsuarioJugadorDto perfil;
    private int consulta;
    private bool entrando;

    // -- Ciclo de vida ---------------------------------------------------------
    private void Awake()
    {
        Cablear();

        if (panelPartidas != null)
        {
            panelPartidas.gameObject.SetActive(false);
            panelPartidas.Configurar(
                jugar: ContinuarPartida,
                crearNueva: CrearPartidaNueva,
                volver: CerrarPanelPartidas);
        }
    }

    private void Start() => Consultar();

    private void OnDestroy() => ++consulta;

    // -- Botones del menu ------------------------------------------------------

    /// <summary>Lo llama el boton Jugar (onClick persistente de la escena).</summary>
    public void Jugar()
    {
        if (entrando) return;

        switch (estado)
        {
            case Estado.SinSesion:
                CargarJuego();
                break;
            case Estado.Error:
                Consultar();   // la consulta fallo: el toque sirve de reintento
                break;
            case Estado.SinPartidas:
                CrearPartidaNueva();
                break;
            case Estado.ConPartidas:
                AbrirPanelPartidas();
                break;
            // Consultando: el boton esta apagado, no llega aqui.
        }
    }

    public void Salir()
    {
        Debug.Log("SALIR");
        Application.Quit();
    }

    // -- Consulta de partidas --------------------------------------------------

    /// <summary>Pregunta al backend si el perfil tiene partidas y ajusta el boton.</summary>
    private void Consultar()
    {
        ApiManager api = ApiManager.Instance;
        if (api == null || !api.IsLoggedIn || !api.HasJugador || api.IsLocalMode)
        {
            estado = Estado.SinSesion;
            AplicarBoton();
            return;
        }

        estado = Estado.Consultando;
        AplicarBoton();

        int actual = ++consulta;
        int jugador = api.JugadorId.Value;
        string sesion = api.Token;

        api.ObtenerPartidasJugador(jugador,
            onSuccess: lista =>
            {
                if (!Vigente(actual, api, sesion)) return;
                if (lista == null || lista.Exists(p => p == null || p.id <= 0))
                {
                    FallarConsulta("El servidor no devolvio una lista de partidas valida.");
                    return;
                }
                partidas = lista;
                estado = lista.Count == 0 ? Estado.SinPartidas : Estado.ConPartidas;
                AplicarBoton();
            },
            onError: error =>
            {
                if (Vigente(actual, api, sesion)) FallarConsulta(error);
            });

        // Solo hace falta el nombre para el titulo del panel ("Perfil de X"). Va aparte
        // para que el boton no espere a esta segunda peticion.
        api.ListarJugadores(
            onSuccess: lista =>
            {
                if (Vigente(actual, api, sesion))
                    perfil = lista?.FirstOrDefault(j => j != null && j.id == jugador);
            });
    }

    private void FallarConsulta(string error)
    {
        Debug.LogWarning($"[MenuDos] No se pudieron consultar las partidas: {error}");
        estado = Estado.Error;
        AplicarBoton();
    }

    private bool Vigente(int numero, ApiManager api, string sesion)
    {
        return this != null && isActiveAndEnabled && numero == consulta &&
            api != null && api == ApiManager.Instance && api.IsLoggedIn && api.Token == sesion;
    }

    // -- Boton Jugar: aspecto --------------------------------------------------

    private void AplicarBoton()
    {
        if (botonJugar != null)
            botonJugar.interactable = estado != Estado.Consultando && !entrando;

        string texto = "Jugar";
        Sprite sprite = null;
        if (estado == Estado.SinPartidas) { texto = "Nueva partida"; sprite = spriteNuevaPartida; }
        else if (estado == Estado.ConPartidas) { texto = "Continuar"; sprite = spriteContinuar; }

        if (textoJugar != null) textoJugar.text = texto;
        // Sin sprite para ese estado se deja el que ya tenia el boton.
        if (imagenJugar != null && sprite != null) imagenJugar.sprite = sprite;
    }

    // -- Panel de partidas -----------------------------------------------------

    private void AbrirPanelPartidas()
    {
        // Sin panel montado no hay lista donde elegir: se retoma la mas reciente, que es
        // lo que hacia el flujo antes de tener panel.
        if (panelPartidas == null)
        {
            Debug.LogWarning("[MenuDos] Falta PanelPartidas en la escena. Se retoma la mas reciente.");
            ContinuarPartida(partidas[0]);
            return;
        }

        MostrarMenuPrincipal(false);
        panelPartidas.Mostrar(perfil ?? new UsuarioJugadorDto { id = ApiManager.Instance.JugadorId.Value });
    }

    private void CerrarPanelPartidas()
    {
        if (panelPartidas != null) panelPartidas.gameObject.SetActive(false);
        MostrarMenuPrincipal(true);
    }

    /// <summary>
    /// Muestra u oculta los botones y el logo de MenuPrincipal SIN apagar el objeto: el
    /// mismo tiene la imagen de fondo de la pantalla, y apagarlo dejaria el panel de
    /// partidas flotando sobre el color vacio de la camara.
    /// </summary>
    private void MostrarMenuPrincipal(bool visible)
    {
        if (menuPrincipal == null) return;
        foreach (Transform hijo in menuPrincipal.transform)
            hijo.gameObject.SetActive(visible);
    }

    // -- Entrar al juego -------------------------------------------------------

    /// <summary>Entra al juego con una partida que ya existia.</summary>
    private void ContinuarPartida(PartidaDto partida)
    {
        if (entrando) return;

        if (!ApiManager.Instance.RetomarPartida(partida))
        {
            LiberarConMensaje("Esa partida no se pudo abrir. Prueba con otra.");
            return;
        }

        EntrarConPartida(partida, "retomada");
    }

    /// <summary>Crea una partida de cero y entra. La llama el boton "Nueva partida" y
    /// tambien el boton de nueva partida del panel.</summary>
    private void CrearPartidaNueva()
    {
        if (entrando) return;

        ApiManager api = ApiManager.Instance;
        if (api == null || !api.IsLoggedIn || !api.HasJugador || api.IsLocalMode)
        {
            LiberarConMensaje("No hay una sesión conectada al servidor.");
            return;
        }

        entrando = true;
        AplicarBoton();
        int actual = ++consulta;
        string sesion = api.Token;

        api.CrearPartida(api.JugadorId.Value, 0f, null,
            onSuccess: partida =>
            {
                if (!Vigente(actual, api, sesion)) return;
                if (partida == null || partida.id <= 0)
                {
                    entrando = false;
                    LiberarConMensaje("No se recibió una partida válida del servidor.");
                    return;
                }
                EntrarConPartida(partida, "creada");
            },
            onError: error =>
            {
                if (!Vigente(actual, api, sesion)) return;
                entrando = false;
                LiberarConMensaje(error);
            });
    }

    private void EntrarConPartida(PartidaDto partida, string verbo)
    {
        // Ata el progreso (desafios, mochila) a esta partida: sin esto lo del perfil
        // anterior seguiria puesto. Es lo mismo que hacia iniciar al elegir partida.
        MisionBackendSync.AtarProgresoALaPartida(partida.id);

        Debug.Log($"[MenuDos] Partida {partida.id} {verbo}.");
        entrando = true;
        AplicarBoton();
        CargarJuego();
    }

    private void CargarJuego()
    {
        if (Application.CanStreamedLevelBeLoaded(escenaJuego))
        {
            SceneManager.LoadScene(escenaJuego);
            return;
        }

        entrando = false;
        AplicarBoton();
        Debug.LogError($"[MenuDos] La escena '{escenaJuego}' no esta en Build Settings.");
        LiberarConMensaje($"No se encontró la escena '{escenaJuego}'.");
    }

    /// <summary>Devuelve el mando al jugador tras un fallo: el panel si esta a la vista,
    /// o el boton Jugar si no.</summary>
    private void LiberarConMensaje(string mensaje)
    {
        Debug.LogWarning($"[MenuDos] {mensaje}");
        if (panelPartidas != null && panelPartidas.isActiveAndEnabled)
            panelPartidas.Liberar(mensaje);
        AplicarBoton();
    }

    // -- Cableado --------------------------------------------------------------

    private void Cablear()
    {
        if (botonJugar == null)
            botonJugar = GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Jugar");
        if (botonJugar != null)
        {
            if (imagenJugar == null) imagenJugar = botonJugar.targetGraphic as Image;
            if (textoJugar == null) textoJugar = botonJugar.GetComponentInChildren<TMP_Text>(true);
        }

        if (panelPartidas == null) panelPartidas = GetComponentInChildren<PanelPartidasUI>(true);
        if (menuPrincipal == null)
        {
            Transform t = GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x.name == "MenuPrincipal");
            if (t != null) menuPrincipal = t.gameObject;
        }
    }
}
