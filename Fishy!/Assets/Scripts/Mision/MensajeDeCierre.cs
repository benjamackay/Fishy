using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cartel de "zona completada" con un botón para seguir explorando.
///
/// Se engancha al 'Al Completar' de un <see cref="AlCompletarMision"/> —por ejemplo,
/// el de la misión final del Arrecife— para que el juego tenga un cierre sin quitarle
/// al niño/a la posibilidad de seguir paseando por el mapa. En 'Al Restaurar' no va
/// nada: al retomar una partida ya terminada no hay que volver a mostrarlo.
///
/// No pausa el juego ni cambia de escena: sólo tapa la pantalla hasta que se pulsa el
/// botón. La UI se arma sola la primera vez que se muestra.
/// </summary>
public class MensajeDeCierre : MonoBehaviour
{
    [Tooltip("Título grande del cartel.")]
    public string titulo = "¡Completaste el Arrecife!";

    [TextArea]
    [Tooltip("Texto bajo el título.")]
    public string mensaje = "Terminaste todas las misiones. Puedes seguir explorando el mapa cuando quieras.";

    [Tooltip("Texto del botón que cierra el cartel.")]
    public string textoBoton = "Seguir explorando";

    private GameObject _canvas;

    /// <summary>Muestra el cartel. Sin parámetros para poder engancharlo desde
    /// cualquier UnityEvent del Inspector.</summary>
    public void Mostrar()
    {
        if (_canvas == null) Construir();
        _canvas.SetActive(true);
    }

    public void Ocultar()
    {
        if (_canvas != null) _canvas.SetActive(false);
    }

    private void Construir()
    {
        _canvas = new GameObject("MensajeDeCierreCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas.transform.SetParent(transform, false);

        var canvas = _canvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 960;   // por encima del cartel de misión (710)

        var scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Velo sobre el mundo: el cartel se lee mejor y se entiende que es un momento aparte.
        var velo = new GameObject("Velo", typeof(RectTransform), typeof(Image));
        velo.transform.SetParent(_canvas.transform, false);
        var veloRT = velo.GetComponent<RectTransform>();
        veloRT.anchorMin = Vector2.zero; veloRT.anchorMax = Vector2.one;
        veloRT.offsetMin = veloRT.offsetMax = Vector2.zero;
        velo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var tarjeta = new GameObject("Tarjeta", typeof(RectTransform), typeof(Image));
        tarjeta.transform.SetParent(velo.transform, false);
        var tarjetaRT = tarjeta.GetComponent<RectTransform>();
        tarjetaRT.anchorMin = tarjetaRT.anchorMax = tarjetaRT.pivot = new Vector2(0.5f, 0.5f);
        tarjetaRT.sizeDelta = new Vector2(900f, 460f);
        FishyUIKit.FondoRedondeado(tarjeta.GetComponent<Image>(), Paleta.MarronOscuro);

        TextMeshProUGUI tituloTxt = FishyUIKit.Texto(tarjeta.transform, "Titulo", titulo,
            60f, Paleta.Arena, TextAlignmentOptions.Center);
        if (FishyUIKit.Titulos != null) tituloTxt.font = FishyUIKit.Titulos;
        Colocar(tituloTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
            new Vector2(820f, 90f));

        TextMeshProUGUI cuerpo = FishyUIKit.Texto(tarjeta.transform, "Mensaje", mensaje,
            34f, Color.white, TextAlignmentOptions.Center);
        Colocar(cuerpo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f),
            new Vector2(800f, 180f));

        Button boton = FishyUIKit.Boton(tarjeta.transform, textoBoton, Paleta.Marron,
            32f, 80f, Ocultar);
        Colocar(boton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(420f, 80f));

        _canvas.SetActive(false);
    }

    private static void Colocar(RectTransform rt, Vector2 ancla, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = ancla;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }
}
