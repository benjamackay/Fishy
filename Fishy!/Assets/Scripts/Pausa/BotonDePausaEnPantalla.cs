using Fishy.Pausa;
using Fishy.World;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// HDU-17 CA1 — El botón de pausa en pantalla, para quien no tiene teclado.
    ///
    /// <b>Por qué no alcanza con Esc.</b> El juego se mueve también con botones
    /// táctiles (<see cref="OttoOnScreenButton"/>), así que hay partidas enteras que
    /// se juegan sin tocar una tecla. En ésas el menú de pausa, que hasta ahora solo
    /// se abría con Esc, sencillamente no existía: el criterio dice «accesible en
    /// cualquier momento durante la partida», y en una tablet no lo era.
    ///
    /// <b>Dónde se pone y por qué ahí.</b> Arriba a la izquierda. Las otras tres
    /// esquinas de la escena de juego ya están ocupadas —el celular arriba a la
    /// derecha, la mochila abajo a la izquierda— y una esquina libre evita el peor
    /// error posible en un botón de pausa: que el dedo le dé a otra cosa.
    ///
    /// Se muestra solo cuando hay partida que pausar y cuando se puede pausar de
    /// verdad. Durante una cinemática o una carga se esconde en vez de quedarse
    /// visible sin hacer nada, que a un niño/a le parece que el juego se rompió.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotonDePausaEnPantalla : MonoBehaviour
    {
        /// <summary>Por debajo del menú de pausa (9500): cuando el menú está abierto
        /// este botón no tiene que asomar por encima del velo.</summary>
        private const int OrdenDeDibujo = 9400;

        private const float Lado = 96f;
        private const float Margen = 28f;

        /// <summary>Cada cuánto se revisa si toca mostrarlo, en segundos sin escalar.
        /// No es cada frame porque la revisión busca a Otto en la escena; medio
        /// segundo es imperceptible para quien mira y gratis para el juego. Sin
        /// escalar porque con la pausa puesta el tiempo normal no corre, y entonces
        /// no se volvería a revisar nunca.</summary>
        private const float CadaCuanto = 0.5f;

        private GameObject _raiz;
        private OttoController _otto;
        private float _proximaRevision;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (FindAnyObjectByType<BotonDePausaEnPantalla>() != null) return;
            new GameObject("BotonDePausa").AddComponent<BotonDePausaEnPantalla>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Construir();
            Revisar();
        }

        private void Update()
        {
            if (Time.unscaledTime < _proximaRevision) return;
            _proximaRevision = Time.unscaledTime + CadaCuanto;

            Revisar();
        }

        /// <summary>
        /// Enciende o apaga el botón según si ahora mismo se puede pausar.
        ///
        /// «Hay un Otto en la escena» es como el resto del código distingue el juego
        /// de un menú, igual que en <see cref="MenuPausa.Abrir"/>. No se pide además
        /// que Otto pueda moverse: durante un chat o un caso del Modo Detective no
        /// puede, y son justo los momentos en los que CA4 dice que la pausa tiene que
        /// funcionar.
        /// </summary>
        private void Revisar()
        {
            if (_otto == null) _otto = FindAnyObjectByType<OttoController>();

            bool mostrar = _otto != null && !EstadoDePausa.Pausado && EstadoDePausa.SePuedePausar();
            if (_raiz != null && _raiz.activeSelf != mostrar) _raiz.SetActive(mostrar);
        }

        private void Pulsar()
        {
            MenuPausa.GetOrCreate().Abrir();

            // Sin esperar al próximo tic: el botón tiene que irse en el mismo frame
            // en que aparece el menú, o se le ve un parpadeo por encima del velo.
            Revisar();
        }

        // ── UI ────────────────────────────────────────────────────────────────

        private void Construir()
        {
            UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("BotonDePausaCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            _raiz = canvasGO;

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OrdenDeDibujo;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var botonGO = new GameObject("Pausa",
                typeof(RectTransform), typeof(Image), typeof(Button));
            botonGO.transform.SetParent(canvasGO.transform, false);

            var rt = (RectTransform)botonGO.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(Lado, Lado);
            rt.anchoredPosition = new Vector2(Margen, -Margen);

            FishyUIKit.FondoRedondeado(botonGO.GetComponent<Image>(), Paleta.Marron, 64, 28);
            botonGO.GetComponent<Button>().onClick.AddListener(Pulsar);

            DibujarDosBarras(botonGO.transform);
        }

        /// <summary>
        /// Las dos barras del símbolo de pausa, dibujadas y no escritas.
        ///
        /// Mismo motivo que la aspa de cerrar (ver <see cref="FishyUIKit.Aspa"/>): el
        /// carácter ⏸ (U+23F8) no está en ninguna de las dos fuentes del juego y
        /// saldría un cuadro hueco. Dos rectángulos siempre se ven, y se leen igual a
        /// cualquier tamaño.
        /// </summary>
        private static void DibujarDosBarras(Transform padre)
        {
            const float AnchoBarra = 14f;
            const float AltoBarra = 44f;
            const float Separacion = 11f;

            for (int i = 0; i < 2; i++)
            {
                var barra = new GameObject($"Barra{i}", typeof(RectTransform), typeof(Image));
                barra.transform.SetParent(padre, false);

                var rt = (RectTransform)barra.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(AnchoBarra, AltoBarra);
                rt.anchoredPosition = new Vector2(i == 0 ? -Separacion : Separacion, 0f);

                var img = barra.GetComponent<Image>();
                img.color = Paleta.Crema;
                img.raycastTarget = false;   // el clic lo recibe el botón, no la barra
            }
        }
    }
}
