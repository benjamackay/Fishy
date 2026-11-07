using System.Collections.Generic;
using Fishy.UI;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// La pantalla de controles del menú: escribe en cada recuadro la tecla que hace
/// esa acción de verdad.
///
/// <b>El diseño no está aquí.</b> Los recuadros, sus colores y dónde cae cada uno
/// están puestos a mano en la escena, colgando de «Controles». Este componente no
/// crea ni coloca nada: busca los huecos por su nombre y les pone el texto. Antes
/// montaba por código la tarjeta de <see cref="TecladoEnPantalla"/>, que está
/// dibujada para salir en una esquina sobre un velo oscuro y como pantalla entera
/// no cuadraba.
///
/// <b>Las teclas no están escritas en la escena.</b> Se le preguntan a quien las
/// escucha de verdad —<see cref="OttoController"/>,
/// <see cref="InteractionDetector"/>, <see cref="MenuPausa"/> y
/// <see cref="MenuController"/>—, reusando los ayudantes de la tarjeta de la
/// pausa para que las dos pantallas no puedan contar cosas distintas. Una
/// pantalla de controles que enseña teclas equivocadas es peor que no tener
/// pantalla de controles.
///
/// <b>Se reescribe cada vez que se entra</b> (<c>OnEnable</c>) y no una sola vez
/// al arrancar: en el menú todavía no hay Otto y todo cae a los respaldos, que
/// son los valores reales del juego, pero si algún día se abre con una partida
/// cargada dirá las teclas que esa partida esté escuchando.
///
/// <b>Los huecos se llaman por dirección y no por tecla.</b> «Arriba» sigue
/// siendo verdad si mañana caminar se ata a I-J-K-L; «TeclaW» ya estaría
/// mintiendo desde el nombre.
/// </summary>
[DisallowMultipleComponent]
public class PantallaDeControles : MonoBehaviour
{
    /// <summary>Las cuatro direcciones, en el orden en que las devuelve
    /// <see cref="TecladoEnPantalla.TeclasDeCaminar"/>.</summary>
    private static readonly string[] Direcciones = { "Arriba", "Izquierda", "Abajo", "Derecha" };

    private const string RutaLetras  = "Movimiento/Letras";
    private const string RutaFlechas = "Movimiento/Flechas";

    /// <summary>Cómo se llama el triangulito que se le añade a un recuadro cuando
    /// su tecla es una flecha. Se reaprovecha entre visitas en vez de crear uno
    /// nuevo cada vez que se abre la pantalla.</summary>
    private const string NombreFlecha = "Flecha";

    /// <summary>Cuánto del recuadro ocupa el triángulo, sobre su lado más corto.
    /// Las teclas de la cruceta son de 33x31, así que sale de unas 14 unidades:
    /// se ve de lejos y no toca el contorno.</summary>
    private const float ProporcionFlecha = 0.45f;

    private readonly List<string> _faltan = new List<string>();
    private bool _avisado;

    private void OnEnable()
    {
        _faltan.Clear();

        OttoController otto = FindAnyObjectByType<OttoController>();

        // Caminar viene agrupado por dirección —arriba = [W, ↑]—, así que la
        // primera tecla de cada grupo va al bloque de letras y la segunda al de
        // flechas. Es lo que dibujan los dos bloques de la escena.
        List<List<Key>> caminar = TecladoEnPantalla.TeclasDeCaminar(otto);
        Transform letras  = transform.Find(RutaLetras);
        Transform flechas = transform.Find(RutaFlechas);

        if (letras == null)  _faltan.Add(RutaLetras);
        if (flechas == null) _faltan.Add(RutaFlechas);

        for (int i = 0; i < Direcciones.Length && i < caminar.Count; i++)
        {
            Escribir(letras,  Direcciones[i], Enesima(caminar[i], 0));
            Escribir(flechas, Direcciones[i], Enesima(caminar[i], 1));
        }

        List<Key> correr = TecladoEnPantalla.TeclasDe(
            otto != null ? otto.AccionCorrer : null, Key.LeftShift, Key.RightShift);

        Escribir(transform, "Correr",      Enesima(correr, 0));
        Escribir(transform, "Interactuar", TecladoEnPantalla.TeclaDeInteractuar());
        Escribir(transform, "Menu",        MenuPausa.TeclaDePausa);
        Escribir(transform, "Celular",     MenuController.TeclaDelCelular);

        // Una sola vez: si falta un hueco va a faltar en todas las visitas, y
        // esta pantalla se abre y se cierra muchas veces seguidas.
        if (_faltan.Count > 0 && !_avisado)
        {
            _avisado = true;
            Debug.LogWarning("[Controles] No encuentro estos huecos colgando de '" +
                             name + "': " + string.Join(", ", _faltan) +
                             ". Esos recuadros se quedan con lo que diga la escena, " +
                             "que puede ser una tecla vieja.", this);
        }
    }

    /// <summary>La tecla n-ésima de un grupo, o nada si el grupo no llega. Una
    /// dirección puede traer una sola tecla si alguien desató las flechas: en ese
    /// caso el recuadro sobrante se apaga, que es mejor que dejar un cuadrado de
    /// color vacío en mitad de la cruceta.</summary>
    private static Key? Enesima(List<Key> teclas, int i) =>
        teclas != null && teclas.Count > i ? teclas[i] : (Key?)null;

    /// <summary>
    /// Pone en un hueco la tecla que le toca. El hueco es el recuadro de la
    /// escena; el texto es el primer TMP que cuelgue de él, se llame como se
    /// llame, para no atar el script al nombre que tenga el hijo.
    /// </summary>
    private void Escribir(Transform padre, string hueco, Key? tecla)
    {
        if (padre == null) return;              // ya se anotó al buscar el bloque

        Transform recuadro = padre.Find(hueco);
        if (recuadro == null) { _faltan.Add(hueco); return; }

        if (!tecla.HasValue) { recuadro.gameObject.SetActive(false); return; }
        recuadro.gameObject.SetActive(true);

        var texto = recuadro.GetComponentInChildren<TextMeshProUGUI>(true);
        float? giro = GiroDeFlecha(tecla.Value);

        if (giro.HasValue)
        {
            if (texto != null) texto.text = string.Empty;
            Triangulo(recuadro, giro.Value, texto != null ? texto.color : Color.white);
            return;
        }

        Transform flecha = recuadro.Find(NombreFlecha);
        if (flecha != null) flecha.gameObject.SetActive(false);

        if (texto != null) texto.text = TecladoEnPantalla.EtiquetaDe(tecla.Value);
    }

    /// <summary>
    /// El giro del triángulo si la tecla es una flecha, y nada si es una letra
    /// —entonces se escribe—. El triángulo del kit nace mirando a la derecha.
    ///
    /// Se mira la tecla y no el nombre del hueco a propósito: si la dirección
    /// «izquierda» acabara atada a la flecha derecha, el triángulo apunta a la
    /// derecha, que es la verdad, por rara que sea.
    /// </summary>
    private static float? GiroDeFlecha(Key tecla)
    {
        switch (tecla)
        {
            case Key.UpArrow:    return 90f;
            case Key.LeftArrow:  return 180f;
            case Key.DownArrow:  return 270f;
            case Key.RightArrow: return 0f;
            default:             return null;
        }
    }

    /// <summary>
    /// Dibuja (o reaprovecha) el triangulito de dirección dentro de un recuadro.
    ///
    /// Va dibujado y no escrito porque ↑↓←→ no están en ninguna de las dos
    /// fuentes del juego: TMP las saca de su fuente de respaldo y salen con otra
    /// tipografía que el resto de las teclas, además de un hijo y un material de
    /// más por cada flecha. Es el mismo triángulo que usa la tarjeta de la pausa.
    ///
    /// El color se toma del TMP del propio recuadro: así el aspecto sigue siendo
    /// cosa de la escena y este script no decide colores.
    /// </summary>
    private static void Triangulo(Transform recuadro, float giro, Color color)
    {
        Transform flecha = recuadro.Find(NombreFlecha);
        if (flecha == null)
        {
            var go = new GameObject(NombreFlecha, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(recuadro, false);
            flecha = go.transform;
        }

        var caja = (RectTransform)recuadro;
        float lado = Mathf.Min(caja.rect.width, caja.rect.height) * ProporcionFlecha;

        var rt = (RectTransform)flecha;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(lado, lado);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.Euler(0f, 0f, giro);

        var img = flecha.GetComponent<Image>();
        if (img == null) img = flecha.gameObject.AddComponent<Image>();
        img.sprite = FishyUIKit.SpriteTriangulo();
        img.color = color;
        img.raycastTarget = false;

        flecha.gameObject.SetActive(true);
    }
}
