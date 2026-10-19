using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fishy.World;

namespace Fishy.Diccionario
{
    /// <summary>
    /// La pantalla de diccionario dentro del celular. Va en <c>DiccionarioPage</c>.
    ///
    /// Hay dos maneras de llegar aquí:
    ///
    ///  · Al entrar a una zona nueva — <see cref="DiccionarioAlEntrarZona"/> pide
    ///    <see cref="Mostrar"/> con obligatorio justo después de <c>ShowPage</c>.
    ///    Sale el botón "Entendido" y es la única salida: el panel está bloqueado.
    ///  · Desde el botón de información del inicio — nadie pide nada, así que el
    ///    <c>OnEnable</c> muestra la zona donde está Otto ahora y esconde el botón,
    ///    porque ahí el panel se cierra con la X como cualquier otra página.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiccionarioPageUI : MonoBehaviour
    {
        [Header("Piezas de la pantalla")]
        [Tooltip("Imagen del banner. Se tiñe con el color de la zona.")]
        public Image banner;
        [Tooltip("Título dentro del banner: 'Zona 1: Bosque de los Desconocidos'.")]
        public TMP_Text bannerTexto;
        [Tooltip("Texto con la lista de términos. Necesita Rich Text activado.")]
        public TMP_Text terminos;
        [Tooltip("Botón 'Entendido'. Sólo aparece cuando la pantalla es obligatoria.")]
        public GameObject botonEntendido;

        [Header("Formato de la lista")]
        [Tooltip("Color del término en negrita, antes de los dos puntos.")]
        public string colorTermino = "#FFB941";

        [Tooltip("Cuánto se corre el texto respecto de la viñeta, en unidades del " +
                 "panel. Es lo que hace que la segunda línea de un término quede " +
                 "alineada con la primera y no debajo del punto.")]
        [Min(0f)] public float sangria = 31f;

        [Tooltip("Qué se muestra si la zona no tiene ningún término cargado.")]
        public string textoSinTerminos = "Todavía no hay palabras para esta zona.";

        /// <summary>Se dispara al pulsar "Entendido", con el id de la zona mostrada.
        /// Lo escucha quien tenga que anotar que ya se vio y cerrar el panel.</summary>
        public event Action<string> AlEntender;

        private string _zonaMostrada;
        private bool   _suscrito;

        /// <summary>Qué zona se está mostrando. Vacío si la pantalla nunca se abrió.</summary>
        public string ZonaMostrada => _zonaMostrada;

        /// <summary>
        /// Pinta la pantalla a propósito, con el botón o sin él. Lo llama
        /// <see cref="DiccionarioAlEntrarZona"/> justo después de <c>ShowPage</c>.
        ///
        /// <b>Por qué se pide aquí y no se deja preparado antes.</b> Al cerrar el
        /// celular sólo se apaga el canvas: esta página se queda marcada como activa,
        /// y al reabrirlo se enciende con el resto <i>antes</i> de que nadie haya
        /// pedido <c>ShowPage</c>. Cualquier estado dejado de antemano se lo comía ese
        /// encendido, y la segunda vez de cada sesión salía sin botón.
        /// </summary>
        public void Mostrar(string zonaId, bool obligatorio)
        {
            Conectar();
            Pintar(zonaId, obligatorio);
        }

        /// <summary>
        /// Encenderse por su cuenta es siempre la visita opcional: o la abrió el botón
        /// de información, o es el eco de reabrir el celular. La obligatoria la pide
        /// el disparador con <see cref="Mostrar"/> y repinta encima en el mismo frame.
        /// </summary>
        private void OnEnable()
        {
            Conectar();
            Pintar(ZonaDeOtto(), false);
        }

        /// <summary>
        /// El botón se conecta aquí y no en Awake porque la página nace apagada
        /// dentro del prefab: Awake no corre hasta que algo la enciende por primera
        /// vez, y para entonces OnEnable ya pasó.
        /// </summary>
        private void Conectar()
        {
            if (_suscrito || botonEntendido == null) return;

            var boton = botonEntendido.GetComponent<Button>();
            if (boton == null)
            {
                Debug.LogWarning("[Diccionario] El objeto asignado como botón 'Entendido' " +
                                 "no tiene componente Button.", this);
                return;
            }

            boton.onClick.AddListener(Entendido);
            _suscrito = true;
        }

        private void OnDestroy()
        {
            if (!_suscrito || botonEntendido == null) return;
            var boton = botonEntendido.GetComponent<Button>();
            if (boton != null) boton.onClick.RemoveListener(Entendido);
        }

        // ── Pintado ──────────────────────────────────────────────────────────────

        private void Pintar(string zonaId, bool obligatorio)
        {
            _zonaMostrada = zonaId;

            if (botonEntendido != null) botonEntendido.SetActive(obligatorio);

            var zona = DiccionarioCatalogo.De(zonaId);
            if (zona == null)
            {
                // Sin entrada en el JSON no hay nada que decir, pero la pantalla ya
                // está abierta: se deja un texto en vez de un hueco.
                if (bannerTexto != null) bannerTexto.text = "Diccionario";
                if (terminos != null)    terminos.text    = textoSinTerminos;
                return;
            }

            PintarBanner(zona);

            if (terminos != null) terminos.text = ArmarLista(zona);
        }

        private void PintarBanner(ZonaDiccionario zona)
        {
            if (banner != null && TryColor(zona.color, out Color fondo))
                banner.color = fondo;

            if (bannerTexto == null) return;

            bannerTexto.text = zona.titulo ?? "";

            // Vacío significa "el que tenga el prefab", no negro: así una zona a la
            // que todavía no se le eligió color de texto se ve bien igual.
            if (TryColor(zona.colorTexto, out Color tinta))
                bannerTexto.color = tinta;
        }

        /// <summary>
        /// Arma la lista en texto enriquecido. La viñeta se queda en el color del
        /// cuerpo y sólo el término va en ámbar y negrita, como en el diseño.
        /// </summary>
        private string ArmarLista(ZonaDiccionario zona)
        {
            if (zona.terminos == null || zona.terminos.Count == 0) return textoSinTerminos;

            var sb = new StringBuilder();
            foreach (var t in zona.terminos)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.termino)) continue;

                if (sb.Length > 0) sb.Append('\n');

                sb.Append("• <indent=").Append(Mathf.RoundToInt(sangria)).Append('>')
                  .Append("<color=").Append(colorTermino).Append("><b>")
                  .Append(t.termino).Append(":</b></color> ")
                  .Append(t.definicion ?? "")
                  .Append("</indent>");
            }

            return sb.Length > 0 ? sb.ToString() : textoSinTerminos;
        }

        // ── Botón ────────────────────────────────────────────────────────────────

        /// <summary>Pública para poder engancharla también desde el inspector.</summary>
        public void Entendido()
        {
            AlEntender?.Invoke(_zonaMostrada);
        }

        // ── Ayudas ───────────────────────────────────────────────────────────────

        private static string ZonaDeOtto()
        {
            var zona = ZonaActual.Instance;
            if (zona == null) zona = FindAnyObjectByType<ZonaActual>();
            return zona != null ? zona.Actual : "";
        }

        /// <summary>Convierte "#RRGGBB". Devuelve false con cadena vacía o mal escrita,
        /// y en ese caso quien llama deja el color que ya tenía.</summary>
        private static bool TryColor(string hex, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex)) return false;

            string limpio = hex.Trim();
            if (!limpio.StartsWith("#")) limpio = "#" + limpio;

            if (ColorUtility.TryParseHtmlString(limpio, out color)) return true;

            Debug.LogWarning($"[Diccionario] '{hex}' no es un color válido; se ignora.");
            return false;
        }
    }
}
