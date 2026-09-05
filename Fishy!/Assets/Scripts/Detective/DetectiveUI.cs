using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Col = Fishy.Detective.DetectiveUITheme.Colores;
using Med = Fishy.Detective.DetectiveUITheme.Medidas;
using Fnt = Fishy.Detective.DetectiveUITheme.Fuente;
using Txt = Fishy.Detective.DetectiveUITheme.Textos;
using Spr = Fishy.Detective.DetectiveUITheme.Sprites;

namespace Fishy.Detective
{
    /// <summary>
    /// HDU-10 — Interfaz del modo detective.
    /// Burbujas izquierda/derecha según autor. Click en burbuja = marcar/desmarcar con borde rojo.
    /// Se autogenera en runtime sin prefabs ni referencias en inspector.
    ///
    /// Usa TextMeshPro (no el Text legacy): a los tamaños grandes que pide el
    /// diseño, el Text legacy se ve borroso porque escala un bitmap, mientras que
    /// TMP renderiza desde SDF y se mantiene nítido. Además es lo que ya usa el
    /// resto del juego (menú del Tab, panel de diálogo).
    /// </summary>
    public class DetectiveUI : MonoBehaviour
    {
        public static DetectiveUI Instance { get; private set; }

        // ── Referencias runtime ───────────────────────────────────────────────
        private GameObject       _window;
        private RectTransform    _content;
        private ScrollRect       _scrollRect;
        private GameObject       _panelResultado;
        private TextMeshProUGUI  _txtMarcador;
        private TextMeshProUGUI  _txtResultado;
        private Button           _btnConfirmar;
        private Button           _btnRepetir;
        private Button           _btnVerExplicacion;
        private RectTransform    _contenedorExplicaciones;

        // ── Fuentes ───────────────────────────────────────────────────────────
        private TMP_FontAsset _fontTitulos;   // Mango, la fuente de la marca
        private TMP_FontAsset _fontCuerpo;    // respaldo por si a Mango le falta un glifo
        private TMP_FontAsset _fontIconos;    // símbolos que no están en las otras dos
        private Sprite        _spriteRedondeado;

        // ── Ritual de permiso (HDU-10 CA1) ───────────────────────────────────
        private GameObject    _panelPermiso;
        private RectTransform _permisoBurbujas;
        private Button        _btnContinuarPermiso;

        // ── Estado ────────────────────────────────────────────────────────────
        private DetectiveCaseManager _manager;
        private Action _onCerrar;
        private Action _onRepetir;
        private List<(DetectiveMessage mensaje, string explicacion)> _noIdentificados;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            CargarRecursos();
            BuildRuntimeUI();
            Hide();
        }

        public static DetectiveUI GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("DetectiveUI");
                Instance = go.AddComponent<DetectiveUI>();
            }
            return Instance;
        }

        private void CargarRecursos()
        {
            _fontCuerpo  = Resources.Load<TMP_FontAsset>(DetectiveUITheme.Fuentes.RutaCuerpo);
            _fontTitulos = Resources.Load<TMP_FontAsset>(DetectiveUITheme.Fuentes.RutaTitulos);

            if (_fontCuerpo == null)
            {
                // Sin fuente de cuerpo no se puede escribir en español: si esto
                // pasa, la ruta del theme quedó mal o el asset se movió.
                _fontCuerpo = _fontTitulos ?? TMP_Settings.defaultFontAsset;
                Debug.LogWarning($"[Detective] No encontré la fuente de cuerpo en " +
                                 $"'{DetectiveUITheme.Fuentes.RutaCuerpo}'. Usando la de respaldo.", this);
            }
            if (_fontTitulos == null)
            {
                // El asset de Mango tiene puntos en el nombre, que es justo lo que
                // suele romper Resources.Load. Si esto salta, lo más simple es
                // renombrarlo a algo plano ("Mango") y actualizar la ruta.
                Debug.LogWarning($"[Detective] No encontré la fuente de la marca en " +
                                 $"'{DetectiveUITheme.Fuentes.RutaTitulos}'. Va con la de cuerpo.", this);
                _fontTitulos = _fontCuerpo;
            }

            // Opcional: si no está, el header simplemente va sin icono.
            _fontIconos = Resources.Load<TMP_FontAsset>(DetectiveUITheme.Fuentes.RutaIconos);

            _spriteRedondeado = CrearSpriteRedondeado(Spr.LadoRedondeado, Spr.RadioRedondeado);
        }

        /// <summary>
        /// Dibuja en memoria el 9-slice de esquinas redondeadas.
        ///
        /// Antes esto pedía el sprite de Unity con
        /// Resources.GetBuiltinResource&lt;Sprite&gt;("UI/Skin/UISprite.psd") y fallaba
        /// siempre: ese método solo llega a "unity default resources" (mallas,
        /// materiales), mientras que los sprites de UI viven en "unity_builtin_extra",
        /// al que únicamente se entra por AssetDatabase y solo desde el editor. O sea
        /// que ni con suerte habría funcionado en un build.
        ///
        /// Generarlo sale más barato que arrastrar un asset: son cuatro arcos y el
        /// borde del 9-slice hace que el radio no se deforme al estirar la burbuja.
        /// </summary>
        private static Sprite CrearSpriteRedondeado(int lado, int radio)
        {
            lado  = Mathf.Max(2, lado);
            radio = Mathf.Clamp(radio, 0, lado / 2);   // pasado de la mitad las esquinas se pisan

            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
            {
                name       = "DetectiveRedondeado",
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                // Que no quede colgando en la escena ni se guarde al salir del play.
                hideFlags  = HideFlags.HideAndDontSave,
            };

            var px = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
            {
                for (int x = 0; x < lado; x++)
                {
                    // Cuánto se mete el píxel en la zona de esquina. Fuera de las
                    // esquinas ambas dan 0 y el píxel queda opaco.
                    float dx = Mathf.Max(radio - (x + 0.5f), (x + 0.5f) - (lado - radio), 0f);
                    float dy = Mathf.Max(radio - (y + 0.5f), (y + 0.5f) - (lado - radio), 0f);

                    // Medio píxel de suavizado a cada lado del arco: sin esto la
                    // curva sale dentada.
                    float alfa = Mathf.Clamp01(radio - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);

            // El borde de 9-slice es justo el radio: las esquinas se copian tal cual
            // y solo se estiran los tramos rectos del medio.
            return Sprite.Create(tex, new Rect(0f, 0f, lado, lado), new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f, extrude: 0, meshType: SpriteMeshType.FullRect,
                border: new Vector4(radio, radio, radio, radio));
        }

        /// <summary>La textura del sprite se crea a mano, así que hay que soltarla a
        /// mano: Unity no recoge lo que se marcó DontSave.</summary>
        private void OnDestroy()
        {
            if (_spriteRedondeado == null) return;

            Texture2D tex = _spriteRedondeado.texture;
            Destroy(_spriteRedondeado);
            if (tex != null) Destroy(tex);
        }

        /// <summary>
        /// Se le pregunta a la fuente de la marca si puede escribir el texto y,
        /// si le falta algún carácter, se cae a la de cuerpo. Preguntar en vez de
        /// asumir evita el bug clásico: un carácter sin glifo se dibuja como un
        /// cuadrito roto y nadie se entera hasta que aparece en pantalla.
        /// </summary>
        private TMP_FontAsset FuentePara(string texto)
        {
            if (_fontTitulos == null || string.IsNullOrEmpty(texto)) return _fontCuerpo;
            return _fontTitulos.HasCharacters(texto) ? _fontTitulos : _fontCuerpo;
        }

        /// <summary>
        /// ¿Esta fuente puede dibujar el símbolo? Se pregunta por code point y no
        /// con HasCharacters(string), que aquí falla por dos motivos distintos:
        ///
        ///   • 🔍 es U+1F50D, fuera del BMP: en C# son dos char (surrogate pair) y
        ///     HasCharacters los revisa por separado, así que da false siempre,
        ///     tenga la fuente el glifo o no.
        ///   • la fuente de iconos se genera en modo Dynamic, o sea que nace con
        ///     la tabla de caracteres VACÍA y rasteriza cada glifo la primera vez
        ///     que se pide. Preguntarle a la tabla, entonces, no dice nada sobre
        ///     lo que el TTF realmente tiene.
        ///
        /// TryAddCharacters hace justo lo que falta: rasteriza el glifo si el TTF
        /// lo trae, y lo devuelve en 'faltantes' si no.
        /// </summary>
        private static bool PuedeDibujar(TMP_FontAsset fuente, string simbolo)
        {
            if (fuente == null || string.IsNullOrEmpty(simbolo)) return false;

            // Sin ConvertToUtf32 directo: revienta si el texto del theme quedó con
            // medio surrogate suelto, que es fácil al editarlo a mano.
            uint punto = simbolo.Length >= 2 && char.IsSurrogatePair(simbolo[0], simbolo[1])
                ? (uint)char.ConvertToUtf32(simbolo[0], simbolo[1])
                : simbolo[0];

            // Una fuente Static ya trae todos sus glifos horneados y no acepta
            // agregados, así que ahí sí corresponde mirar la tabla.
            if (fuente.atlasPopulationMode == AtlasPopulationMode.Static)
                return fuente.HasCharacter((int)punto);

            return fuente.TryAddCharacters(new[] { punto }, out uint[] faltantes)
                   && (faltantes == null || faltantes.Length == 0);
        }

        // ── API pública ───────────────────────────────────────────────────────

        public void Inicializar(DetectiveCaseManager manager, Action onCerrar, Action onRepetir)
        {
            _manager   = manager;
            _onCerrar  = onCerrar;
            _onRepetir = onRepetir;
        }

        /// <summary>
        /// Ritual de permiso previo al caso (HDU-10 CA1): Otto le pide permiso al
        /// NPC para revisar su conversación, y el NPC autoriza explícitamente
        /// pidiendo ayuda para identificar señales de riesgo. Al continuar, recién
        /// ahí se abre el bloque de conversación observada.
        /// </summary>
        public void MostrarPermiso(DetectiveCase caso, Action onContinuar)
        {
            LimpiarPermiso();
            _panelResultado.SetActive(false);
            _window.SetActive(true);
            _panelPermiso.SetActive(true);
            _panelPermiso.transform.SetAsLastSibling();

            CrearBurbujaPermiso(caso.permisoPlayerText, "Otto", esIzquierda: false, Col.BurbujaOtto);
            CrearBurbujaPermiso(caso.permisoNpcResponse, caso.permisoNpcNombre, esIzquierda: true, Col.BurbujaIzquierda);

            _btnContinuarPermiso.onClick.RemoveAllListeners();
            _btnContinuarPermiso.onClick.AddListener(() =>
            {
                _panelPermiso.SetActive(false);
                onContinuar?.Invoke();
            });
        }

        public void MostrarConversacion()
        {
            LimpiarHistorial();
            LimpiarExplicaciones();
            _panelResultado.SetActive(false);
            _panelPermiso.SetActive(false);
            _btnConfirmar.interactable = false;
            _window.SetActive(true);
            StartCoroutine(ReproducirMensajes(_manager.GetMensajes()));
        }

        public void Hide()
        {
            if (_window != null) _window.SetActive(false);
        }

        // ── Reproducción ──────────────────────────────────────────────────────

        private IEnumerator ReproducirMensajes(List<DetectiveMessage> mensajes)
        {
            foreach (var msg in mensajes)
            {
                CrearBurbuja(msg);
                yield return new WaitForSeconds(DetectiveUITheme.Ritmo.RetardoEntreMensajes);
            }
            _btnConfirmar.interactable = true;
        }

        private void CrearBurbuja(DetectiveMessage msg)
        {
            bool esIzquierda = msg.autor == _manager.GetMensajes()[0].autor;
            Color colorBase  = esIzquierda ? Col.BurbujaIzquierda : Col.BurbujaDerecha;

            // ── Fila ─────────────────────────────────────────────────────────
            var row = new GameObject("Row_" + msg.id,
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_content, false);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = esIzquierda ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            hlg.childControlWidth     = true; hlg.childControlHeight     = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.padding = Med.PaddingFila;

            // Spacer lado contrario para empujar la burbuja
            if (!esIzquierda) AgendarSpacer(row.transform);

            // ── Burbuja ───────────────────────────────────────────────────────
            var bubble = new GameObject("Bubble",
                typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement),
                typeof(Button));                          // Button para capturar el click
            bubble.transform.SetParent(row.transform, false);

            var bubbleImg = bubble.GetComponent<Image>();
            AplicarFondoRedondeado(bubbleImg, colorBase);

            // Borde (Outline component) — invisible por defecto, rojo al marcar
            var outline = bubble.AddComponent<Outline>();
            outline.effectColor    = Color.clear;
            outline.effectDistance = new Vector2(Med.GrosorBordeMarcado, -Med.GrosorBordeMarcado);
            outline.useGraphicAlpha = false;

            var vlg = bubble.GetComponent<VerticalLayoutGroup>();
            vlg.padding = Med.PaddingBurbuja;
            vlg.spacing = Med.EspaciadoBurbuja;
            vlg.childControlWidth     = true; vlg.childControlHeight     = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var fitter = bubble.GetComponent<ContentSizeFitter>();
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            var le = bubble.GetComponent<LayoutElement>();
            le.preferredWidth = Med.AnchoBurbuja;
            le.flexibleWidth  = Med.FlexBurbuja;

            // Quitar visual de hover/pressed del Button (solo queremos el click)
            var btn = bubble.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = colorBase;
            colors.pressedColor     = colorBase;
            colors.selectedColor    = colorBase;
            btn.colors = colors;
            btn.transition = Selectable.Transition.None;

            if (esIzquierda) AgendarSpacer(row.transform);

            // ── Contenido de la burbuja ───────────────────────────────────────

            // Nombre del autor (solo burbuja izquierda)
            if (esIzquierda)
            {
                var at = CrearTexto(bubble.transform, "Autor", msg.autor, Fnt.AutorBurbuja,
                    Col.TextoSuave, TextAlignmentOptions.TopLeft);
                at.fontStyle = FontStyles.Bold;
            }

            // Texto del mensaje
            CrearTexto(bubble.transform, "Texto", msg.texto, Fnt.TextoBurbuja,
                Col.Texto, TextAlignmentOptions.TopLeft);

            // Timestamp
            CrearTexto(bubble.transform, "Time", DateTime.Now.ToString("HH:mm"), Fnt.Hora,
                new Color(Col.Texto.r, Col.Texto.g, Col.Texto.b, Col.AlfaTextoSecundario),
                TextAlignmentOptions.Right);

            // ── Click para marcar/desmarcar ───────────────────────────────────
            btn.onClick.AddListener(() =>
            {
                _manager.ToggleMarca(msg.id);
                bool marcado = _manager.EstaMarcado(msg.id);

                // Borde rojo visible / invisible
                outline.effectColor = marcado ? Col.BordeMarcado : Color.clear;

                // Leve tinte rojo en la burbuja al marcar
                bubbleImg.color = marcado
                    ? Color.Lerp(colorBase, Col.BordeMarcado, Col.FuerzaTinteMarcado)
                    : colorBase;
            });

            ScrollToBottom();
        }

        private void CrearBurbujaPermiso(string texto, string autor, bool esIzquierda, Color colorBase)
        {
            var row = new GameObject("RowPermiso", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_permisoBurbujas, false);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = esIzquierda ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            hlg.childControlWidth     = true; hlg.childControlHeight     = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            if (!esIzquierda) AgendarSpacer(row.transform);

            var bubble = new GameObject("Bubble",
                typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            bubble.transform.SetParent(row.transform, false);
            AplicarFondoRedondeado(bubble.GetComponent<Image>(), colorBase);
            var vlg = bubble.GetComponent<VerticalLayoutGroup>();
            vlg.padding = Med.PaddingBurbujaPermiso; vlg.spacing = Med.EspaciadoBurbujaPermiso;
            vlg.childControlWidth     = true; vlg.childControlHeight     = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = bubble.GetComponent<ContentSizeFitter>();
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bubble.GetComponent<LayoutElement>().preferredWidth = Med.AnchoBurbujaPermiso;

            var at = CrearTexto(bubble.transform, "Autor", autor, Fnt.AutorPermiso,
                new Color(Col.Texto.r, Col.Texto.g, Col.Texto.b, Col.AlfaAutorPermiso),
                TextAlignmentOptions.TopLeft);
            at.fontStyle = FontStyles.Bold;

            CrearTexto(bubble.transform, "Texto", texto, Fnt.TextoPermiso,
                Col.Texto, TextAlignmentOptions.TopLeft);

            if (esIzquierda) AgendarSpacer(row.transform);
        }

        private static void AgendarSpacer(Transform parent)
        {
            var sp = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            sp.transform.SetParent(parent, false);
            var le = sp.GetComponent<LayoutElement>();
            le.minWidth      = Med.AnchoEspaciador;
            le.flexibleWidth = 1f;
        }

        // ── Confirmar ─────────────────────────────────────────────────────────

        private void OnConfirmar()
        {
            var resultado = _manager.CalcularResultado();
            MostrarResultado(resultado);
        }

        private void MostrarResultado(DetectiveCaseResult r)
        {
            _panelResultado.SetActive(true);
            _panelResultado.transform.SetAsLastSibling();

            _txtMarcador.text = $"{r.aciertos} / {r.totalRiesgo}";
            _txtMarcador.gameObject.SetActive(r.totalRiesgo > 0);

            _txtResultado.text = r.totalRiesgo > 0
                ? Txt.ResultadoConSenales
                : Txt.ResultadoSinSenales;
            _txtResultado.font = FuentePara(_txtResultado.text);

            // Repetir solo se ofrece si le fue mal (< 50%); la explicación, siempre.
            _btnRepetir.gameObject.SetActive(r.DebeOfrecerRepetir);
            _btnVerExplicacion.gameObject.SetActive(true);
            _noIdentificados = r.noIdentificados;
        }

        private void MostrarExplicaciones()
        {
            LimpiarExplicaciones();
            foreach (var (msg, exp) in _noIdentificados)
            {
                var item = new GameObject("Exp_" + msg.id,
                    typeof(RectTransform), typeof(Image),
                    typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                item.transform.SetParent(_contenedorExplicaciones, false);
                AplicarFondoRedondeado(item.GetComponent<Image>(),
                    new Color(Col.BotonExplicacion.r, Col.BotonExplicacion.g,
                              Col.BotonExplicacion.b, Col.AlfaExplicacion));
                var vlg = item.GetComponent<VerticalLayoutGroup>();
                vlg.padding = Med.PaddingExplicacion;
                vlg.childControlWidth = true; vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                item.GetComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;

                CrearTexto(item.transform, "Txt", $"<b>\"{msg.texto}\"</b>\n{exp}",
                    Fnt.Explicacion, Col.Texto, TextAlignmentOptions.TopLeft);
            }
            _btnVerExplicacion.interactable = false;
        }

        // ── Construcción de UI en runtime ─────────────────────────────────────

        private void BuildRuntimeUI()
        {
            Fishy.UI.UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("DetectiveCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            _window = new GameObject("Root", typeof(RectTransform));
            _window.transform.SetParent(canvasGO.transform, false);
            Stretch(_window.GetComponent<RectTransform>());

            // Backdrop
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(_window.transform, false);
            Stretch(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = Col.Backdrop;

            // Panel principal
            var panel = new GameObject("ChatWindow", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_window.transform, false);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot     = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = Med.Ventana;
            AplicarFondoRedondeado(panel.GetComponent<Image>(), Col.Ventana);

            BuildHeader(panel.transform);
            BuildScroll(panel.transform);
            BuildBarraInferior(panel.transform);
            BuildPanelResultado(_window.transform);
            BuildPanelPermiso(_window.transform);
        }

        private void BuildPanelPermiso(Transform parent)
        {
            _panelPermiso = new GameObject("PanelPermiso", typeof(RectTransform), typeof(Image));
            _panelPermiso.transform.SetParent(parent, false);
            Stretch(_panelPermiso.GetComponent<RectTransform>());
            _panelPermiso.GetComponent<Image>().color = Col.PanelResultado;

            var card = CrearCard(_panelPermiso.transform, Med.AnchoCardPermiso, Med.PaddingCardPermiso);

            AgregarTitulo(card, Txt.TituloPermiso);

            var burbujasGO = new GameObject("Burbujas",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            burbujasGO.transform.SetParent(card, false);
            _permisoBurbujas = burbujasGO.GetComponent<RectTransform>();
            var bvlg = burbujasGO.GetComponent<VerticalLayoutGroup>();
            bvlg.spacing = Med.EspaciadoEntreBurbujasPermiso;
            bvlg.childControlWidth     = true; bvlg.childControlHeight     = true;
            bvlg.childForceExpandWidth = true; bvlg.childForceExpandHeight = false;
            burbujasGO.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            _btnContinuarPermiso = CrearBotonCard(card, Txt.BotonContinuarPermiso, Col.BotonConfirmar, null);

            _panelPermiso.SetActive(false);
        }

        private void BuildHeader(Transform parent)
        {
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(parent, false);
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, Med.AlturaHeader);
            header.GetComponent<Image>().color = Col.Header;

            bool hayIcono = CrearIconoLupa(header.transform);
            float sangria = hayIcono ? Med.SangriaTextoHeader : Med.SangriaTextoHeaderSinIcono;

            // Título
            var lbl = CrearTexto(header.transform, "Titulo", Txt.TituloHeader, Fnt.TituloHeader,
                Col.Texto, TextAlignmentOptions.BottomLeft);
            lbl.fontStyle = FontStyles.Bold;
            var lblRT = lbl.rectTransform;
            lblRT.anchorMin = new Vector2(0f, 0.5f); lblRT.anchorMax = new Vector2(1f, 1f);
            lblRT.offsetMin = new Vector2(sangria, 0f);
            lblRT.offsetMax = new Vector2(Med.MargenDerechoHeader, 0f);

            // Subtítulo
            var sub = CrearTexto(header.transform, "Sub", Txt.SubtituloHeader, Fnt.SubtituloHeader,
                Col.TextoSuave, TextAlignmentOptions.TopLeft);
            var subRT = sub.rectTransform;
            subRT.anchorMin = new Vector2(0f, 0f); subRT.anchorMax = new Vector2(1f, 0.5f);
            subRT.offsetMin = new Vector2(sangria, 0f);
            subRT.offsetMax = new Vector2(Med.MargenDerechoHeader, 0f);
        }

        /// <summary>
        /// Lupa del header, por orden de preferencia:
        ///   1. un sprite propio en Resources, si Valentina hizo uno;
        ///   2. el glifo 🔍 de la fuente de iconos, que es monocromática y por eso
        ///      se puede teñir con el color de la paleta;
        ///   3. nada, y el título se corre a la izquierda.
        /// Nunca se dibuja el glifo con Mango: no lo tiene y saldría un cuadro roto.
        /// </summary>
        /// <returns>True si se creó el icono.</returns>
        private bool CrearIconoLupa(Transform parent)
        {
            var propio = Resources.Load<Sprite>(Spr.IconoLupa);
            if (propio != null)
            {
                var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
                avatar.transform.SetParent(parent, false);
                ColocarEnRanuraDelIcono(avatar.GetComponent<RectTransform>());

                var img = avatar.GetComponent<Image>();
                img.sprite        = propio;
                img.color         = Color.white;   // sin teñir: el sprite manda
                img.raycastTarget = false;
                return true;
            }

            // Los dos motivos van separados a propósito: juntos no se sabe cuál
            // arreglar, que es exactamente lo que costó encontrar este bug.
            if (_fontIconos == null)
            {
                Debug.LogWarning($"[Detective] El header va sin lupa: no cargó la fuente de " +
                                 $"iconos desde Resources/'{DetectiveUITheme.Fuentes.RutaIconos}'. " +
                                 "Corre Fishy → Generar fuente de iconos.", this);
                return false;
            }

            if (!PuedeDibujar(_fontIconos, Txt.IconoHeader))
            {
                Debug.LogWarning($"[Detective] El header va sin lupa: la fuente '{_fontIconos.name}' " +
                                 $"cargó bien pero no tiene el glifo '{Txt.IconoHeader}' " +
                                 $"(U+{char.ConvertToUtf32(Txt.IconoHeader, 0):X4}). Si cambiaste " +
                                 "IconoHeader en el theme, vuelve a correr Fishy → Generar fuente " +
                                 "de iconos para hornearlo.", this);
                return false;
            }

            var icono = CrearTexto(parent, "Icono", Txt.IconoHeader, Fnt.IconoHeader,
                Col.TextoSuave, TextAlignmentOptions.Center);
            icono.font = _fontIconos;          // se impone a la elección automática
            icono.raycastTarget = false;
            ColocarEnRanuraDelIcono(icono.rectTransform);
            return true;
        }

        /// <summary>Deja el icono pegado al borde izquierdo del header y centrado
        /// en vertical, con el tamaño reservado para él.</summary>
        private static void ColocarEnRanuraDelIcono(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Med.MargenAvatar, 0f);
            rt.sizeDelta        = Med.TamanoAvatar;
        }

        private void BuildScroll(Transform parent)
        {
            var scrollGO = new GameObject("Scroll",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGO.transform.SetParent(parent, false);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0f, 0f); scrollRT.anchorMax = new Vector2(1f, 1f);
            scrollRT.offsetMin = new Vector2(0f, Med.MargenScrollAbajo);
            scrollRT.offsetMax = new Vector2(0f, -Med.MargenScrollArriba);
            scrollGO.GetComponent<Image>().color = Col.Historial;
            _scrollRect = scrollGO.GetComponent<ScrollRect>();
            _scrollRect.horizontal        = false;
            _scrollRect.vertical          = true;
            _scrollRect.scrollSensitivity = 30f;

            var contentGO = new GameObject("Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(scrollGO.transform, false);
            _content = contentGO.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot     = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero; _content.offsetMax = Vector2.zero;
            var cvlg = contentGO.GetComponent<VerticalLayoutGroup>();
            cvlg.spacing  = Med.EspaciadoHistorial; cvlg.padding = Med.PaddingHistorial;
            cvlg.childControlWidth     = true; cvlg.childControlHeight     = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            contentGO.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            _scrollRect.content = _content;
        }

        private void BuildBarraInferior(Transform parent)
        {
            var barra = new GameObject("BarraInferior", typeof(RectTransform), typeof(Image));
            barra.transform.SetParent(parent, false);
            var barraRT = barra.GetComponent<RectTransform>();
            barraRT.anchorMin = new Vector2(0f, 0f); barraRT.anchorMax = new Vector2(1f, 0f);
            barraRT.pivot     = new Vector2(0.5f, 0f);
            barraRT.sizeDelta = new Vector2(0f, Med.AlturaBarraInferior);
            barra.GetComponent<Image>().color = Col.BarraInferior;

            var btnGO = new GameObject("BtnConfirmar",
                typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(barra.transform, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0f, 0f); btnRT.anchorMax = new Vector2(1f, 1f);
            btnRT.offsetMin = Med.MargenBotonBarra;
            btnRT.offsetMax = new Vector2(-Med.MargenBotonBarra.x, -Med.MargenBotonBarra.y);
            AplicarFondoRedondeado(btnGO.GetComponent<Image>(), Col.BotonConfirmar);
            _btnConfirmar = btnGO.GetComponent<Button>();
            _btnConfirmar.onClick.AddListener(OnConfirmar);

            var t = CrearTexto(btnGO.transform, "Text", Txt.BotonConfirmar, Fnt.Boton,
                Col.TextoSobre(Col.BotonConfirmar), TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
        }

        private void BuildPanelResultado(Transform parent)
        {
            _panelResultado = new GameObject("PanelResultado",
                typeof(RectTransform), typeof(Image));
            _panelResultado.transform.SetParent(parent, false);
            Stretch(_panelResultado.GetComponent<RectTransform>());
            _panelResultado.GetComponent<Image>().color = Col.PanelResultado;

            var card = CrearCard(_panelResultado.transform, Med.AnchoCardResultado, Med.PaddingCardResultado);

            AgregarTitulo(card, Txt.TituloResultado);

            _txtMarcador = CrearTexto(card, "Marcador", "", Fnt.Marcador,
                Col.Texto, TextAlignmentOptions.Center);
            _txtMarcador.fontStyle        = FontStyles.Bold;
            _txtMarcador.textWrappingMode = TextWrappingModes.NoWrap;
            // Nace vacío, así que hay que decirle con qué se va a llenar ("3 / 4")
            // para que elija fuente: si no, CrearTexto asume la de cuerpo.
            _txtMarcador.font = FuentePara("0123456789 /");

            _txtResultado = CrearTexto(card, "Resultado", "", Fnt.Resultado,
                Col.Texto, TextAlignmentOptions.Center);

            var expGO = new GameObject("Explicaciones",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            expGO.transform.SetParent(card, false);
            _contenedorExplicaciones = expGO.GetComponent<RectTransform>();
            var explVlg = expGO.GetComponent<VerticalLayoutGroup>();
            explVlg.spacing = Med.EspaciadoExplicaciones;
            explVlg.childControlWidth = true; explVlg.childControlHeight = true;
            explVlg.childForceExpandWidth = true; explVlg.childForceExpandHeight = false;
            expGO.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            _btnRepetir = CrearBotonCard(card, Txt.BotonRepetir, Col.BotonRepetir,
                () => { Hide(); _onRepetir?.Invoke(); });
            _btnVerExplicacion = CrearBotonCard(card, Txt.BotonExplicacion, Col.BotonExplicacion,
                () => MostrarExplicaciones());
            CrearBotonCard(card, Txt.BotonCerrar, Col.BotonConfirmar,
                () => { Hide(); _onCerrar?.Invoke(); });

            _panelResultado.SetActive(false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Tarjeta centrada de ancho fijo y alto automático según su contenido.</summary>
        private Transform CrearCard(Transform parent, float ancho, RectOffset padding)
        {
            var card = new GameObject("Card",
                typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            card.transform.SetParent(parent, false);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f); cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot     = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(ancho, 0f);
            AplicarFondoRedondeado(card.GetComponent<Image>(), Col.Card);
            var cvlg = card.GetComponent<VerticalLayoutGroup>();
            cvlg.padding = padding; cvlg.spacing = Med.EspaciadoCard;
            cvlg.childControlWidth     = true; cvlg.childControlHeight     = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            card.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            return card.transform;
        }

        private void AgregarTitulo(Transform parent, string texto)
        {
            var t = CrearTexto(parent, "Titulo", texto, Fnt.TituloCard,
                Col.Texto, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
        }

        private Button CrearBotonCard(Transform parent, string texto, Color color, Action onClick)
        {
            var btnGO = new GameObject("Btn_" + texto,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGO.transform.SetParent(parent, false);
            AplicarFondoRedondeado(btnGO.GetComponent<Image>(), color);
            btnGO.GetComponent<LayoutElement>().minHeight = Med.AlturaBoton;
            var btn = btnGO.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var t = CrearTexto(btnGO.transform, "Text", texto, Fnt.Boton,
                Col.TextoSobre(color), TextAlignmentOptions.Center);
            Stretch(t.rectTransform);

            return btn;
        }

        /// <summary>
        /// Crea un texto TMP ya configurado. La fuente se decide sola según los
        /// caracteres que trae el texto (ver <see cref="FuentePara"/>).
        /// </summary>
        private TextMeshProUGUI CrearTexto(Transform parent, string nombre, string texto,
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

        /// <summary>
        /// Deja el fondo con esquinas redondeadas. El sprite es 9-slice, así que
        /// el radio se mantiene igual sin importar cuánto se estire la burbuja.
        /// </summary>
        private void AplicarFondoRedondeado(Image img, Color color)
        {
            if (_spriteRedondeado != null)
            {
                img.sprite = _spriteRedondeado;
                img.type   = Image.Type.Sliced;
            }
            img.color = color;
        }

        private void LimpiarHistorial()
        {
            if (_content == null) return;
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
        }

        private void LimpiarPermiso()
        {
            if (_permisoBurbujas == null) return;
            for (int i = _permisoBurbujas.childCount - 1; i >= 0; i--)
                Destroy(_permisoBurbujas.GetChild(i).gameObject);
        }

        private void LimpiarExplicaciones()
        {
            if (_contenedorExplicaciones == null) return;
            for (int i = _contenedorExplicaciones.childCount - 1; i >= 0; i--)
                Destroy(_contenedorExplicaciones.GetChild(i).gameObject);
        }

        private void ScrollToBottom()
        {
            if (_scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        private static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
