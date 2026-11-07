using System.Linq;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Las opciones del menú principal, montadas <b>dentro</b> de la pantalla y no en una
/// tarjeta que salta encima.
///
/// <b>Por qué no reusa <see cref="PanelConfiguracionUI"/>.</b> Esa clase arma la misma
/// configuración, pero como una tarjeta flotante: le sirve a la pausa, que sale encima
/// del juego. Aquí la pantalla de opciones ya es una pantalla entera del menú, con el
/// fondo parallax detrás, así que los controles se colocan en la escena a mano y este
/// componente solo los ata a la configuración.
///
/// <b>Los controles no están aquí.</b> Son su propia pantalla
/// (<see cref="PantallaDeControles"/>), a la que se entra apagando esta y encendiendo
/// aquella, igual que se entra a esta desde el menú. El botón «Controles» de la escena
/// hace esos dos SetActive por su cuenta y este componente no lo toca.
///
/// <b>Los valores no viven aquí.</b> Los guarda <see cref="ConfiguracionDelJuego"/> en
/// PlayerPrefs, que es lo mismo que lee la configuración de la pausa. Lo que se cambie
/// en un sitio aparece en el otro sin hacer nada.
///
/// <b>Cada ajuste dice su estado en palabras</b>, el mismo criterio que la tarjeta de
/// la pausa: a los 9 años un interruptor encendido y uno apagado se parecen demasiado,
/// y quien no distinga bien los colores no tiene de dónde agarrarse.
///
/// <b>El valor va escrito en el botón</b> y no al lado: el botón de pantalla completa
/// dice «Sí» o «No», y el de velocidad dice «Lenta», «Normal» o «Rápida». Así se lee de
/// una vez lo que hay puesto y lo que se va a cambiar, sin mirar a dos sitios.
///
/// Las etiquetas del nombre del ajuste («Pantalla completa», «Velocidad Texto») <b>no se
/// tocan</b>: son de quien monta la pantalla, con sus palabras y su tamaño. La única
/// excepción es la del volumen, que lleva el porcentaje porque un deslizador no tiene
/// dónde escribirlo; y si un botón no tuviera texto dentro, su valor se cae a la
/// etiqueta, que es el único sitio que queda.
///
/// Las referencias se pueden dejar vacías: se buscan por nombre entre los hijos, como
/// hacen <see cref="iniciar"/> y <see cref="Segundomenu"/>.
/// </summary>
[DisallowMultipleComponent]
public class OpcionesDelMenu : MonoBehaviour
{
    [Header("Pantalla completa (si faltan se buscan por nombre)")]
    [Tooltip("Nombre del ajuste: se le escribe «Pantalla completa». Nombre buscado: EtiquetaPantalla.")]
    [SerializeField] private TMP_Text etiquetaPantalla;
    [Tooltip("Botón que la enciende y la apaga. Su texto dice «Sí» o «No». " +
             "Nombre buscado: BotonPantalla.")]
    [SerializeField] private Button botonPantalla;

    [Header("Volumen")]
    [Tooltip("Deslizador de 0 a 1. Nombre buscado: DeslizadorVolumen.")]
    [SerializeField] private Slider deslizadorVolumen;
    [Tooltip("Texto con el porcentaje. Nombre buscado: EtiquetaVolumen.")]
    [SerializeField] private TMP_Text etiquetaVolumen;

    [Header("Velocidad del texto")]
    [Tooltip("Nombre del ajuste: se le escribe «Velocidad del texto». Nombre buscado: EtiquetaVelocidad.")]
    [SerializeField] private TMP_Text etiquetaVelocidad;
    [Tooltip("Botón que va lenta -> normal -> rápida. Su texto dice cuál está puesta. " +
             "Nombre buscado: BotonVelocidad.")]
    [SerializeField] private Button botonVelocidad;

    /// <summary>El texto de dentro de cada botón, que es donde va el valor. Se sacan
    /// del propio botón: no hay que arrastrarlos.</summary>
    private TMP_Text textoBotonPantalla, textoBotonVelocidad;

    // -- Ciclo de vida ---------------------------------------------------------
    private void Awake()
    {
        Cablear();

        if (botonPantalla != null) botonPantalla.onClick.AddListener(CambiarPantalla);
        if (botonVelocidad != null) botonVelocidad.onClick.AddListener(CambiarVelocidad);

        if (deslizadorVolumen != null)
        {
            deslizadorVolumen.minValue = 0f;
            deslizadorVolumen.maxValue = 1f;
            deslizadorVolumen.wholeNumbers = false;
            deslizadorVolumen.onValueChanged.AddListener(CambiarVolumen);
        }
    }

    /// <summary>
    /// Se repinta cada vez que se entra, no solo al construir: la configuración puede
    /// haber cambiado desde la pausa entre una entrada y la siguiente.
    /// </summary>
    private void OnEnable()
    {
        PintarPantalla();
        PintarVelocidad();
        PintarVolumen();

        // SetValueWithoutNotify y no 'value': asignarlo dispararía onValueChanged, que
        // volvería a guardar el mismo valor en cada entrada a la pantalla.
        if (deslizadorVolumen != null)
            deslizadorVolumen.SetValueWithoutNotify(ConfiguracionDelJuego.Volumen);
    }

    // -- Lo que hacen los controles --------------------------------------------

    public void CambiarPantalla()
    {
        ConfiguracionDelJuego.PantallaCompleta = !ConfiguracionDelJuego.PantallaCompleta;
        PintarPantalla();
    }

    public void CambiarVelocidad()
    {
        ConfiguracionDelJuego.VelocidadTexto = ConfiguracionDelJuego.SiguienteVelocidad(
            ConfiguracionDelJuego.VelocidadTexto);
        PintarVelocidad();
    }

    public void CambiarVolumen(float valor)
    {
        ConfiguracionDelJuego.Volumen = valor;
        PintarVolumen();
    }

    // -- Lo que se lee en pantalla ---------------------------------------------

    private void PintarPantalla()
    {
        bool completa = ConfiguracionDelJuego.PantallaCompleta;

        if (textoBotonPantalla != null)
        {
            // La etiqueta no se toca: el nombre del ajuste lo escribe quien monta la
            // pantalla, con sus palabras y su tamaño. Aquí solo se pone el valor.
            textoBotonPantalla.text = completa ? "Sí" : "No";
            return;
        }

        // Respaldo: un botón sin texto dentro (una imagen con la palabra dibujada) no
        // tiene dónde poner el valor, así que se escribe la frase entera en la etiqueta.
        if (etiquetaPantalla != null)
            etiquetaPantalla.text = completa
                ? "Pantalla completa: sí"
                : "Pantalla completa: no (en ventana)";
    }

    private void PintarVelocidad()
    {
        string palabra;
        switch (ConfiguracionDelJuego.VelocidadTexto)
        {
            case ConfiguracionDelJuego.VelocidadDelTexto.Lenta: palabra = "Lenta"; break;
            case ConfiguracionDelJuego.VelocidadDelTexto.Rapida: palabra = "Rápida"; break;
            default: palabra = "Normal"; break;
        }

        if (textoBotonVelocidad != null)
        {
            textoBotonVelocidad.text = palabra;
            return;
        }

        if (etiquetaVelocidad != null)
            etiquetaVelocidad.text = $"Velocidad del texto: {palabra.ToLowerInvariant()}";
    }

    private void PintarVolumen()
    {
        if (etiquetaVolumen == null) return;

        int porcentaje = ConfiguracionDelJuego.VolumenEnPorcentaje;
        etiquetaVolumen.text = porcentaje == 0
            ? "Volumen: apagado"
            : $"Volumen: {porcentaje}%";
    }

    // -- Cableado --------------------------------------------------------------

    private void Cablear()
    {
        var textos = GetComponentsInChildren<TMP_Text>(true);
        var botones = GetComponentsInChildren<Button>(true);

        if (etiquetaPantalla == null) etiquetaPantalla = PorNombre(textos, "EtiquetaPantalla");
        if (etiquetaVolumen == null) etiquetaVolumen = PorNombre(textos, "EtiquetaVolumen");
        if (etiquetaVelocidad == null) etiquetaVelocidad = PorNombre(textos, "EtiquetaVelocidad");

        if (botonPantalla == null) botonPantalla = PorNombre(botones, "BotonPantalla");
        if (botonVelocidad == null) botonVelocidad = PorNombre(botones, "BotonVelocidad");

        // El texto del valor: el que cuelga del propio botón. Si el botón es la etiqueta
        // misma (un TMP con su Button), vale ese.
        if (botonPantalla != null)
            textoBotonPantalla = botonPantalla.GetComponentInChildren<TMP_Text>(true);
        if (botonVelocidad != null)
            textoBotonVelocidad = botonVelocidad.GetComponentInChildren<TMP_Text>(true);

        if (deslizadorVolumen == null)
            deslizadorVolumen = GetComponentsInChildren<Slider>(true)
                .FirstOrDefault(s => s.name == "DeslizadorVolumen")
                ?? GetComponentInChildren<Slider>(true);
    }

    private static T PorNombre<T>(T[] donde, string nombre) where T : Component
        => donde.FirstOrDefault(c => c.name == nombre);
}
