using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fishy.Net;

namespace Fishy.UI
{
    /// <summary>
    /// Menú principal que aparece justo después de iniciar sesión: Jugar / Opciones / Salir.
    /// Si no se asignan referencias, construye su propia UI en runtime (mismo criterio
    /// que AuthScreen y LoadingScreen).
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        [Header("Destino")]
        [Tooltip("Escena de juego a cargar al presionar Jugar.")]
        public string playSceneName = "SampleScene";
        [Tooltip("Pantalla de carga. Si está vacía se busca en la escena.")]
        public LoadingScreen loadingScreen;

        [Header("Referencias (opcionales; se generan en runtime)")]
        public GameObject panel;
        public Text titleLabel;
        public Text greetingLabel;
        public Button playButton;
        public Button optionsButton;
        public Button quitButton;

        public GameObject optionsPanel;
        public Slider volumeSlider;
        public Button closeOptionsButton;

        private Font font;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UiBootstrap.EnsureEventSystem();

            if (loadingScreen == null) loadingScreen = FindFirstObjectByType<LoadingScreen>();

            if (panel == null) BuildRuntimeUI();

            if (playButton    != null) playButton.onClick.AddListener(OnPlay);
            if (optionsButton != null) optionsButton.onClick.AddListener(OnOpenOptions);
            if (quitButton    != null) quitButton.onClick.AddListener(OnQuit);
            if (closeOptionsButton != null) closeOptionsButton.onClick.AddListener(OnCloseOptions);
            if (volumeSlider  != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            UpdateGreeting();
            CloseOptionsInstant();
        }

        private void UpdateGreeting()
        {
            if (greetingLabel == null) return;
            var api = ApiManager.Instance;
            greetingLabel.text = (api != null && api.IsLoggedIn)
                ? "Sesión iniciada"
                : "";
        }

        // ── Acciones ─────────────────────────────────────────────────────────────
        private void OnPlay()
        {
            if (loadingScreen != null) loadingScreen.LoadScene(playSceneName);
            else SceneManager.LoadScene(playSceneName);
        }

        private void OnOpenOptions()
        {
            if (optionsPanel != null)
            {
                if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(AudioListener.volume);
                optionsPanel.SetActive(true);
            }
        }

        private void OnCloseOptions()
        {
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        private void CloseOptionsInstant()
        {
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        private void OnVolumeChanged(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Construcción de UI en runtime ──────────────────────────────────────
        private void BuildRuntimeUI()
        {
            var canvasGO = new GameObject("MainMenuCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = UiTheme.Background;

            // Sombra + tarjeta central
            var shadow = new GameObject("CardShadow", typeof(RectTransform), typeof(Image));
            shadow.transform.SetParent(panel.transform, false);
            var shadowRT = shadow.GetComponent<RectTransform>();
            shadowRT.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRT.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRT.pivot     = new Vector2(0.5f, 0.5f);
            shadowRT.sizeDelta = new Vector2(600f, 640f);
            shadowRT.anchoredPosition = new Vector2(0f, -10f);
            var shadowImg = shadow.GetComponent<Image>();
            shadowImg.color = UiTheme.CardShadow;
            UiTheme.MakeRounded(shadowImg);

            var card = new GameObject("Card",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(panel.transform, false);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot     = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(600f, 640f);
            var cardImg = card.GetComponent<Image>();
            cardImg.color = UiTheme.CardBg;
            UiTheme.MakeRounded(cardImg);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(48, 48, 56, 48);
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            titleLabel = CreateLabel(card.transform, "Fishy!", 56, FontStyle.Bold, TextAnchor.MiddleCenter, 84f);
            titleLabel.color = UiTheme.TextPrimary;

            greetingLabel = CreateLabel(card.transform, "", 22, FontStyle.Normal, TextAnchor.MiddleCenter, 30f);
            greetingLabel.color = UiTheme.TextMuted;

            AddSpacer(card.transform, 10f);

            playButton = CreateMenuButton(card.transform, "Jugar", UiTheme.Accent, Color.white, out _);
            optionsButton = CreateMenuButton(card.transform, "Opciones", UiTheme.Secondary, UiTheme.TextPrimary, out _);
            quitButton = CreateMenuButton(card.transform, "Salir", UiTheme.Secondary, UiTheme.TextPrimary, out _);

            BuildOptionsPanel(panel.transform);
        }

        private void BuildOptionsPanel(Transform parent)
        {
            optionsPanel = new GameObject("OptionsPanel", typeof(RectTransform), typeof(Image));
            optionsPanel.transform.SetParent(parent, false);
            Stretch(optionsPanel.GetComponent<RectTransform>());
            optionsPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(optionsPanel.transform, false);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot     = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(560f, 360f);
            var cardImg = card.GetComponent<Image>();
            cardImg.color = UiTheme.CardBg;
            UiTheme.MakeRounded(cardImg);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(44, 44, 40, 40);
            vlg.spacing = 22f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var title = CreateLabel(card.transform, "Opciones", 34, FontStyle.Bold, TextAnchor.MiddleCenter, 50f);
            title.color = UiTheme.TextPrimary;

            var volLabel = CreateLabel(card.transform, "Volumen", 22, FontStyle.Normal, TextAnchor.MiddleLeft, 30f);
            volLabel.color = UiTheme.TextMuted;

            volumeSlider = CreateSlider(card.transform);

            AddSpacer(card.transform, 8f);

            closeOptionsButton = CreateMenuButton(card.transform, "Cerrar", UiTheme.Accent, Color.white, out _);
        }

        private Slider CreateSlider(Transform parent)
        {
            var go = new GameObject("VolumeSlider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().minHeight = 40f;
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = AudioListener.volume;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.4f);
            bgRT.anchorMax = new Vector2(1f, 0.6f);
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = UiTheme.Secondary;
            UiTheme.MakeRounded(bgImg, soft: true);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRT = fillArea.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0f, 0.4f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.6f);
            fillAreaRT.offsetMin = new Vector2(6f, 0f);
            fillAreaRT.offsetMax = new Vector2(-6f, 0f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.sizeDelta = new Vector2(20f, 0f);
            var fillImg = fill.GetComponent<Image>();
            fillImg.color = UiTheme.Accent;
            UiTheme.MakeRounded(fillImg, soft: true);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), 10f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(28f, 28f);
            var handleImg = handle.GetComponent<Image>();
            handleImg.color = Color.white;
            UiTheme.MakeRounded(handleImg, soft: true);

            slider.fillRect   = fillRT;
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handleImg;
            slider.direction  = Slider.Direction.LeftToRight;

            return slider;
        }

        private Button CreateMenuButton(Transform parent, string label, Color bg, Color textColor, out Text labelOut)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            UiTheme.MakeRounded(img, soft: true);
            go.GetComponent<LayoutElement>().minHeight = 84f;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(bg, Color.white, 0.12f);
            colors.pressedColor     = Color.Lerp(bg, Color.black, 0.12f);
            button.colors = colors;

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(go.transform, false);
            Stretch(txtGO.GetComponent<RectTransform>(), 10f, 4f);
            labelOut = txtGO.GetComponent<Text>();
            labelOut.font      = font;
            labelOut.fontSize  = 30;
            labelOut.fontStyle = FontStyle.Bold;
            labelOut.color     = textColor;
            labelOut.alignment = TextAnchor.MiddleCenter;
            labelOut.text      = label;

            return button;
        }

        private void AddSpacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().minHeight = height;
        }

        private Text CreateLabel(Transform parent, string text,
            int size, FontStyle style, TextAnchor anchor, float minHeight)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().minHeight = minHeight;
            var t = go.GetComponent<Text>();
            t.font      = font;
            t.fontSize  = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color     = Color.white;
            t.text      = text;
            return t;
        }

        private static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
