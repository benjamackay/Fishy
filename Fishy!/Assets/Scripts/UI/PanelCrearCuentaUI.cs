using System;
using Fishy.Net;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Crear cuenta": el alta del adulto responsable.
///
/// Hasta ahora esto no era una pantalla sino un MODO del cartel del login: el mismo
/// cartel se estiraba 51 unidades, se clonaba un tercer campo en runtime y se
/// reescribian tres textos. Cada elemento nuevo del diseno habia que acordarse de
/// registrarlo a mano en la lista que se movia, y el dibujo de Otto se deformaba al
/// crecer el rect. Ahora es un panel propio con su cartel a medida.
///
/// El backend pide tres datos y los tres son obligatorios: nombre (unico, y es el
/// que despues sirve para entrar), email (unico) y password de al menos 4 caracteres
/// (Backend/backend/api/serializers.py:14). apellido y edad existen en el endpoint
/// pero son opcionales, y esta pantalla no los pide.
///
/// /auth/registro/ devuelve token, asi que al crear la cuenta la sesion ya queda
/// abierta: no se vuelve al login a escribir lo mismo otra vez.
/// </summary>
public class PanelCrearCuentaUI : MonoBehaviour
{
    [Header("Campos")]
    [SerializeField] private TMP_InputField campoUsuario;
    [SerializeField] private TMP_InputField campoEmail;
    [SerializeField] private TMP_InputField campoPassword;
    [SerializeField] private TMP_Text mensajeError;

    [Header("Botones")]
    [SerializeField] private Button botonCrear;
    [Tooltip("Boton 'Volver': sale de la pantalla de ingreso.")]
    [SerializeField] private Button botonVolver;
    [Tooltip("Enlace 'Iniciar sesión': devuelve al cartel del login.")]
    [SerializeField] private Button botonIniciarSesion;

    /// <summary>El backend corta el nombre en 150 (models.py: CharField(max_length=150)).
    /// Avisar aqui es mas claro que esperar 700 ms a que vuelva un 400.</summary>
    private const int LargoMaximoNombre = 150;

    /// <summary>Minimo que exige el serializer del registro (min_length=4).</summary>
    private const int LargoMinimoPassword = 4;

    private Action alCrear;
    private Action alIniciarSesion;
    private Action alVolver;
    private bool ocupado;
    private int solicitud;

    private void Awake()
    {
        if (botonCrear != null)         botonCrear.onClick.AddListener(PulsarCrear);
        if (botonVolver != null)        botonVolver.onClick.AddListener(PulsarVolver);
        if (botonIniciarSesion != null) botonIniciarSesion.onClick.AddListener(PulsarIniciarSesion);

        // Enter en cualquiera de los tres campos envia el formulario: con tres filas,
        // obligar a bajar al boton cada vez es innecesario.
        if (campoUsuario != null)  campoUsuario.onSubmit.AddListener(_ => PulsarCrear());
        if (campoEmail != null)    campoEmail.onSubmit.AddListener(_ => PulsarCrear());
        if (campoPassword != null) campoPassword.onSubmit.AddListener(_ => PulsarCrear());
    }

    private void OnDestroy()
    {
        if (botonCrear != null)         botonCrear.onClick.RemoveListener(PulsarCrear);
        if (botonVolver != null)        botonVolver.onClick.RemoveListener(PulsarVolver);
        if (botonIniciarSesion != null) botonIniciarSesion.onClick.RemoveListener(PulsarIniciarSesion);
    }

    /// <summary>Le dice al panel a quien avisar. Lo llama <c>iniciar</c> en su Awake.</summary>
    public void Configurar(Action crear, Action iniciarSesion, Action volver)
    {
        alCrear = crear;
        alIniciarSesion = iniciarSesion;
        alVolver = volver;
    }

    /// <summary>Abre el formulario en blanco. No reaprovecha lo escrito la vez
    /// anterior: nombre y email son unicos en el backend, asi que dejarlos puestos
    /// solo serviria para chocar con la cuenta que se acaba de crear.</summary>
    public void Mostrar()
    {
        ++solicitud;
        ocupado = false;

        if (campoUsuario != null)  campoUsuario.text = string.Empty;
        if (campoEmail != null)    campoEmail.text = string.Empty;
        if (campoPassword != null) campoPassword.text = string.Empty;
        LimpiarError();

        gameObject.SetActive(true);
        ActualizarControles();

        if (campoUsuario != null) campoUsuario.ActivateInputField();
    }

    private void PulsarCrear()
    {
        if (ocupado || !isActiveAndEnabled) return;

        string nombre   = campoUsuario != null ? campoUsuario.text.Trim() : string.Empty;
        string email    = campoEmail != null ? campoEmail.text.Trim() : string.Empty;
        string password = campoPassword != null ? campoPassword.text : string.Empty;

        if (string.IsNullOrEmpty(nombre))
        {
            MostrarError("Escribe un nombre de usuario.");
            if (campoUsuario != null) campoUsuario.ActivateInputField();
            return;
        }

        if (nombre.Length > LargoMaximoNombre)
        {
            MostrarError("Ese nombre de usuario es demasiado largo.");
            return;
        }

        // El email es obligatorio en el registro aunque despues no sirva para entrar:
        // se entra con el nombre. El "@" es lo minimo que se puede comprobar aqui sin
        // inventar reglas; de la validacion de verdad se encarga el backend.
        if (string.IsNullOrEmpty(email) || !email.Contains("@"))
        {
            MostrarError("Escribe un correo electrónico válido.");
            if (campoEmail != null) campoEmail.ActivateInputField();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            MostrarError("Escribe una contraseña.");
            if (campoPassword != null) campoPassword.ActivateInputField();
            return;
        }

        if (password.Length < LargoMinimoPassword)
        {
            MostrarError($"La contraseña necesita al menos {LargoMinimoPassword} caracteres.");
            if (campoPassword != null) campoPassword.ActivateInputField();
            return;
        }

        ApiManager api = ApiManager.Instance;
        if (api == null || api.IsLocalMode)
        {
            // En modo local la cuenta quedaria en PlayerPrefs y no llegaria nada a
            // Supabase, pero la pantalla pareceria haber funcionado. Mejor el error.
            MostrarError("Sin conexión con el servidor. No se pudo crear la cuenta.");
            return;
        }

        ocupado = true;
        LimpiarError();
        ActualizarControles();

        int actual = ++solicitud;

        api.Registro(nombre, email, password,
            onSuccess: () =>
            {
                if (!RespuestaVigente(actual)) return;

                // Quien cierra el panel es iniciar, que es el que sabe a donde seguir.
                alCrear?.Invoke();
            },
            onError: error =>
            {
                if (!RespuestaVigente(actual)) return;
                ocupado = false;
                ActualizarControles();
                MostrarError(Traducir(error));
            });
    }

    private void PulsarVolver()
    {
        if (ocupado) return;
        alVolver?.Invoke();
    }

    private void PulsarIniciarSesion()
    {
        if (ocupado) return;
        alIniciarSesion?.Invoke();
    }

    /// <summary>
    /// El "ya existe" del backend aqui puede venir por el nombre o por el email, que
    /// son unicos los dos, y la respuesta no siempre dice cual. Por eso el mensaje los
    /// nombra a ambos en vez de adivinar y mandar a corregir el campo equivocado.
    /// </summary>
    private static string Traducir(string error)
    {
        if (string.IsNullOrEmpty(error)) return "No se pudo crear la cuenta.";

        string detalle = TextoDeError.Detalle(error);
        string comparable = detalle.ToLowerInvariant();

        if (comparable.Contains("ya existe") || comparable.Contains("already exists") ||
            comparable.Contains("unico") || comparable.Contains("único"))
            return "Ese usuario o ese correo ya están registrados.";

        if (comparable.Contains("correo electr") || comparable.Contains("valid email"))
            return "El correo electrónico no es válido.";

        if (comparable.Contains("caracteres") || comparable.Contains("characters"))
            return $"La contraseña necesita al menos {LargoMinimoPassword} caracteres.";

        return TextoDeError.Recortar(detalle);
    }

    private void MostrarError(string texto)
    {
        if (mensajeError == null)
        {
            Debug.LogWarning($"[PanelCrearCuentaUI] {texto}", this);
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

    private void ActualizarControles()
    {
        if (botonCrear != null)         botonCrear.interactable = !ocupado;
        if (botonVolver != null)        botonVolver.interactable = !ocupado;
        if (botonIniciarSesion != null) botonIniciarSesion.interactable = !ocupado;
        if (campoUsuario != null)       campoUsuario.interactable = !ocupado;
        if (campoEmail != null)         campoEmail.interactable = !ocupado;
        if (campoPassword != null)      campoPassword.interactable = !ocupado;
    }

    /// <summary>
    /// Contra Supabase cada peticion tarda 600-800 ms, y en ese rato se puede haber
    /// cerrado el panel o vuelto al login. A diferencia del panel de perfiles aqui no
    /// se comprueba la sesion: justamente todavia no hay ninguna.
    /// </summary>
    private bool RespuestaVigente(int numero)
    {
        return this != null && isActiveAndEnabled && numero == solicitud;
    }

    private void OnDisable()
    {
        // Ignora respuestas que lleguen despues de cerrar el panel.
        ++solicitud;
        ocupado = false;
    }
}
