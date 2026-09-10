using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Carcasa de celular alrededor de una pantalla ya construida: bisel, cámara
    /// frontal, barra de estado con reloj e iconos, y barra de inicio.
    ///
    /// No construye la pantalla ni sabe qué hay dentro. Se le pasa el RectTransform
    /// del panel que hace de pantalla y lo envuelve, así que sirve igual para el
    /// chat por teléfono y para el Modo Detective, que son dos pantallas distintas
    /// del mismo aparato: el celular de Otto.
    ///
    /// El bisel es negro a propósito y no sale de <see cref="Paleta"/>: un celular
    /// tiene el marco negro, y teñirlo de marrón lo haría parecer un mueble en vez
    /// de un aparato.
    /// </summary>
    [DisallowMultipleComponent]
    public class MarcoTelefono : MonoBehaviour
    {
        public struct Ajustes
        {
            /// <summary>Tamaño de la pantalla. El bisel se calcula a partir de esto
            /// más <see cref="Borde"/> por lado, así que ensanchar una cosa ya no
            /// descuadra la otra.</summary>
            public Vector2 Pantalla;

            /// <summary>Grosor del bisel a cada lado.</summary>
            public float Borde;

            public float AlturaBarraEstado;
            public float TamanoReloj;

            /// <summary>
            /// Cómo entra la barra de estado en la pantalla. En true se cuela como
            /// una fila más de un VerticalLayoutGroup y pide su alto (es el caso del
            /// chat, cuyo panel es una pila); en false se ancla arriba y es la
            /// pantalla la que tiene que dejarle sitio (el detective, que coloca sus
            /// zonas con anclajes).
            /// </summary>
            public bool BarraEstadoEnPila;

            public bool  ConBarraInicio;

            /// <summary>Alto al que flota la barra de inicio sobre el borde
            /// inferior. Se ajusta porque cada pantalla tiene cosas distintas ahí
            /// abajo y la barra no debe montarse encima de un botón.</summary>
            public float MargenBarraInicio;

            /// <summary>En true la pantalla se centra y se redimensiona a
            /// <see cref="Pantalla"/>. El chat lo necesita porque fuera del teléfono
            /// su panel vive en una esquina; una pantalla que ya está centrada y
            /// medida puede dejarlo en false.</summary>
            public bool RecolocarPantalla;

            public Color ColorBisel;
            public Color ColorBarraEstado;

            public static Ajustes PorDefecto => new Ajustes
            {
                Pantalla          = new Vector2(780f, 920f),
                Borde             = 20f,
                AlturaBarraEstado = 48f,
                TamanoReloj       = 24f,
                BarraEstadoEnPila = false,
                ConBarraInicio    = true,
                MargenBarraInicio = 10f,
                RecolocarPantalla = false,
                ColorBisel        = new Color(0.06f, 0.06f, 0.08f, 1f),
                ColorBarraEstado  = new Color(0.05f, 0.07f, 0.10f, 1f),
            };
        }

        private GameObject      _barraEstado;
        private GameObject      _barraInicio;
        private TextMeshProUGUI _reloj;
        private Coroutine       _rutinaReloj;

        /// <summary>
        /// Envuelve una pantalla con la carcasa. Devuelve el marco para poder
        /// quitarlo después con <see cref="Desmontar"/>.
        /// </summary>
        public static MarcoTelefono Montar(RectTransform pantalla, Ajustes ajustes)
        {
            if (pantalla == null) return null;

            if (ajustes.RecolocarPantalla)
            {
                pantalla.anchorMin = new Vector2(0.5f, 0.5f);
                pantalla.anchorMax = new Vector2(0.5f, 0.5f);
                pantalla.pivot     = new Vector2(0.5f, 0.5f);
                pantalla.anchoredPosition = Vector2.zero;
                pantalla.sizeDelta = ajustes.Pantalla;
            }

            // El bisel es hermano de la pantalla y va justo detrás: así asoma por los
            // cuatro lados sin taparla.
            var biselGO = new GameObject("MarcoTelefono",
                typeof(RectTransform), typeof(Image), typeof(MarcoTelefono));
            biselGO.transform.SetParent(pantalla.parent, false);
            biselGO.transform.SetSiblingIndex(pantalla.GetSiblingIndex());

            var biselRT = biselGO.GetComponent<RectTransform>();
            biselRT.anchorMin = new Vector2(0.5f, 0.5f);
            biselRT.anchorMax = new Vector2(0.5f, 0.5f);
            biselRT.pivot     = new Vector2(0.5f, 0.5f);
            biselRT.anchoredPosition = pantalla.anchoredPosition;
            biselRT.sizeDelta = ajustes.Pantalla + Vector2.one * (ajustes.Borde * 2f);

            var biselImg = biselGO.GetComponent<Image>();
            biselImg.color         = ajustes.ColorBisel;
            biselImg.raycastTarget = false;

            var marco = biselGO.GetComponent<MarcoTelefono>();
            marco.CrearNotch(biselGO.transform, ajustes);
            marco.CrearBarraEstado(pantalla, ajustes);
            if (ajustes.ConBarraInicio) marco.CrearBarraInicio(pantalla, ajustes);

            return marco;
        }

        /// <summary>Quita la carcasa entera, incluidas las piezas que viven dentro
        /// de la pantalla.</summary>
        public void Desmontar()
        {
            if (_rutinaReloj != null) { StopCoroutine(_rutinaReloj); _rutinaReloj = null; }
            if (_barraEstado != null) { Destroy(_barraEstado); _barraEstado = null; }
            if (_barraInicio != null) { Destroy(_barraInicio); _barraInicio = null; }
            _reloj = null;
            Destroy(gameObject);
        }

        /// <summary>Cámara frontal, asomando por el borde de arriba del bisel.</summary>
        private void CrearNotch(Transform bisel, Ajustes ajustes)
        {
            var notch = new GameObject("Notch", typeof(RectTransform), typeof(Image));
            notch.transform.SetParent(bisel, false);

            var rt = notch.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(120f, 28f);

            var img = notch.GetComponent<Image>();
            img.color         = new Color(0.04f, 0.04f, 0.05f, 1f);
            img.raycastTarget = false;
        }

        private void CrearBarraEstado(RectTransform pantalla, Ajustes ajustes)
        {
            _barraEstado = new GameObject("BarraEstado",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            _barraEstado.transform.SetParent(pantalla, false);
            _barraEstado.transform.SetAsFirstSibling();

            var le = _barraEstado.GetComponent<LayoutElement>();
            var rt = _barraEstado.GetComponent<RectTransform>();

            if (ajustes.BarraEstadoEnPila)
            {
                // Una fila más de la pila, que pide su alto. Anclarla a mano aquí la
                // dejaba encima de la cabecera.
                le.minHeight       = ajustes.AlturaBarraEstado;
                le.preferredHeight = ajustes.AlturaBarraEstado;
            }
            else
            {
                // La pantalla coloca sus zonas con anclajes, así que la barra se ancla
                // arriba como una más. Quien la use tiene que haber bajado ya su
                // cabecera, o quedarían una encima de otra.
                le.ignoreLayout = true;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(0f, -ajustes.AlturaBarraEstado);
                rt.offsetMax = Vector2.zero;
            }

            var img = _barraEstado.GetComponent<Image>();
            img.color         = ajustes.ColorBarraEstado;
            img.raycastTarget = false;

            // Reloj (izq).
            var relojGO = new GameObject("Reloj", typeof(RectTransform), typeof(TextMeshProUGUI));
            relojGO.transform.SetParent(_barraEstado.transform, false);
            var relojRT = relojGO.GetComponent<RectTransform>();
            relojRT.anchorMin = new Vector2(0f, 0f); relojRT.anchorMax = new Vector2(0f, 1f);
            relojRT.pivot     = new Vector2(0f, 0.5f);
            relojRT.anchoredPosition = new Vector2(18f, 0f);
            relojRT.sizeDelta        = new Vector2(160f, 0f);

            _reloj = relojGO.GetComponent<TextMeshProUGUI>();
            _reloj.font          = FishyUIKit.Cuerpo;
            _reloj.fontSize      = ajustes.TamanoReloj;
            _reloj.color         = Color.white;
            _reloj.alignment     = TextAlignmentOptions.MidlineLeft;
            _reloj.raycastTarget = false;
            _reloj.text          = System.DateTime.Now.ToString("HH:mm");

            CrearIconosEstado(_barraEstado.transform);

            _rutinaReloj = StartCoroutine(RutinaReloj());
        }

        /// <summary>
        /// Iconos de la barra de estado, dibujados con rectángulos en vez de con
        /// caracteres.
        ///
        /// Antes eran el texto "▲▲▲ WiFi 🔋" y salía "▲▲▲ WiFi □": la fuente de
        /// cuerpo es estática y trae 250 caracteres —Latin-1 y poco más—, así que no
        /// tiene ni los triángulos (venían de una fuente de respaldo) ni la batería,
        /// que además es U+1F50B, fuera del BMP. Es el mismo callejón que la lupa del
        /// Modo Detective. Dibujarlos no depende de ninguna fuente.
        /// </summary>
        private static void CrearIconosEstado(Transform barra)
        {
            var cont = new GameObject("Iconos", typeof(RectTransform));
            cont.transform.SetParent(barra, false);
            var rt = cont.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-16f, 0f);
            rt.sizeDelta        = new Vector2(96f, 22f);

            // Cobertura: tres barras crecientes, alineadas por su base.
            for (int i = 0; i < 3; i++)
            {
                float alto = 6f + i * 4f;
                Rectangulo(cont.transform, $"Senal{i}",
                    x: -85f + i * 9f, y: -(14f - alto) / 2f,
                    ancho: 6f, alto: alto, alfa: 0.9f);
            }

            // WiFi: tres trazos de ancho creciente hacia arriba, centrados en un
            // mismo eje para que se lean como abanico y no como escalera.
            for (int i = 0; i < 3; i++)
            {
                float ancho = 6f + i * 6f;
                Rectangulo(cont.transform, $"Wifi{i}",
                    x: -54f + ancho / 2f, y: -6f + i * 5f,
                    ancho: ancho, alto: 3f, alfa: 0.9f - i * 0.15f);
            }

            // Batería: carcasa, carga dentro y borne pegado al cuerpo.
            Rectangulo(cont.transform, "BateriaBorde", x: -5f,  y: 0f, ancho: 30f, alto: 15f, alfa: 0.45f);
            Rectangulo(cont.transform, "BateriaCarga", x: -8f,  y: 0f, ancho: 24f, alto: 9f,  alfa: 0.95f);
            Rectangulo(cont.transform, "BateriaBorne", x: -2f,  y: 0f, ancho: 3f,  alto: 6f,  alfa: 0.45f);
        }

        /// <summary>Rectangulito blanco de la barra de estado, centrado en (x, y)
        /// respecto al borde derecho del contenedor.</summary>
        private static void Rectangulo(Transform parent, string nombre,
            float x, float y, float ancho, float alto, float alfa)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta        = new Vector2(ancho, alto);

            var img = go.GetComponent<Image>();
            FishyUIKit.FondoRedondeado(img, new Color(1f, 1f, 1f, alfa), radio: 3);
            img.raycastTarget = false;
        }

        private void CrearBarraInicio(RectTransform pantalla, Ajustes ajustes)
        {
            _barraInicio = new GameObject("BarraInicio",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            _barraInicio.transform.SetParent(pantalla, false);

            // Flota sobre el borde inferior como en un celular de verdad, sin entrar
            // en el reparto de alto de la pantalla.
            _barraInicio.GetComponent<LayoutElement>().ignoreLayout = true;

            var rt = _barraInicio.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.25f, 0f);
            rt.anchorMax = new Vector2(0.75f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, ajustes.MargenBarraInicio);
            rt.sizeDelta        = new Vector2(0f, 8f);

            var img = _barraInicio.GetComponent<Image>();
            img.color         = new Color(1f, 1f, 1f, 0.35f);
            img.raycastTarget = false;
        }

        /// <summary>
        /// Unity para las corrutinas al desactivar el objeto y no las reanuda sola.
        /// El detective apaga el teléfono entero mientras Otto pide permiso, así que
        /// sin esto el reloj volvería congelado en la hora en que se construyó la
        /// pantalla, que puede ser de hace rato.
        /// </summary>
        private void OnEnable()
        {
            if (_reloj == null || _rutinaReloj != null) return;
            _rutinaReloj = StartCoroutine(RutinaReloj());
        }

        private void OnDisable()
        {
            // Ya la paró Unity al desactivar; queda anotado para que OnEnable la
            // vuelva a lanzar.
            _rutinaReloj = null;
        }

        private IEnumerator RutinaReloj()
        {
            while (true)
            {
                if (_reloj != null) _reloj.text = System.DateTime.Now.ToString("HH:mm");
                yield return new WaitForSecondsRealtime(30f);
            }
        }
    }
}
