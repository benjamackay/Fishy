using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.World
{
    /// <summary>
    /// HDU-5 — Mensaje emergente para zonas bloqueadas.
    ///
    /// Singleton sencillo que muestra un cartel con un texto y lo oculta solo tras
    /// unos segundos. Si no se asignan referencias en el inspector, construye su
    /// propia UI (Canvas + panel + texto) en tiempo de ejecución, de modo que
    /// funciona "out of the box" sin montar nada en la escena.
    ///
    /// Uso desde cualquier script:
    ///     ZonePopupUI.Show("Esta zona aún está cerrada...");
    /// </summary>
    public class ZonePopupUI : MonoBehaviour
    {
        public static ZonePopupUI Instance { get; private set; }

        [Header("Referencias (opcionales: si faltan, se generan en runtime)")]
        [Tooltip("Grupo que se muestra/oculta. Si es null se crea automáticamente.")]
        public CanvasGroup panel;
        [Tooltip("Texto del mensaje. Si es null se crea automáticamente.")]
        public Text label;

        [Header("Comportamiento")]
        [Tooltip("Segundos que permanece visible el mensaje.")]
        public float showDuration = 3f;
        [Tooltip("Duración del fundido de entrada/salida.")]
        public float fadeDuration = 0.25f;

        private Coroutine hideRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (panel == null || label == null)
                BuildRuntimeUI();

            HideImmediate();
        }

        /// <summary>
        /// Muestra el mensaje. Crea la UI automáticamente si aún no existe en la escena.
        /// </summary>
        public static void Show(string message)
        {
            if (Instance == null)
            {
                var go = new GameObject("ZonePopupUI");
                Instance = go.AddComponent<ZonePopupUI>();
            }
            Instance.ShowMessage(message);
        }

        public void ShowMessage(string message)
        {
            if (label != null) label.text = message;
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            yield return Fade(0f, 1f, fadeDuration);
            yield return new WaitForSecondsRealtime(showDuration);
            yield return Fade(1f, 0f, fadeDuration);
        }

        private IEnumerator Fade(float from, float to, float dur)
        {
            if (panel == null) yield break;
            panel.gameObject.SetActive(true);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                panel.alpha = Mathf.Lerp(from, to, dur <= 0f ? 1f : t / dur);
                yield return null;
            }
            panel.alpha = to;
            if (to <= 0f) panel.gameObject.SetActive(false);
        }

        private void HideImmediate()
        {
            if (panel == null) return;
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }

        // ── Construcción de UI por defecto ─────────────────────────────────────
        private void BuildRuntimeUI()
        {
            // Canvas
            var canvasGO = new GameObject("PopupCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Panel
            var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0f);
            panelRT.anchorMax = new Vector2(0.5f, 0f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.anchoredPosition = new Vector2(0f, 140f);
            panelRT.sizeDelta = new Vector2(900f, 160f);
            var panelImg = panelGO.GetComponent<Image>();
            panelImg.color = new Color(0.05f, 0.08f, 0.15f, 0.85f);
            panel = panelGO.GetComponent<CanvasGroup>();

            // Texto
            var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(panelGO.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(30f, 20f);
            textRT.offsetMax = new Vector2(-30f, -20f);
            label = textGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 40;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = "";
        }
    }
}
