using System.Collections.Generic;
using Fishy.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelPerfilesUI : MonoBehaviour
{
    [Header("Lista de perfiles")]
    [SerializeField] private TarjetaPerfilUI tarjetaPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private ScrollRect scrollPerfiles;
    [SerializeField] private TMP_Text mensajeSinPerfiles;

    [Header("Botones")]
    [SerializeField] private Button botonCrearPerfil;
    [SerializeField] private Button botonAceptar;

    private readonly List<TarjetaPerfilUI> tarjetas = new List<TarjetaPerfilUI>();
    private TarjetaPerfilUI seleccionada;
    private bool listaCargada;
    private int solicitud;
    private System.Action alCrearPerfil;

    private void Awake()
    {
        if (botonCrearPerfil != null) botonCrearPerfil.onClick.AddListener(PulsarCrearPerfil);
    }

    private void OnDestroy()
    {
        if (botonCrearPerfil != null) botonCrearPerfil.onClick.RemoveListener(PulsarCrearPerfil);
    }

    /// <summary>Le dice al panel a quien avisar cuando toquen "Crear perfil". Lo llama
    /// <c>iniciar</c> en su Awake: abrir otro panel no es cosa de este.</summary>
    public void Configurar(System.Action crearPerfil)
    {
        alCrearPerfil = crearPerfil;
    }

    private void PulsarCrearPerfil()
    {
        if (!listaCargada) return;
        alCrearPerfil?.Invoke();
    }

    public UsuarioJugadorDto PerfilSeleccionado => seleccionada != null
        ? seleccionada.Perfil : null;

    // El controlador de ingreso llama esto DESPUES de autenticar al adulto.
    // No consultamos desde OnEnable: el panel puede estar visible en el editor.
    public void Mostrar()
    {
        gameObject.SetActive(true);
        Recargar();
    }

    public void Recargar()
    {
        if (!isActiveAndEnabled)
            return;

        if (tarjetaPrefab == null || content == null || scrollPerfiles == null ||
            mensajeSinPerfiles == null || botonCrearPerfil == null || botonAceptar == null)
        {
            Debug.LogError("[PanelPerfilesUI] Faltan referencias en el Inspector.", this);
            return;
        }

        int actual = ++solicitud;
        LimpiarTarjetas();
        listaCargada = false;
        ActualizarSeleccion();
        MostrarMensaje("Cargando perfiles...");

        ApiManager api = ApiManager.Instance;
        if (api == null || !api.IsLoggedIn)
        {
            MostrarMensaje("Inicia sesión para ver tus perfiles.");
            return;
        }

        // Esta pantalla utiliza los perfiles del backend, no los de prueba locales.
        if (api.IsLocalMode)
        {
            MostrarMensaje("Sin conexión con el servidor. No se pudieron cargar los perfiles.");
            return;
        }

        int? adulto = api.AdultoId;
        string sesion = api.Token;

        api.ListarJugadores(
            onSuccess: perfiles =>
            {
                if (!RespuestaVigente(actual, api, adulto, sesion)) return;

                // Una respuesta nula es un error, no una cuenta sin perfiles.
                if (perfiles == null || perfiles.Exists(p => p == null || p.id <= 0))
                {
                    MostrarMensaje("No se pudieron leer los perfiles del servidor.");
                    return;
                }

                foreach (UsuarioJugadorDto perfil in perfiles)
                {
                    TarjetaPerfilUI tarjeta = Instantiate(tarjetaPrefab, content);
                    tarjeta.Configurar(perfil, Seleccionar);
                    tarjeta.gameObject.SetActive(true);
                    tarjetas.Add(tarjeta);
                }

                listaCargada = true;
                ActualizarSeleccion();

                bool hayPerfiles = tarjetas.Count > 0;
                scrollPerfiles.gameObject.SetActive(hayPerfiles);
                mensajeSinPerfiles.gameObject.SetActive(!hayPerfiles);
                mensajeSinPerfiles.text = "No hay ningún perfil para jugar\n(o_o)?";

                if (hayPerfiles)
                {
                    Canvas.ForceUpdateCanvases();
                    scrollPerfiles.horizontalNormalizedPosition = 0f;
                }
            },
            onError: _ =>
            {
                if (!RespuestaVigente(actual, api, adulto, sesion)) return;
                MostrarMensaje("No se pudieron cargar los perfiles. Vuelve a intentarlo.");
            });
    }

    public void Seleccionar(TarjetaPerfilUI tarjeta)
    {
        if (!listaCargada || tarjeta == null || !tarjetas.Contains(tarjeta)) return;
        seleccionada = seleccionada == tarjeta ? null : tarjeta;
        ActualizarSeleccion();
    }

    private void ActualizarSeleccion()
    {
        foreach (TarjetaPerfilUI tarjeta in tarjetas)
            tarjeta.MostrarSeleccion(tarjeta == seleccionada);

        bool haySeleccion = seleccionada != null;
        botonCrearPerfil.gameObject.SetActive(!haySeleccion);
        botonCrearPerfil.interactable = listaCargada;
        botonAceptar.gameObject.SetActive(haySeleccion);
        botonAceptar.interactable = listaCargada && haySeleccion;
    }

    private void MostrarMensaje(string texto)
    {
        scrollPerfiles.gameObject.SetActive(false);
        mensajeSinPerfiles.text = texto;
        mensajeSinPerfiles.gameObject.SetActive(true);
    }

    private void LimpiarTarjetas()
    {
        seleccionada = null;
        foreach (TarjetaPerfilUI tarjeta in tarjetas)
        {
            if (tarjeta == null) continue;
            tarjeta.gameObject.SetActive(false);
            Destroy(tarjeta.gameObject);
        }
        tarjetas.Clear();
    }

    private bool RespuestaVigente(int numero, ApiManager api, int? adulto, string sesion)
    {
        return this != null && isActiveAndEnabled && numero == solicitud &&
            api != null && api == ApiManager.Instance && api.IsLoggedIn &&
            api.AdultoId == adulto && api.Token == sesion;
    }

    private void OnDisable()
    {
        // Ignora respuestas que lleguen despues de cerrar el panel.
        ++solicitud;
        listaCargada = false;
        seleccionada = null;
    }
}