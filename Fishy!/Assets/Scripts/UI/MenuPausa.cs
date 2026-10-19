using System;
using System.Collections;
using Fishy.Net;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Menú de pausa: Esc para abrirlo, y desde ahí se vuelve al menú principal
    /// (<see cref="escenaMenu"/>) guardando.
    ///
    /// Existe porque cerrar la ventana a lo bruto era la única forma de salir, y
    /// desde que el guardado periódico y el de cada interacción ya no están, el
    /// cierre es uno de los <b>dos</b> momentos en que se guarda (ver
    /// <see cref="SaveManager"/>). Volver al menú cuenta como ese cierre: se deja de
    /// jugar la partida, así que se guarda igual que al cerrar. Cerrar la aplicación
    /// sigue siendo cosa del aspa o del botón Salir de los menús.
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

        [Tooltip("Escena a la que lleva el botón de salir. Tiene que estar en Build " +
                 "Settings; si no está, el botón cierra el juego como antes.")]
        public string escenaMenu = "MenuDos";

        [Tooltip("Segundos que se espera tras pedir el guardado antes de cerrar. " +
                 "Guardar solo LANZA las peticiones al backend; si se cierra en el " +
                 "mismo frame, se cortan a medio camino.")]
        [Min(0f)]
        public float esperaAntesDeSalir = 1f;

        /// <summary>Si el menú está a la vista. Con él abierto el juego está en pausa.</summary>
        public bool Abierto => _raiz != null && _raiz.activeSelf;

        private GameObject _raiz;
        private TextMeshProUGUI _titulo;
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

        /// <summary>Guarda y vuelve al menú. Es lo que hace el botón "Guardar y volver al menú".</summary>
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

            // Ahora SÍ se espera: `GuardarYEsperar` termina cuando la cola está vacía o
            // se acabó el plazo. Antes esto era un WaitForSecondsRealtime a ciegas y el
            // texto de arriba mentía por diseño — decía "guardando" y no sabía nada.
            var save = SaveManager.Instance;
            if (save != null)
                yield return save.GuardarYEsperar(SaveManager.Motivo.CierreDeAplicacion,
                                                  save.topeDeCierre);

            // Si algo quedó sin subir se pregunta, igual que en el cierre por el aspa.
            while (ColaDeCambios.Pendientes > 0)
            {
                bool esperar = false, respondido = false;
                PreguntarSiEsperar(ColaDeCambios.Pendientes,
                    alEsperar: () => { esperar = true;  respondido = true; },
                    alCerrar:  () => { esperar = false; respondido = true; });

                while (!respondido) yield return null;
                if (!esperar) break;

                // El texto ya lo puso `Congelar` al pulsar el botón.
                var cola = ColaDeCambios.Instance;
                if (cola == null) break;
                yield return cola.Vaciar("CierreDeAplicacion",
                                         save != null ? save.topeDeCierre : esperaAntesDeSalir,
                                         reintentarSiFalla: true);
            }

            bool quedoAlgo = ColaDeCambios.Pendientes > 0;
            if (_estado != null)
                _estado.text = quedoAlgo
                    ? "No se pudo guardar todo. Volviendo al menú…"
                    : "Listo, ya se guardó.";

            // El jugador ya eligió irse sabiendo que se perdía. La aplicación sigue
            // abierta, así que esto no muere con el proceso: hay que tirarlo a mano.
            if (quedoAlgo) ColaDeCambios.DescartarTodo("se volvió al menú sin esperar");

            // Que quede a 1 pase lo que pase: timeScale es global y el menú no lo toca.
            Time.timeScale = _timeScalePrevio;

            // Un frame para que se lea el mensaje final antes de que desaparezca todo.
            yield return new WaitForSecondsRealtime(0.4f);

            if (!Application.CanStreamedLevelBeLoaded(escenaMenu))
            {
                Debug.LogError($"[MenuPausa] La escena '{escenaMenu}' no está en Build " +
                               "Settings: se cierra el juego en vez de volver al menú.");
                SaveManager.MarcarCierreListo();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                yield break;
            }

            // Va ANTES de cambiar de escena: los sincronizadores olvidan lo restaurado
            // en esta, y volver a entrar a la partida la restaura como la primera vez.
            // No se llama a SaveManager.MarcarCierreListo: la aplicación sigue abierta,
            // y el aspa desde el menú tiene que seguir guardando.
            ApiManager.Instance?.CerrarPartida();

            // El menú se rehace entero en el próximo Esc: si se preguntó por la
            // conexión, los botones quedaron con otros textos y otras acciones.
            if (_raiz != null) Destroy(_raiz);
            _raiz = null;
            _saliendo = false;

            SceneManager.LoadScene(escenaMenu);
        }

        // ── El cartel de "el servidor no responde" ─────────────────────────────

        /// <summary>
        /// Pregunta si esperar más o cerrar perdiendo lo que quede.
        ///
        /// Lo llaman los dos caminos de cierre: este menú y el
        /// <see cref="SaveManager"/> cuando se cierra por el aspa o Alt+F4, donde no hay
        /// ningún menú abierto. Por eso construye su propia UI si hace falta.
        ///
        /// No decide por el jugador: puede que solo falte esperar un poco más, y puede
        /// que prefiera irse sabiendo lo que pierde. Lo que no vale es cerrar en
        /// silencio, que es lo que hacía antes.
        /// </summary>
        /// <remarks>
        /// <paramref name="cambiosPendientes"/> ya no sale en el texto: el cartel dice qué
        /// está pasando, no cuántas filas de base de datos faltan, que a un niño/a no le
        /// dice nada. Se conserva en la firma porque es lo que el llamador sabe y lo que
        /// se escribe en el log cuando alguien elige salir.
        /// </remarks>
        public void PreguntarSiEsperar(int cambiosPendientes, Action alEsperar, Action alCerrar)
        {
            if (_raiz == null) Construir();

            _timeScalePrevio = Abierto ? _timeScalePrevio : Time.timeScale;
            Time.timeScale = 0f;
            _raiz.SetActive(true);

            if (_titulo != null) _titulo.text = "Guardando";
            if (_estado != null)
                _estado.text = "El guardado del juego está tardando más de lo esperado\n" +
                               "¿Salir ahora? Se perderán datos";

            Reemplazar(_btnSeguir, "Seguir esperando", () =>
            {
                Congelar("Guardando…");
                alEsperar?.Invoke();
            });

            Reemplazar(_btnSalir, "Salir ahora", () =>
            {
                Congelar("Saliendo sin guardar…");
                alCerrar?.Invoke();
            });
        }

        /// <summary>Cambia el texto y la acción de un botón ya construido.</summary>
        private static void Reemplazar(Button boton, string texto, Action alPulsar)
        {
            if (boton == null) return;
            boton.interactable = true;
            boton.onClick.RemoveAllListeners();
            boton.onClick.AddListener(() => alPulsar?.Invoke());

            var etiqueta = boton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (etiqueta != null) etiqueta.text = texto;
        }

        /// <summary>
        /// Apaga los dos botones y dice qué se está haciendo.
        ///
        /// Los botones NO vuelven a ser "Seguir jugando" / "Guardar y volver al menú": una vez que
        /// se preguntó, el juego se está cerrando por un camino o por otro. Devolverlos a
        /// su estado de menú dejaba un "Seguir jugando" pulsable en mitad del cierre por
        /// el aspa —donde `_saliendo` es false—, y pulsarlo escondía el menú mientras
        /// Unity seguía esperando a que la cola terminara. Si hay que volver a preguntar,
        /// <see cref="PreguntarSiEsperar"/> los reenciende.
        /// </summary>
        private void Congelar(string queEstaPasando)
        {
            if (_estado != null) _estado.text = queEstaPasando;
            if (_btnSeguir != null) _btnSeguir.interactable = false;
            if (_btnSalir != null) _btnSalir.interactable = false;
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

            _titulo = FishyUIKit.Texto(tarjetaGO.transform, "Titulo", "Pausa",
                64f, Paleta.Crema, TextAlignmentOptions.Center);

            _estado = FishyUIKit.Texto(tarjetaGO.transform, "Estado", "¿Qué quieres hacer?",
                30f, Paleta.Arena, TextAlignmentOptions.Center);

            _btnSeguir = FishyUIKit.Boton(tarjetaGO.transform, "Seguir jugando",
                Paleta.Verde, 34f, 88f, Cerrar);

            _btnSalir = FishyUIKit.Boton(tarjetaGO.transform, "Guardar y volver al menú",
                Paleta.Rojo, 34f, 88f, Salir);

            _raiz = canvasGO;
        }
    }
}
