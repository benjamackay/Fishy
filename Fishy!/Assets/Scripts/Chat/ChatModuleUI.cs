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
        private GameObject     _phoneChromeRoot; // bezel + status bar generados
        private TextMeshProUGUI _phoneClockText;
        private Coroutine      _clockCoroutine;

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
            _phoneChromeRoot = null;
            _phoneClockText  = null;

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

            // 1. Centrar y redimensionar el panel a proporción de celular (9:16)
            //    (el modo normal lo deja anclado a una esquina, más chico).
            _chatPanelRT.anchorMin = new Vector2(0.5f, 0.5f);
            _chatPanelRT.anchorMax = new Vector2(0.5f, 0.5f);
            _chatPanelRT.pivot = new Vector2(0.5f, 0.5f);
            _chatPanelRT.anchoredPosition = Vector2.zero;
            _chatPanelRT.sizeDelta = MedTel.Ventana;

            // Actualizar etiqueta de contacto en el header existente.
            if (headerLabel != null) headerLabel.text = contactName;

            // 2. Crear carcasa del celular (visible detrás del panel, con borde).
            _phoneChromeRoot = new GameObject("PhoneChrome", typeof(RectTransform), typeof(Image));
            _phoneChromeRoot.transform.SetParent(_chatPanelRT.parent, false);
            _phoneChromeRoot.transform.SetSiblingIndex(_chatPanelRT.GetSiblingIndex()); // debajo del panel

            var bezelRT = _phoneChromeRoot.GetComponent<RectTransform>();
            bezelRT.anchorMin = new Vector2(0.5f, 0.5f);
            bezelRT.anchorMax = new Vector2(0.5f, 0.5f);
            bezelRT.pivot     = new Vector2(0.5f, 0.5f);
            // Se calcula del panel: antes era un 640x1120 fijo que no seguía a la
            // pantalla, y con el canvas de 1080 de alto el marco salía cortado.
            bezelRT.sizeDelta = MedTel.Ventana
                              + Vector2.one * (MedTel.Borde * 2f);
            _phoneChromeRoot.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 1f);

            // 3. Notch / cámara frontal.
            var notch = new GameObject("Notch", typeof(RectTransform), typeof(Image));
            notch.transform.SetParent(_phoneChromeRoot.transform, false);
            var notchRT = notch.GetComponent<RectTransform>();
            notchRT.anchorMin = new Vector2(0.5f, 1f);
            notchRT.anchorMax = new Vector2(0.5f, 1f);
            notchRT.pivot     = new Vector2(0.5f, 1f);
            notchRT.anchoredPosition = new Vector2(0f, -8f);
            notchRT.sizeDelta = new Vector2(120f, 28f);
            notch.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.05f, 1f);

            // 4. Barra de estado DENTRO del panel del chat (encima del header).
            var statusGO = new GameObject("StatusBar",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            statusGO.transform.SetParent(_chatPanelRT, false);
            statusGO.transform.SetAsFirstSibling();

            // El panel es una pila vertical, así que la barra entra como una fila
            // más y pide su alto. Anclándola a mano quedaba encima de la cabecera.
            var statusLE = statusGO.GetComponent<LayoutElement>();
            statusLE.minHeight = MedTel.AlturaBarraEstado;
            statusLE.preferredHeight = MedTel.AlturaBarraEstado;
            statusGO.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.10f, 1f);

            // Reloj (izq).
            var clockGO = new GameObject("Clock", typeof(RectTransform), typeof(TextMeshProUGUI));
            clockGO.transform.SetParent(statusGO.transform, false);
            var clockRT = clockGO.GetComponent<RectTransform>();
            clockRT.anchorMin = new Vector2(0f, 0f); clockRT.anchorMax = new Vector2(0f, 1f);
            clockRT.pivot     = new Vector2(0f, 0.5f);
            clockRT.anchoredPosition = new Vector2(18f, 0f);
            clockRT.sizeDelta        = new Vector2(160f, 0f);
            _phoneClockText = clockGO.GetComponent<TextMeshProUGUI>();
            _phoneClockText.font      = FishyUIKit.Cuerpo;
            _phoneClockText.fontSize  = ChatFnt.Reloj;
            _phoneClockText.color     = Color.white;
            _phoneClockText.alignment = TextAlignmentOptions.MidlineLeft;
            _phoneClockText.text      = System.DateTime.Now.ToString("HH:mm");

            CrearIconosEstado(statusGO.transform);

            // 6. Barra de inicio (home bar) en el fondo del panel.
            var homeGO = new GameObject("HomeBar",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            homeGO.transform.SetParent(_chatPanelRT, false);

            // El panel es una pila vertical: sin esto la barra de inicio entraría
            // como una fila más y empujaría al resto, en vez de quedarse flotando
            // sobre el borde inferior como en un celular de verdad.
            homeGO.GetComponent<LayoutElement>().ignoreLayout = true;
            var homeRT = homeGO.GetComponent<RectTransform>();
            homeRT.anchorMin = new Vector2(0.25f, 0f);
            homeRT.anchorMax = new Vector2(0.75f, 0f);
            homeRT.pivot     = new Vector2(0.5f, 0f);
            homeRT.anchoredPosition = new Vector2(0f, 10f);
            homeRT.sizeDelta        = new Vector2(0f, 8f);
            homeGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);

            // 7. Iniciar reloj actualizable.
            _clockCoroutine = StartCoroutine(ClockRoutine());
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
        private void CrearIconosEstado(Transform barra)
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

        private void RemovePhoneChrome()
        {
            if (_clockCoroutine != null) { StopCoroutine(_clockCoroutine); _clockCoroutine = null; }
            if (_phoneChromeRoot != null) { Destroy(_phoneChromeRoot); _phoneChromeRoot = null; }
            _phoneClockText = null;

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

        private System.Collections.IEnumerator ClockRoutine()
        {
            while (true)
            {
                if (_phoneClockText != null)
                    _phoneClockText.text = System.DateTime.Now.ToString("HH:mm");
                yield return new WaitForSecondsRealtime(30f);
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
