using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        public Text headerLabel;
        public ScrollRect scrollRect;
        public RectTransform content;
        public RectTransform optionsContainer;
        public Button closeButton;

        [Header("Panel de estado emocional (Otto)")]
        public GameObject moodPanel;
        public Text moodEmoji;
        public Text moodMessage;
        public Button moodCloseButton;

        private readonly List<GameObject> spawnedOptions = new List<GameObject>();
        private Font font;

        // ── Modo teléfono ──────────────────────────────────────────────────────
        private bool           _phoneMode;
        private RectTransform  _chatPanelRT;  // referencia al panel del chat
        private Image          _backdropImage;
        private static readonly Vector2 NormalWindowSize = new Vector2(560f, 900f);
        private GameObject     _phoneChromeRoot; // bezel + status bar generados
        private Text           _phoneClockText;
        private Coroutine      _clockCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (window == null || content == null || optionsContainer == null)
                BuildRuntimeUI();

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
        public void EnablePhoneMode(bool on) => _phoneMode = on;

        public void Open(string contactName, Action onCloseRequested)
        {
            if (window != null) window.SetActive(true);
            if (moodPanel != null) moodPanel.SetActive(false);
            if (headerLabel != null) headerLabel.text = contactName;

            if (_phoneMode) ApplyPhoneChrome(contactName);
            else            RemovePhoneChrome();

            if (_backdropImage != null)
                _backdropImage.color = new Color(0f, 0f, 0f, _phoneMode ? 0.55f : 0.12f);

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
                    new Color(0.16f, 0.22f, 0.34f, 0.95f), () => onPick?.Invoke(index)));
            }
        }

        public void ClearOptions()
        {
            foreach (var go in spawnedOptions)
                if (go != null) Destroy(go);
            spawnedOptions.Clear();
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
        private void AddBubble(string text, bool npc, bool system)
        {
            var row = new GameObject(npc ? "NpcRow" : "ChildRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(content, false);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = npc ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var bubble = new GameObject("Bubble",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter), typeof(LayoutElement));
            bubble.transform.SetParent(row.transform, false);
            bubble.GetComponent<Image>().color = system
                ? new Color(0.35f, 0.3f, 0.12f, 0.95f)          // sistema (aviso)
                : npc ? new Color(0.2f, 0.22f, 0.27f, 0.95f)    // NPC
                      : new Color(0.13f, 0.35f, 0.55f, 0.95f);  // niño/a
            var bvlg = bubble.GetComponent<VerticalLayoutGroup>();
            bvlg.padding = new RectOffset(20, 20, 14, 14);
            bvlg.childControlWidth = true; bvlg.childControlHeight = true;
            bvlg.childForceExpandWidth = true; bvlg.childForceExpandHeight = false;
            var fitter = bubble.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bubble.GetComponent<LayoutElement>().preferredWidth = 420f;

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(bubble.transform, false);
            var t = txtGO.GetComponent<Text>();
            t.font = font; t.fontSize = 30; t.color = Color.white;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;

            ScrollToBottom();
        }

        private void ClearHistory()
        {
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
            var btnGO = new GameObject("Option",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGO.transform.SetParent(parent, false);
            btnGO.GetComponent<Image>().color = color;
            btnGO.GetComponent<LayoutElement>().minHeight = 68f;
            var btn = btnGO.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(btnGO.transform, false);
            var rt = txtGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(18f, 6f); rt.offsetMax = new Vector2(-18f, -6f);
            var t = txtGO.GetComponent<Text>();
            t.font = font; t.fontSize = 28; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;

            return btnGO;
        }

        private void BuildRuntimeUI()
        {
            Fishy.UI.UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("ChatCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
            _backdropImage = backdrop.GetComponent<Image>();

            // Ventana del chat: angosta y alargada, pegada a una esquina para
            // dejar ver al personaje en el resto de la pantalla.
            var chatPanel = new GameObject("ChatWindow", typeof(RectTransform), typeof(Image));
            chatPanel.transform.SetParent(window.transform, false);
            var winRT = chatPanel.GetComponent<RectTransform>();
            winRT.anchorMin = new Vector2(1f, 0f);
            winRT.anchorMax = new Vector2(1f, 0f);
            winRT.pivot = new Vector2(1f, 0f);
            winRT.anchoredPosition = new Vector2(-40f, 40f);
            winRT.sizeDelta = NormalWindowSize;
            chatPanel.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 1f);
            _chatPanelRT = winRT; // guardamos referencia para el modo teléfono

            // Header.
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(chatPanel.transform, false);
            var headRT = header.GetComponent<RectTransform>();
            headRT.anchorMin = new Vector2(0f, 1f); headRT.anchorMax = new Vector2(1f, 1f);
            headRT.pivot = new Vector2(0.5f, 1f);
            headRT.sizeDelta = new Vector2(0f, 90f);
            header.GetComponent<Image>().color = new Color(0.1f, 0.13f, 0.2f, 1f);

            var headLabelGO = new GameObject("Name", typeof(RectTransform), typeof(Text));
            headLabelGO.transform.SetParent(header.transform, false);
            Stretch(headLabelGO.GetComponent<RectTransform>(), 30f, 0f);
            headerLabel = headLabelGO.GetComponent<Text>();
            headerLabel.font = font; headerLabel.fontSize = 36; headerLabel.fontStyle = FontStyle.Bold;
            headerLabel.color = Color.white; headerLabel.alignment = TextAnchor.MiddleLeft;

            // Botón cerrar (X).
            var closeGO = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGO.transform.SetParent(header.transform, false);
            var closeRT = closeGO.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1f, 0.5f); closeRT.anchorMax = new Vector2(1f, 0.5f);
            closeRT.pivot = new Vector2(1f, 0.5f);
            closeRT.anchoredPosition = new Vector2(-20f, 0f);
            closeRT.sizeDelta = new Vector2(64f, 64f);
            closeGO.GetComponent<Image>().color = new Color(0.6f, 0.2f, 0.2f, 1f);
            closeButton = closeGO.GetComponent<Button>();
            var closeTxtGO = new GameObject("X", typeof(RectTransform), typeof(Text));
            closeTxtGO.transform.SetParent(closeGO.transform, false);
            Stretch(closeTxtGO.GetComponent<RectTransform>());
            var closeTxt = closeTxtGO.GetComponent<Text>();
            closeTxt.font = font; closeTxt.fontSize = 40; closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter; closeTxt.text = "✕";

            // Zona de opciones (abajo).
            var optionsGO = new GameObject("Options",
                typeof(RectTransform), typeof(VerticalLayoutGroup));
            optionsGO.transform.SetParent(chatPanel.transform, false);
            optionsContainer = optionsGO.GetComponent<RectTransform>();
            optionsContainer.anchorMin = new Vector2(0f, 0f); optionsContainer.anchorMax = new Vector2(1f, 0f);
            optionsContainer.pivot = new Vector2(0.5f, 0f);
            optionsContainer.anchoredPosition = new Vector2(0f, 24f);
            optionsContainer.sizeDelta = new Vector2(-48f, 300f);
            var ovlg = optionsGO.GetComponent<VerticalLayoutGroup>();
            ovlg.spacing = 12f; ovlg.childAlignment = TextAnchor.LowerCenter;
            ovlg.childControlWidth = true; ovlg.childControlHeight = true;
            ovlg.childForceExpandWidth = true; ovlg.childForceExpandHeight = false;

            // ScrollRect del historial (entre header y opciones).
            var scrollGO = new GameObject("Scroll",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGO.transform.SetParent(chatPanel.transform, false);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0f, 0f); scrollRT.anchorMax = new Vector2(1f, 1f);
            scrollRT.offsetMin = new Vector2(20f, 330f);   // deja espacio para opciones
            scrollRT.offsetMax = new Vector2(-20f, -100f); // deja espacio para header
            scrollGO.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.6f);
            scrollRect = scrollGO.GetComponent<ScrollRect>();
            scrollRect.horizontal = false; scrollRect.vertical = true;

            var contentGO = new GameObject("Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(scrollGO.transform, false);
            content = contentGO.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(10f, 0f); content.offsetMax = new Vector2(-10f, 0f);
            var cvlg = contentGO.GetComponent<VerticalLayoutGroup>();
            cvlg.spacing = 12f; cvlg.padding = new RectOffset(8, 8, 8, 8);
            cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            var cFitter = contentGO.GetComponent<ContentSizeFitter>();
            cFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;

            BuildMoodPanel(window.transform);
        }

        private void BuildMoodPanel(Transform canvas)
        {
            moodPanel = new GameObject("MoodPanel", typeof(RectTransform), typeof(Image));
            moodPanel.transform.SetParent(canvas, false);
            Stretch(moodPanel.GetComponent<RectTransform>());
            moodPanel.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.92f);

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(moodPanel.transform, false);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f); cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(900f, 700f);
            card.GetComponent<Image>().color = new Color(0.1f, 0.13f, 0.2f, 1f);

            var emojiGO = new GameObject("Emoji", typeof(RectTransform), typeof(Text));
            emojiGO.transform.SetParent(card.transform, false);
            var emojiRT = emojiGO.GetComponent<RectTransform>();
            emojiRT.anchorMin = new Vector2(0.5f, 1f); emojiRT.anchorMax = new Vector2(0.5f, 1f);
            emojiRT.pivot = new Vector2(0.5f, 1f);
            emojiRT.anchoredPosition = new Vector2(0f, -60f);
            emojiRT.sizeDelta = new Vector2(400f, 240f);
            moodEmoji = emojiGO.GetComponent<Text>();
            moodEmoji.font = font; moodEmoji.fontSize = 160; moodEmoji.alignment = TextAnchor.MiddleCenter;
            moodEmoji.color = Color.white;

            var msgGO = new GameObject("Message", typeof(RectTransform), typeof(Text));
            msgGO.transform.SetParent(card.transform, false);
            var msgRT = msgGO.GetComponent<RectTransform>();
            msgRT.anchorMin = new Vector2(0.5f, 0.5f); msgRT.anchorMax = new Vector2(0.5f, 0.5f);
            msgRT.pivot = new Vector2(0.5f, 0.5f);
            msgRT.anchoredPosition = new Vector2(0f, -40f);
            msgRT.sizeDelta = new Vector2(800f, 220f);
            moodMessage = msgGO.GetComponent<Text>();
            moodMessage.font = font; moodMessage.fontSize = 44; moodMessage.alignment = TextAnchor.MiddleCenter;
            moodMessage.color = Color.white;
            moodMessage.horizontalOverflow = HorizontalWrapMode.Wrap;
            moodMessage.verticalOverflow = VerticalWrapMode.Overflow;

            var btnGO = new GameObject("CloseMood", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(card.transform, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0f); btnRT.anchorMax = new Vector2(0.5f, 0f);
            btnRT.pivot = new Vector2(0.5f, 0f);
            btnRT.anchoredPosition = new Vector2(0f, 50f);
            btnRT.sizeDelta = new Vector2(360f, 90f);
            btnGO.GetComponent<Image>().color = new Color(0.16f, 0.45f, 0.3f, 1f);
            moodCloseButton = btnGO.GetComponent<Button>();
            var btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            btnTxtGO.transform.SetParent(btnGO.transform, false);
            Stretch(btnTxtGO.GetComponent<RectTransform>());
            var btnTxt = btnTxtGO.GetComponent<Text>();
            btnTxt.font = font; btnTxt.fontSize = 34; btnTxt.color = Color.white;
            btnTxt.alignment = TextAnchor.MiddleCenter; btnTxt.text = "Continuar";

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
            _chatPanelRT.sizeDelta = new Vector2(600f, 1060f);

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
            bezelRT.sizeDelta = new Vector2(640f, 1120f); // 20px de borde en cada lado
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
            var statusGO = new GameObject("StatusBar", typeof(RectTransform), typeof(Image));
            statusGO.transform.SetParent(_chatPanelRT, false);
            statusGO.transform.SetAsFirstSibling();
            var statusRT = statusGO.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0f, 1f);
            statusRT.anchorMax = new Vector2(1f, 1f);
            statusRT.pivot     = new Vector2(0.5f, 1f);
            statusRT.sizeDelta = new Vector2(0f, 48f);
            statusGO.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.10f, 1f);

            // Reloj (izq).
            var clockGO = new GameObject("Clock", typeof(RectTransform), typeof(Text));
            clockGO.transform.SetParent(statusGO.transform, false);
            var clockRT = clockGO.GetComponent<RectTransform>();
            clockRT.anchorMin = new Vector2(0f, 0f); clockRT.anchorMax = new Vector2(0f, 1f);
            clockRT.pivot     = new Vector2(0f, 0.5f);
            clockRT.anchoredPosition = new Vector2(18f, 0f);
            clockRT.sizeDelta        = new Vector2(160f, 0f);
            _phoneClockText = clockGO.GetComponent<Text>();
            _phoneClockText.font      = font; _phoneClockText.fontSize = 24;
            _phoneClockText.color     = Color.white;
            _phoneClockText.alignment = TextAnchor.MiddleLeft;
            _phoneClockText.text      = System.DateTime.Now.ToString("HH:mm");

            // Iconos de señal + batería (der).
            var iconsGO = new GameObject("Icons", typeof(RectTransform), typeof(Text));
            iconsGO.transform.SetParent(statusGO.transform, false);
            var iconsRT = iconsGO.GetComponent<RectTransform>();
            iconsRT.anchorMin = new Vector2(1f, 0f); iconsRT.anchorMax = new Vector2(1f, 1f);
            iconsRT.pivot     = new Vector2(1f, 0.5f);
            iconsRT.anchoredPosition = new Vector2(-14f, 0f);
            iconsRT.sizeDelta        = new Vector2(180f, 0f);
            var iconsTxt = iconsGO.GetComponent<Text>();
            iconsTxt.font = font; iconsTxt.fontSize = 22;
            iconsTxt.color = Color.white; iconsTxt.alignment = TextAnchor.MiddleRight;
            iconsTxt.text = "▲▲▲  WiFi  🔋";

            // 5. Mover el header del chat hacia abajo para dejar espacio a la status bar.
            var headerTF = _chatPanelRT.Find("Header");
            if (headerTF != null)
            {
                var hRT = headerTF.GetComponent<RectTransform>();
                // Añadimos padding superior equivalente al status bar.
                hRT.offsetMin = new Vector2(0f, 0f);
                hRT.anchoredPosition = new Vector2(hRT.anchoredPosition.x, -48f);
            }

            // 6. Barra de inicio (home bar) en el fondo del panel.
            var homeGO = new GameObject("HomeBar", typeof(RectTransform), typeof(Image));
            homeGO.transform.SetParent(_chatPanelRT, false);
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
