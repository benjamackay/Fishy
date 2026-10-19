using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Fishy.UI;

namespace Fishy.Detective
{
    /// <summary>
    /// HDU-11 — Celebración del pin obtenido: el ítem aparece con un "pop" en el
    /// centro de la pantalla y recién después entran los textos, arriba y abajo
    /// del mismo pin.
    ///
    /// <b>Por qué guarda el pin en vez de mostrarlo al recibirlo:</b>
    /// <see cref="DetectiveRewardEvents.OnRecompensaOtorgada"/> se dispara dentro
    /// de <c>CalcularResultado()</c>, o sea en el instante en que se abre el panel
    /// de feedback. Celebrar ahí taparía el feedback —que es el contenido
    /// educativo de la HDU— con una animación. Así que la recompensa queda
    /// <see cref="_pendiente"/> y <see cref="DetectiveUI"/> la suelta con
    /// <see cref="MostrarPendiente"/> cuando el jugador pulsa "Continuar".
    ///
    /// La cadena de eventos no cambió: <see cref="DetectiveCaseManager"/> sigue sin
    /// saber nada de presentación, tal como prometía el comentario de
    /// <see cref="DetectiveRewardEvents"/>.
    ///
    /// La UI se construye por código, como el resto del modo Detective
    /// (<see cref="DetectiveUI"/>, <c>ZonePopupUI</c>): no hay prefab que mantener
    /// ni referencias que se puedan romper al renombrar algo en la escena.
    /// </summary>
    public class DetectiveRewardPopup : MonoBehaviour
    {
        // ── Ritmo de la animación (segundos) ──────────────────────────────────
        private const float DurBackdrop  = 0.20f;
        private const float DurPop       = 0.45f;
        private const float PausaTrasPop = 0.10f;
        private const float DurTexto     = 0.25f;
        private const float StaggerTexto = 0.12f;
        private const float DurEspera    = 2.20f;
        private const float DurSalida    = 0.30f;

        /// <summary>Cuánto se pasa de 1 el "pop" antes de asentarse. 1.25 = 25% más
        /// grande en el rebote. Subirlo lo hace más caricaturesco.</summary>
        private const float Sobrepaso = 1.25f;

        // ── Medidas (canvas de referencia 1920x1080) ──────────────────────────
        private const float LadoPin      = 340f;
        private const float AltoTexto    = 270f;   // separación del pin al texto
        private const float TamanoTitulo = 88f;
        private const float TamanoNombre = 44f;

        // ── Textos ────────────────────────────────────────────────────────────
        private const string TextoArriba = "HAS OBTENIDO";
        private const string TextoAbajo  = "UN NUEVO PIN";

        private static DetectiveRewardPopup _instance;

        private RecompensaOtorgada? _pendiente;
        private Coroutine _rutina;

        // Piezas de la UI, creadas una vez y reutilizadas.
        private CanvasGroup     _backdrop;
        private CanvasGroup     _grupoPin;
        private RectTransform   _rtPin;
        private Image           _imgPin;
        private CanvasGroup     _grupoArriba, _grupoAbajo, _grupoNombre;
        private RectTransform   _rtArriba, _rtAbajo;
        private TextMeshProUGUI _txtNombre;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (_instance != null) return;
            var go = new GameObject("DetectiveRewardPopup");
            _instance = go.AddComponent<DetectiveRewardPopup>();
            DontDestroyOnLoad(go);
        }

        private void OnEnable()  => DetectiveRewardEvents.OnRecompensaOtorgada += Encolar;
        private void OnDisable() => DetectiveRewardEvents.OnRecompensaOtorgada -= Encolar;

        private void Encolar(RecompensaOtorgada r) => _pendiente = r;

        /// <summary>
        /// Celebra el pin que quedó pendiente, si lo hay, y llama a
        /// <paramref name="alTerminar"/> cuando la animación acaba.
        ///
        /// <paramref name="alTerminar"/> se invoca <b>siempre</b> y una sola vez —sin
        /// pin pendiente, en el acto—, porque de ahí cuelga el cierre del modo
        /// Detective: si se perdiera, el jugador se quedaría sin poder moverse.
        /// </summary>
        public static void MostrarPendiente(Action alTerminar = null)
        {
            if (_instance == null) AutoCrear();

            if (_instance == null || _instance._pendiente == null)
            {
                alTerminar?.Invoke();
                return;
            }

            var r = _instance._pendiente.Value;
            _instance._pendiente = null;   // se celebra una vez, no en cada cierre
            _instance.Reproducir(r, alTerminar);
        }

        private void Reproducir(RecompensaOtorgada r, Action alTerminar)
        {
            if (_backdrop == null) ConstruirUI();

            if (_rutina != null) StopCoroutine(_rutina);
            _rutina = StartCoroutine(Secuencia(r, alTerminar));
        }

        // ── La animación ──────────────────────────────────────────────────────

        private IEnumerator Secuencia(RecompensaOtorgada r, Action alTerminar)
        {
            PrepararContenido(r);

            _backdrop.gameObject.SetActive(true);
            _backdrop.blocksRaycasts = true;   // nadie toca el mundo mientras tanto

            // 1. Se oscurece la pantalla, con todo lo demás aún invisible.
            yield return Fundir(_backdrop, 0f, 1f, DurBackdrop);

            // 2. El pin entra solo: crece desde cero, se pasa un poco y se asienta.
            //    Es el "pop" que pediste, y va antes que los textos para que la
            //    vista aterrice primero en el ítem.
            yield return PopDelPin();

            yield return new WaitForSecondsRealtime(PausaTrasPop);

            // 3. Recién ahora los textos, arriba y abajo del pin ya asentado.
            //    Entran escalonados y deslizándose hacia el pin: los dos apuntan
            //    al centro, que es donde está lo que importa.
            StartCoroutine(EntrarTexto(_grupoArriba, _rtArriba,  AltoTexto + 40f, AltoTexto));
            yield return new WaitForSecondsRealtime(StaggerTexto);
            StartCoroutine(EntrarTexto(_grupoAbajo,  _rtAbajo,  -AltoTexto - 40f, -AltoTexto));
            yield return new WaitForSecondsRealtime(StaggerTexto);
            yield return Fundir(_grupoNombre, 0f, 1f, DurTexto);

            // 4. Se queda un rato, pero sin obligar: un clic o una tecla la salta.
            yield return EsperarOSaltar(DurEspera);

            // 5. Salida en bloque.
            yield return Fundir(_backdrop, 1f, 0f, DurSalida);
            _backdrop.blocksRaycasts = false;
            _backdrop.gameObject.SetActive(false);

            _rutina = null;
            alTerminar?.Invoke();
        }

        private void PrepararContenido(RecompensaOtorgada r)
        {
            // El ícono sale del mismo ItemData que ya usa la mochila, buscado por
            // itemId: así el pin se dibuja igual aquí, en el inventario y en el
            // Álbum, y asignar un ícono nuevo no obliga a tocar este archivo.
            // Comprobación explícita y no con '?.': sobre un UnityEngine.Object el
            // operador condicional se salta el null "falso" de Unity. Mismo patrón
            // que DetectiveCaseManager al resolver el ItemData.
            ItemData datos = CatalogoItems.Buscar(r.itemId);
            Sprite icono = datos != null ? datos.itemIcon : null;
            if (icono == null)
            {
                Debug.LogWarning($"[Detective] El pin '{r.itemId}' no tiene ícono en su " +
                                  "ItemData: la celebración sale sin imagen. Asígnalo en " +
                                  "Resources/Items.");
            }

            _imgPin.sprite = icono;
            _imgPin.enabled = icono != null;

            _txtNombre.text = r.nombre ?? string.Empty;
            _txtNombre.font = FishyUIKit.FuentePara(_txtNombre.text);

            // Estado inicial de cada pieza. Se resetea en cada pase y no solo al
            // construir, porque el popup se reutiliza entre casos.
            _backdrop.alpha    = 0f;
            _grupoPin.alpha    = 0f;
            _grupoArriba.alpha = 0f;
            _grupoAbajo.alpha  = 0f;
            _grupoNombre.alpha = 0f;
            _rtPin.localScale  = Vector3.zero;
        }

        /// <summary>Crece de 0 a <see cref="Sobrepaso"/> y vuelve a 1. El rebote es
        /// lo que hace que se lea como "premio" y no como "apareció un cuadro".</summary>
        private IEnumerator PopDelPin()
        {
            float t = 0f;
            while (t < DurPop)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / DurPop);

                _rtPin.localScale = Vector3.one * BackOut(p, Sobrepaso);
                _grupoPin.alpha   = Mathf.Clamp01(p * 3f);   // opaco mucho antes de asentarse
                yield return null;
            }
            _rtPin.localScale = Vector3.one;
            _grupoPin.alpha   = 1f;
        }

        /// <summary>Interpolación con sobrepaso: llega a <paramref name="sobrepaso"/>
        /// y regresa suavemente a 1. Escrita a mano para no meter una dependencia de
        /// tweening al proyecto por una sola animación.</summary>
        private static float BackOut(float p, float sobrepaso)
        {
            float s = Mathf.Max(0f, sobrepaso - 1f) * 2.7f;   // 2.7 ≈ rebote clásico
            p -= 1f;
            return p * p * ((s + 1f) * p + s) + 1f;
        }

        private IEnumerator EntrarTexto(CanvasGroup grupo, RectTransform rt, float desde, float hasta)
        {
            float t = 0f;
            while (t < DurTexto)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / DurTexto));

                grupo.alpha = p;
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(desde, hasta, p));
                yield return null;
            }
            grupo.alpha = 1f;
            rt.anchoredPosition = new Vector2(0f, hasta);
        }

        private static IEnumerator Fundir(CanvasGroup grupo, float desde, float hasta, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                grupo.alpha = Mathf.Lerp(desde, hasta, dur <= 0f ? 1f : t / dur);
                yield return null;
            }
            grupo.alpha = hasta;
        }

        /// <summary>Espera, o corta antes si el jugador hace clic o pulsa una tecla.
        /// Input System nuevo (<c>activeInputHandler: 1</c>): los <c>.current</c> son
        /// null si no hay ese dispositivo, así que se preguntan con guarda.</summary>
        private static IEnumerator EsperarOSaltar(float segundos)
        {
            float t = 0f;
            while (t < segundos)
            {
                t += Time.unscaledDeltaTime;

                bool clic  = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                bool tecla = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
                if (clic || tecla) yield break;

                yield return null;
            }
        }

        // ── Construcción de la UI ─────────────────────────────────────────────

        private void ConstruirUI()
        {
            var canvasGO = new GameObject("RewardCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Por encima del canvas del Detective (950) y del cartel de zona (1000):
            // la celebración es lo último que se ve y nada debe quedarle encima.
            canvas.sortingOrder = 1100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            // Velo a pantalla completa. Es el padre de todo, así que un solo fundido
            // de salida se lleva la escena entera, y además bloquea los clics.
            var fondoGO = new GameObject("Backdrop",
                typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            fondoGO.transform.SetParent(canvasGO.transform, false);
            FishyUIKit.Estirar(fondoGO.GetComponent<RectTransform>());
            fondoGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            _backdrop = fondoGO.GetComponent<CanvasGroup>();

            // ── El pin, al centro ─────────────────────────────────────────────
            var pinGO = new GameObject("Pin",
                typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            pinGO.transform.SetParent(fondoGO.transform, false);

            _rtPin = pinGO.GetComponent<RectTransform>();
            _rtPin.anchorMin = _rtPin.anchorMax = new Vector2(0.5f, 0.5f);
            _rtPin.pivot     = new Vector2(0.5f, 0.5f);
            _rtPin.anchoredPosition = Vector2.zero;
            _rtPin.sizeDelta = new Vector2(LadoPin, LadoPin);

            _imgPin = pinGO.GetComponent<Image>();
            // Los pines no son cuadrados exactos; sin esto se deformarían al entrar
            // en una caja cuadrada.
            _imgPin.preserveAspect = true;
            _imgPin.raycastTarget  = false;
            _grupoPin = pinGO.GetComponent<CanvasGroup>();

            // ── Textos, arriba y abajo del pin ────────────────────────────────
            (_grupoArriba, _rtArriba) = CrearTitulo("TextoArriba", TextoArriba,  AltoTexto);
            (_grupoAbajo,  _rtAbajo)  = CrearTitulo("TextoAbajo",  TextoAbajo,  -AltoTexto);

            // El nombre del pin va debajo de "UN NUEVO PIN" y más pequeño: la frase
            // manda, pero sin el nombre el jugador no sabría cuál de los tres ganó.
            var nombreGO = new GameObject("Nombre", typeof(RectTransform), typeof(CanvasGroup));
            nombreGO.transform.SetParent(fondoGO.transform, false);
            var rtNombre = nombreGO.GetComponent<RectTransform>();
            rtNombre.anchorMin = rtNombre.anchorMax = new Vector2(0.5f, 0.5f);
            rtNombre.pivot     = new Vector2(0.5f, 0.5f);
            rtNombre.anchoredPosition = new Vector2(0f, -AltoTexto - 85f);
            rtNombre.sizeDelta = new Vector2(1100f, 60f);
            _grupoNombre = nombreGO.GetComponent<CanvasGroup>();

            _txtNombre = FishyUIKit.Texto(nombreGO.transform, "Txt", "", TamanoNombre,
                Paleta.Arena, TextAlignmentOptions.Center);
            FishyUIKit.Estirar(_txtNombre.rectTransform);
            _txtNombre.raycastTarget = false;

            _backdrop.alpha = 0f;
            fondoGO.SetActive(false);
        }

        private (CanvasGroup, RectTransform) CrearTitulo(string nombre, string texto, float y)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_backdrop.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(1200f, 110f);

            var tmp = FishyUIKit.Texto(go.transform, "Txt", texto, TamanoTitulo,
                Paleta.Crema, TextAlignmentOptions.Center);
            FishyUIKit.Estirar(tmp.rectTransform);
            tmp.raycastTarget = false;

            return (go.GetComponent<CanvasGroup>(), rt);
        }
    }
}
