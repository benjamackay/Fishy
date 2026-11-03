using System;
using Fishy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La pregunta del comienzo: Ver tutorial o Saltar tutorial, encima del mundo ya
/// cargado. La guía no empieza hasta que se elige.
///
/// Solo pregunta: qué hace cada respuesta lo decide <see cref="TutorialPasos"/>. Se
/// monta sola en código y se destruye al responder, igual que
/// <see cref="FinalDelTutorial"/>, y usa las mismas piezas que el menú de pausa para
/// que se lea como una tarjeta del juego.
/// </summary>
public class OfertaDelTutorial : MonoBehaviour
{
    /// <summary>Por encima del celular del guía (1250) y por debajo de la celebración
    /// (1400), de la pantalla de carga (1500) y de la pausa (9500).</summary>
    private const int OrdenDeDibujo = 1350;

    private Action alVer, alSaltar;
    private bool respondida;

    public static void Mostrar(Action alVer, Action alSaltar)
    {
        var pieza = new GameObject("OfertaDelTutorial").AddComponent<OfertaDelTutorial>();
        pieza.alVer = alVer;
        pieza.alSaltar = alSaltar;
        pieza.Construir();
    }

    private void Construir()
    {
        var canvasGO = new GameObject("Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OrdenDeDibujo;

        var escala = canvasGO.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;

        // Oscurece el mundo y se come los clics: hasta responder no hay nada más que tocar.
        var velo = new GameObject("Velo", typeof(RectTransform), typeof(Image));
        velo.transform.SetParent(canvasGO.transform, false);
        FishyUIKit.Estirar(velo.GetComponent<RectTransform>());
        velo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var tarjetaGO = new GameObject("Tarjeta",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        tarjetaGO.transform.SetParent(canvasGO.transform, false);
        FishyUIKit.FondoRedondeado(tarjetaGO.GetComponent<Image>(), Paleta.Marron);

        var rt = tarjetaGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(660f, 440f);

        var vlg = tarjetaGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(44, 44, 40, 40);
        vlg.spacing = 18f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        FishyUIKit.Texto(tarjetaGO.transform, "Titulo", "¡Bienvenido/a!",
            64f, Paleta.Crema, TextAlignmentOptions.Center);

        FishyUIKit.Texto(tarjetaGO.transform, "Pregunta", "¿Quieres ver el tutorial?",
            30f, Paleta.Arena, TextAlignmentOptions.Center);

        CrearBoton(tarjetaGO.transform, "Ver tutorial", Paleta.Verde, alVer);
        CrearBoton(tarjetaGO.transform, "Saltar tutorial", Paleta.Madera, alSaltar);
    }

    private void CrearBoton(Transform padre, string texto, Color color, Action respuesta)
    {
        Button boton = FishyUIKit.Boton(padre, texto, color, 34f, 88f, () => Responder(respuesta));

        // Sin navegación, para que WASD y las flechas no anden moviendo la selección.
        boton.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private void Responder(Action respuesta)
    {
        if (respondida) return;
        respondida = true;

        Destroy(gameObject);
        respuesta?.Invoke();
    }
}
