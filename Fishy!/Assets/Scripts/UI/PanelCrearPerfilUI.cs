using System;
using Fishy.Net;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Nuevo perfil": el formulario para dar de alta un perfil de menor dentro de la
/// cuenta del adulto. Reusa el cartel del login (Otto con el letrero), asi que vive
/// en un duplicado de ese objeto y no en un panel con marco propio.
///
/// Un perfil solo tiene dos campos: el nombre, obligatorio y unico dentro de la
/// cuenta, y la edad, opcional. El panel no elige por el usuario: si no tocan el
/// desplegable, el perfil se crea sin edad.
/// </summary>
public class PanelCrearPerfilUI : MonoBehaviour
{
    [Header("Campos")]
    [SerializeField] private TMP_InputField campoNombre;
    [SerializeField] private TMP_Dropdown campoEdad;
    [SerializeField] private TMP_Text mensajeError;

    [Header("Botones")]
    [SerializeField] private Button botonCancelar;
    [SerializeField] private Button botonCrear;

    /// <summary>
    /// Edad que representa cada opcion del desplegable, en el mismo orden en que
    /// estan puestas en el Inspector. El primer hueco es null a proposito: es la
    /// opcion "No opina", y es la que queda elegida si nadie toca el campo.
    ///
    /// Si algun dia se agregan edades, hay que tocar las dos: la lista del Inspector
    /// y esta. Por eso, si no coinciden en largo, se avisa en consola en vez de
    /// guardar una edad equivocada en silencio.
    /// </summary>
    private static readonly int?[] EdadPorOpcion = { null, 9, 10, 11, 12, 13 };

    private Action<UsuarioJugadorDto> alCrear;
    private Action alCancelar;
    private bool ocupado;
    private int solicitud;

    private void Awake()
    {
        if (botonCancelar != null) botonCancelar.onClick.AddListener(PulsarCancelar);
        if (botonCrear != null)    botonCrear.onClick.AddListener(PulsarCrear);

        // Enter en el nombre tambien envia: es un formulario de dos campos y obligar
        // a apuntar al boton es innecesario.
        if (campoNombre != null) campoNombre.onSubmit.AddListener(_ => PulsarCrear());
    }

    private void OnDestroy()
    {
        if (botonCancelar != null) botonCancelar.onClick.RemoveListener(PulsarCancelar);
        if (botonCrear != null)    botonCrear.onClick.RemoveListener(PulsarCrear);
    }

    /// <summary>Le dice al panel a quien avisar. Lo llama <c>iniciar</c> en su Awake.</summary>
    public void Configurar(Action<UsuarioJugadorDto> crear, Action cancelar)
    {
        alCrear = crear;
        alCancelar = cancelar;
    }

    /// <summary>Abre el formulario en blanco. Nunca reaprovecha lo que quedo escrito
    /// la vez anterior: el nombre no se puede repetir dentro de la cuenta, asi que
    /// dejarlo puesto solo serviria para chocar con el perfil recien creado.</summary>
    public void Mostrar()
    {
        ++solicitud;
        ocupado = false;

        if (campoNombre != null) campoNombre.text = string.Empty;
        if (campoEdad != null)   campoEdad.value = 0;
        LimpiarError();

        gameObject.SetActive(true);
        ActualizarBotones();

        if (campoNombre != null)
            campoNombre.ActivateInputField();
    }

    private void PulsarCrear()
    {
        if (ocupado || !isActiveAndEnabled) return;

        string nombre = campoNombre != null ? campoNombre.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(nombre))
        {
            MostrarError("Escribe un nombre para el perfil.");
            if (campoNombre != null) campoNombre.ActivateInputField();
            return;
        }

        // El backend corta a 150 (models.py: nombre = CharField(max_length=150)).
        // Avisar aqui es mas claro que dejar que vuelva un 400 desde el servidor
        // despues de 700 ms de espera.
        if (nombre.Length > 150)
        {
            MostrarError("Ese nombre es demasiado largo.");
            return;
        }

        ApiManager api = ApiManager.Instance;
        if (api == null || !api.IsLoggedIn || api.IsLocalMode)
        {
            MostrarError("Sin conexión con el servidor. No se pudo crear el perfil.");
            return;
        }

        ocupado = true;
        LimpiarError();
        ActualizarBotones();

        int actual = ++solicitud;
        string sesion = api.Token;

        api.CrearJugador(nombre, EdadElegida(),
            onSuccess: perfil =>
            {
                if (!RespuestaVigente(actual, api, sesion)) return;

                if (perfil == null || perfil.id <= 0)
                {
                    ocupado = false;
                    ActualizarBotones();
                    MostrarError("El servidor no devolvió un perfil válido.");
                    return;
                }

                // Quien cierra el panel es iniciar, que es el que sabe a donde volver.
                alCrear?.Invoke(perfil);
            },
            onError: error =>
            {
                if (!RespuestaVigente(actual, api, sesion)) return;
                ocupado = false;
                ActualizarBotones();
                MostrarError(Traducir(error));
            });
    }

    private void PulsarCancelar()
    {
        if (ocupado) return;
        alCancelar?.Invoke();
    }

    /// <summary>La edad elegida, o null si marcaron "No opina".</summary>
    private int? EdadElegida()
    {
        if (campoEdad == null) return null;

        int indice = campoEdad.value;
        if (indice < 0 || indice >= EdadPorOpcion.Length)
        {
            Debug.LogWarning($"[PanelCrearPerfilUI] La opción {indice} del desplegable no " +
                             "tiene edad asignada. Revisa que las opciones del Inspector " +
                             "coincidan con EdadPorOpcion. El perfil se crea sin edad.", this);
            return null;
        }
        return EdadPorOpcion[indice];
    }

    /// <summary>
    /// El "ya existe" del backend aqui significa algo muy concreto, y hay que decirlo
    /// con esas palabras: la restriccion es (adulto, nombre), no el nombre a secas, o
    /// sea que el choque es con OTRO hijo de la misma cuenta.
    /// </summary>
    private static string Traducir(string error)
    {
        if (string.IsNullOrEmpty(error)) return "No se pudo crear el perfil.";

        string detalle = TextoDeError.Detalle(error);
        string comparable = detalle.ToLowerInvariant();

        if (comparable.Contains("ya existe") || comparable.Contains("already exists") ||
            comparable.Contains("unico") || comparable.Contains("único"))
            return "Ya tienes un perfil con ese nombre.";

        return TextoDeError.Recortar(detalle);
    }

    private void MostrarError(string texto)
    {
        if (mensajeError == null)
        {
            Debug.LogWarning($"[PanelCrearPerfilUI] {texto}", this);
            return;
        }
        mensajeError.text = texto;
        mensajeError.gameObject.SetActive(true);
    }

    private void LimpiarError()
    {
        if (mensajeError == null) return;
        mensajeError.text = string.Empty;
    }

    private void ActualizarBotones()
    {
        if (botonCrear != null)    botonCrear.interactable = !ocupado;
        if (botonCancelar != null) botonCancelar.interactable = !ocupado;
        if (campoNombre != null)   campoNombre.interactable = !ocupado;
        if (campoEdad != null)     campoEdad.interactable = !ocupado;
    }

    private bool RespuestaVigente(int numero, ApiManager api, string sesion)
    {
        return this != null && isActiveAndEnabled && numero == solicitud &&
            api != null && api == ApiManager.Instance && api.IsLoggedIn && api.Token == sesion;
    }

    private void OnDisable()
    {
        // Ignora respuestas que lleguen despues de cerrar el panel.
        ++solicitud;
        ocupado = false;
    }
}
