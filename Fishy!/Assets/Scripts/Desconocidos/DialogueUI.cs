using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.Desconocidos
{
    /// <summary>
    /// HDU-2 — Presentación del diálogo con los NPCs (panel inferior con la línea
    /// del NPC y los botones de respuesta del niño/a).
    ///
    /// Si no se asignan referencias en el inspector, construye su propia UI en
    /// tiempo de ejecución (Canvas + panel + botones), de modo que funciona sin
    /// montar nada en la escena. La lógica vive en <see cref="DialogueController"/>.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        public static DialogueUI Instance { get; private set; }

        [Header("Referencias (opcionales: si faltan, se generan en runtime)")]
        public GameObject root;
        public Text nameLabel;
        public Text bodyText;
        public RectTransform choicesContainer;

        private readonly List<GameObject> spawnedChoices = new List<GameObject>();
        private Font font;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (root == null || nameLabel == null || bodyText == null || choicesContainer == null)
                BuildRuntimeUI();

            Hide();
        }

        public static DialogueUI GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("DialogueUI");
                Instance = go.AddComponent<DialogueUI>();
            }
            return Instance;
        }

        public void Show() { if (root != null) root.SetActive(true); }

        public void Hide()
        {
            ClearChoices();
            if (root != null) root.SetActive(false);
        }

        /// <summary>Muestra la línea del NPC (o del sistema) con su nombre.</summary>
        public void SetLine(string speaker, string line)
        {
            Show();
            if (nameLabel != null) nameLabel.text = speaker;
            if (bodyText != null) bodyText.text = line;
        }

        /// <summary>Muestra las opciones; invoca onPick con el índice elegido.</summary>
        public void SetChoices(IReadOnlyList<string> choices, Action<int> onPick)
        {
            ClearChoices();
            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                var btn = CreateButton(choices[i], () => onPick?.Invoke(index));
                spawnedChoices.Add(btn);
            }
        }

        /// <summary>Muestra un único botón de continuar/cerrar.</summary>
        public void SetContinue(string label, Action onClick)
        {
            ClearChoices();
            spawnedChoices.Add(CreateButton(label, onClick));
        }

        private void ClearChoices()
        {
            foreach (var go in spawnedChoices)
                if (go != null) Destroy(go);
            spawnedChoices.Clear();
        }

        // ── Construcción de UI ─────────────────────────────────────────────────
        private GameObject CreateButton(string text, Action onClick)
        {
            var btnGO = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGO.transform.SetParent(choicesContainer, false);
            btnGO.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.34f, 0.95f);
            btnGO.GetComponent<LayoutElement>().minHeight = 70f;

            var btn = btnGO.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.28f, 0.4f, 0.6f, 1f);
            colors.pressedColor = new Color(0.1f, 0.15f, 0.25f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(btnGO.transform, false);
            var rt = txtGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, 8f); rt.offsetMax = new Vector2(-20f, -8f);
            var t = txtGO.GetComponent<Text>();
            t.font = font; t.fontSize = 30; t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;

            return btnGO;
        }

        private void BuildRuntimeUI()
        {
            Fishy.UI.UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("DialogueCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasGO.transform, false);
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero; rootRT.offsetMax = Vector2.zero;

            // Panel inferior con la línea del NPC.
            var panelGO = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
            panelGO.transform.SetParent(root.transform, false);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0f);
            panelRT.anchorMax = new Vector2(0.5f, 0f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.anchoredPosition = new Vector2(0f, 40f);
            panelRT.sizeDelta = new Vector2(1400f, 420f);
            panelGO.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.15f, 0.92f);

            // Nombre del NPC.
            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGO.transform.SetParent(panelGO.transform, false);
            var nameRT = nameGO.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 1f); nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.pivot = new Vector2(0f, 1f);
            nameRT.anchoredPosition = new Vector2(30f, -16f);
            nameRT.sizeDelta = new Vector2(-60f, 50f);
            nameLabel = nameGO.GetComponent<Text>();
            nameLabel.font = font; nameLabel.fontSize = 34; nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.color = new Color(0.6f, 0.85f, 1f);

            // Texto del NPC.
            var bodyGO = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGO.transform.SetParent(panelGO.transform, false);
            var bodyRT = bodyGO.GetComponent<RectTransform>();
            bodyRT.anchorMin = new Vector2(0f, 1f); bodyRT.anchorMax = new Vector2(1f, 1f);
            bodyRT.pivot = new Vector2(0f, 1f);
            bodyRT.anchoredPosition = new Vector2(30f, -70f);
            bodyRT.sizeDelta = new Vector2(-60f, 120f);
            bodyText = bodyGO.GetComponent<Text>();
            bodyText.font = font; bodyText.fontSize = 32; bodyText.color = Color.white;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;

            // Contenedor de opciones (lista vertical).
            var choicesGO = new GameObject("Choices",
                typeof(RectTransform), typeof(VerticalLayoutGroup));
            choicesGO.transform.SetParent(panelGO.transform, false);
            choicesContainer = choicesGO.GetComponent<RectTransform>();
            choicesContainer.anchorMin = new Vector2(0f, 0f);
            choicesContainer.anchorMax = new Vector2(1f, 0f);
            choicesContainer.pivot = new Vector2(0.5f, 0f);
            choicesContainer.anchoredPosition = new Vector2(0f, 20f);
            choicesContainer.sizeDelta = new Vector2(-60f, 230f);
            var vlg = choicesGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        }
    }
}
