using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Fishy.Detective
{
    /// <summary>
    /// HDU-10 — Interfaz del modo detective.
    /// Burbujas izquierda/derecha según autor. Click en burbuja = marcar/desmarcar con borde rojo.
    /// Se autogenera en runtime sin prefabs ni referencias en inspector.
    /// </summary>
    public class DetectiveUI : MonoBehaviour
    {
        public static DetectiveUI Instance { get; private set; }

        // ── Referencias runtime ───────────────────────────────────────────────
        private GameObject    _window;
        private RectTransform _content;
        private ScrollRect    _scrollRect;
        private GameObject    _panelResultado;
        private Text          _txtResultado;
        private Button        _btnConfirmar;
        private Button        _btnRepetir;
        private Button        _btnVerExplicacion;
        private RectTransform _contenedorExplicaciones;
        private Font          _font;

        // ── Estado ────────────────────────────────────────────────────────────
        private DetectiveCaseManager _manager;
        private Action _onCerrar;
        private Action _onRepetir;
        private List<(DetectiveMessage mensaje, string explicacion)> _noIdentificados;

        // ── Paleta ────────────────────────────────────────────────────────────
        private static readonly Color ColFondo         = new Color(0.10f, 0.12f, 0.15f, 1f);
        private static readonly Color ColHeader        = new Color(0.13f, 0.20f, 0.18f, 1f);
        private static readonly Color ColScroll        = new Color(0.08f, 0.10f, 0.13f, 1f);
        private static readonly Color ColBurbuja1      = new Color(0.18f, 0.21f, 0.26f, 1f); // izquierda
        private static readonly Color ColBurbuja2      = new Color(0.10f, 0.28f, 0.22f, 1f); // derecha
        private static readonly Color ColBorde         = new Color(0.85f, 0.15f, 0.15f, 1f); // rojo marcado
        private static readonly Color ColBarraInferior = new Color(0.10f, 0.14f, 0.16f, 1f);
        private static readonly Color ColBtnConfirmar  = new Color(0.08f, 0.47f, 0.35f, 1f);
        private static readonly Color ColPanelRes      = new Color(0.05f, 0.07f, 0.09f, 0.97f);
        private static readonly Color ColCard          = new Color(0.12f, 0.16f, 0.18f, 1f);
        private static readonly Color ColBtnRepetir    = new Color(0.18f, 0.22f, 0.27f, 1f);
        private static readonly Color ColBtnExplica    = new Color(0.28f, 0.16f, 0.38f, 1f);

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        // ── API pública ───────────────────────────────────────────────────────

        public void Inicializar(DetectiveCaseManager manager, Action onCerrar, Action onRepetir)
        {
            _manager   = manager;
            _onCerrar  = onCerrar;
            _onRepetir = onRepetir;
        }

        public void MostrarConversacion()
        {
            LimpiarHistorial();
            LimpiarExplicaciones();
            _panelResultado.SetActive(false);
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
                yield return new WaitForSeconds(0.5f);
            }
            _btnConfirmar.interactable = true;
        }

        private void CrearBurbuja(DetectiveMessage msg)
        {
            bool esIzquierda = msg.autor == _manager.GetMensajes()[0].autor;
            Color colorBase  = esIzquierda ? ColBurbuja1 : ColBurbuja2;

            // ── Fila ─────────────────────────────────────────────────────────
            var row = new GameObject("Row_" + msg.id,
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_content, false);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = esIzquierda ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            hlg.childControlWidth     = true; hlg.childControlHeight     = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(8, 8, 3, 3);

            // Spacer lado contrario para empujar la burbuja
            if (!esIzquierda) AgendarSpacer(row.transform);

            // ── Burbuja ───────────────────────────────────────────────────────
            var bubble = new GameObject("Bubble",
                typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement),
                typeof(Button));                          // Button para capturar el click
            bubble.transform.SetParent(row.transform, false);

            var bubbleImg = bubble.GetComponent<Image>();
            bubbleImg.color = colorBase;

            // Borde (Outline component) — invisible por defecto, rojo al marcar
            var outline = bubble.AddComponent<Outline>();
            outline.effectColor    = Color.clear;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;

            var vlg = bubble.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 10, 10);
            vlg.spacing = 5f;
            vlg.childControlWidth     = true; vlg.childControlHeight     = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var fitter = bubble.GetComponent<ContentSizeFitter>();
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            var le = bubble.GetComponent<LayoutElement>();
            le.preferredWidth = 480f;
            le.flexibleWidth  = 200f;

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
                var autorGO = new GameObject("Autor", typeof(RectTransform), typeof(Text));
                autorGO.transform.SetParent(bubble.transform, false);
                var at = autorGO.GetComponent<Text>();
                at.font      = _font; at.fontSize = 18; at.fontStyle = FontStyle.Bold;
                at.color     = new Color(0.45f, 0.85f, 0.68f, 1f);
                at.text      = msg.autor;
                at.horizontalOverflow = HorizontalWrapMode.Wrap;
                at.verticalOverflow   = VerticalWrapMode.Overflow;
            }

            // Texto del mensaje
            var txtGO = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(bubble.transform, false);
            var txt = txtGO.GetComponent<Text>();
            txt.font      = _font; txt.fontSize = 22; txt.color = Color.white;
            txt.alignment = TextAnchor.UpperLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            txt.text = msg.texto;

            // Timestamp
            var timeGO = new GameObject("Time", typeof(RectTransform), typeof(Text));
            timeGO.transform.SetParent(bubble.transform, false);
            var timeTxt = timeGO.GetComponent<Text>();
            timeTxt.font      = _font; timeTxt.fontSize = 16;
            timeTxt.color     = new Color(1f, 1f, 1f, 0.4f);
            timeTxt.alignment = TextAnchor.MiddleRight;
            timeTxt.text      = DateTime.Now.ToString("HH:mm");

            // ── Click para marcar/desmarcar ───────────────────────────────────
            btn.onClick.AddListener(() =>
            {
                _manager.ToggleMarca(msg.id);
                bool marcado = _manager.EstaMarcado(msg.id);

                // Borde rojo visible / invisible
                outline.effectColor = marcado ? ColBorde : Color.clear;

                // Leve tinte rojo en la burbuja al marcar
                bubbleImg.color = marcado
                    ? Color.Lerp(colorBase, new Color(0.6f, 0.1f, 0.1f, 1f), 0.2f)
                    : colorBase;
            });

            ScrollToBottom();
        }

        private static void AgendarSpacer(Transform parent)
        {
            var sp = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            sp.transform.SetParent(parent, false);
            var le = sp.GetComponent<LayoutElement>();
            le.minWidth      = 60f;
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

            _txtResultado.text = r.totalRiesgo > 0
                ? $"Identificaste {r.aciertos} de {r.totalRiesgo} señales de riesgo."
                : "¡No había señales de riesgo en esta conversación!";

            _btnRepetir.gameObject.SetActive(r.DebeOfrecerRepetir);
            _btnVerExplicacion.gameObject.SetActive(r.DebeOfrecerRepetir);
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
                item.GetComponent<Image>().color = new Color(0.15f, 0.1f, 0.25f, 0.95f);
                var vlg = item.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(14, 14, 10, 10);
                vlg.childControlWidth = true; vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                item.GetComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;

                var txtGO = new GameObject("Txt", typeof(RectTransform), typeof(Text));
                txtGO.transform.SetParent(item.transform, false);
                var t = txtGO.GetComponent<Text>();
                t.font = _font; t.fontSize = 20; t.color = Color.white;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow   = VerticalWrapMode.Overflow;
                t.text = $"<b>\"{msg.texto}\"</b>\n{exp}";
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
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Panel principal
            var panel = new GameObject("ChatWindow", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_window.transform, false);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot     = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(900f, 600f);
            panel.GetComponent<Image>().color = ColFondo;

            BuildHeader(panel.transform);
            BuildScroll(panel.transform);
            BuildBarraInferior(panel.transform);
            BuildPanelResultado(_window.transform);
        }

        private void BuildHeader(Transform parent)
        {
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(parent, false);
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 60f);
            header.GetComponent<Image>().color = ColHeader;

            // Avatar
            var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatar.transform.SetParent(header.transform, false);
            var avRT = avatar.GetComponent<RectTransform>();
            avRT.anchorMin = new Vector2(0f, 0.5f); avRT.anchorMax = new Vector2(0f, 0.5f);
            avRT.pivot     = new Vector2(0f, 0.5f);
            avRT.anchoredPosition = new Vector2(14f, 0f);
            avRT.sizeDelta        = new Vector2(40f, 40f);
            avatar.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.4f, 1f);

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Text));
            iconGO.transform.SetParent(avatar.transform, false);
            Stretch(iconGO.GetComponent<RectTransform>());
            var icon = iconGO.GetComponent<Text>();
            icon.font = _font; icon.fontSize = 22; icon.color = Color.white;
            icon.alignment = TextAnchor.MiddleCenter; icon.text = "🔍";

            // Título
            var lblGO = new GameObject("Titulo", typeof(RectTransform), typeof(Text));
            lblGO.transform.SetParent(header.transform, false);
            var lblRT = lblGO.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0f, 0.5f); lblRT.anchorMax = new Vector2(1f, 1f);
            lblRT.offsetMin = new Vector2(64f, 0f);  lblRT.offsetMax = new Vector2(-12f, 0f);
            var lbl = lblGO.GetComponent<Text>();
            lbl.font = _font; lbl.fontSize = 22; lbl.fontStyle = FontStyle.Bold;
            lbl.color = Color.white; lbl.alignment = TextAnchor.LowerLeft;
            lbl.text = "Modo Detective";

            // Subtítulo
            var subGO = new GameObject("Sub", typeof(RectTransform), typeof(Text));
            subGO.transform.SetParent(header.transform, false);
            var subRT = subGO.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0f, 0f); subRT.anchorMax = new Vector2(1f, 0.5f);
            subRT.offsetMin = new Vector2(64f, 0f); subRT.offsetMax = new Vector2(-12f, 0f);
            var sub = subGO.GetComponent<Text>();
            sub.font = _font; sub.fontSize = 17;
            sub.color = new Color(0.55f, 0.88f, 0.72f, 1f);
            sub.alignment = TextAnchor.UpperLeft;
            sub.text = "toca un mensaje para marcarlo como sospechoso";
        }

        private void BuildScroll(Transform parent)
        {
            var scrollGO = new GameObject("Scroll",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGO.transform.SetParent(parent, false);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0f, 0f); scrollRT.anchorMax = new Vector2(1f, 1f);
            scrollRT.offsetMin = new Vector2(0f, 58f);
            scrollRT.offsetMax = new Vector2(0f, -60f);
            scrollGO.GetComponent<Image>().color = ColScroll;
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
            cvlg.spacing  = 6f; cvlg.padding = new RectOffset(0, 0, 8, 8);
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
            barraRT.sizeDelta = new Vector2(0f, 58f);
            barra.GetComponent<Image>().color = ColBarraInferior;

            var btnGO = new GameObject("BtnConfirmar",
                typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(barra.transform, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0f, 0f); btnRT.anchorMax = new Vector2(1f, 1f);
            btnRT.offsetMin = new Vector2(14f, 9f); btnRT.offsetMax = new Vector2(-14f, -9f);
            btnGO.GetComponent<Image>().color = ColBtnConfirmar;
            _btnConfirmar = btnGO.GetComponent<Button>();
            _btnConfirmar.onClick.AddListener(OnConfirmar);

            var tGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tGO.transform.SetParent(btnGO.transform, false);
            Stretch(tGO.GetComponent<RectTransform>());
            var t = tGO.GetComponent<Text>();
            t.font = _font; t.fontSize = 22; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = "✔ Confirmar marcas";
        }

        private void BuildPanelResultado(Transform parent)
        {
            _panelResultado = new GameObject("PanelResultado",
                typeof(RectTransform), typeof(Image));
            _panelResultado.transform.SetParent(parent, false);
            Stretch(_panelResultado.GetComponent<RectTransform>());
            _panelResultado.GetComponent<Image>().color = ColPanelRes;

            var card = new GameObject("Card",
                typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            card.transform.SetParent(_panelResultado.transform, false);
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f); cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot     = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(480f, 0f);
            card.GetComponent<Image>().color = ColCard;
            var cvlg = card.GetComponent<VerticalLayoutGroup>();
            cvlg.padding = new RectOffset(28, 28, 28, 28); cvlg.spacing = 16f;
            cvlg.childControlWidth     = true; cvlg.childControlHeight     = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            card.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            AgregarTexto(card.transform, "🔍 Resultado", 26, FontStyle.Bold, TextAnchor.MiddleCenter);

            var resGO = new GameObject("Resultado", typeof(RectTransform), typeof(Text));
            resGO.transform.SetParent(card.transform, false);
            _txtResultado = resGO.GetComponent<Text>();
            _txtResultado.font      = _font; _txtResultado.fontSize = 22;
            _txtResultado.color     = Color.white;
            _txtResultado.alignment = TextAnchor.MiddleCenter;
            _txtResultado.horizontalOverflow = HorizontalWrapMode.Wrap;
            _txtResultado.verticalOverflow   = VerticalWrapMode.Overflow;

            var expGO = new GameObject("Explicaciones",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            expGO.transform.SetParent(card.transform, false);
            _contenedorExplicaciones = expGO.GetComponent<RectTransform>();
            var explVlg = expGO.GetComponent<VerticalLayoutGroup>();
            explVlg.spacing = 10f;
            explVlg.childControlWidth = true; explVlg.childControlHeight = true;
            explVlg.childForceExpandWidth = true; explVlg.childForceExpandHeight = false;
            expGO.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            _btnRepetir = CrearBotonCard(card.transform, "↺ Repetir caso", ColBtnRepetir,
                () => { Hide(); _onRepetir?.Invoke(); });
            _btnVerExplicacion = CrearBotonCard(card.transform, "💡 Ver explicación", ColBtnExplica,
                () => MostrarExplicaciones());
            CrearBotonCard(card.transform, "Continuar", ColBtnConfirmar,
                () => { Hide(); _onCerrar?.Invoke(); });

            _panelResultado.SetActive(false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Button CrearBotonCard(Transform parent, string texto, Color color, Action onClick)
        {
            var btnGO = new GameObject("Btn_" + texto,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGO.transform.SetParent(parent, false);
            btnGO.GetComponent<Image>().color = color;
            btnGO.GetComponent<LayoutElement>().minHeight = 52f;
            var btn = btnGO.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var tGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tGO.transform.SetParent(btnGO.transform, false);
            Stretch(tGO.GetComponent<RectTransform>());
            var t = tGO.GetComponent<Text>();
            t.font = _font; t.fontSize = 22; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.text = texto;

            return btn;
        }

        private void AgregarTexto(Transform parent, string texto, int size,
            FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.UpperLeft)
        {
            var go = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.color = Color.white; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            t.text = texto;
        }

        private void LimpiarHistorial()
        {
            if (_content == null) return;
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
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