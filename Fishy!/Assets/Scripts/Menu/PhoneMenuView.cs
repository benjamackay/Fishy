using System;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Phone shell for the existing Tab pages. No scene references are replaced.</summary>
[DisallowMultipleComponent]
public class PhoneMenuView : MonoBehaviour
{
    public static readonly Color Shell = Paleta.Hex(0x523224);
    public static readonly Color Surface = Paleta.Hex(0x714735);
    public static readonly Color Accent = Paleta.Hex(0xC98D70);
    public bool IsBuilt { get; private set; }
    TabsController tabs;
    RectTransform frame;
    GameObject home, back;
    TMP_Text title, clock;
    Button firstApp;

    public void Build(TabsController controller)
    {
        if (IsBuilt) return;
        tabs = controller;
        frame = (RectTransform)transform;
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
        frame.anchoredPosition = Vector2.zero;
        frame.sizeDelta = new Vector2(930, 516);
        Paint(GetComponent<Image>(), Color.black);
        var screen = Box(transform, "PhoneScreen", 12, 12, 906, 492, Shell, true);
        screen.SetAsFirstSibling();
        var layout = tabs.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        foreach (Transform child in tabs.transform) child.gameObject.SetActive(false);
        var pagesRoot = tabs.pagesRoot != null ? tabs.pagesRoot : transform.Find("Pages");
        if (pagesRoot != null) Stretch((RectTransform)pagesRoot);

        Box(transform, "StatusBar", 28, 22, 874, 40, Paleta.Hex(0x0D171C));
        clock = Text(transform, "Clock", "", 40, 22, 150, 40, 28, Color.white, false);
        Icon(transform, PhoneMenuIcon.Kind.Status, 775, 28, 110, 26, Color.white);
        title = Text(transform, "Heading", "Otto", 40, 76, 660, 45, 34, Accent);
        back = Button(transform, "Volver al inicio", 30, 76, 60, 45, ShowHome, false).gameObject;
        Icon(back.transform, PhoneMenuIcon.Kind.Back, 5, 4, 48, 36, Accent);
        home = Box(transform, "PhoneHome", 0, 0, 930, 516, Color.clear).gameObject;
        home.GetComponent<Image>().raycastTarget = false;
        string[] names = { "PlayerPage", "QuestPage", "InventoryPage", "MapPage" };
        string[] labels = { "Perfil", "Misiones", "Inventario", "Mapa" };
        for (int n = 0; n < names.Length; n++)
        {
            int page = Array.FindIndex(tabs.pages, p => p != null && p.name == names[n]);
            float x = 58 + n * 208;
            var button = Button(home.transform, labels[n], x, 130, 192, 192, () => ShowPage(page));
            button.interactable = page >= 0;
            if (n == 0) firstApp = button;
            Icon(button.transform, (PhoneMenuIcon.Kind)n, 30, 28, 132, 140, Accent);
            Text(home.transform, labels[n] + "Label", labels[n], x - 8, 329, 208, 52, 34, Accent).alignment = TextAlignmentOptions.Center;
        }
        int settings = Array.FindIndex(tabs.pages, p => p != null && p.name == "SettingsPage");
        var settingsButton = Button(transform, "Opciones", 840, 424, 66, 66, () => ShowPage(settings), false);
        settingsButton.interactable = settings >= 0;
        Icon(settingsButton.transform, PhoneMenuIcon.Kind.Settings, 8, 8, 50, 50, Accent);

        foreach (var page in tabs.pages)
        {
            if (page == null) continue;
            page.SetActive(false);
            Place((RectTransform)page.transform, 58, 134, 766, 352);
            if (page.TryGetComponent<Image>(out var image)) Paint(image, Accent);
            var inset = Box(page.transform, "PhonePageSurface", 6, 6, 754, 340, Surface, true);
            inset.SetAsFirstSibling();
            if (page.name == "QuestPage") SetupQuests(page);
            if (page.name == "InventoryPage") SetupInventory(page);
        }
        IsBuilt = true;
        Fit();
        ShowHome();
    }

    void OnEnable() { if (IsBuilt) ShowHome(); }
    void Update()
    {
        if (!IsBuilt) return;
        clock.text = DateTime.Now.ToString("HH:mm");
        Fit();
    }
    void Fit()
    {
        var parent = frame.parent as RectTransform;
        if (parent == null) return;
        float scale = Mathf.Min(parent.rect.width * .82f / 930, parent.rect.height * .82f / 516);
        frame.localScale = Vector3.one * Mathf.Max(.01f, scale);
    }
    public void ShowHome()
    {
        if (!IsBuilt) return;
        foreach (var page in tabs.pages) if (page != null) page.SetActive(false);
        home.SetActive(true);
        back.SetActive(false);
        title.text = "Otto";
        Place(title.rectTransform, 40, 76, 660, 45);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstApp.gameObject);
    }
    public void ShowPage(int index)
    {
        if (!IsBuilt || index < 0 || index >= tabs.pages.Length || tabs.pages[index] == null) return;
        home.SetActive(false);
        back.SetActive(true);
        for (int i = 0; i < tabs.pages.Length; i++) if (tabs.pages[i] != null) tabs.pages[i].SetActive(i == index);
        string pageName = tabs.pages[index].name;
        title.text = pageName == "QuestPage" ? "Misiones" : pageName == "InventoryPage" ? "Inventario" : pageName == "PlayerPage" ? "Perfil" : pageName == "MapPage" ? "Mapa" : "Opciones";
        Place(title.rectTransform, 100, 76, 650, 45);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(back);
    }

    void SetupQuests(GameObject page)
    {
        var ui = page.GetComponent<QuestPageUI>();
        if (ui == null) ui = page.AddComponent<QuestPageUI>();
        // Force initialization before moving its existing content; preserve its live subscription.
        page.SetActive(true);
        page.SetActive(false);
        var content = ui.questContainer as RectTransform;
        HideOldChildren(page.transform, content);
        var scroll = Scroll(page.transform);
        content.SetParent(scroll.viewport, false);
        Content(content);
        scroll.content = content;
        var group = content.GetComponent<VerticalLayoutGroup>();
        if (group != null) { group.spacing = 24; group.padding = new RectOffset(16, 24, 16, 16); }
        ui.tituloFontSize = 25;
        ui.objetivoFontSize = 20;
        ui.verboseLogs = false;
        ui.phoneLayout = true;
    }
    void SetupInventory(GameObject page)
    {
        var ui = page.GetComponent<InventoryManagerUI>();
        if (ui == null) ui = page.AddComponent<InventoryManagerUI>();
        page.SetActive(true);
        page.SetActive(false);
        var content = ui.inventoryContainer as RectTransform;
        HideOldChildren(page.transform, content);
        var scroll = Scroll(page.transform);
        content.SetParent(scroll.viewport, false);
        Content(content);
        scroll.content = content;
        var grid = content.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.cellSize = new Vector2(117, 102);
        grid.spacing = new Vector2(5, 5);
        grid.padding = new RectOffset(3, 3, 3, 3);
        var lines = content.GetComponent<Image>();
        if (lines == null) lines = content.gameObject.AddComponent<Image>();
        lines.color = Accent;
        lines.raycastTarget = false;
        ui.minimumVisibleSlots = 18;
        ui.slotBackgroundColor = Surface;
        ui.emptyMessage = "";
        ui.verboseLogs = false;
        ui.Refresh();
        foreach (Transform slot in content) if (slot.TryGetComponent<Image>(out var image)) image.color = Surface;
    }
    static void HideOldChildren(Transform page, RectTransform content)
    {
        if (content == null) throw new InvalidOperationException("Phone page has no content container.");
        content.SetParent(page, false);
        foreach (Transform child in page)
            if (child != content && child.name != "PhonePageSurface") child.gameObject.SetActive(false);
    }
    static void Content(RectTransform content)
    {
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
    static ScrollRect Scroll(Transform page)
    {
        var area = Box(page, "PhoneScroll", 14, 14, 738, 324, Color.clear);
        var viewport = Box(area, "Viewport", 0, 0, 734, 324, Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
        var track = Box(area, "ScrollTrack", 736, 0, 8, 324, Shell, true);
        var handle = Box(track, "Handle", 0, 0, 8, 80, Accent, true);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }
    public static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;
    }
    static void Stretch(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    static void Paint(Image image, Color color)
    { if (image == null) return; image.sprite = FishyUIKit.SpriteRedondeado(); image.type = Image.Type.Sliced; image.color = color; }
    static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool round = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false); Place(rt, x, y, w, h);
        var image = go.GetComponent<Image>(); image.color = color;
        if (round) Paint(image, color);
        return rt;
    }
    static TMP_Text Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color, bool heading = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false); Place(rt, x, y, w, h);
        var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.color = color;
        text.font = heading ? FishyUIKit.Titulos : FishyUIKit.Cuerpo;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        return text;
    }
    static Button Button(Transform parent, string name, float x, float y, float w, float h, UnityEngine.Events.UnityAction action, bool tile = true)
    {
        var rt = Box(parent, name, x, y, w, h, tile ? Accent : Color.clear, tile);
        var button = rt.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
        button.targetGraphic = rt.GetComponent<Image>();
        if (tile)
        {
            rt.GetComponent<Image>().sprite = FishyUIKit.SpriteRedondeado(128, 48);
            var inner = Box(rt, "TileSurface", 6, 6, w - 12, h - 12, Surface, true);
            inner.GetComponent<Image>().sprite = FishyUIKit.SpriteRedondeado(128, 42);
            inner.GetComponent<Image>().raycastTarget = false;
        }
        var colors = button.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f); colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.8f, .8f, .8f); button.colors = colors;
        return button;
    }
    static void Icon(Transform parent, PhoneMenuIcon.Kind kind, float x, float y, float w, float h, Color color)
    {
        var go = new GameObject(kind.ToString(), typeof(RectTransform), typeof(PhoneMenuIcon));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false); Place(rt, x, y, w, h);
        var graphic = go.GetComponent<PhoneMenuIcon>(); graphic.kind = kind; graphic.color = color; graphic.raycastTarget = false;
    }
}
