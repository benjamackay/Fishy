using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fishy.Net;

namespace Fishy.UI
{
    /// <summary>
    /// Pantalla de login / registro previa al juego.
    ///
    /// Flujo inteligente en modo "Entrar":
    ///   1. Intenta Login con las credenciales dadas.
    ///   2. Si el login falla → intenta registrar la cuenta automáticamente.
    ///   3. Si el registro también falla (usuario existe → contraseña incorrecta) → muestra error.
    ///
    /// Esto significa que el jugador no necesita saber si ya tiene cuenta o no:
    /// basta con escribir su nombre y contraseña y pulsar "Entrar".
    ///
    /// Al arrancar hace un ping al backend (CheckHealth). Si el servidor no
    /// responde, activa el modo local (PlayerPrefs) de forma automática y el
    /// juego sigue funcionando sin conexión.
    /// </summary>
    public class AuthScreen : MonoBehaviour
    {
        public enum Mode { Login, Register }

        [Header("Destino")]
        [Tooltip("Nombre de la escena del juego (debe estar en File → Build Settings).")]
        public string gameSceneName = "SampleScene";
        [Tooltip("Pantalla de carga. Si está vacía se busca en la escena.")]
        public LoadingScreen loadingScreen;

        [Header("Sesión")]
        [Tooltip("Modo inicial al abrir la pantalla.")]
        public Mode mode = Mode.Login;
        [Tooltip("Crear una partida automáticamente tras autenticarse.")]
        public bool crearPartidaAlIngresar = true;

        [Header("Referencias (opcionales; se generan en runtime)")]
        public GameObject panel;
        public Text titleLabel;
        public InputField usernameField;
        public InputField passwordField;
        public Button submitButton;
        public Text submitLabel;
        public Button toggleModeButton;
        public Text toggleModeLabel;
        public Text statusLabel;
        public Text connectionBadge;   // indicador de conexión (generado en runtime)

        private Font font;
        private bool busy;
        private bool backendReady;

        // ── Ciclo de vida ──────────────────────────────────────────────────────
        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UiBootstrap.EnsureEventSystem();
            EnsureApiManager();

            if (loadingScreen == null) loadingScreen = FindFirstObjectByType<LoadingScreen>();

            // Si el ApiManager ya tiene sesión activa (DontDestroyOnLoad entre escenas),
            // saltamos la pantalla de login directamente.
            if (ApiManager.Instance.IsLoggedIn)
            {
                OnAuthSuccess();
                return;
            }

            if (panel == null) BuildRuntimeUI();

            if (submitButton     != null) submitButton.onClick.AddListener(Submit);
            if (toggleModeButton != null) toggleModeButton.onClick.AddListener(ToggleMode);

            ApplyMode();
            Show();

            // Bloquear formulario mientras se verifica la conexión con el backend.
            SetBusy(true);
            SetStatus("Conectando con el servidor…", false);

            ApiManager.Instance.CheckHealth(ok =>
            {
                backendReady = ok;
                SetBusy(false);
                UpdateConnectionBadge(ok);
                SetStatus("", false);
            });
        }

        // ── Visibilidad ────────────────────────────────────────────────────────
        public void Show() { if (panel != null) panel.SetActive(true); }
        public void Hide() { if (panel != null) panel.SetActive(false); }

        // ── Cambio de modo ─────────────────────────────────────────────────────
        public void ToggleMode()
        {
            mode = mode == Mode.Login ? Mode.Register : Mode.Login;
            ApplyMode();
        }

        private void ApplyMode()
        {
            bool login = mode == Mode.Login;
            if (titleLabel     != null) titleLabel.text     = login ? "Iniciar sesión" : "Crear cuenta";
            if (submitLabel    != null) submitLabel.text    = login ? "Entrar"         : "Registrarme";
            if (toggleModeLabel != null)
                toggleModeLabel.text = login
                    ? "¿Primera vez? Crear cuenta"
                    : "¿Ya tienes cuenta? Inicia sesión";
            SetStatus("", false);
        }

        // ── Submit principal ───────────────────────────────────────────────────
        public void Submit()
        {
            if (busy) return;

            string user = usernameField != null ? usernameField.text.Trim() : "";
            string pass = passwordField != null ? passwordField.text         : "";

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                SetStatus("Escribe tu nombre y tu contraseña.", true);
                return;
            }
            if (pass.Length < 4)
            {
                SetStatus("La contraseña debe tener al menos 4 caracteres.", true);
                return;
            }

            SetBusy(true);

            if (mode == Mode.Register)
            {
                // Registro explícito: solo intenta crear la cuenta.
                SetStatus("Creando cuenta…", false);
                ApiManager.Instance.Registro(user, pass,
                    onSuccess: OnAuthSuccess,
                    onError:   err => { SetBusy(false); SetStatus(FriendlyError(err), true); });
            }
            else
            {
                // Login inteligente:
                //   1. Intenta login.
                //   2. Si falla → intenta crear la cuenta automáticamente.
                //   3. Si la creación también falla (usuario existe) → contraseña incorrecta.
                SetStatus("Ingresando…", false);
                ApiManager.Instance.Login(user, pass,
                    onSuccess: OnAuthSuccess,
                    onError:   _ => TryAutoRegister(user, pass));
            }
        }

        /// <summary>
        /// Si el login falló (el usuario no existe o la contraseña es incorrecta),
        /// intenta registrar la cuenta con las mismas credenciales.
        ///
        /// • Registro OK  → cuenta nueva creada, continúa al juego.
        /// • Registro falla "ya existe" → el usuario existe pero la contraseña es
        ///   incorrecta → muestra "Contraseña incorrecta".
        /// • Otro error → muestra mensaje genérico.
        /// </summary>
        private void TryAutoRegister(string user, string pass)
        {
            SetStatus("Creando tu cuenta…", false);
            ApiManager.Instance.Registro(user, pass,
                onSuccess: OnAuthSuccess,
                onError: err =>
                {
                    SetBusy(false);
                    string low = err.ToLowerInvariant();
                    bool userExists = low.Contains("already") || low.Contains("ya existe") ||
                                     low.Contains("unique")   || low.Contains("exist");
                    SetStatus(userExists ? "Contraseña incorrecta." : FriendlyError(err), true);
                });
        }

        // ── Éxito de autenticación ─────────────────────────────────────────────
        private void OnAuthSuccess()
        {
            // Si ya existe una partida en la sesión (login reutilizado), ir directo al juego.
            if (!crearPartidaAlIngresar || ApiManager.Instance.PartidaId.HasValue)
            {
                StartGame();
                return;
            }

            SetStatus("Preparando tu partida…", false);
            ApiManager.Instance.CrearPartida(progreso: 0f,
                onSuccess: _ => StartGame(),
                onError: err =>
                {
                    // Una partida fallida no bloquea el juego: avisamos y continuamos.
                    Debug.LogWarning($"[AuthScreen] No se pudo crear la partida: {err}");
                    StartGame();
                });
        }

        private void StartGame()
        {
            SetStatus("¡Listo!", false);
            Hide();
            if (loadingScreen != null) loadingScreen.LoadScene(gameSceneName);
            else SceneManager.LoadScene(gameSceneName);
        }

        // ── Estado de UI ───────────────────────────────────────────────────────
        private void SetBusy(bool value)
        {
            busy = value;
            if (submitButton     != null) submitButton.interactable     = !value;
            if (toggleModeButton != null) toggleModeButton.interactable = !value;
            if (usernameField    != null) usernameField.interactable    = !value;
            if (passwordField    != null) passwordField.interactable    = !value;
        }

        private void SetStatus(string message, bool isError)
        {
            if (statusLabel == null) return;
            statusLabel.text  = message;
            statusLabel.color = isError
                ? new Color(1f, 0.45f, 0.45f)
                : new Color(0.65f, 0.82f, 1f);
        }

        private void UpdateConnectionBadge(bool connected)
        {
            if (connectionBadge == null) return;
            connectionBadge.text  = connected ? "🟢 Conectado"         : "🔴 Sin conexión (modo local)";
            connectionBadge.color = connected ? new Color(0.4f, 0.9f, 0.5f) : new Color(1f, 0.6f, 0.3f);
        }

        private void EnsureApiManager()
        {
            if (ApiManager.Instance == null)
            {
                var go = new GameObject("ApiManager");
                go.AddComponent<ApiManager>();  // DontDestroyOnLoad interno
            }
        }

        // ── Mensajes de error amigables ────────────────────────────────────────
        private string FriendlyError(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "No se pudo conectar. Inténtalo de nuevo.";
            string low = raw.ToLowerInvariant();
            if (low.Contains("already") || low.Contains("ya existe") ||
                low.Contains("unique")  || low.Contains("exist"))
                return "Ese nombre de usuario ya está en uso.";
            if (low.Contains("invalid") || low.Contains("incorrect") ||
                low.Contains("credential") || low.Contains("contrase") ||
                low.Contains("unable to log"))
                return "Usuario o contraseña incorrectos.";
            if (low.Contains("cannot connect") || low.Contains("timeout") ||
                low.Contains("curl")   || low.Contains("connection") ||
                low.Contains("network") || low.Contains("unreachable"))
                return "No se pudo conectar con el servidor.";
            if (low.Contains("min_length") || low.Contains("short") ||
                low.Contains("4 character"))
                return "La contraseña debe tener al menos 4 caracteres.";
            return "Ocurrió un problema. Inténtalo de nuevo.";
        }

        // ── Construcción de UI en runtime ──────────────────────────────────────
        private void BuildRuntimeUI()
        {
            // Canvas
            var canvasGO = new GameObject("AuthCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1500;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            // Fondo
            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.13f, 1f);

            // Badge de conexión (esquina superior derecha)
            var badgeGO = new GameObject("ConnectionBadge",
                typeof(RectTransform), typeof(Text));
            badgeGO.transform.SetParent(panel.transform, false);
            var badgeRT = badgeGO.GetComponent<RectTransform>();
            badgeRT.anchorMin       = new Vector2(1f, 1f);
            badgeRT.anchorMax       = new Vector2(1f, 1f);
            badgeRT.pivot           = new Vector2(1f, 1f);
            badgeRT.anchoredPosition = new Vector2(-24f, -18f);
            badgeRT.sizeDelta       = new Vector2(480f, 48f);
            connectionBadge = badgeGO.GetComponent<Text>();
            connectionBadge.font      = font;
            connectionBadge.fontSize  = 26;
            connectionBadge.alignment = TextAnchor.MiddleRight;
            connectionBadge.color     = new Color(0.5f, 0.5f, 0.5f);
            connectionBadge.text      = "Conectando…";

            // Tarjeta central
            var card = new GameObject("Card",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(panel.transform, false);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin  = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax  = new Vector2(0.5f, 0.5f);
            cardRT.pivot      = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta  = new Vector2(760f, 720f);
            card.GetComponent<Image>().color = new Color(0.1f, 0.13f, 0.2f, 1f);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding             = new RectOffset(50, 50, 40, 40);
            vlg.spacing             = 22f;
            vlg.childAlignment      = TextAnchor.UpperCenter;
            vlg.childControlWidth   = true;
            vlg.childControlHeight  = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            // Título
            titleLabel = CreateLabel(card.transform, "Iniciar sesión",
                48, FontStyle.Bold, TextAnchor.MiddleCenter, 80f);

            // Campos
            usernameField = CreateInputField(card.transform, "Nombre de jugador", password: false);
            passwordField = CreateInputField(card.transform, "Contraseña",         password: true);

            // Botón principal
            submitButton = CreateButton(card.transform, "Entrar",
                new Color(0.16f, 0.5f, 0.34f, 1f), out submitLabel, 86f);

            // Estado / error
            statusLabel = CreateLabel(card.transform, "",
                26, FontStyle.Normal, TextAnchor.MiddleCenter, 60f);
            statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusLabel.verticalOverflow   = VerticalWrapMode.Overflow;

            // Cambiar modo
            toggleModeButton = CreateButton(card.transform,
                "¿Primera vez? Crear cuenta",
                new Color(0.16f, 0.22f, 0.34f, 1f), out toggleModeLabel, 70f);
        }

        // ── Helpers de construcción ────────────────────────────────────────────
        private InputField CreateInputField(Transform parent, string placeholder, bool password)
        {
            var go = new GameObject(placeholder,
                typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.93f, 0.95f, 0.98f, 1f);
            go.GetComponent<LayoutElement>().minHeight = 84f;

            var input = go.GetComponent<InputField>();

            // Texto principal
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            Stretch(textGO.GetComponent<RectTransform>(), 20f, 10f);
            var text = textGO.GetComponent<Text>();
            text.font      = font;
            text.fontSize  = 32;
            text.color     = new Color(0.1f, 0.12f, 0.16f);
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            // Placeholder
            var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            phGO.transform.SetParent(go.transform, false);
            Stretch(phGO.GetComponent<RectTransform>(), 20f, 10f);
            var ph = phGO.GetComponent<Text>();
            ph.font      = font;
            ph.fontSize  = 32;
            ph.fontStyle = FontStyle.Italic;
            ph.color     = new Color(0.45f, 0.5f, 0.55f);
            ph.alignment = TextAnchor.MiddleLeft;
            ph.text      = placeholder;

            input.textComponent = text;
            input.placeholder   = ph;
            input.lineType      = InputField.LineType.SingleLine;
            input.contentType   = password
                ? InputField.ContentType.Password
                : InputField.ContentType.Standard;
            input.characterLimit = 64;

            return input;
        }

        private Button CreateButton(Transform parent, string label,
            Color color, out Text labelOut, float minHeight)
        {
            var go = new GameObject(label,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            go.GetComponent<LayoutElement>().minHeight = minHeight;

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(go.transform, false);
            Stretch(txtGO.GetComponent<RectTransform>(), 10f, 4f);
            labelOut = txtGO.GetComponent<Text>();
            labelOut.font      = font;
            labelOut.fontSize  = 30;
            labelOut.color     = Color.white;
            labelOut.alignment = TextAnchor.MiddleCenter;
            labelOut.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelOut.text = label;

            return go.GetComponent<Button>();
        }

        private Text CreateLabel(Transform parent, string text,
            int size, FontStyle style, TextAnchor anchor, float minHeight)
        {
            var go = new GameObject("Label",
                typeof(RectTransform), typeof(Text), typeof(LayoutElement));
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
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.offsetMin  = new Vector2(padX, padY);
            rt.offsetMax  = new Vector2(-padX, -padY);
        }
    }
}
