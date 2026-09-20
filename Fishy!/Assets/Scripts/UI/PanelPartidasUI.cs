using System;
using System.Collections.Generic;
using Fishy.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Perfil de X": la lista de partidas guardadas de un perfil de menor, para elegir
/// cual retomar o empezar una nueva.
///
/// Esta calcado de <see cref="PanelPerfilesUI"/>, incluidas sus defensas contra
/// respuestas que llegan tarde. No es copiar por copiar: los dos paneles hacen lo
/// mismo (pedir una lista al backend, montar tarjetas, dejar elegir una) contra un
/// servidor que tarda 600-800 ms por peticion, y en ese rato el usuario puede haber
/// cerrado el panel, vuelto a perfiles o cambiado de cuenta.
///
/// El panel NO entra al juego ni crea partidas: solo avisa que la tocaron. Quien
/// decide es <c>iniciar</c>, que ya sabe atar el progreso y cargar la escena.
/// </summary>
public class PanelPartidasUI : MonoBehaviour
{
    [Header("Encabezado")]
    [SerializeField] private TMP_Text titulo;

    [Header("Lista de partidas")]
    [SerializeField] private TarjetaPartidaUI tarjetaPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private ScrollRect scrollPartidas;
    [SerializeField] private TMP_Text mensajeSinPartidas;

    [Header("Botones")]
    [SerializeField] private Button botonVolver;
    [SerializeField] private Button botonNuevaPartida;
    [SerializeField] private Button botonJugar;

    [Header("Pruebas")]
    [Tooltip("ANDAMIO: mientras el backend no mande `zona_actual` en la lista de " +
             "partidas, le reparte zonas de mentira a las tarjetas que vengan sin " +
             "ella, para poder ver los colores. APAGALO cuando el backend lo mande: " +
             "si no, tapa el dato real de toda partida recien creada, que legitimamente " +
             "viene sin zona.")]
    [SerializeField] private bool zonasDePrueba = true;

    /// <summary>Se reparten en orden a las tarjetas sin zona. Son los ids reales del
    /// juego, asi que salen con su nombre y su color de verdad.</summary>
    private static readonly string[] ZonasDePrueba = { "zona_1", "zona_2", "zona_3" };

    private readonly List<TarjetaPartidaUI> tarjetas = new List<TarjetaPartidaUI>();
    private TarjetaPartidaUI seleccionada;
    private UsuarioJugadorDto perfil;
    private bool listaCargada;
    private bool ocupado;
    private int solicitud;

    private Action<PartidaDto> alJugar;
    private Action alCrearNueva;
    private Action alVolver;

    public PartidaDto PartidaSeleccionada => seleccionada != null
        ? seleccionada.Partida : null;

    private void Awake()
    {
        if (botonVolver != null)       botonVolver.onClick.AddListener(PulsarVolver);
        if (botonNuevaPartida != null) botonNuevaPartida.onClick.AddListener(PulsarNueva);
        if (botonJugar != null)        botonJugar.onClick.AddListener(PulsarJugar);
    }

    private void OnDestroy()
    {
        if (botonVolver != null)       botonVolver.onClick.RemoveListener(PulsarVolver);
        if (botonNuevaPartida != null) botonNuevaPartida.onClick.RemoveListener(PulsarNueva);
        if (botonJugar != null)        botonJugar.onClick.RemoveListener(PulsarJugar);
    }

    /// <summary>Le dice al panel a quien avisar. Lo llama <c>iniciar</c> en su Awake.</summary>
    public void Configurar(Action<PartidaDto> jugar, Action crearNueva, Action volver)
    {
        alJugar = jugar;
        alCrearNueva = crearNueva;
        alVolver = volver;
    }

    /// <summary>
    /// Lo llama <c>iniciar</c> DESPUES de que se eligio el perfil. No se consulta
    /// desde OnEnable porque el panel puede quedar visible en el editor.
    /// </summary>
    public void Mostrar(UsuarioJugadorDto perfilElegido)
    {
        perfil = perfilElegido;
        gameObject.SetActive(true);
        Recargar();
    }

    public void Recargar()
    {
        if (!isActiveAndEnabled)
            return;

        if (tarjetaPrefab == null || content == null || scrollPartidas == null ||
            mensajeSinPartidas == null || botonNuevaPartida == null || botonJugar == null)
        {
            Debug.LogError("[PanelPartidasUI] Faltan referencias en el Inspector.", this);
            return;
        }

        int actual = ++solicitud;
        LimpiarTarjetas();
        listaCargada = false;
        ocupado = false;
        ActualizarSeleccion();
        ActualizarTitulo();
        MostrarMensaje("Cargando partidas...");

        if (perfil == null || perfil.id <= 0)
        {
            MostrarMensaje("Elige un perfil para ver sus partidas.");
            return;
        }

        ApiManager api = ApiManager.Instance;
        if (api == null || !api.IsLoggedIn)
        {
            MostrarMensaje("Inicia sesión para ver tus partidas.");
            return;
        }

        // Igual que el panel de perfiles: esta pantalla trabaja contra el backend,
        // no contra los datos de prueba locales.
        if (api.IsLocalMode)
        {
            MostrarMensaje("Sin conexión con el servidor. No se pudieron cargar las partidas.");
            return;
        }

        int? adulto = api.AdultoId;
        string sesion = api.Token;
        int dePerfil = perfil.id;

        api.ObtenerPartidasJugador(dePerfil,
            onSuccess: partidas =>
            {
                if (!RespuestaVigente(actual, api, adulto, sesion, dePerfil)) return;

                // Una respuesta nula es un error, no un perfil sin partidas.
                if (partidas == null || partidas.Exists(p => p == null || p.id <= 0))
                {
                    MostrarMensaje("No se pudieron leer las partidas del servidor.");
                    return;
                }

                // Se muestran TODAS. El cartel viejo se quedaba en las 3 mas recientes
                // porque los botones se apilaban a mano y no cabian mas; aqui hay un
                // scroll y esconder partidas guardadas no le hace ningun favor al nino.
                for (int i = 0; i < partidas.Count; i++)
                {
                    // El andamio solo rellena el hueco: si el backend ya mando una
                    // zona, esa manda siempre.
                    if (zonasDePrueba && string.IsNullOrWhiteSpace(partidas[i].zona_actual))
                        partidas[i].zona_actual = ZonasDePrueba[i % ZonasDePrueba.Length];

                    TarjetaPartidaUI tarjeta = Instantiate(tarjetaPrefab, content);
                    tarjeta.Configurar(partidas[i], i == 0, Seleccionar);
                    tarjeta.gameObject.SetActive(true);
                    tarjetas.Add(tarjeta);
                }

                listaCargada = true;
                ActualizarSeleccion();

                bool hayPartidas = tarjetas.Count > 0;
                scrollPartidas.gameObject.SetActive(hayPartidas);
                mensajeSinPartidas.gameObject.SetActive(!hayPartidas);
                mensajeSinPartidas.text = "No hay partidas guardadas";

                if (hayPartidas)
                {
                    Canvas.ForceUpdateCanvases();
                    scrollPartidas.verticalNormalizedPosition = 1f;
                }
            },
            onError: _ =>
            {
                if (!RespuestaVigente(actual, api, adulto, sesion, dePerfil)) return;
                MostrarMensaje("No se pudieron cargar las partidas. Vuelve a intentarlo.");
            });
    }

    public void Seleccionar(TarjetaPartidaUI tarjeta)
    {
        if (ocupado || !listaCargada || tarjeta == null || !tarjetas.Contains(tarjeta)) return;
        seleccionada = seleccionada == tarjeta ? null : tarjeta;
        ActualizarSeleccion();
    }

    private void ActualizarSeleccion()
    {
        foreach (TarjetaPartidaUI tarjeta in tarjetas)
            tarjeta.MostrarSeleccion(tarjeta == seleccionada);

        bool haySeleccion = seleccionada != null;

        // Comparten posicion en la escena: se ve uno o el otro, nunca los dos.
        botonNuevaPartida.gameObject.SetActive(!haySeleccion);
        botonNuevaPartida.interactable = listaCargada && !ocupado;
        botonJugar.gameObject.SetActive(haySeleccion);
        botonJugar.interactable = listaCargada && haySeleccion && !ocupado;

        // Volver sigue disponible aunque la lista no haya cargado: si el servidor
        // falla, es la unica salida que le queda al nino sin cerrar el juego.
        if (botonVolver != null) botonVolver.interactable = !ocupado;
    }

    private void ActualizarTitulo()
    {
        if (titulo == null) return;

        titulo.text = perfil != null && !string.IsNullOrWhiteSpace(perfil.nombre)
            ? $"Perfil de {perfil.nombre}"
            : "Perfil de JUGADOR";
    }

    private void PulsarJugar()
    {
        PartidaDto partida = PartidaSeleccionada;
        if (ocupado || !listaCargada || partida == null) return;

        ocupado = true;
        ActualizarSeleccion();
        alJugar?.Invoke(partida);
    }

    private void PulsarNueva()
    {
        if (ocupado || !listaCargada) return;

        ocupado = true;
        ActualizarSeleccion();
        alCrearNueva?.Invoke();
    }

    private void PulsarVolver()
    {
        if (ocupado) return;
        alVolver?.Invoke();
    }

    /// <summary>
    /// Vuelve a dejar el panel usable despues de que <c>iniciar</c> no pudiera entrar
    /// a la partida elegida. Sin esto el panel se queda con todo apagado esperando un
    /// cambio de escena que ya no va a pasar.
    /// </summary>
    public void Liberar(string mensaje = null)
    {
        ocupado = false;
        ActualizarSeleccion();

        // Se escribe donde el cartel de "sin partidas", que es el unico sitio de texto
        // libre que tiene el panel. Solo se ve si ademas no hay tarjetas montadas.
        if (!string.IsNullOrEmpty(mensaje) && mensajeSinPartidas != null &&
            tarjetas.Count == 0)
            MostrarMensaje(mensaje);
        else if (!string.IsNullOrEmpty(mensaje))
            Debug.LogWarning($"[PanelPartidasUI] {mensaje}", this);
    }

    private void MostrarMensaje(string texto)
    {
        scrollPartidas.gameObject.SetActive(false);
        mensajeSinPartidas.text = texto;
        mensajeSinPartidas.gameObject.SetActive(true);
    }

    private void LimpiarTarjetas()
    {
        seleccionada = null;
        foreach (TarjetaPartidaUI tarjeta in tarjetas)
        {
            if (tarjeta == null) continue;
            tarjeta.gameObject.SetActive(false);
            Destroy(tarjeta.gameObject);
        }
        tarjetas.Clear();
    }

    /// <summary>
    /// Si esta respuesta todavia le sirve a alguien. Ademas de lo que mira el panel
    /// de perfiles, comprueba que sigamos en el MISMO perfil: entre la peticion y la
    /// respuesta se puede haber vuelto atras y entrado a otro, y las partidas de un
    /// hermano no van en la pantalla del otro.
    /// </summary>
    private bool RespuestaVigente(int numero, ApiManager api, int? adulto, string sesion,
        int dePerfil)
    {
        return this != null && isActiveAndEnabled && numero == solicitud &&
            api != null && api == ApiManager.Instance && api.IsLoggedIn &&
            api.AdultoId == adulto && api.Token == sesion &&
            perfil != null && perfil.id == dePerfil;
    }

    private void OnDisable()
    {
        // Ignora respuestas que lleguen despues de cerrar el panel.
        ++solicitud;
        listaCargada = false;
        ocupado = false;
        seleccionada = null;
    }
}
