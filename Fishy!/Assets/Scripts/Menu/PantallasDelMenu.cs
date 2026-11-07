using Fishy.Net;
using UnityEngine;

/// <summary>
/// Enciende y apaga las pantallas del menú de inicio, que ahora viven las tres en una
/// sola escena.
///
/// <b>Qué sustituye.</b> El arranque eran tres escenas que se cargaban una tras otra:
/// MenuUno (el logo y los dos botones), Ingresar (login, cuenta y perfiles) y MenuDos
/// (Jugar / Opciones / Salir). Ahora son tres contenedores hermanos bajo el mismo
/// Canvas y cambiar de pantalla es encender uno y apagar los otros. Eso es lo que
/// permite que el fondo parallax siga corriendo sin reiniciarse al pasar de una a otra,
/// que es para lo que se hizo la escena única.
///
/// <b>Sin tocar a quien ya funcionaba.</b> <see cref="iniciar"/> sigue gobernando por
/// dentro el cartel del login, crear cuenta, perfiles y crear perfil, y
/// <see cref="Segundomenu"/> sigue gobernando el menú y la lista de partidas. Este
/// componente solo decide cuál de los tres bloques está a la vista; por eso va en el
/// Canvas y no dentro de ninguna pantalla (tiene que estar encendido siempre).
///
/// <b>Respaldo.</b> <see cref="Ir"/> devuelve false cuando en la escena no hay
/// pantallas, y entonces quien llamaba carga la escena de siempre. Por eso MenuUno,
/// Ingresar y MenuDos siguen funcionando sueltas mientras no se borren.
/// </summary>
[DisallowMultipleComponent]
public class PantallasDelMenu : MonoBehaviour
{
    public enum Pantalla { Inicio, Ingresar, Menu }

    [Header("Pantallas (los contenedores hermanos del Canvas)")]
    [Tooltip("El logo y los dos botones de pescado: lo primero que se ve al abrir el juego.")]
    [SerializeField] private GameObject pantallaInicio;

    [Tooltip("La que lleva el componente 'iniciar': login, crear cuenta, perfiles y crear perfil.")]
    [SerializeField] private GameObject pantallaIngresar;

    [Tooltip("La que lleva 'Segundomenu': Jugar / Opciones / Salir y la lista de partidas.")]
    [SerializeField] private GameObject pantallaMenu;

    /// <summary>
    /// La de la escena cargada, para que los scripts del menú la encuentren sin tener
    /// que arrastrarla en el inspector. Es null en las escenas viejas, y eso es
    /// justamente lo que las deja seguir funcionando como antes.
    /// </summary>
    public static PantallasDelMenu Actual { get; private set; }

    // -- Ciclo de vida ---------------------------------------------------------
    private void Awake()
    {
        Actual = this;
        Mostrar(PantallaDeArranque());
    }

    private void OnDestroy()
    {
        // Al entrar al juego esta escena se descarga: sin esto quedaría apuntando a un
        // objeto destruido.
        if (Actual == this) Actual = null;
    }

    // -- Para cablear en el inspector ------------------------------------------
    //
    // Los tres métodos de abajo no tienen parámetros a propósito: así aparecen en la
    // lista del On Click() de un botón, que es como se cablean los dos pescados de la
    // pantalla de inicio.

    public void MostrarInicio() => Mostrar(Pantalla.Inicio);
    public void MostrarIngresar() => Mostrar(Pantalla.Ingresar);
    public void MostrarMenu() => Mostrar(Pantalla.Menu);

    /// <summary>Cierra el juego. Lo llama el pescado "Salir" de la pantalla de inicio,
    /// igual que hacía <c>menuinicial.Salir</c> en MenuUno.</summary>
    public void Salir()
    {
        Debug.Log("SALIR");
        Application.Quit();
    }

    // -- Cambio de pantalla ----------------------------------------------------

    public void Mostrar(Pantalla cual)
    {
        GameObject destino = Objeto(cual);
        if (destino == null)
        {
            Debug.LogError($"[Menu] Falta la pantalla '{cual}' en el inspector de " +
                           $"'{name}': no hay a dónde ir, la pantalla actual se queda.", this);
            return;
        }

        // El != destino no es por capricho: sin él, pedir la pantalla que ya está
        // puesta la apagaría y no quedaría ninguna a la vista.
        if (pantallaInicio != null && pantallaInicio != destino) pantallaInicio.SetActive(false);
        if (pantallaIngresar != null && pantallaIngresar != destino) pantallaIngresar.SetActive(false);
        if (pantallaMenu != null && pantallaMenu != destino) pantallaMenu.SetActive(false);

        destino.SetActive(true);
    }

    /// <summary>
    /// Lo llaman los scripts que antes cargaban una escena. Devuelve true si se cambió
    /// de pantalla, y false si en esta escena no hay pantallas: entonces el que llama
    /// hace lo de siempre y carga la escena.
    /// </summary>
    public static bool Ir(Pantalla cual)
    {
        if (Actual == null) return false;

        Actual.Mostrar(cual);
        return true;
    }

    private GameObject Objeto(Pantalla cual)
    {
        switch (cual)
        {
            case Pantalla.Inicio: return pantallaInicio;
            case Pantalla.Ingresar: return pantallaIngresar;
            default: return pantallaMenu;
        }
    }

    /// <summary>
    /// Con cuál de las tres se abre la escena.
    ///
    /// Importa al volver del juego: MenuPausa carga esta escena otra vez, pero
    /// <see cref="ApiManager"/> sobrevive entre escenas con la sesión y el perfil ya
    /// puestos. Sin esto se vería el logo y habría que repetir el login para nada.
    ///
    /// Con sesión pero sin perfil elegido se abre en la pantalla de ingresar, porque
    /// <c>iniciar.Start()</c> ve que ya hay sesión y salta solo al panel de perfiles.
    /// </summary>
    private Pantalla PantallaDeArranque()
    {
        // En el primer arranque del juego todavía no existe: lo crea 'iniciar' al
        // encenderse su pantalla.
        ApiManager api = ApiManager.Instance;
        if (api == null || !api.IsLoggedIn) return Pantalla.Inicio;

        return api.HasJugador ? Pantalla.Menu : Pantalla.Ingresar;
    }
}
