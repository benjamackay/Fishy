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
/// cuenta, y la edad, opcional. El panel no elige por el usuario: si no tocan las
/// flechas, el perfil se crea sin edad.
/// </summary>
public class PanelCrearPerfilUI : MonoBehaviour
{
    [Header("Campos")]
    [SerializeField] private TMP_InputField campoNombre;
    [SerializeField] private TMP_Text numeroEdad;
    [SerializeField] private TMP_Text mensajeError;

    [Header("Botones")]
    [SerializeField] private Button botonCancelar;
    [SerializeField] private Button botonCrear;

    [Header("Flechas de la edad")]
    [SerializeField] private Button botonEdadMenos;
    [SerializeField] private Button botonEdadMas;

    /// <summary>
    /// El selector de edad es una tira de valores: primero "no opina" y despues las
    /// edades de <see cref="EdadMinima"/> a <see cref="EdadMaxima"/>. Antes esto era
    /// un desplegable de 9 a 13.
    ///
    /// <see cref="SinEdad"/> vale -1 a proposito, justo debajo de EdadMinima: asi la
    /// tira entera es un rango continuo y "no opina" es simplemente el primer valor,
    /// sin un caso aparte que se olvide en alguna rama.
    ///
    /// Arranca en "no opina" y no en una edad concreta porque edad es opcional en el
    /// backend. Si arrancara en un numero, todo perfil que nadie toque quedaria
    /// guardado con una edad que nadie dijo, y eso no se distingue despues de una
    /// respuesta de verdad.
    /// </summary>
    private const int SinEdad = -1;
    private const int EdadMinima = 0;
    private const int EdadMaxima = 99;
    private const int EdadInicial = SinEdad;

    /// <summary>Lo que se lee en la caja cuando no hay edad elegida.</summary>
    private const string TextoSinEdad = "No opina";

    private Action<UsuarioJugadorDto> alCrear;
    private Action alCancelar;
    private bool ocupado;
    private int solicitud;
    private int edad = EdadInicial;
    private RepetidorDePulsacion repetidorMenos, repetidorMas;

    private void Awake()
    {
        if (botonCancelar != null) botonCancelar.onClick.AddListener(PulsarCancelar);
        if (botonCrear != null)    botonCrear.onClick.AddListener(PulsarCrear);

        // Las flechas no van por onClick: con 101 valores hace falta mantener pulsado.
        // El repetidor ya dispara en el primer toque, asi que lo sustituye en vez de
        // sumarse a el; con los dos puestos, un clic suelto contaria dos veces.
        repetidorMenos = Repetidor(botonEdadMenos, BajarEdad);
        repetidorMas   = Repetidor(botonEdadMas, SubirEdad);

        // Enter en el nombre tambien envia: es un formulario de dos campos y obligar
        // a apuntar al boton es innecesario.
        if (campoNombre != null) campoNombre.onSubmit.AddListener(_ => PulsarCrear());
    }

    private void OnDestroy()
    {
        if (botonCancelar != null) botonCancelar.onClick.RemoveListener(PulsarCancelar);
        if (botonCrear != null)    botonCrear.onClick.RemoveListener(PulsarCrear);

        if (repetidorMenos != null) repetidorMenos.AlRepetir -= BajarEdad;
        if (repetidorMas != null)   repetidorMas.AlRepetir -= SubirEdad;
    }

    /// <summary>Deja una flecha lista para repetir mientras se sostiene. El repetidor
    /// se agrega aqui y no en la escena para que no dependa de acordarse de ponerlo a
    /// mano en cada flecha.</summary>
    private static RepetidorDePulsacion Repetidor(Button boton, Action accion)
    {
        if (boton == null) return null;

        var repetidor = boton.GetComponent<RepetidorDePulsacion>();
        if (repetidor == null) repetidor = boton.gameObject.AddComponent<RepetidorDePulsacion>();

        repetidor.AlRepetir += accion;
        return repetidor;
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
        edad = EdadInicial;
        PintarEdad();
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

    private void BajarEdad() => CambiarEdad(-1);
    private void SubirEdad() => CambiarEdad(+1);

    /// <summary>
    /// Mueve la edad un paso por la tira, dando la vuelta en los extremos: despues de
    /// 99 viene otra vez "No opina", y hacia atras desde "No opina" se llega a 99.
    /// Con 101 valores, toparse con un tope y tener que soltar para volver a empezar
    /// por el otro lado es mas molesto que la vuelta.
    ///
    /// El resto en C# conserva el signo (-1 % 101 da -1), asi que se normaliza a mano
    /// antes de usarlo como indice.
    /// </summary>
    private void CambiarEdad(int paso)
    {
        if (ocupado) return;

        int valores = EdadMaxima - SinEdad + 1;
        int indice = edad - SinEdad + paso;

        edad = SinEdad + ((indice % valores) + valores) % valores;
        PintarEdad();
    }

    /// <summary>Escribe el valor en la caja. Las flechas solo se apagan mientras se
    /// espera al servidor: como la tira da la vuelta, nunca se quedan sin sitio a
    /// donde ir.</summary>
    private void PintarEdad()
    {
        if (numeroEdad != null)
            numeroEdad.text = edad == SinEdad ? TextoSinEdad : edad.ToString();

        if (botonEdadMenos != null) botonEdadMenos.interactable = !ocupado;
        if (botonEdadMas != null)   botonEdadMas.interactable   = !ocupado;
    }

    /// <summary>La edad elegida, o null si esta en "No opina".</summary>
    private int? EdadElegida()
    {
        return edad == SinEdad ? (int?)null : edad;
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

        // Las flechas dependen ademas de en que parte de la tira estamos.
        PintarEdad();
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
