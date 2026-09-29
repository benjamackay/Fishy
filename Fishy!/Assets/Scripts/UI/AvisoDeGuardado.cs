using System.Collections.Generic;
using Fishy.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Avisa, sin interrumpir, de que hay algo que todavía no se ha podido guardar, y
    /// deja volver a intentarlo.
    ///
    /// <b>Por qué.</b> Hasta ahora lo único que el jugador veía del guardado era un
    /// cartel al cerrar, y llegaba tarde: para entonces ya no se podía hacer nada salvo
    /// esperar o irse. Con el diario nada se pierde, pero "nada se pierde" no es lo mismo
    /// que "está guardado": si el servidor no responde, el avance de la tarde sigue
    /// solamente en este equipo. Esto lo hace visible mientras se juega y pone el
    /// reintento en manos de quien está delante.
    ///
    /// <b>Discreto a propósito.</b> No aparece por cualquier cosa: la cola se vacía sola
    /// cada pocos segundos, así que un pendiente de paso es lo normal y no significa
    /// nada. Solo sale cuando algo lleva un buen rato sin subir, o cuando la cola dejó de
    /// reintentar por su cuenta.
    ///
    /// <b>El icono es un placeholder.</b> Es un círculo con un "!" dibujado con la
    /// tipografía del juego, y está pensado para que se reemplace por arte de verdad: ver
    /// <see cref="iconoPlaceholder"/>. No se usó ningún símbolo de nube porque la fuente
    /// de cuerpo del proyecto es estática y trae poco más que Latin-1, así que un glifo
    /// así saldría como un cuadro hueco (el mismo motivo por el que
    /// <c>FishyUIKit.Aspa</c> dibuja la cruz en vez de escribirla).
    ///
    /// Se crea solo y sobrevive entre escenas; no hay nada que arrastrar en el editor.
    /// </summary>
    [DisallowMultipleComponent]
    public class AvisoDeGuardado : MonoBehaviour
    {
        public static AvisoDeGuardado Instance { get; private set; }

        [Header("Cuándo aparece")]
        [Tooltip("Segundos que algo tiene que llevar sin subir antes de avisar. Por " +
                 "debajo de esto no se dice nada: la cola se vacía sola cada pocos " +
                 "segundos y un pendiente de paso no significa nada.")]
        [Min(1f)] public float segundosAntesDeAvisar = 30f;

        [Tooltip("Cada cuánto se mira si hay algo pendiente. No hace falta cada frame.")]
        [Min(0.1f)] public float cadaCuantoSeRevisa = 0.5f;

        [Header("Arte")]
        [Tooltip("PLACEHOLDER: el icono del aviso. Dejándolo vacío se dibuja un círculo " +
                 "con un signo de exclamación. Poner aquí el sprite definitivo es todo lo " +
                 "que hace falta para reemplazarlo.")]
        public Sprite iconoPlaceholder;

        private GameObject _canvas;
        private GameObject _boton;
        private GameObject _panel;
        private TextMeshProUGUI _lista;
        private TextMeshProUGUI _encabezado;

        /// <summary>Desde cuándo hay algo esperando (realtime), o -1 si no hay nada.</summary>
        private float _pendienteDesde = -1f;
        private float _proximaRevision;

        // ── Ciclo de vida ─────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (Instance != null) return;
            new GameObject("AvisoDeGuardado").AddComponent<AvisoDeGuardado>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Todo el tiempo va en realtime: el menú de pausa pone Time.timeScale = 0, y con
        // tiempo de juego este aviso se congelaría justo cuando más falta hace.
        private void Update()
        {
            if (Time.realtimeSinceStartup < _proximaRevision) return;
            _proximaRevision = Time.realtimeSinceStartup + Mathf.Max(0.1f, cadaCuantoSeRevisa);

            int pendientes = ColaDeCambios.Pendientes;

            if (pendientes <= 0)
            {
                _pendienteDesde = -1f;
                Mostrar(false);
                return;
            }

            if (_pendienteDesde < 0f) _pendienteDesde = Time.realtimeSinceStartup;

            bool llevaRato = Time.realtimeSinceStartup - _pendienteDesde >= segundosAntesDeAvisar;
            bool hayQueAvisar = (llevaRato || ColaDeCambios.HayAtasco) && SeEstaJugando();

            // Con el menú de pausa abierto manda él: ya tiene su propio cartel para esto
            // y dos avisos a la vez sobre lo mismo confunden.
            var menu = MenuPausa.Instance;
            if (menu != null && menu.Abierto) hayQueAvisar = false;

            Mostrar(hayQueAvisar);
            if (hayQueAvisar && _panel != null && _panel.activeSelf) Repintar();
        }

        /// <summary>
        /// Solo se avisa dentro del juego. En el login y en los menús no hay nada que el
        /// jugador pueda hacer al respecto, y un aviso ahí sería ruido.
        /// </summary>
        private static bool SeEstaJugando()
            => Object.FindAnyObjectByType<Fishy.World.OttoController>() != null;

        // ── Mostrar y esconder ────────────────────────────────────────────────

        private void Mostrar(bool visible)
        {
            if (!visible)
            {
                if (_canvas != null) _canvas.SetActive(false);
                return;
            }

            if (_canvas == null) Construir();
            _canvas.SetActive(true);
        }

        private void Abrir()
        {
            if (_panel == null) return;
            Repintar();
            _panel.SetActive(true);
            _boton.SetActive(false);
        }

        private void Cerrar()
        {
            if (_panel == null) return;
            _panel.SetActive(false);
            _boton.SetActive(true);
        }

        private void Repintar()
        {
            List<string> que = ColaDeCambios.Descripciones();

            if (_encabezado != null)
                _encabezado.text = que.Count == 1
                    ? "Falta 1 cosa por guardar"
                    : $"Faltan {que.Count} cosas por guardar";

            if (_lista == null) return;

            // Se enseñan unas pocas y se resume el resto: la lista es para entender qué
            // pasa, no un inventario. Y la tarjeta tiene un tamaño fijo.
            const int Tope = 6;
            var lineas = new List<string>();
            for (int i = 0; i < que.Count && i < Tope; i++) lineas.Add($"·  {que[i]}");
            if (que.Count > Tope) lineas.Add($"…y {que.Count - Tope} más");

            string cola = ColaDeCambios.HayAtasco
                ? "\n\nNo se pudo conectar con el servidor. Tu avance está guardado en este " +
                  "equipo y se sube en cuanto haya conexión."
                : "\n\nSe están subiendo solas.";

            _lista.text = string.Join("\n", lineas) + cola;
        }

        // ── UI ────────────────────────────────────────────────────────────────

        private void Construir()
        {
            UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("AvisoDeGuardadoCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO;

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Por encima del juego y del zoom del teléfono (9000), por debajo del menú de
            // pausa (9500), que es el que manda cuando está abierto.
            canvas.sortingOrder = 9200;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            ConstruirBoton(canvasGO.transform);
            ConstruirPanel(canvasGO.transform);
        }

        private void ConstruirBoton(Transform padre)
        {
            var go = new GameObject("BotonAviso", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            _boton = go;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-48f, -48f);
            rt.sizeDelta = new Vector2(96f, 96f);

            var img = go.GetComponent<Image>();
            if (iconoPlaceholder != null)
            {
                img.sprite = iconoPlaceholder;
                img.color = Color.white;
            }
            else
            {
                // Placeholder: el redondeado del kit con el marrón del juego, y un "!"
                // encima. Reemplazar por arte es poner el sprite en el Inspector.
                FishyUIKit.FondoRedondeado(img, Paleta.Marron, lado: 96, radio: 48);
                var signo = FishyUIKit.Texto(go.transform, "Placeholder", "!", 54f,
                    Paleta.Crema, TextAlignmentOptions.Center);
                FishyUIKit.Estirar(signo.rectTransform);
            }

            go.GetComponent<Button>().onClick.AddListener(Abrir);
        }

        private void ConstruirPanel(Transform padre)
        {
            var go = new GameObject("PanelAviso",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            go.transform.SetParent(padre, false);
            go.SetActive(false);
            _panel = go;

            FishyUIKit.FondoRedondeado(go.GetComponent<Image>(), Paleta.Marron);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-48f, -48f);
            rt.sizeDelta = new Vector2(620f, 460f);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(36, 36, 32, 32);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;  vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            _encabezado = FishyUIKit.Texto(go.transform, "Encabezado", "Faltan cosas por guardar",
                34f, Paleta.Crema, TextAlignmentOptions.TopLeft);

            _lista = FishyUIKit.Texto(go.transform, "Lista", "", 26f,
                Paleta.Arena, TextAlignmentOptions.TopLeft);
            _lista.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            FishyUIKit.Boton(go.transform, "Intentar ahora", Paleta.Verde, 28f, 64f, () =>
            {
                ColaDeCambios.ReintentarAhora();

                // Se cierra en vez de quedarse enseñando lo mismo: si funcionó, en el
                // próximo Update el aviso desaparece solo; si no, vuelve a salir.
                Cerrar();
            });

            FishyUIKit.Boton(go.transform, "Cerrar", Paleta.MarronClaro, 26f, 56f, Cerrar);
        }
    }
}
