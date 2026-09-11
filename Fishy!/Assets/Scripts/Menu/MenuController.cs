using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.UI;
public class MenuController : MonoBehaviour

{
    public GameObject menuCanvas;
    RawImage backdrop;
    Texture2D snapshot;
    Coroutine opening;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (menuCanvas != null && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (opening != null) { StopCoroutine(opening); opening = null; return; }
            if (menuCanvas.activeSelf) Close();
            else opening = StartCoroutine(Open());
        }
    }
    IEnumerator Open()
    {
        yield return new WaitForEndOfFrame();
        if (backdrop == null)
        {
            var go = new GameObject("PhoneBackdrop", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(menuCanvas.transform.parent, false);
            go.transform.SetSiblingIndex(menuCanvas.transform.GetSiblingIndex());
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            backdrop = go.GetComponent<RawImage>();
            Color colorPanel = new Color32(82, 50, 36, 255); // #523224
            backdrop.color = Color.Lerp(Color.white, colorPanel, 0.4f);
        }
        if (snapshot != null) Destroy(snapshot);
        var capture = ScreenCapture.CaptureScreenshotAsTexture();
        int w = Mathf.Max(1, Screen.width / 8), h = Mathf.Max(1, Screen.height / 8);
        var reduced = RenderTexture.GetTemporary(w, h, 0);
        var previous = RenderTexture.active;
        Graphics.Blit(capture, reduced);
        RenderTexture.active = reduced;
        snapshot = new Texture2D(w, h, TextureFormat.RGB24, false);
        snapshot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(reduced);
        Destroy(capture);
        var pixels = snapshot.GetPixels();
        var blurred = new Color[pixels.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    sum += pixels[Mathf.Clamp(y + dy, 0, h - 1) * w + Mathf.Clamp(x + dx, 0, w - 1)];
                blurred[y * w + x] = sum / 9;
            }
            var swap = pixels; pixels = blurred; blurred = swap;
        }
        snapshot.SetPixels(pixels); snapshot.Apply(); snapshot.filterMode = FilterMode.Bilinear;
        backdrop.texture = snapshot;
        backdrop.gameObject.SetActive(true);
        menuCanvas.SetActive(true);
        opening = null;
    }
    public void Close()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (backdrop != null) backdrop.gameObject.SetActive(false);
    }
    void OnDisable()
    {
        if (opening != null) StopCoroutine(opening);
        opening = null;
        Close();
    }
    void OnDestroy()
    {
        if (snapshot != null) Destroy(snapshot);
        if (backdrop != null) Destroy(backdrop.gameObject);
    }
}
