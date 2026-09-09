using System.Collections;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Menú de pausa: Esc para abrirlo, y desde ahí se sale del juego guardando.
    ///
    /// Existe porque cerrar la ventana a lo bruto era la única forma de salir, y
    /// desde que el guardado periódico y el de cada interacción ya no están, el
    /// cierre es uno de los <b>dos</b> momentos en que se guarda (ver
    /// <see cref="SaveManager"/>). Convenía que hubiera una puerta de salida
    /// explícita y no solo el aspa de la ventana.
    ///
    /// Se crea sola y sobrevive entre escenas, igual que el SaveManager: no hay
    /// nada que arrastrar en el editor. La UI no se construye hasta el primer Esc,
    /// así que en una partida en la que nadie la abre no cuesta nada.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuPausa : MonoBehaviour
    {
        public static MenuPausa Instance { get; private set; }

        [Header("Configuración")]
        [Tooltip("Abrir solo cuando Otto está en la escena, es decir, cuando de verdad " +
                 "hay una partida que pausar. Con esto Esc no hace nada en el login ni " +
                 "en los menús, que ya tienen su propio botón de salir.")]
        public bool soloDuranteElJuego = true;

        [Tooltip("Segundos que se espera tras pedir el guardado antes de cerrar. " +
                 "Guardar solo LANZA las peticiones al backend; si se cierra en el " +
                 "mismo frame, se cortan a medio camino.")]
        [Min(0f)]
        public float esperaAntesDeSalir = 1f;

        /// <summary>Si el menú está a la vista. Con él abierto el juego está en pausa.</summary>
        public bool Abierto => _raiz != null && _raiz.activeSelf;

        private GameObject _raiz;
        private TextMeshProUGUI _estado;
        private Button _btnSeguir, _btnSalir;
        private float _timeScalePrevio = 1f;
        private bool _saliendo;

        // ── Ciclo de vida ─────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear() => GetOrCreate();

        public static MenuPausa GetOrCreate()
        {
            if (Instance != null) return Instance;

            var encontrado = FindAnyObjectByType<MenuPausa>();
            if (encontrado != null) return encontrado;

            return new GameObject("MenuPausa").AddComponent<MenuPausa>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;

            // Dejar el juego congelado al destruirse sería peor que no haber
            // pausado nunca: nada volvería a moverse y no habría menú que cerrar.
            if (Abierto) Time.timeScale = _timeScalePrevio;
        }

        private void Update()
        {
            if (_saliendo) return;

            // Solo el nuevo Input System (activeInputHandler: 1). Sin teclado
            // —un móvil— `current` es null y aquí no hay nada que hacer.
            var teclado = Keyboard.current;
            if (teclado == null || !teclado.escapeKey.wasPressedThisFrame) return;

            if (Abierto) Cerrar();
            else Abrir();
        }

        // ── Abrir y cerrar ────────────────────────────────────────────────────

        public void Abrir()
        {
            if (Abierto || _saliendo) return;

            // "Hay Otto en la escena" es como el resto del código distingue el juego
            // de un menú (ver PersonajeBackendSync.BuscarOtto).
            if (soloDuranteElJuego && FindAnyObjectByType<OttoController>() == null) return;

            if (_raiz == null) Construir();

            _timeScalePrevio = Time.timeScale;
            Time.timeScale = 0f;

            if (_estado != null) _estado.text = "¿Qué quieres hacer?";
            if (_btnSeguir != null) _btnSeguir.interactable = true;
            if (_btnSalir != null) _btnSalir.interactable = true;

            _raiz.SetActive(true);
        }

        public void Cerrar()
        {
            if (!Abierto || _saliendo) return;

            _raiz.SetActive(false);
            Time.timeScale = _timeScalePrevio;
        }

        // ── Salir ─────────────────────────────────────────────────────────────

        /// <summary>Guarda y cierra el juego. Es lo que hace el botón "Guardar y salir".</summary>
        public void Salir()
        {
            if (_saliendo) return;
            StartCoroutine(GuardarYSalir());
        }

        private IEnumerator GuardarYSalir()
        {
            _saliendo = true;
            if (_btnSeguir != null) _btnSeguir.interactable = false;
            if (_btnSalir != null) _btnSalir.interactable = false;
            if (_estado != null) _estado.text = "Guardando tu partida…";

            // CierreDeAplicacion y no Manual: es exactamente eso, y además es el único
            // motivo que se salta la espera mínima entre guardados.
            SaveManager.Instance?.Guardar(SaveManager.Motivo.CierreDeAplicacion);

            // Guardar deja las peticiones EN VUELO, no esperadas: no hay ningún aviso
            // de "ya subió" al que engancharse. Se les da un respiro antes de cerrar,
            // en tiempo real porque el juego está en pausa (timeScale = 0).
            yield return new WaitForSecondsRealtime(esperaAntesDeSalir);

            // Que quede a 1 pase lo que pase: en el editor el proceso sigue vivo
            // después de parar el Play y timeScale es global.
            Time.timeScale = _timeScalePrevio;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── UI ────────────────────────────────────────────────────────────────

        private void Construir()
        {
            UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("MenuPausaCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.SetActive(false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Por encima de todo lo demás: el zoom del teléfono va a 9000.
            canvas.sortingOrder = 9500;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Velo: oscurece el juego y, sobre todo, se come los clics para que no
            // lleguen a lo que hay debajo.
            var velo = new GameObject("Velo", typeof(RectTransform), typeof(Image));
            velo.transform.SetParent(canvasGO.transform, false);
            FishyUIKit.Estirar(velo.GetComponent<RectTransform>());
            velo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var tarjetaGO = new GameObject("Tarjeta",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            tarjetaGO.transform.SetParent(canvasGO.transform, false);
            FishyUIKit.FondoRedondeado(tarjetaGO.GetComponent<Image>(), Paleta.Marron);

            var rt = tarjetaGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(660f, 470f);

            var vlg = tarjetaGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(44, 44, 40, 40);
            vlg.spacing = 20f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            FishyUIKit.Texto(tarjetaGO.transform, "Titulo", "Pausa",
                64f, Paleta.Crema, TextAlignmentOptions.Center);

            _estado = FishyUIKit.Texto(tarjetaGO.transform, "Estado", "¿Qué quieres hacer?",
                30f, Paleta.Arena, TextAlignmentOptions.Center);

            _btnSeguir = FishyUIKit.Boton(tarjetaGO.transform, "Seguir jugando",
                Paleta.Verde, 34f, 88f, Cerrar);

            _btnSalir = FishyUIKit.Boton(tarjetaGO.transform, "Guardar y salir",
                Paleta.Rojo, 34f, 88f, Salir);

            _raiz = canvasGO;
        }
    }
}
