using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.Mision
{
    /// <summary>
    /// HDU-1 — Panel de "Misión activa": lista los desafíos del <see cref="MissionManager"/>,
    /// distinguiendo visualmente los disponibles (punto amarillo) de los completados
    /// (punto verde + "✔ Completado").
    ///
    /// No requiere montaje manual en la escena: si no se le asignan referencias en el
    /// inspector, construye su propia UI (botón "Misiones" + panel lateral) en tiempo
    /// de ejecución, igual que <c>ZonePopupUI</c> y <c>DialogueUI</c>.
    ///
    /// Uso: basta con que exista en la escena (o llamar a <see cref="GetOrCreate"/>).
    /// El botón "Misiones" arriba a la derecha abre/cierra el panel.
    /// </summary>
    public class MissionPanelUI : MonoBehaviour
    {
        public static MissionPanelUI Instance { get; private set; }

        [Header("Referencias (opcionales: si faltan, se generan en runtime)")]
        public GameObject panelRoot;
        public Transform listContainer;
        public Button toggleButton;

        [Header("Aspecto")]
        public Color colorDisponible = new Color(1f, 0.85f, 0.2f);
        public Color colorCompletado = new Color(0.4f, 0.85f, 0.4f);

        [Tooltip("Si está activo, el panel se muestra abierto la primera vez que aparece un desafío nuevo.")]
        public bool abrirAlHaberNovedad = true;

        private readonly List<GameObject> renderedRows = new List<GameObject>();
        private bool panelVisible;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (panelRoot == null || listContainer == null || toggleButton == null)
                BuildRuntimeUI();

            SetPanelVisible(false);
        }

        private void OnEnable()
        {
            var mm = MissionManager.GetOrCreate();
            mm.onPanelActualizado.AddListener(Refresh);
            mm.onDesafioDisponible.AddListener(OnNuevoDesafioDisponible);
            Refresh();
        }

        private void OnDisable()
        {
            if (MissionManager.Instance == null) return;
            MissionManager.Instance.onPanelActualizado.RemoveListener(Refresh);
            MissionManager.Instance.onDesafioDisponible.RemoveListener(OnNuevoDesafioDisponible);
        }

        /// <summary>Devuelve la instancia activa, creándola si aún no existe en la escena.</summary>
        public static MissionPanelUI GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("MissionPanelUI");
                Instance = go.AddComponent<MissionPanelUI>();
            }
            return Instance;
        }

        private void OnNuevoDesafioDisponible(DesafioRuntime _)
        {
            if (abrirAlHaberNovedad) SetPanelVisible(true);
        }

        public void TogglePanel() => SetPanelVisible(!panelVisible);

        public void SetPanelVisible(bool visible)
        {
            panelVisible = visible;
            if (panelRoot != null) panelRoot.SetActive(visible);
        }

        /// <summary>Reconstruye la lista visible a partir del estado actual del MissionManager.</summary>
        public void Refresh()
        {
            foreach (var row in renderedRows)
                if (row != null) Destroy(row);
            renderedRows.Clear();

            if (MissionManager.Instance == null || listContainer == null) return;

            var lista = MissionManager.Instance.GetListaOrdenada();
            foreach (var desafio in lista)
                renderedRows.Add(BuildRow(desafio));

            if (lista.Count == 0)
                renderedRows.Add(BuildEmptyRow());
        }

        private GameObject BuildEmptyRow()
        {
            var rowGO = new GameObject("SinDesafios", typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            rowGO.transform.SetParent(listContainer, false);
            rowGO.GetComponent<LayoutElement>().minHeight = 40f;
            var text = rowGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = new Color(1f, 1f, 1f, 0.6f);
            text.text = "Sin desafíos por ahora.";
            return rowGO;
        }

        private GameObject BuildRow(DesafioRuntime desafio)
        {
            var rowGO = new GameObject($"Desafio_{desafio.Id}", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGO.transform.SetParent(listContainer, false);

            var rowLayout = rowGO.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = false;
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            var rowLE = rowGO.GetComponent<LayoutElement>();
            rowLE.minHeight = 44f;
            rowLE.preferredHeight = 44f;

            // Punto de estado.
            var dotGO = new GameObject("Estado", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            dotGO.transform.SetParent(rowGO.transform, false);
            var dotLE = dotGO.GetComponent<LayoutElement>();
            dotLE.minWidth = 20f;
            dotLE.minHeight = 20f;
            dotGO.GetComponent<Image>().color =
                desafio.estado == EstadoDesafio.Completado ? colorCompletado : colorDisponible;

            // Título + estado.
            var textGO = new GameObject("Titulo", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textGO.transform.SetParent(rowGO.transform, false);
            var textLE = textGO.GetComponent<LayoutElement>();
            textLE.minWidth = 300f;
            textLE.flexibleWidth = 1f;
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            string estadoTexto = desafio.estado == EstadoDesafio.Completado ? "✔ Completado" : "Disponible";
            text.text = $"{desafio.Titulo} — {estadoTexto}";

            return rowGO;
        }

        // ── Construcción de UI por defecto ─────────────────────────────────────
        private void BuildRuntimeUI()
        {
            var canvasGO = new GameObject("MisionCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Botón "Misiones" (esquina superior derecha).
            var btnGO = new GameObject("BotonMisiones", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(canvasGO.transform, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(1f, 1f);
            btnRT.anchorMax = new Vector2(1f, 1f);
            btnRT.pivot = new Vector2(1f, 1f);
            btnRT.anchoredPosition = new Vector2(-30f, -30f);
            btnRT.sizeDelta = new Vector2(180f, 60f);
            btnGO.GetComponent<Image>().color = new Color(0.1f, 0.15f, 0.3f, 0.9f);
            toggleButton = btnGO.GetComponent<Button>();
            toggleButton.onClick.AddListener(TogglePanel);

            var btnTextGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTextRT = btnTextGO.GetComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.offsetMin = Vector2.zero;
            btnTextRT.offsetMax = Vector2.zero;
            var btnText = btnTextGO.GetComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 28;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.text = "Misiones";

            // Panel lateral con la lista de desafíos.
            var panelGO = new GameObject("PanelMisiones",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(1f, 1f);
            panelRT.anchorMax = new Vector2(1f, 1f);
            panelRT.pivot = new Vector2(1f, 1f);
            panelRT.anchoredPosition = new Vector2(-30f, -100f);
            panelRT.sizeDelta = new Vector2(440f, 0f);
            panelGO.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.15f, 0.9f);

            var vlg = panelGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.spacing = 8f;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = panelGO.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            panelRoot = panelGO;
            listContainer = panelGO.transform;
        }
    }
}
