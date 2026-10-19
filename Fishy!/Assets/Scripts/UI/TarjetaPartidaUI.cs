using System;
using System.Collections.Generic;
using Fishy.Net;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una fila de la lista de partidas. Hermana de <see cref="TarjetaPerfilUI"/> y con
/// el mismo trato: el panel la instancia, la configura y le pasa a quien avisar
/// cuando la tocan. La tarjeta no habla con el backend ni decide nada.
///
/// Hay DOS estados visuales que no son lo mismo y se pueden dar a la vez:
///   - ser la MAS RECIENTE, que pinta los textos y la huella de dorado y muestra
///     el distintivo "(Ultima partida)";
///   - estar SELECCIONADA, que oscurece el fondo.
/// La mas reciente lo es siempre; seleccionada puede estarlo cualquiera.
/// </summary>
[RequireComponent(typeof(Button))]
public class TarjetaPartidaUI : MonoBehaviour
{
    [SerializeField] private Image fondo;
    [SerializeField] private Image huella;
    [SerializeField] private TMP_Text zona;
    [SerializeField] private TMP_Text etiqueta;
    [SerializeField] private TMP_Text fecha;
    [SerializeField] private TMP_Text progreso;

    public PartidaDto Partida { get; private set; }

    private Button boton;
    private Action<TarjetaPartidaUI> alSeleccionar;
    private bool masReciente;

    // Los nombres salen de MainScene: `zona_2` y `zona_3` los guarda cada ZonaMundo
    // en su `nombreVisible`, y `zona_1` no tiene ZonaMundo (no tiene poligono), asi
    // que su nombre vive en ZonaActual.nombrePorDefecto.
    //
    // No se usa ZonaMundo.NombreDe() a proposito: esa busca entre las zonas VIVAS de
    // la escena, y en Ingresar no hay ninguna cargada, asi que devolveria el id tal
    // cual ("zona_2"). Esta pantalla necesita el nombre sin tener el mundo montado.
    private static readonly Dictionary<string, string> NombresDeZona =
        new Dictionary<string, string>
        {
            { "zona_1", "Bosque de los desconocidos" },
            { "zona_2", "Pantano de los susurros" },
            { "zona_3", "Arrecife de los desafíos" },
        };

    private static readonly Dictionary<string, string> ColoresDeZona =
        new Dictionary<string, string>
        {
            { "zona_1", "#8BC34A" },
            { "zona_2", "#7FC4A3" },
            { "zona_3", "#5BB8E8" },
        };

    private const string CafeClaro = "#C5876C";   // igual que TarjetaPerfilUI
    private const string Dorado    = "#FFB941";   // igual que TarjetaPerfilUI
    private const string FondoNormal       = "#503124";
    private const string FondoSeleccionado = "#2D211F";

    private void Awake()
    {
        boton = GetComponent<Button>();
        boton.onClick.AddListener(NotificarClic);
    }

    public void Configurar(PartidaDto partida, bool esMasReciente,
        Action<TarjetaPartidaUI> seleccion)
    {
        Partida = partida;
        masReciente = esMasReciente;
        alSeleccionar = seleccion;

        if (zona != null)
        {
            zona.text  = NombreDeZona(partida.zona_actual);
            zona.color = ColorDeZona(partida.zona_actual);
        }

        // Solo la primera lleva el distintivo: el backend manda las partidas de la
        // mas reciente a la mas antigua. Se apaga el objeto en vez de dejarlo con
        // texto vacio para que no siga comiendo sitio en la esquina.
        if (etiqueta != null)
        {
            etiqueta.text = "(Última partida)";
            etiqueta.gameObject.SetActive(esMasReciente);
        }

        if (fecha != null)
            fecha.text = ConMayuscula(TextoDePartida.Cuando(partida.fecha_update));

        // Un "0% explorado" no informa y ocupa el mismo sitio que algo que si
        // informaria, asi que en cero no se escribe. Mismo criterio que
        // TextoDePartida.Etiqueta, que es la otra pantalla donde se ve esto.
        if (progreso != null)
        {
            int avance = Mathf.RoundToInt(partida.progreso);
            progreso.text = avance > 0 ? $"{avance}% explorado" : string.Empty;
        }

        AplicarColoresDeTexto();
        MostrarSeleccion(false);
    }

    private void NotificarClic()
    {
        if (Partida != null)
            alSeleccionar?.Invoke(this);
    }

    /// <summary>Oscurece el fondo. No toca los textos: el dorado de la mas reciente
    /// no depende de estar seleccionada.</summary>
    public void MostrarSeleccion(bool seleccionada)
    {
        if (fondo == null) return;

        fondo.color = ColorHex(seleccionada ? FondoSeleccionado : FondoNormal);
    }

    private void AplicarColoresDeTexto()
    {
        Color color = ColorHex(masReciente ? Dorado : CafeClaro);

        if (etiqueta != null) etiqueta.color = color;
        if (fecha    != null) fecha.color    = color;
        if (progreso != null) progreso.color = color;
        if (huella   != null) huella.color   = color;
    }

    /// <summary>
    /// El nombre legible de la zona donde quedo Otto.
    ///
    /// Viene vacio mas a menudo de lo que parece: la fila de PersonajeJugador se crea
    /// recien la primera vez que el juego guarda la posicion, asi que TODA partida
    /// recien creada pasa por aqui sin zona. No es un caso raro de borde.
    /// </summary>
    private static string NombreDeZona(string zonaId)
    {
        string id = (zonaId ?? string.Empty).Trim();
        if (id.Length == 0) return "Partida recién empezada";

        return NombresDeZona.TryGetValue(id, out string nombre) ? nombre : id;
    }

    private static Color ColorDeZona(string zonaId)
    {
        string id = (zonaId ?? string.Empty).Trim();
        return ColorHex(ColoresDeZona.TryGetValue(id, out string hex) ? hex : CafeClaro);
    }

    /// <summary>TextoDePartida.Cuando() devuelve "hoy a las 22:00" en minuscula
    /// porque alla va en medio de una frase. Aqui empieza la linea.</summary>
    private static string ConMayuscula(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        return char.ToUpper(texto[0]) + texto.Substring(1);
    }

    private static Color ColorHex(string hexadecimal)
    {
        ColorUtility.TryParseHtmlString(hexadecimal, out Color color);
        return color;
    }

    private void OnDestroy()
    {
        if (boton != null)
            boton.onClick.RemoveListener(NotificarClic);
    }
}
