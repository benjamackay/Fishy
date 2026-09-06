using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Paleta de Fishy, sacada de los sprites del juego. Es la única copia: los
    /// temas de cada pantalla componen a partir de aquí en vez de repetir hex.
    /// </summary>
    public static class Paleta
    {
        public static Color Hex(int rgb, float alfa = 1f) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            alfa);

        public static readonly Color MarronOscuro = Hex(0x33211A);
        public static readonly Color Marron       = Hex(0x3E281F);
        public static readonly Color MarronMedio  = Hex(0x4A3226);
        public static readonly Color MarronClaro  = Hex(0x5A4234);
        public static readonly Color MarronSuave  = Hex(0x7A5A42);
        public static readonly Color Madera       = Hex(0x8A5E3F);
        public static readonly Color Arena        = Hex(0xBD926F);
        public static readonly Color Crema        = Hex(0xF4CBA5);
        public static readonly Color Rojo         = Hex(0xC0392B);
        public static readonly Color Verde        = Hex(0x5E8C4A);
    }

    /// <summary>
    /// Piezas de UI compartidas por las pantallas de conversación: el sprite de
    /// esquinas redondeadas, las fuentes, los textos, las burbujas y los botones.
    ///
    /// Existe para que el chat de NPCs y el diálogo de NPCs neutros se vean como
    /// el Modo Detective sin copiar su código. Solo aporta la FORMA: los colores y
    /// las medidas los pone cada pantalla, porque cada una tiene su propio tamaño
    /// y sus propios tonos.
    ///
    /// Todo es estático y cacheado: el sprite redondeado es una textura de 64x64
    /// (16 KB) que se dibuja una vez y la comparten todas las pantallas. No se
    /// destruye a propósito —si una pantalla la soltara al cerrarse, la siguiente
    /// se quedaría sin fondo—.
    /// </summary>
    public static class FishyUIKit
    {
        // ── Fuentes ───────────────────────────────────────────────────────────

        /// <summary>Mango, la fuente de la marca. Para títulos y botones.</summary>
        public const string RutaTitulos = "Fonts & Materials/Mango-Regular.bc40fb1accca30c3fca322d704";

        /// <summary>Respaldo legible, para el cuerpo de los mensajes.</summary>
        public const string RutaCuerpo = "Fonts & Materials/LiberationSans SDF";

        private static TMP_FontAsset _titulos, _cuerpo;
        private static bool _fuentesCargadas;

        public static TMP_FontAsset Titulos { get { CargarFuentes(); return _titulos; } }
        public static TMP_FontAsset Cuerpo  { get { CargarFuentes(); return _cuerpo;  } }

        private static void CargarFuentes()
        {
            if (_fuentesCargadas) return;
            _fuentesCargadas = true;

            _cuerpo  = Resources.Load<TMP_FontAsset>(RutaCuerpo);
            _titulos = Resources.Load<TMP_FontAsset>(RutaTitulos);

            if (_cuerpo == null)
            {
                _cuerpo = _titulos ?? TMP_Settings.defaultFontAsset;
                Debug.LogWarning($"[FishyUI] No encontré la fuente de cuerpo en " +
                                 $"'{RutaCuerpo}'. Uso la de respaldo.");
            }
            if (_titulos == null)
            {
                // El asset de Mango tiene puntos en el nombre, que es lo que suele
                // romper Resources.Load. Si esto salta, renombrarlo a algo plano.
                Debug.LogWarning($"[FishyUI] No encontré Mango en '{RutaTitulos}'. " +
                                 "Los títulos van con la fuente de cuerpo.");
                _titulos = _cuerpo;
            }
        }

        /// <summary>
        /// Se le pregunta a Mango si puede escribir el texto y, si le falta algún
        /// carácter, se cae a la de cuerpo. Preguntar en vez de asumir evita el bug
        /// clásico: un carácter sin glifo sale como un cuadrito roto y nadie se
        /// entera hasta verlo en pantalla.
        /// </summary>
        public static TMP_FontAsset FuentePara(string texto)
        {
            CargarFuentes();
            if (_titulos == null || string.IsNullOrEmpty(texto)) return _cuerpo;
            return _titulos.HasCharacters(texto) ? _titulos : _cuerpo;
        }

        // ── Sprite de esquinas redondeadas ────────────────────────────────────

        private static readonly Dictionary<int, Sprite> _redondeados = new Dictionary<int, Sprite>();

        /// <summary>
        /// 9-slice de esquinas redondeadas, dibujado en memoria y cacheado.
        ///
        /// No se pide a Unity con Resources.GetBuiltinResource&lt;Sprite&gt;("UI/Skin/
        /// UISprite.psd"): ese método solo llega a "unity default resources" (mallas,
        /// materiales), mientras que los sprites de UI viven en "unity_builtin_extra",
        /// al que únicamente se entra por AssetDatabase y solo desde el editor. En un
        /// build nunca habría funcionado.
        /// </summary>
        public static Sprite SpriteRedondeado(int lado = 64, int radio = 22)
        {
            lado  = Mathf.Max(2, lado);
            radio = Mathf.Clamp(radio, 0, lado / 2);   // pasado de la mitad las esquinas se pisan

            int clave = lado * 1000 + radio;
            if (_redondeados.TryGetValue(clave, out Sprite cacheado) && cacheado != null)
                return cacheado;

            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
            {
                for (int x = 0; x < lado; x++)
                {
                    // Cuánto se mete el píxel en la zona de esquina. Fuera de las
                    // esquinas ambas dan 0 y el píxel queda opaco.
                    float dx = Mathf.Max(radio - (x + 0.5f), (x + 0.5f) - (lado - radio), 0f);
                    float dy = Mathf.Max(radio - (y + 0.5f), (y + 0.5f) - (lado - radio), 0f);

                    // Medio píxel de suavizado: sin esto la curva sale dentada.
                    float alfa = Mathf.Clamp01(radio - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);

                    // Las cuatro esquinas son iguales, así que da lo mismo que
                    // SetPixels32 vaya de abajo hacia arriba: la forma es simétrica.
                    px[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * 255f));
                }
            }

            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
            {
                name       = $"FishyRedondeado{lado}_{radio}",
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                hideFlags  = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, lado, lado), new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f, extrude: 0, meshType: SpriteMeshType.FullRect,
                border: new Vector4(radio, radio, radio, radio));
            sprite.name      = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            _redondeados[clave] = sprite;
            return sprite;
        }

        /// <summary>Deja el fondo con esquinas redondeadas. Al ser 9-slice, el radio
        /// no se deforma por mucho que se estire la burbuja.</summary>
        public static void FondoRedondeado(Image img, Color color, int lado = 64, int radio = 22)
        {
            if (img == null) return;
            img.sprite = SpriteRedondeado(lado, radio);
            img.type   = Image.Type.Sliced;
            img.color  = color;
        }

        // ── Texto ─────────────────────────────────────────────────────────────

        /// <summary>Crea un texto TMP ya configurado. La fuente se decide sola según
        /// los caracteres que trae el texto (ver <see cref="FuentePara"/>).</summary>
        public static TextMeshProUGUI Texto(Transform parent, string nombre, string texto,
            float tamano, Color color, TextAlignmentOptions alineacion)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var t = go.GetComponent<TextMeshProUGUI>();
            t.font             = FuentePara(texto);
            t.fontSize         = tamano;
            t.color            = color;
            t.alignment        = alineacion;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode     = TextOverflowModes.Overflow;
            t.text             = texto;
            return t;
        }

        /// <summary>Crema o marrón según lo claro que sea el fondo, para que el texto
        /// siempre se lea. Se mide por luminancia y no por "a ojo".</summary>
        public static Color TextoSobre(Color fondo)
        {
            float luz = 0.299f * fondo.r + 0.587f * fondo.g + 0.114f * fondo.b;
            return luz > 0.6f ? Paleta.Marron : Paleta.Crema;
        }

        // ── Piezas ────────────────────────────────────────────────────────────

        public static void Estirar(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        /// <summary>
        /// Botón con el fondo redondeado y el texto en Mango. El color del texto se
        /// calcula del fondo, así que un botón claro no queda con letra crema ilegible.
        ///
        /// El alto lo manda el texto, no al revés: las opciones del chat son frases
        /// enteras que envuelven a dos o tres líneas, y antes el texto se estiraba
        /// sobre un botón de altura fija y se desbordaba encima de los vecinos.
        /// <paramref name="alturaMin"/> es solo un suelo, para que un botón de una
        /// palabra no quede raquítico.
        /// </summary>
        public static Button Boton(Transform parent, string texto, Color fondo,
            float tamanoTexto, float alturaMin, Action onClick)
        {
            var go = new GameObject("Btn_" + texto,
                typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);

            FondoRedondeado(go.GetComponent<Image>(), fondo);
            go.GetComponent<LayoutElement>().minHeight = alturaMin;

            // El grupo mide el texto y le pide esa altura al contenedor de arriba.
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 14, 14);
            vlg.childAlignment         = TextAnchor.MiddleCenter;
            vlg.childControlWidth      = true; vlg.childControlHeight      = true;
            vlg.childForceExpandWidth  = true; vlg.childForceExpandHeight  = false;

            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            Texto(go.transform, "Text", texto, tamanoTexto,
                TextoSobre(fondo), TextAlignmentOptions.Center);

            return btn;
        }

        /// <summary>
        /// Aspa de cerrar, dibujada con dos barras cruzadas.
        ///
        /// No es un carácter porque no hay ninguno disponible: ✕ (U+2715) no está ni
        /// en Mango ni en la fuente de cuerpo —que es estática y trae 250 caracteres,
        /// Latin-1 y poco más—, así que salía un cuadro hueco. Quedaban × (U+00D7) y
        /// la X ASCII, pero son signos de texto: finos, con el peso de una letra y
        /// alineados a la línea base, no al centro del botón. Dibujarla da el grosor
        /// y el centrado exactos, y no depende de ninguna fuente.
        /// </summary>
        public static void Aspa(Transform parent, Color color, float largo, float grosor)
        {
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"Aspa{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(largo, grosor);
                rt.localRotation    = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);

                var img = go.GetComponent<Image>();
                // El radio es medio grosor: así las puntas quedan redondeadas en vez
                // de en pico, que a este tamaño se ve sucio.
                FondoRedondeado(img, color, radio: Mathf.Max(1, Mathf.RoundToInt(grosor / 2f)));
                img.raycastTarget = false;   // el clic es del botón, no de las barras
            }
        }

        /// <summary>
        /// Las piezas de un panel de diálogo, para que quien lo construya pueda
        /// escribir en ellas después.
        /// </summary>
        public class PanelDialogo
        {
            public GameObject      Raiz;
            public TextMeshProUGUI Nombre;
            public TextMeshProUGUI Texto;
            /// <summary>Línea de la última respuesta del jugador. En un panel no hay
            /// historial, así que sin esto lo que el niño/a contesta se pierde en el
            /// mismo frame en que el NPC responde.</summary>
            public TextMeshProUGUI Respuesta;
            /// <summary>Dónde van los botones de opción, si la pantalla los usa. El
            /// diálogo de NPCs neutros lo deja vacío: no tiene opciones.</summary>
            public RectTransform   Opciones;
        }

        /// <summary>
        /// Panel de diálogo pegado abajo: nombre, texto, la última respuesta del
        /// jugador y un hueco para opciones. Es el aspecto de "hablar en persona",
        /// el que comparten los NPCs neutros y los sospechosos cara a cara.
        ///
        /// Crece hacia arriba según el contenido, así que da igual cuántas opciones
        /// traiga el nodo.
        /// </summary>
        public static PanelDialogo CrearPanelDialogo(Transform parent, Color fondo,
            Color colorNombre, Color colorTexto, Color colorRespuesta,
            float ancho, float tamNombre, float tamTexto, float tamRespuesta,
            float margenInferior, int radio = 22)
        {
            var refs = new PanelDialogo();

            var panel = new GameObject("PanelDialogo",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            panel.transform.SetParent(parent, false);
            refs.Raiz = panel;

            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, margenInferior);
            rt.sizeDelta = new Vector2(ancho, 0f);

            FondoRedondeado(panel.GetComponent<Image>(), fondo, radio: radio);

            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(36, 36, 28, 28);
            vlg.spacing = 12f;
            vlg.childControlWidth      = true; vlg.childControlHeight      = true;
            vlg.childForceExpandWidth  = true; vlg.childForceExpandHeight  = false;

            // Solo la altura se ajusta al contenido: el ancho lo fija el diseño, o
            // el panel se encogería hasta el largo de la frase más corta.
            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            refs.Nombre = Texto(panel.transform, "Nombre", "", tamNombre,
                colorNombre, TextAlignmentOptions.TopLeft);
            refs.Nombre.fontStyle = FontStyles.Bold;

            refs.Texto = Texto(panel.transform, "Texto", "", tamTexto,
                colorTexto, TextAlignmentOptions.TopLeft);

            refs.Respuesta = Texto(panel.transform, "Respuesta", "", tamRespuesta,
                colorRespuesta, TextAlignmentOptions.TopLeft);
            refs.Respuesta.fontStyle = FontStyles.Italic;
            refs.Respuesta.gameObject.SetActive(false);   // no ocupa sitio hasta que haya respuesta

            var opcionesGO = new GameObject("Opciones",
                typeof(RectTransform), typeof(VerticalLayoutGroup));
            opcionesGO.transform.SetParent(panel.transform, false);
            refs.Opciones = opcionesGO.GetComponent<RectTransform>();

            var ovlg = opcionesGO.GetComponent<VerticalLayoutGroup>();
            ovlg.spacing = 10f;
            ovlg.padding = new RectOffset(0, 0, 12, 0);
            ovlg.childControlWidth      = true; ovlg.childControlHeight      = true;
            ovlg.childForceExpandWidth  = true; ovlg.childForceExpandHeight  = false;

            return refs;
        }

        /// <summary>
        /// Burbuja de chat: una fila alineada a un lado y, dentro, la burbuja con su
        /// autor opcional y el texto. Devuelve el texto para poder animarlo después.
        /// </summary>
        public static TextMeshProUGUI Burbuja(Transform contenedor, string texto, string autor,
            bool izquierda, Color fondo, float anchoMax, float tamanoTexto, float tamanoAutor)
        {
            var fila = new GameObject(izquierda ? "FilaIzq" : "FilaDer",
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            fila.transform.SetParent(contenedor, false);

            var hlg = fila.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = izquierda ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            hlg.childControlWidth     = true; hlg.childControlHeight     = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var burbuja = new GameObject("Burbuja",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter), typeof(LayoutElement));
            burbuja.transform.SetParent(fila.transform, false);

            FondoRedondeado(burbuja.GetComponent<Image>(), fondo);

            var vlg = burbuja.GetComponent<VerticalLayoutGroup>();
            vlg.padding                = new RectOffset(24, 24, 16, 16);
            vlg.spacing                = 4f;
            vlg.childControlWidth      = true; vlg.childControlHeight      = true;
            vlg.childForceExpandWidth  = true; vlg.childForceExpandHeight  = false;

            var fitter = burbuja.GetComponent<ContentSizeFitter>();
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            burbuja.GetComponent<LayoutElement>().preferredWidth = anchoMax;

            Color colorTexto = TextoSobre(fondo);

            if (!string.IsNullOrEmpty(autor))
            {
                var a = Texto(burbuja.transform, "Autor", autor, tamanoAutor,
                    new Color(colorTexto.r, colorTexto.g, colorTexto.b, 0.75f),
                    TextAlignmentOptions.TopLeft);
                a.fontStyle = FontStyles.Bold;
            }

            return Texto(burbuja.transform, "Texto", texto, tamanoTexto,
                colorTexto, TextAlignmentOptions.TopLeft);
        }
    }
}
