using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Navigation only: all visuals and page references are authored in the scene.</summary>
[DisallowMultipleComponent]
public class PhoneMenuNavigation : MonoBehaviour
{
    public GameObject home;
    public GameObject[] pages;
    public string[] pageTitles;
    public Button backButton;
    public Button firstButton;
    public TMP_Text heading;
    public TMP_Text clock;
    public string homeTitle = "Otto";
    public Vector2 pageHeadingOffset = new Vector2(90, 0);
    Vector2 homeHeadingPosition;
    bool initialized;

    void Awake() { Initialize(); }
    void Initialize()
    {
        if (initialized) return;
        if (heading != null) homeHeadingPosition = heading.rectTransform.anchoredPosition;
        initialized = true;
    }
    void OnEnable() { ShowHome(); UpdateClock(); }
    void Update() { UpdateClock(); }
    void UpdateClock()
    {
        if (clock == null) return;
        string value = DateTime.Now.ToString("HH:mm");
        if (clock.text != value) clock.text = value;
    }
    public void ShowHome()
    {
        Initialize();
        if (pages != null) foreach (var page in pages) if (page != null) page.SetActive(false);
        if (home != null) home.SetActive(true);
        if (backButton != null) backButton.gameObject.SetActive(false);
        if (heading != null)
        {
            heading.text = homeTitle;
            heading.rectTransform.anchoredPosition = homeHeadingPosition;
        }
        Select(firstButton);
    }
    public void ShowPage(int index)
    {
        Initialize();
        if (pages == null || index < 0 || index >= pages.Length || pages[index] == null) return;
        if (home != null) home.SetActive(false);
        for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(i == index);
        if (backButton != null) backButton.gameObject.SetActive(true);
        if (heading != null)
        {
            heading.text = pageTitles != null && index < pageTitles.Length ? pageTitles[index] : pages[index].name;
            heading.rectTransform.anchoredPosition = homeHeadingPosition + pageHeadingOffset;
        }
        Canvas.ForceUpdateCanvases();
        foreach (var scroll in pages[index].GetComponentsInChildren<ScrollRect>(true))
        {
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
        Select(backButton);
    }
    static void Select(Button button)
    {
        if (button != null && button.gameObject.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
