using System;
using System.Collections;
using Fishy.Net;
using Fishy.Pausa;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// HDU-17 — El menú de pausa: Esc lo abre en cualquier momento de la partida y
    /// ofrece <b>Continuar</b>, <b>Configuración</b> y <b>Guardar y salir al menú
    /// principal</b>, que pide confirmación y guarda antes de irse.
    ///
    /// <b>Esta clase es la pantalla, no la pausa.</b> Quién está pausado, qué pasa
    /// con el <c>timeScale</c>, a quién se le quita la entrada y en qué momentos no
    /// se puede pausar vive en <see cref="EstadoDePausa"/>, en el ensamblado
    /// Fishy.Pausa — que es lo que deja probarlo con NUnit en PlayMode, igual que
    /// <c>AlbumDeEvidencias</c>. Acá solo se dibujan botones y se llama a esa puerta.
    ///
    /// Volver al menú cuenta como cierre de partida: se deja de jugar, así que se
    /// guarda igual que al cerrar la aplicación (ver <see cref="SaveManager"/>, que
    /// guarda en dos momentos y este es uno). Cerrar la aplicación sigue siendo cosa
    /// del aspa o del botón Salir de los menús.
    ///
    /// Se crea sola y sobrevive entre escenas, igual que el SaveManager: no hay nada
    /// que arrastrar en el editor. La UI no se construye hasta el primer Esc, así que
    /// en una partida en la que nadie la abre no cuesta nada.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuPausa : MonoBehaviour, ISalidaAlMenu
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

        /// <summary>
        /// Veces, como máximo, que se le pregunta «¿esperas o te vas?» en una salida.
        ///
        /// Es un tope duro además de la regla de «solo si la espera sirvió de algo»: el
        /// cartel es una disculpa, no una herramienta. Si a la tercera no se guardó, lo
        /// que falta no se arregla con otro clic del niño/a, y seguir preguntando es
        /// trasladarle un problema del servidor que no puede resolver.
        /// </summary>
        private const int MaxPreguntas = 2;

        private const string TextoContinuar = "Continuar";
        private const string TextoConfiguracion = "Configuración";
        private const string TextoSalir = "Guardar y salir al menú principal";

        /// <summary>Si el menú está a la vista. Con él abierto el juego está en pausa.</summary>
        public bool Abierto => _raiz != null && _raiz.activeSelf;

        private GameObject _raiz;
        private TextMeshProUGUI _titulo;
        private TextMeshProUGUI _estado;
        private Transform _botonera;
        private Button _btnSeguir, _btnConfig, _btnSalir;
        private PanelConfiguracionUI _config;
        private TecladoEnPantalla _controles;
        private Transform _canvas;
        private bool _saliendo;
        private bool _confirmandoSalida;

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

            // Dejar el juego congelado al destruirse sería peor que no haber pausado
            // nunca: nada volvería a moverse y no habría menú que cerrar.
            if (EstadoDePausa.Pausado) EstadoDePausa.Restablecer();
        }

        /// <summary>
        /// La tecla que abre y cierra el menú.
        ///
        /// Está declarada en vez de escrita dentro del <c>Update</c> para que el
        /// teclado de ayuda (<see cref="TecladoEnPantalla"/>) pueda enseñar la de
        /// verdad: si mañana la pausa pasa a otra tecla, el cartel lo dice solo.
        /// </summary>
        public const Key TeclaDePausa = Key.Escape;

        private void Update()
        {
            if (_saliendo) return;

            // Solo el nuevo Input System (activeInputHandler: 1). Sin teclado
            // —un móvil— `current` es null y aquí no hay nada que hacer.
            var teclado = Keyboard.current;
            if (teclado == null || !teclado[TeclaDePausa].wasPressedThisFrame) return;

            // Con la configuración abierta, Esc es «volver», no «seguir jugando»:
            // es lo que hace el botón que se está mirando, y saltar del ajuste al
            // juego de un tecleo deja a cualquiera sin saber si guardó el cambio.
            if (_controles != null && _controles.Abierto) { CerrarControles(); return; }
            if (_config != null && _config.Abierto) { CerrarConfiguracion(); return; }

            // Igual con la confirmación de salida: Esc es «no», no «a jugar».
            if (_confirmandoSalida) { CancelarSalida(); return; }

            if (Abierto) Cerrar();
            else Abrir();
        }

        // ── Abrir y cerrar ────────────────────────────────────────────────────

        /// <summary>
        /// CA1 y CA3 — Abre el menú y congela el juego, si es que se puede: durante
        /// una cinemática o una transición de carga no se puede, y entonces Esc no
        /// hace nada (ver <see cref="BloqueosDePausa"/>).
        /// </summary>
        public void Abrir()
        {
            if (Abierto || _saliendo) return;

            // "Hay Otto en la escena" es como el resto del código distingue el juego
            // de un menú (ver PersonajeBackendSync.BuscarOtto).
            if (soloDuranteElJuego && FindAnyObjectByType<OttoController>() == null) return;

            // El veto se consulta ANTES de construir nada: si no se va a pausar,
            // tampoco hay que dejar un canvas montado que nadie pidió.
            if (!EstadoDePausa.Pausar()) return;

            if (_raiz == null) Construir();

            MostrarPausa();
            _raiz.SetActive(true);
        }

        /// <summary>Pone la tarjeta en su estado normal de pausa.</summary>
        private void MostrarPausa()
        {
            _confirmandoSalida = false;

            if (_titulo != null) _titulo.text = "Pausa";
            if (_estado != null) _estado.text = "¿Qué quieres hacer?";
            RestaurarBotones();
        }

        /// <summary>CA4 — Vuelve al juego exactamente donde estaba.</summary>
        public void Cerrar()
        {
            if (!Abierto || _saliendo) return;

            _config?.Cerrar();
            _controles?.Cerrar();
            _raiz.SetActive(false);

            EstadoDePausa.Reanudar();
        }

        // ── Configuración (CA2) ───────────────────────────────────────────────

        /// <summary>Tapa la botonera con la tarjeta de ajustes. El juego sigue
        /// congelado: configurar es parte de estar en pausa.</summary>
        public void AbrirConfiguracion()
        {
            if (_saliendo) return;
            if (_raiz == null) Construir();

            if (_botonera != null) _botonera.gameObject.SetActive(false);
            _config.Abrir();
        }

        public void CerrarConfiguracion()
        {
            _controles?.Cerrar();
            _config?.Cerrar();
            if (_botonera != null) _botonera.gameObject.SetActive(true);
        }

        /// <summary>Cambia la tarjeta de configuración por la de controles. El juego
        /// sigue en pausa, y la tarjeta no se va sola: solo con «Volver» o Esc.</summary>
        public void AbrirControles()
        {
            if (_saliendo) return;
            if (_raiz == null) Construir();

            if (_controles == null)
                _controles = TecladoEnPantalla.CrearEn(_canvas, CerrarControles);

            if (_botonera != null) _botonera.gameObject.SetActive(false);
            _config?.Cerrar();
            _controles.Abrir();
        }

        /// <summary>De los controles se vuelve a la configuración, que es de donde
        /// se vino.</summary>
        public void CerrarControles()
        {
            _controles?.Cerrar();
            _config?.Abrir();
        }

        // ── Salir al menú principal (CA5) ─────────────────────────────────────

        /// <summary>
        /// Lo que hace el botón de salir: pregunta antes de irse, sobre la misma tarjeta.
        /// El juego sigue congelado y no se ha guardado nada todavía.
        ///
        /// Cancelar va arriba y en verde, donde estaba Continuar, para que la opción
        /// que no cambia nada sea también la que está a mano. Configuración se esconde
        /// porque aquí no hay nada que configurar.
        /// </summary>
        public void PedirConfirmacionDeSalida()
        {
            if (!Abierto || _saliendo) return;

            _confirmandoSalida = true;

            if (_titulo != null) _titulo.text = "¿Seguro que deseas salir?";
            if (_estado != null) _estado.text = "Tu partida se guardará y volverás al menú principal.";

            if (_btnConfig != null) _btnConfig.gameObject.SetActive(false);
            Reemplazar(_btnSeguir, "Cancelar", CancelarSalida);
            Reemplazar(_btnSalir, "Sí, guardar y salir", Salir);
        }

        /// <summary>Vuelve a la tarjeta de pausa. La partida no se ha tocado: solo se
        /// cambió lo que decía la tarjeta.</summary>
        public void CancelarSalida()
        {
            if (!_confirmandoSalida || _saliendo) return;
            MostrarPausa();
        }

        /// <summary>Guarda y vuelve al menú. Es lo que hace el botón de confirmar. El
        /// orden —guardar primero, cargar después— lo impone
        /// <see cref="SecuenciaDeSalida"/>, que es donde se puede comprobar.</summary>
        public void Salir()
        {
            if (_saliendo) return;

            _confirmandoSalida = false;
            StartCoroutine(SalirAlMenu());
        }

        private IEnumerator SalirAlMenu()
        {
            _saliendo = true;
            CerrarConfiguracion();

            yield return SecuenciaDeSalida.Ejecutar(this);

            _saliendo = false;
        }

        /// <summary>
        /// CA5 — El guardado automático de la salida: vacía la cola hacia el backend y
        /// espera a que termine. Lo llama <see cref="SecuenciaDeSalida"/>, que se
        /// encarga de que la escena del menú no se cargue hasta que esto acabe.
        /// </summary>
        IEnumerator ISalidaAlMenu.GuardarTodo()
        {
            if (_btnSeguir != null) _btnSeguir.interactable = false;
            if (_btnConfig != null) _btnConfig.interactable = false;
            if (_btnSalir != null) _btnSalir.interactable = false;
            if (_titulo != null) _titulo.text = "Guardando";
            if (_estado != null) _estado.text = "Guardando tu partida…";

            // `GuardarYEsperar` termina cuando la cola está vacía o se acabó el plazo.
            // Antes esto era un WaitForSecondsRealtime a ciegas y el texto de arriba
            // mentía por diseño — decía "guardando" y no sabía nada.
            var save = SaveManager.Instance;
            if (save != null)
                yield return save.GuardarYEsperar(SaveManager.Motivo.CierreDeAplicacion,
                                                  save.topeDeCierre);

            // Si algo quedó sin subir se pregunta, igual que en el cierre por el aspa.
            // Pero se pregunta POCO: ver `MaxPreguntas` y la regla de abajo.
            for (int pregunta = 0; ColaDeCambios.Pendientes > 0 && pregunta < MaxPreguntas; pregunta++)
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

                int antes = ColaDeCambios.Pendientes;
                yield return cola.Vaciar("CierreDeAplicacion",
                                         save != null ? save.topeDeCierre : esperaAntesDeSalir,
                                         reintentarSiFalla: true);

                // Esperar no sirvió de nada: no se vuelve a preguntar lo mismo.
                //
                // Es la regla que arregla el «pide confirmación a cada rato». Antes el
                // bucle solo miraba si quedaban cambios, así que contra un servidor que
                // no responde volvía a salir el mismo cartel indefinidamente, y cada
                // «Seguir esperando» costaba el plazo entero (10 s). Diez clics eran
                // 100 segundos para acabar igual que al principio.
                //
                // Si la ronda bajó la cola, el servidor está respondiendo y preguntar
                // otra vez es razonable. Si no bajó nada, el niño/a no tiene forma de
                // saberlo y no es asunto suyo: se decide aquí y se vuelve al menú.
                if (ColaDeCambios.Pendientes >= antes) break;
            }

            bool quedoAlgo = ColaDeCambios.Pendientes > 0 || ColaDeCambios.AlgoSeQuedoSinGuardar;
            if (_estado != null)
                _estado.text = quedoAlgo
                    ? "No se pudo guardar todo. Volviendo al menú…"
                    : "Listo, ya se guardó.";

            // El jugador ya eligió irse sabiendo que se perdía. La aplicación sigue
            // abierta, así que esto no muere con el proceso: hay que tirarlo a mano.
            if (quedoAlgo) ColaDeCambios.DescartarTodo("se volvió al menú sin esperar");

            // Un frame para que se lea el mensaje final antes de que desaparezca todo.
            yield return new WaitForSecondsRealtime(0.4f);
        }

        /// <summary>
        /// CA5 — Carga la escena del menú. Corre después del guardado, nunca antes.
        ///
        /// Sin escena a la que ir se cierra el juego, que es lo que esto hacía antes
        /// de que el menú existiera en Build Settings.
        /// </summary>
        void ISalidaAlMenu.CargarMenu()
        {
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
                return;
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
            _config = null;
            _controles = null;
            _canvas = null;
            _botonera = null;

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

            _confirmandoSalida = false;
            CerrarConfiguracion();

            // Pausar y no tocar el timeScale a mano: puede que ya estemos en pausa
            // —se salió desde este mismo menú— y puede que no —se cerró con el aspa—.
            // `Pausar()` sabe distinguirlo; dos pantallas congelando por su cuenta era
            // justo lo que dejaba el juego parado para siempre.
            EstadoDePausa.Pausar();
            _raiz.SetActive(true);

            if (_titulo != null) _titulo.text = "Guardando";
            if (_estado != null)
                _estado.text = "El guardado del juego está tardando más de lo esperado\n" +
                               "¿Salir ahora? Se perderán datos";

            // Con el cierre en marcha no hay nada que configurar.
            if (_btnConfig != null) _btnConfig.gameObject.SetActive(false);

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

        /// <summary>Devuelve los tres botones a lo que dicen en el menú de pausa. Hace
        /// falta porque <see cref="PreguntarSiEsperar"/> les cambia el texto y la
        /// acción, y tras un cierre que el jugador canceló el menú se vuelve a abrir
        /// con «Seguir esperando» escrito donde va «Continuar». La confirmación de
        /// salida también pasa por aquí al cancelarse.</summary>
        private void RestaurarBotones()
        {
            Reemplazar(_btnSeguir, TextoContinuar, Cerrar);
            Reemplazar(_btnSalir, TextoSalir, PedirConfirmacionDeSalida);

            if (_btnConfig == null) return;
            _btnConfig.gameObject.SetActive(true);
            Reemplazar(_btnConfig, TextoConfiguracion, AbrirConfiguracion);
        }

        /// <summary>
        /// Apaga los botones y dice qué se está haciendo.
        ///
        /// Los botones NO vuelven a ser "Continuar" / "Guardar y salir": una vez que
        /// se preguntó, el juego se está cerrando por un camino o por otro. Devolverlos a
        /// su estado de menú dejaba un "Continuar" pulsable en mitad del cierre por
        /// el aspa —donde `_saliendo` es false—, y pulsarlo escondía el menú mientras
        /// Unity seguía esperando a que la cola terminara. Si hay que volver a preguntar,
        /// <see cref="PreguntarSiEsperar"/> los reenciende.
        /// </summary>
        private void Congelar(string queEstaPasando)
        {
            if (_estado != null) _estado.text = queEstaPasando;
            if (_btnSeguir != null) _btnSeguir.interactable = false;
            if (_btnSalir != null) _btnSalir.interactable = false;
            if (_btnConfig != null) _btnConfig.interactable = false;
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
            // lleguen a lo que hay debajo. Es la otra mitad de "se desactiva el input
            // del jugador" (CA3): la PuertaDeEntrada calla el teclado y el mando, y
            // esto tapa los botones táctiles y lo que haya en pantalla.
            var velo = new GameObject("Velo", typeof(RectTransform), typeof(Image));
            velo.transform.SetParent(canvasGO.transform, false);
            FishyUIKit.Estirar(velo.GetComponent<RectTransform>());
            velo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var tarjetaGO = new GameObject("Tarjeta",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            tarjetaGO.transform.SetParent(canvasGO.transform, false);
            FishyUIKit.FondoRedondeado(tarjetaGO.GetComponent<Image>(), Paleta.Marron);
            _botonera = tarjetaGO.transform;

            var rt = tarjetaGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            // Más alta que antes: ahora son tres botones y no dos. Y el ancho es el que
            // necesita el texto del botón de salir para caber en una línea.
            rt.sizeDelta = new Vector2(800f, 570f);

            var vlg = tarjetaGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(44, 44, 40, 40);
            vlg.spacing = 18f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            _titulo = FishyUIKit.Texto(tarjetaGO.transform, "Titulo", "Pausa",
                64f, Paleta.Crema, TextAlignmentOptions.Center);

            _estado = FishyUIKit.Texto(tarjetaGO.transform, "Estado", "¿Qué quieres hacer?",
                30f, Paleta.Arena, TextAlignmentOptions.Center);

            _btnSeguir = FishyUIKit.Boton(tarjetaGO.transform, TextoContinuar,
                Paleta.Verde, 34f, 88f, Cerrar);

            _btnConfig = FishyUIKit.Boton(tarjetaGO.transform, TextoConfiguracion,
                Paleta.Madera, 34f, 88f, AbrirConfiguracion);

            _btnSalir = FishyUIKit.Boton(tarjetaGO.transform, TextoSalir,
                Paleta.Rojo, 34f, 88f, PedirConfirmacionDeSalida);

            // Hermana de la tarjeta y no hija: se muestra en su lugar, tapándola.
            _config = new PanelConfiguracionUI(canvasGO.transform, CerrarConfiguracion,
                AbrirControles);
            _canvas = canvasGO.transform;

            _raiz = canvasGO;
        }
    }
}
