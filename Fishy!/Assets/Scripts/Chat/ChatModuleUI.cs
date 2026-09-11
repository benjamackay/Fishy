using System;
using System.Collections.Generic;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ChatCol = Fishy.Chat.ChatUITheme.Colores;
using ChatMed = Fishy.Chat.ChatUITheme.Medidas;
using MedCara = Fishy.Chat.ChatUITheme.CaraACara;
using MedTel  = Fishy.Chat.ChatUITheme.Telefono;
using ChatFnt = Fishy.Chat.ChatUITheme.Fuente;
using ChatTxt = Fishy.Chat.ChatUITheme.Textos;

namespace Fishy.Chat
{
    /// <summary>
    /// HDU-8 — Interfaz tipo mensajería del módulo de chat: historial con burbujas
    /// (NPC a la izquierda, niño/a a la derecha) y botones de respuesta abajo.
    /// Al cerrar la sesión muestra el estado emocional de Otto.
    ///
    /// Se autogenera en runtime si no se asignan referencias en el inspector. La
    /// lógica vive en <see cref="ChatModuleController"/>.
    /// </summary>
    public class ChatModuleUI : MonoBehaviour
    {
        public static ChatModuleUI Instance { get; private set; }

        [Header("Referencias (opcionales: si faltan, se generan en runtime)")]
        public GameObject window;
        public TextMeshProUGUI headerLabel;
        public ScrollRect scrollRect;
        public RectTransform content;
        public RectTransform optionsContainer;
        public Button closeButton;

        [Header("Panel de estado emocional (Otto)")]
        public GameObject moodPanel;
        public TextMeshProUGUI moodEmoji;
        public TextMeshProUGUI moodMessage;
        public Button moodCloseButton;

        private readonly List<GameObject> spawnedOptions = new List<GameObject>();
        private string _contactName;
        private bool       _construida, _debeReconstruir;
        private GameObject _canvasRoot;
        private FishyUIKit.PanelDialogo _panel;
        private ScrollRect    _opcionesScroll;
        private LayoutElement _opcionesLayout;

        // ── Modo teléfono ──────────────────────────────────────────────────────
        private bool           _phoneMode;
        private RectTransform  _chatPanelRT;  // referencia al panel del chat
        private Image          _backdropImage;
        private static Vector2 NormalWindowSize => ChatMed.Ventana;
        private MarcoTelefono  _marcoTelefono;   // bisel, barra de estado y reloj

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // No se construye aquí: el diseño depende de si la conversación es
            // cara a cara o por teléfono, y eso se sabe recién al abrirla.
            Hide();
            if (moodPanel != null) moodPanel.SetActive(false);
        }

        public static ChatModuleUI GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("ChatModuleUI");
                Instance = go.AddComponent<ChatModuleUI>();
            }
            return Instance;
        }

        /// <summary>
        /// Activa el modo teléfono antes de llamar a <see cref="Open"/>.
        /// En modo teléfono el chat se encuadra como la pantalla de un celular.
        /// </summary>
        /// <summary>
        /// Cara a cara o por teléfono. Cambia el diseño entero, no solo el marco:
        /// hablando en persona se ve un panel de diálogo como el de los NPCs
        /// neutros, y por el teléfono una app de mensajería como el Modo Detective.
        /// La distinción es del juego, no decorativa: enseña que lo que pasa en un
        /// chat no es lo mismo que lo que pasa cara a cara.
        /// </summary>
        public void EnablePhoneMode(bool on)
        {
            if (_phoneMode == on && _construida) return;
            _phoneMode = on;
            _debeReconstruir = true;
        }

        /// <summary>Rehace la UI si hace falta. Se llama al abrir, cuando ya se sabe
        /// el modo.</summary>
        private void AsegurarUI()
        {
            if (_construida && !_debeReconstruir) return;

            if (_canvasRoot != null) Destroy(_canvasRoot);
            // Todo lo que colgaba del canvas queda destruido: dejarlo apuntando
            // ahí funcionaría por el null falso de Unity, pero se presta a errores.
            _canvasRoot      = null;
            _panel           = null;
            _opcionesScroll  = null;
            _opcionesLayout  = null;
            window           = null;
            content          = null;
            optionsContainer = null;
            scrollRect       = null;
            headerLabel      = null;
            closeButton      = null;
            moodPanel        = null;
            _chatPanelRT     = null;
            _backdropImage   = null;
            _marcoTelefono   = null;

            BuildRuntimeUI();
            _construida      = true;
            _debeReconstruir = false;
        }

        public void Open(string contactName, Action onCloseRequested)
        {
            _contactName = contactName;
            AsegurarUI();
            if (window != null) window.SetActive(true);
            if (moodPanel != null) moodPanel.SetActive(false);
            if (headerLabel != null) headerLabel.text = contactName;

            if (_phoneMode) ApplyPhoneChrome(contactName);
            else            RemovePhoneChrome();

            if (_backdropImage != null)
                _backdropImage.color = _phoneMode ? ChatCol.BackdropTelefono : ChatCol.Backdrop;

            ClearHistory();
            ClearOptions();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => onCloseRequested?.Invoke());
            }
        }

        public void Hide()
        {
            RemovePhoneChrome();
            if (window != null) window.SetActive(false);
            if (moodPanel != null) moodPanel.SetActive(false);
        }

        public void PostNpc(string text, bool isSystem) => AddBubble(text, npc: true, system: isSystem);
        public void PostChild(string text) => AddBubble(text, npc: false, system: false);

        public void ShowOptions(IReadOnlyList<string> options, Action<int> onPick)
        {
            ClearOptions();
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                spawnedOptions.Add(CreateButton(optionsContainer, options[i],
                    ChatCol.BotonOpcion, () => onPick?.Invoke(index)));
            }
            AjustarAltoOpciones();
        }

        public void ClearOptions()
        {
            foreach (var go in spawnedOptions)
                if (go != null) Destroy(go);
            spawnedOptions.Clear();

            // Sin opciones la zona se encoge y el historial recupera el sitio.
            if (_opcionesLayout != null) _opcionesLayout.preferredHeight = 0f;
        }

        /// <summary>
        /// Deja la zona de respuestas justo del alto que piden los botones, con un
        /// tope. Hay que medir después de crearlos porque el alto de cada uno sale
        /// de cuántas líneas envuelve su texto, y eso no se sabe hasta que el layout
        /// resuelve el ancho.
        /// </summary>
        private void AjustarAltoOpciones()
        {
            if (_opcionesLayout == null || optionsContainer == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(optionsContainer);
            float pedido = LayoutUtility.GetPreferredHeight(optionsContainer);

            float altoPanel = _chatPanelRT != null ? _chatPanelRT.rect.height : ChatMed.Ventana.y;
            float tope      = altoPanel * ChatMed.FraccionMaxOpciones;

            _opcionesLayout.preferredHeight = Mathf.Min(pedido, tope);
        }

        public void ShowMood(string emoji, string message, Color color, Action onClose)
        {
            ClearOptions();
            if (moodPanel == null) return;
            moodPanel.SetActive(true);
            moodPanel.transform.SetAsLastSibling();
            if (moodEmoji != null) moodEmoji.text = emoji;
            if (moodMessage != null) { moodMessage.text = message; moodMessage.color = color; }
            if (moodCloseButton != null)
            {
                moodCloseButton.onClick.RemoveAllListeners();
                moodCloseButton.onClick.AddListener(() => onClose?.Invoke());
            }
        }

        // ── Burbujas ────────────────────────────────────────────────────────────
        /// <summary>
        /// Una burbuja del historial. La forma la pone FishyUIKit —la misma que usa
        /// el Modo Detective—, así que si allí se cambia el redondeo, aquí también.
        /// El autor solo se muestra en los mensajes del NPC: en los del niño/a
        /// sobra, porque la posición a la derecha ya dice de quién son.
        /// </summary>
        private void AddBubble(string text, bool npc, bool system)
        {
            if (_panel != null) { EscribirEnPanel(text, npc, system); return; }

            Color fondo = system ? ChatCol.BurbujaSistema
                        : npc    ? ChatCol.BurbujaNpc
                                 : ChatCol.BurbujaNino;

            string autor = npc && !system && !string.IsNullOrEmpty(_contactName)
                ? _contactName
                : null;

            FishyUIKit.Burbuja(content, text, autor,
                izquierda: npc,
                fondo: fondo,
                anchoMax: AnchoBurbujaActual(),
                tamanoTexto: ChatFnt.TextoBurbuja,
                tamanoAutor: ChatFnt.Autor);

            ScrollToBottom();
        }

        /// <summary>
        /// Versión cara a cara: el panel no acumula, reemplaza.
        ///
        /// La respuesta del jugador va en su propia línea y se queda ahí. Hace falta
        /// porque el controller llama a PostChild e inmediatamente después avanza al
        /// siguiente nodo, sin pausa: si la respuesta se escribiera en el mismo sitio
        /// que el texto del NPC, se borraría en el mismo frame y el niño/a nunca
        /// llegaría a leer lo que acababa de elegir.
        /// </summary>
        private void EscribirEnPanel(string text, bool npc, bool system)
        {
            if (npc)
            {
                _panel.Nombre.text = system ? "" : _contactName;
                _panel.Nombre.gameObject.SetActive(!system);
                _panel.Texto.text  = text;
                _panel.Texto.color = system ? ChatCol.TextoSuave : ChatCol.Texto;
                return;
            }

            _panel.Respuesta.text = ChatTxt.PrefijoRespuesta + text;
            _panel.Respuesta.gameObject.SetActive(true);
        }

        /// <summary>
        /// Ancho máximo de burbuja para el tamaño de pantalla que haya ahora. Se mide
        /// en vez de fijarse porque el panel cambia de tamaño entre cara a cara y
        /// teléfono, y una burbuja pensada para uno se ve mal en el otro.
        /// </summary>
        private float AnchoBurbujaActual()
        {
            if (_chatPanelRT == null) return ChatMed.AnchoBurbuja;

            float ancho = _chatPanelRT.rect.width;
            if (ancho <= 1f) ancho = _chatPanelRT.sizeDelta.x;   // aún sin resolver el layout
            if (ancho <= 1f) return ChatMed.AnchoBurbuja;

            return ancho * MedTel.FraccionAnchoBurbuja;
        }

        private void ClearHistory()
        {
            if (_panel != null)
            {
                _panel.Nombre.text = "";
                _panel.Texto.text  = "";
                _panel.Respuesta.text = "";
                _panel.Respuesta.gameObject.SetActive(false);
                return;
            }

            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }

        private void ScrollToBottom()
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }

        // ── Construcción de UI ─────────────────────────────────────────────────
        private GameObject CreateButton(Transform parent, string text, Color color, Action onClick)
        {
            return FishyUIKit.Boton(parent, text, color,
                ChatFnt.Boton, ChatMed.AlturaBoton, onClick).gameObject;
        }

        private void BuildRuntimeUI()
        {
            Fishy.UI.UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("ChatCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasRoot = canvasGO;
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Contenedor raíz: se muestra/oculta completo (backdrop + ventana + estado).
            window = new GameObject("Root", typeof(RectTransform));
            window.transform.SetParent(canvasGO.transform, false);
            Stretch(window.GetComponent<RectTransform>());

            // Backdrop oscuro a pantalla completa.
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(window.transform, false);
            Stretch(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = ChatCol.Backdrop;
            _backdropImage = backdrop.GetComponent<Image>();

            // Cara a cara se ve como el diálogo de un NPC neutro; por teléfono, como
            // el Modo Detective. No es decoración: el juego enseña que un chat y una
            // conversación en persona no son lo mismo, y la interfaz lo respalda.
            if (_phoneMode) BuildDisenoMensajeria();
            else            BuildDisenoCaraACara();

            BuildMoodPanel(window.transform);
        }

        /// <summary>
        /// Tarjeta del estado de ánimo de Otto al terminar la conversación. Usa las
        /// mismas piezas que el resto para que no parezca de otro juego.
        /// </summary>
        /// <summary>Ventana de mensajería: cabecera, historial de burbujas y
        /// opciones abajo. Es el aspecto del Modo Detective, para el chat por
        /// teléfono.</summary>
        private void BuildDisenoMensajeria()
        {
            // El panel es una PILA vertical, no un montón de anclajes con márgenes
            // fijos. Antes las opciones tenían 300px reservados y el historial se
            // recortaba a mano para dejarles sitio: en cuanto un nodo traía tres
            // respuestas largas, no cabían y se pisaban entre ellas. Así cada zona
            // pide el alto que necesita y el historial se queda con el resto.
            var chatPanel = new GameObject("ChatWindow",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            chatPanel.transform.SetParent(window.transform, false);

            var winRT = chatPanel.GetComponent<RectTransform>();
            winRT.anchorMin = new Vector2(1f, 0f);
            winRT.anchorMax = new Vector2(1f, 0f);
            winRT.pivot     = new Vector2(1f, 0f);
            winRT.anchoredPosition = new Vector2(-40f, 40f);
            winRT.sizeDelta = NormalWindowSize;
            FishyUIKit.FondoRedondeado(chatPanel.GetComponent<Image>(), ChatCol.Ventana,
                radio: ChatMed.RadioEsquina);
            _chatPanelRT = winRT;

            var pila = chatPanel.GetComponent<VerticalLayoutGroup>();
            pila.padding = new RectOffset(MedTel.PadPanel, MedTel.PadPanel,
                                          MedTel.PadPanel, MedTel.PadPanel);
            pila.spacing = ChatMed.EspaciadoLista;
            pila.childControlWidth      = true; pila.childControlHeight      = true;
            pila.childForceExpandWidth  = true; pila.childForceExpandHeight  = false;

            BuildHeaderMensajeria(chatPanel.transform);
            BuildHistorial(chatPanel.transform);
            BuildOpciones(chatPanel.transform);
        }

        /// <summary>Cabecera: nombre del contacto y botón de cerrar.</summary>
        private void BuildHeaderMensajeria(Transform parent)
        {
            var header = new GameObject("Header",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            header.transform.SetParent(parent, false);

            var le = header.GetComponent<LayoutElement>();
            le.minHeight = MedTel.AlturaHeader;
            le.preferredHeight = MedTel.AlturaHeader;

            FishyUIKit.FondoRedondeado(header.GetComponent<Image>(), ChatCol.Header,
                radio: ChatMed.RadioEsquina);

            headerLabel = FishyUIKit.Texto(header.transform, "Name", "", ChatFnt.Header,
                ChatCol.Texto, TextAlignmentOptions.MidlineLeft);
            headerLabel.fontStyle = FontStyles.Bold;
            var lblRT = headerLabel.rectTransform;
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(24f, 0f);
            // Deja libre la esquina del botón de cerrar, o el nombre se le mete debajo.
            lblRT.offsetMax = new Vector2(-(MedTel.LadoBotonCerrar + 24f), 0f);

            var closeGO = new GameObject("Close",
                typeof(RectTransform), typeof(Image), typeof(Button));
            closeGO.transform.SetParent(header.transform, false);
            var closeRT = closeGO.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1f, 0.5f); closeRT.anchorMax = new Vector2(1f, 0.5f);
            closeRT.pivot     = new Vector2(1f, 0.5f);
            closeRT.anchoredPosition = new Vector2(-14f, 0f);
            closeRT.sizeDelta = new Vector2(MedTel.LadoBotonCerrar, MedTel.LadoBotonCerrar);
            FishyUIKit.FondoRedondeado(closeGO.GetComponent<Image>(), ChatCol.BotonCerrar, radio: 18);
            closeButton = closeGO.GetComponent<Button>();

            FishyUIKit.Aspa(closeGO.transform, ChatCol.Texto,
                largo: MedTel.LadoBotonCerrar * MedTel.FraccionAspa,
                grosor: MedTel.GrosorAspa);
        }

        /// <summary>Historial de burbujas. Es el que cede espacio: se queda con lo que
        /// dejen la cabecera y las opciones.</summary>
        private void BuildHistorial(Transform parent)
        {
            var scrollGO = new GameObject("Scroll",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect),
                typeof(RectMask2D), typeof(LayoutElement));
            scrollGO.transform.SetParent(parent, false);

            var le = scrollGO.GetComponent<LayoutElement>();
            le.flexibleHeight = 1f;   // el único elástico de la pila
            le.minHeight      = 120f;

            FishyUIKit.FondoRedondeado(scrollGO.GetComponent<Image>(), ChatCol.Historial,
                radio: ChatMed.RadioEsquina);

            scrollRect = scrollGO.GetComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            // El tapiz va como hijo del Scroll y no como su Image, para que el
            // color liso siga debajo: así el alfa del tinte mezcla ilustración y
            // paleta en vez de reemplazarla. Y va antes que Content, o taparía
            // los mensajes.
            CrearTapiz(scrollGO.transform);

            var contentGO = new GameObject("Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(scrollGO.transform, false);
            content = contentGO.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot     = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;

            var cvlg = contentGO.GetComponent<VerticalLayoutGroup>();
            cvlg.spacing = ChatMed.EspaciadoLista;
            cvlg.padding = new RectOffset(14, 14, 14, 14);
            cvlg.childControlWidth      = true; cvlg.childControlHeight      = true;
            cvlg.childForceExpandWidth  = true; cvlg.childForceExpandHeight  = false;

            contentGO.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;
        }

        /// <summary>
        /// Papel tapiz del historial, lo mismo que hace el Modo Detective: mientras
        /// se elige cuál usar lo maneja <see cref="FondoAleatorio"/>, que va rotando
        /// imágenes de una carpeta con una tecla. Sin imágenes no pasa nada: el
        /// historial se ve con su color liso.
        ///
        /// Solo se llama desde el diseño de mensajería, así que el tapiz sale
        /// únicamente en el chat por teléfono. El cara a cara se queda liso a
        /// propósito: una conversación en persona no es una pantalla.
        ///
        /// Va envuelto en una máscara y no suelto como en el detective porque aquí
        /// el historial tiene las esquinas redondeadas: una imagen rectangular
        /// asomaría por las cuatro puntas. La máscara usa el mismo 9-slice que pinta
        /// el redondeo, así que el recorte encaja solo aunque cambie el radio.
        /// </summary>
        private void CrearTapiz(Transform parent)
        {
            var recorteGO = new GameObject("TapizRecorte",
                typeof(RectTransform), typeof(Image), typeof(Mask));
            recorteGO.transform.SetParent(parent, false);
            Stretch(recorteGO.GetComponent<RectTransform>());

            var recorteImg = recorteGO.GetComponent<Image>();
            recorteImg.sprite        = FishyUIKit.SpriteRedondeado(radio: ChatMed.RadioEsquina);
            recorteImg.type          = Image.Type.Sliced;
            recorteImg.raycastTarget = false;
            recorteGO.GetComponent<Mask>().showMaskGraphic = false;

            var go = new GameObject("Tapiz", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(recorteGO.transform, false);
            Stretch(go.GetComponent<RectTransform>());

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;   // los clics son de las burbujas
            img.enabled       = false;   // hasta que haya una imagen que poner

            var rotador = go.AddComponent<FondoAleatorio>();
            rotador.destino          = img;
            // El rótulo de prueba se cuelga de la raíz y no del panel: el panel es
            // una pila vertical y un hijo más le descuadraría el reparto de alto.
            rotador.etiquetaPadre    = window != null ? window.transform : null;
            rotador.carpeta          = MedTel.Fondo.Carpeta;
            rotador.rotar            = MedTel.Fondo.Rotar;
            rotador.fijoPorNombre    = MedTel.Fondo.FijoPorNombre;
            rotador.teclaSiguiente   = MedTel.Fondo.TeclaSiguiente;
            rotador.tinte            = MedTel.Fondo.Tinte;
            rotador.repetir          = MedTel.Fondo.Repetir;
            rotador.mostrarNombre    = MedTel.Fondo.MostrarNombre;
            rotador.tamanoNombre     = MedTel.Fondo.TamanoNombre;
            rotador.colorNombre      = ChatCol.TextoSuave;
            rotador.rutaFuenteNombre = FishyUIKit.RutaCuerpo;
            rotador.modulo           = "Chat";
            rotador.Iniciar();
        }

        /// <summary>
        /// Zona de respuestas. Crece con los botones, pero con tope: si un nodo trae
        /// muchas opciones largas, hacen scroll en vez de comerse el historial.
        /// </summary>
        private void BuildOpciones(Transform parent)
        {
            var opcionesGO = new GameObject("Options",
                typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D),
                typeof(LayoutElement));
            opcionesGO.transform.SetParent(parent, false);

            var le = opcionesGO.GetComponent<LayoutElement>();
            le.flexibleHeight = 0f;
            le.preferredHeight = ChatMed.AlturaBoton * 3f;   // se recalcula al mostrarlas

            _opcionesScroll = opcionesGO.GetComponent<ScrollRect>();
            _opcionesScroll.horizontal = false; _opcionesScroll.vertical = true;
            _opcionesScroll.scrollSensitivity = 30f;
            _opcionesLayout = le;

            var listaGO = new GameObject("Lista",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            listaGO.transform.SetParent(opcionesGO.transform, false);
            optionsContainer = listaGO.GetComponent<RectTransform>();
            optionsContainer.anchorMin = new Vector2(0f, 1f);
            optionsContainer.anchorMax = new Vector2(1f, 1f);
            optionsContainer.pivot     = new Vector2(0.5f, 1f);
            optionsContainer.offsetMin = Vector2.zero; optionsContainer.offsetMax = Vector2.zero;

            var vlg = listaGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childControlWidth      = true; vlg.childControlHeight      = true;
            vlg.childForceExpandWidth  = true; vlg.childForceExpandHeight  = false;

            listaGO.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            _opcionesScroll.content = optionsContainer;
        }

        /// <summary>
        /// Panel de diálogo abajo, igual que el de los NPCs neutros, para hablar
        /// cara a cara. No hay historial ni burbujas: el NPC dice una cosa, el
        /// jugador elige y se sigue.
        /// </summary>
        private void BuildDisenoCaraACara()
        {
            _panel = FishyUIKit.CrearPanelDialogo(window.transform,
                fondo: ChatCol.Ventana,
                colorNombre: ChatCol.Texto,
                colorTexto: ChatCol.Texto,
                colorRespuesta: ChatCol.TextoSuave,
                ancho: MedCara.AnchoPanel,
                tamNombre: ChatFnt.NombrePanel,
                tamTexto: ChatFnt.TextoPanel,
                tamRespuesta: ChatFnt.RespuestaPanel,
                margenInferior: MedCara.MargenInferior,
                radio: ChatMed.RadioEsquina);

            // ShowOptions y ClearOptions no saben de diseños: escriben aquí.
            optionsContainer = _panel.Opciones;

            // Sin ventana de mensajería no hay cabecera ni historial que rellenar.
            headerLabel = _panel.Nombre;
            content     = null;
            scrollRect  = null;
        }

        private void BuildMoodPanel(Transform canvas)
        {
            moodPanel = new GameObject("MoodPanel", typeof(RectTransform), typeof(Image));
            moodPanel.transform.SetParent(canvas, false);
            Stretch(moodPanel.GetComponent<RectTransform>());
            moodPanel.GetComponent<Image>().color = ChatCol.BackdropTelefono;

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(moodPanel.transform, false);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f); cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = ChatMed.CardAnimo;
            FishyUIKit.FondoRedondeado(card.GetComponent<Image>(), ChatCol.Card,
                radio: ChatMed.RadioEsquina);

            // El emoji va con la fuente de cuerpo, no con Mango: Mango es la fuente
            // de la marca y no trae pictogramas, así que saldrían cuadros rotos.
            moodEmoji = FishyUIKit.Texto(card.transform, "Emoji", "", ChatFnt.Emoji,
                ChatCol.Texto, TextAlignmentOptions.Center);
            moodEmoji.font = FishyUIKit.Cuerpo;
            var emojiRT = moodEmoji.rectTransform;
            emojiRT.anchorMin = new Vector2(0.5f, 1f); emojiRT.anchorMax = new Vector2(0.5f, 1f);
            emojiRT.pivot = new Vector2(0.5f, 1f);
            emojiRT.anchoredPosition = new Vector2(0f, -60f);
            emojiRT.sizeDelta = new Vector2(400f, 240f);

            moodMessage = FishyUIKit.Texto(card.transform, "Message", "", ChatFnt.MensajeAnimo,
                ChatCol.Texto, TextAlignmentOptions.Center);
            var msgRT = moodMessage.rectTransform;
            msgRT.anchorMin = new Vector2(0.5f, 0.5f); msgRT.anchorMax = new Vector2(0.5f, 0.5f);
            msgRT.pivot = new Vector2(0.5f, 0.5f);
            msgRT.anchoredPosition = new Vector2(0f, -40f);
            msgRT.sizeDelta = new Vector2(800f, 220f);

            moodCloseButton = FishyUIKit.Boton(card.transform, ChatTxt.BotonContinuar,
                ChatCol.BotonContinuar, ChatFnt.Boton, ChatMed.BotonAnimo.y, null);
            var btnRT = moodCloseButton.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0f); btnRT.anchorMax = new Vector2(0.5f, 0f);
            btnRT.pivot = new Vector2(0.5f, 0f);
            btnRT.anchoredPosition = new Vector2(0f, 50f);
            btnRT.sizeDelta = ChatMed.BotonAnimo;

            moodPanel.SetActive(false);
        }

        // ── Modo teléfono: chrome ─────────────────────────────────────────────
        /// <summary>
        /// Reencuadra el chat como la pantalla de un celular diegético:
        /// proporción portrait, borde oscuro (carcasa), barra de estado superior
        /// y barra de inicio inferior.
        /// </summary>
        private void ApplyPhoneChrome(string contactName)
        {
            RemovePhoneChrome(); // limpiar si había uno previo

            if (_chatPanelRT == null) return;

            // Actualizar etiqueta de contacto en el header existente.
            if (headerLabel != null) headerLabel.text = contactName;

            var ajustes = MarcoTelefono.Ajustes.PorDefecto;
            ajustes.Pantalla          = MedTel.Ventana;
            ajustes.Borde             = MedTel.Borde;
            ajustes.AlturaBarraEstado = MedTel.AlturaBarraEstado;
            ajustes.TamanoReloj       = ChatFnt.Reloj;
            ajustes.MargenBarraInicio = MedTel.MargenBarraInicio;
            // El panel del chat es una pila vertical, y fuera del teléfono vive en una
            // esquina y más chico: hay que centrarlo y medirlo al montar la carcasa.
            ajustes.BarraEstadoEnPila = true;
            ajustes.RecolocarPantalla = true;

            _marcoTelefono = MarcoTelefono.Montar(_chatPanelRT, ajustes);
        }

        private void RemovePhoneChrome()
        {
            if (_marcoTelefono != null) { _marcoTelefono.Desmontar(); _marcoTelefono = null; }

            // Restaurar posición/tamaño del panel normal (esquina, angosto).
            if (_chatPanelRT != null && !_phoneMode)
            {
                _chatPanelRT.anchorMin = new Vector2(1f, 0f);
                _chatPanelRT.anchorMax = new Vector2(1f, 0f);
                _chatPanelRT.pivot = new Vector2(1f, 0f);
                _chatPanelRT.anchoredPosition = new Vector2(-40f, 40f);
                _chatPanelRT.sizeDelta = NormalWindowSize;
            }
        }


        private static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
