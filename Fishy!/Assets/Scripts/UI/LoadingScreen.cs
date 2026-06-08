using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Pantalla de carga con barra de progreso FUNCIONAL: carga una escena de forma
    /// asíncrona y refleja el progreso real (SceneManager.LoadSceneAsync).
    ///
    /// Si no se asignan referencias, construye su propia UI en runtime. Puede usar
    /// un <see cref="Slider"/> o una Image de tipo "Filled" como barra.
    ///
    /// Uso:
    ///     loadingScreen.LoadScene("SampleScene");
    /// (la escena debe estar agregada en File → Build Settings.)
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        [Header("Referencias (opcionales; se generan en runtime)")]
        public GameObject panel;
        [Tooltip("Barra como Slider (opcional).")]
        public Slider progressBar;
        [Tooltip("Barra como Image 'Filled' (opcional).")]
        public Image fillImage;
        [Tooltip("Barra por anchors (opcional): se ajusta anchorMax.x = progreso.")]
        public RectTransform fillRect;
        public Text percentLabel;
        public Text statusLabel;
        public Text tipLabel;

        [Header("Comportamiento")]
        [Tooltip("Tiempo mínimo que se muestra la pantalla (evita parpadeos).")]
        public float minDisplayTime = 1.5f;
        [Tooltip("Suavizado del avance visual de la barra.")]
        public float fillLerpSpeed = 2.5f;
        [Tooltip("Mensajes/consejos que rotan durante la carga.")]
        [TextArea]
        public string[] tips =
        {
            "Consejo: nunca compartas tu dirección con desconocidos.",
            "Consejo: si algo te incomoda, cuéntale a un adulto de confianza.",
            "Consejo: puedes bloquear a quien te haga sentir mal.",
            "Otto está preparando el mundo...",
        };

        private Font font;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (panel == null) BuildRuntimeUI();
            Hide();
        }

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
            SetProgress(0f);
            if (tipLabel != null && tips != null && tips.Length > 0)
                tipLabel.text = tips[Random.Range(0, tips.Length)];
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>Muestra la pantalla y carga la escena indicada de forma asíncrona.</summary>
        public void LoadScene(string sceneName)
        {
            Show();
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            float startTime = Time.unscaledTime;
            if (statusLabel != null) statusLabel.text = "Cargando...";

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                if (statusLabel != null)
                    statusLabel.text = $"No se encontró la escena '{sceneName}'. Agrégala en Build Settings.";
                Debug.LogError($"[LoadingScreen] Escena '{sceneName}' no está en Build Settings.");
                yield break;
            }

            op.allowSceneActivation = false;
            float displayed = 0f;

            while (!op.isDone)
            {
                // Progreso real de carga (0..0.9 → 0..1).
                float loadProgress = Mathf.Clamp01(op.progress / 0.9f);
                // Progreso por tiempo mínimo (para que la barra no termine antes de tiempo).
                float timeProgress = minDisplayTime <= 0f
                    ? 1f
                    : Mathf.Clamp01((Time.unscaledTime - startTime) / minDisplayTime);

                float target = Mathf.Min(loadProgress, timeProgress);
                displayed = Mathf.MoveTowards(displayed, target, fillLerpSpeed * Time.unscaledDeltaTime);
                SetProgress(displayed);

                bool ready = loadProgress >= 1f && timeProgress >= 1f;
                if (ready)
                {
                    SetProgress(1f);
                    op.allowSceneActivation = true; // activa la escena → se cierra esta pantalla
                }

                yield return null;
            }
        }

        public void SetProgress(float value01)
        {
            value01 = Mathf.Clamp01(value01);
            if (progressBar != null) progressBar.value = value01;
            if (fillImage != null) fillImage.fillAmount = value01;
            if (fillRect != null)
            {
                var a = fillRect.anchorMax;
                a.x = value01;
                fillRect.anchorMax = a;
            }
            if (percentLabel != null) percentLabel.text = Mathf.RoundToInt(value01 * 100f) + "%";
        }

        // ── Construcción de UI ─────────────────────────────────────────────────
        private void BuildRuntimeUI()
        {
            var canvasGO = new GameObject("LoadingCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 1f);

            // Título.
            var title = CreateText(panel.transform, "Cargando...", 64, FontStyle.Bold, TextAnchor.MiddleCenter);
            var titleRT = title.rectTransform;
            titleRT.anchorMin = new Vector2(0.5f, 0.5f); titleRT.anchorMax = new Vector2(0.5f, 0.5f);
            titleRT.pivot = new Vector2(0.5f, 0.5f);
            titleRT.anchoredPosition = new Vector2(0f, 140f);
            titleRT.sizeDelta = new Vector2(1200f, 100f);

            // Fondo de la barra.
            var barBg = new GameObject("BarBackground", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(panel.transform, false);
            var barBgRT = barBg.GetComponent<RectTransform>();
            barBgRT.anchorMin = new Vector2(0.5f, 0.5f); barBgRT.anchorMax = new Vector2(0.5f, 0.5f);
            barBgRT.pivot = new Vector2(0.5f, 0.5f);
            barBgRT.anchoredPosition = new Vector2(0f, 0f);
            barBgRT.sizeDelta = new Vector2(900f, 44f);
            barBg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.24f, 1f);

            // Relleno: crece por anchors (color sólido, sin depender de un sprite).
            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(barBg.transform, false);
            fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);   // anchorMax.x se actualiza en SetProgress
            fillRect.offsetMin = new Vector2(6f, 6f);
            fillRect.offsetMax = new Vector2(-6f, -6f);
            fillGO.GetComponent<Image>().color = new Color(0.3f, 0.75f, 0.5f, 1f);

            // Porcentaje.
            percentLabel = CreateText(panel.transform, "0%", 32, FontStyle.Bold, TextAnchor.MiddleCenter);
            var pctRT = percentLabel.rectTransform;
            pctRT.anchorMin = new Vector2(0.5f, 0.5f); pctRT.anchorMax = new Vector2(0.5f, 0.5f);
            pctRT.pivot = new Vector2(0.5f, 0.5f);
            pctRT.anchoredPosition = new Vector2(0f, -60f);
            pctRT.sizeDelta = new Vector2(400f, 50f);

            // Estado.
            statusLabel = CreateText(panel.transform, "", 26, FontStyle.Normal, TextAnchor.MiddleCenter);
            var stRT = statusLabel.rectTransform;
            stRT.anchorMin = new Vector2(0.5f, 0.5f); stRT.anchorMax = new Vector2(0.5f, 0.5f);
            stRT.pivot = new Vector2(0.5f, 0.5f);
            stRT.anchoredPosition = new Vector2(0f, -110f);
            stRT.sizeDelta = new Vector2(1200f, 40f);
            statusLabel.color = new Color(0.7f, 0.8f, 0.9f);

            // Consejo (abajo).
            tipLabel = CreateText(panel.transform, "", 28, FontStyle.Italic, TextAnchor.MiddleCenter);
            var tipRT = tipLabel.rectTransform;
            tipRT.anchorMin = new Vector2(0.5f, 0f); tipRT.anchorMax = new Vector2(0.5f, 0f);
            tipRT.pivot = new Vector2(0.5f, 0f);
            tipRT.anchoredPosition = new Vector2(0f, 90f);
            tipRT.sizeDelta = new Vector2(1400f, 80f);
            tipLabel.color = new Color(0.6f, 0.7f, 0.85f);
            tipLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private Text CreateText(Transform parent, string text, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.alignment = anchor;
            t.color = Color.white; t.text = text;
            return t;
        }

        private static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
