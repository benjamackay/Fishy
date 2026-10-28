using System;
using System.Collections;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La celebración de haber terminado el tutorial: la pantalla se oscurece un poco y
/// aparece el mensaje con un rebote, igual que cuando el Modo Detective entrega un
/// pin nuevo.
///
/// <b>Por qué no se reutiliza aquella.</b> <c>DetectiveRewardPopup</c> hace justo
/// esto, pero sus dos renglones son <c>private const</c> ("HAS OBTENIDO" / "UN NUEVO
/// PIN") y se dispara con el evento de una recompensa: para decir otra cosa habría
/// que editarla, y es un script del juego que usa todo el equipo. Lo que sí se copia
/// es su <b>ritmo</b> —los tiempos y el rebote de abajo son los suyos—, que es lo que
/// hace que las dos se sientan la misma animación.
///
/// Se monta sola en código y se destruye al acabar: no hay nada que colocar en la
/// escena, igual que con el celular del guía.
/// </summary>
public class FinalDelTutorial : MonoBehaviour
{
    // ── Ritmo (segundos), copiado de DetectiveRewardPopup ────────────────────
    private const float DurBackdrop  = 0.20f;
    private const float DurPop       = 0.45f;
    private const float PausaTrasPop = 0.10f;
    private const float DurTexto     = 0.25f;
    private const float DurEspera    = 2.20f;
    private const float DurSalida    = 0.30f;

    /// <summary>Cuánto se pasa de 1 el "pop" antes de asentarse: 1,25 = un 25% más
    /// grande en el rebote. Es lo que le da el aire de dibujo animado.</summary>
    private const float Sobrepaso = 1.25f;

    /// <summary>Por encima del celular del guía (1250) y de todo lo demás: es lo
    /// último que pasa en la escena y nada debería taparlo.</summary>
    private const int OrdenDeDibujo = 1400;

    private const float OpacidadFondo = 0.65f;
    private const float TamanoTitulo  = 88f;
    private const float TamanoSubtitulo = 44f;

    /// <summary>El Otto contento del panel de ánimo del chat: la misma hoja de
    /// sprites, la misma carpeta y el mismo ritmo, para que sea reconociblemente él
    /// y no un dibujo parecido. Si no está, la celebración sale solo con texto.</summary>
    private const string RutaOtto = "Otto/otto_safe";
    private const float SegundosPorFrame = 0.25f;
    private const float LadoOtto = 340f;

    private CanvasGroup fondo;
    private CanvasGroup grupoPop, grupoSubtitulo;
    private RectTransform cajaPop;
    private Image otto;
    private Sprite[] frames;

    /// <summary>
    /// Oscurece, celebra y se va. <paramref name="alTerminar"/> se llama siempre y una
    /// sola vez, al final: de ahí cuelga la despedida del guía, así que perderlo
    /// dejaría el tutorial sin su último mensaje.
    /// </summary>
    public static void Mostrar(string titulo, string subtitulo, Action alTerminar = null)
    {
        var go = new GameObject("FinalDelTutorial");
        var pieza = go.AddComponent<FinalDelTutorial>();
        pieza.Construir(titulo, subtitulo);
        pieza.StartCoroutine(pieza.Reproducir(alTerminar));
    }

    private void Construir(string titulo, string subtitulo)
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OrdenDeDibujo;

        var escala = canvasGO.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;

        // El fondo tapa toda la pantalla y además se come los clics: durante la
        // celebración no hay nada que tocar, y un botón asomando por debajo invitaría
        // a pulsarlo.
        var fondoGO = new GameObject("Fondo", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        fondoGO.transform.SetParent(canvasGO.transform, false);
        Estirar((RectTransform)fondoGO.transform);
        fondoGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, OpacidadFondo);
        fondo = fondoGO.GetComponent<CanvasGroup>();
        fondo.alpha = 0f;

        // Otto y el título van en la misma caja para que el rebote los tome juntos:
        // si el dibujo creciera por su lado y el texto por el suyo, se vería como dos
        // animaciones a la vez en lugar de una sola cosa apareciendo.
        var popGO = new GameObject("Celebracion", typeof(RectTransform), typeof(CanvasGroup));
        popGO.transform.SetParent(canvasGO.transform, false);
        cajaPop = (RectTransform)popGO.transform;
        cajaPop.anchorMin = cajaPop.anchorMax = new Vector2(0.5f, 0.5f);
        cajaPop.pivot = new Vector2(0.5f, 0.5f);
        cajaPop.sizeDelta = new Vector2(1400f, 620f);
        cajaPop.anchoredPosition = new Vector2(0f, 40f);
        grupoPop = popGO.GetComponent<CanvasGroup>();
        grupoPop.alpha = 0f;

        frames = CargarFrames();
        if (frames.Length > 0)
        {
            var ottoGO = new GameObject("Otto", typeof(RectTransform), typeof(Image));
            ottoGO.transform.SetParent(popGO.transform, false);

            var rtOtto = (RectTransform)ottoGO.transform;
            rtOtto.anchorMin = rtOtto.anchorMax = new Vector2(0.5f, 0.5f);
            rtOtto.pivot = new Vector2(0.5f, 0.5f);
            rtOtto.sizeDelta = new Vector2(LadoOtto, LadoOtto);
            rtOtto.anchoredPosition = new Vector2(0f, 120f);

            otto = ottoGO.GetComponent<Image>();
            otto.sprite = frames[0];
            otto.preserveAspect = true;
            otto.raycastTarget = false;
        }

        var tituloGO = new GameObject("Titulo", typeof(RectTransform));
        tituloGO.transform.SetParent(popGO.transform, false);
        var rtTitulo = (RectTransform)tituloGO.transform;
        rtTitulo.anchorMin = rtTitulo.anchorMax = new Vector2(0.5f, 0.5f);
        rtTitulo.pivot = new Vector2(0.5f, 0.5f);
        rtTitulo.sizeDelta = new Vector2(1400f, 160f);
        // Sin dibujo, el título se queda en el centro en vez de dejar un hueco arriba.
        rtTitulo.anchoredPosition = new Vector2(0f, frames.Length > 0 ? -150f : 0f);

        TextMeshProUGUI t = FishyUIKit.Texto(tituloGO.transform, "Texto", titulo,
            TamanoTitulo, Paleta.Crema, TextAlignmentOptions.Center);
        Estirar((RectTransform)t.transform);
        t.raycastTarget = false;

        var subGO = new GameObject("Subtitulo", typeof(RectTransform), typeof(CanvasGroup));
        subGO.transform.SetParent(canvasGO.transform, false);
        var cajaSub = (RectTransform)subGO.transform;
        cajaSub.anchorMin = cajaSub.anchorMax = new Vector2(0.5f, 0.5f);
        cajaSub.pivot = new Vector2(0.5f, 0.5f);
        cajaSub.sizeDelta = new Vector2(1400f, 100f);
        cajaSub.anchoredPosition = new Vector2(0f, frames.Length > 0 ? -290f : -80f);
        grupoSubtitulo = subGO.GetComponent<CanvasGroup>();
        grupoSubtitulo.alpha = 0f;

        TextMeshProUGUI s = FishyUIKit.Texto(subGO.transform, "Texto", subtitulo,
            TamanoSubtitulo, Paleta.Arena, TextAlignmentOptions.Center);
        Estirar((RectTransform)s.transform);
        s.raycastTarget = false;
    }

    /// <summary>
    /// Toda la animación va en tiempo <b>sin escalar</b>: si algún día el tutorial
    /// pausa el juego para celebrar, una animación atada a Time.time se quedaría
    /// congelada y nadie volvería del fundido.
    /// </summary>
    private IEnumerator Reproducir(Action alTerminar)
    {
        if (frames.Length > 1) StartCoroutine(AnimarOtto());

        yield return Fundir(fondo, 0f, 1f, DurBackdrop);

        // El título entra con rebote y el subtítulo detrás, no a la vez: leídos de
        // golpe son un bloque, escalonados se leen como "¡felicidades!" … "por esto".
        yield return Reventar(DurPop);
        yield return new WaitForSecondsRealtime(PausaTrasPop);
        yield return Fundir(grupoSubtitulo, 0f, 1f, DurTexto);

        yield return new WaitForSecondsRealtime(DurEspera);

        float t = 0f;
        while (t < DurSalida)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / DurSalida);
            fondo.alpha = k;
            grupoPop.alpha = k;
            grupoSubtitulo.alpha = k;
            yield return null;
        }

        alTerminar?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>
    /// Pasa los frames en bucle mientras la celebración está en pantalla. Se corta
    /// sola al destruirse el objeto, que es cuando acaba todo.
    /// </summary>
    private IEnumerator AnimarOtto()
    {
        int i = 0;
        while (true)
        {
            otto.sprite = frames[i];
            i = (i + 1) % frames.Length;
            yield return new WaitForSecondsRealtime(SegundosPorFrame);
        }
    }

    /// <summary>
    /// Los frames de la hoja, en orden. <c>LoadAll</c> no promete ninguno, y con
    /// "_0, _1, _2" el orden alfabético es el bueno; sin ordenarlos, Otto parpadearía
    /// hacia atrás en algunas cargas y no en otras.
    /// </summary>
    private static Sprite[] CargarFrames()
    {
        Sprite[] hoja = Resources.LoadAll<Sprite>(RutaOtto);
        if (hoja == null || hoja.Length == 0) return new Sprite[0];

        Array.Sort(hoja, (a, b) => string.CompareOrdinal(a.name, b.name));
        return hoja;
    }

    /// <summary>El "pop": de nada a un 25% pasado y de ahí a su tamaño.</summary>
    private IEnumerator Reventar(float duracion)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracion);

            // Primera mitad creciendo hasta el sobrepaso, segunda asentándose en 1.
            float escala = k < 0.6f
                ? Mathf.Lerp(0.4f, Sobrepaso, k / 0.6f)
                : Mathf.Lerp(Sobrepaso, 1f, (k - 0.6f) / 0.4f);

            cajaPop.localScale = Vector3.one * escala;
            grupoPop.alpha = Mathf.Clamp01(k * 2f);
            yield return null;
        }

        cajaPop.localScale = Vector3.one;
        grupoPop.alpha = 1f;
    }

    private static IEnumerator Fundir(CanvasGroup grupo, float desde, float hasta, float duracion)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            grupo.alpha = Mathf.Lerp(desde, hasta, Mathf.Clamp01(t / duracion));
            yield return null;
        }
        grupo.alpha = hasta;
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
