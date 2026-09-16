using System;
using System.Collections;
using Fishy.Net;
using UnityEngine;

namespace Fishy.World
{
    /// <summary>
    /// HDU-15 — El punto único desde el que se guarda la partida.
    ///
    /// <b>No sube nada por su cuenta, y ya nadie más decide cuándo subir.</b> Los seis
    /// sincronizadores —posición, inventario, objetos recogidos, NPCs, misiones y el
    /// registro de chats— dejaron de llamar al backend cuando les pasa algo: ahora
    /// dejan el cambio en <see cref="ColaDeCambios"/>. Esta clase es la que suelta esa
    /// cola, y decide en qué momento.
    ///
    /// Hace falta porque antes cada sincronizador elegía su propio momento: unos por
    /// temporizador, otros al cambiar algo. Así no había forma de comprobar cuándo se
    /// guarda la partida, ni de forzarlo desde una prueba, y el juego se pasaba la
    /// partida goteando peticiones de 600-800 ms contra Supabase.
    ///
    /// <b>Se guarda en dos momentos, y solo en esos dos:</b> al cambiar de zona y al
    /// cerrar el juego. Hubo además un guardado periódico y otro al terminar cada
    /// interacción (chat, desbloqueo de zona); se quitaron a propósito. Los
    /// sincronizadores ya suben lo suyo en cuanto cambia, así que aquellos no tapaban
    /// un hueco de datos, y a cambio llenaban la consola y el tráfico de guardados que
    /// no guardaban nada. Si se vuelven a echar de menos, lo que falta es un motivo
    /// nuevo en <see cref="Motivo"/>, no reabrir los de antes.
    ///
    /// Se crea solo y sobrevive entre escenas.
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveManager : MonoBehaviour
    {
        /// <summary>Por qué se está guardando. Va en el log y en el evento, para que
        /// una prueba pueda afirmar "se guardó por esto y no de casualidad".
        ///
        /// Los valores son potencias de dos para que un <see cref="Motivo"/> se pueda
        /// convertir directo a <see cref="Momentos"/> y preguntarle al interruptor sin
        /// un switch que haya que acordarse de ampliar.</summary>
        public enum Motivo
        {
            CambioDeZona       = 1,
            CierreDeAplicacion = 2,
            /// <summary>Alguien pidió guardar a mano: una prueba, o el menú de pausa
            /// antes de salir.</summary>
            Manual             = 4,
        }

        /// <summary>En qué momentos se sube de verdad. Ver <see cref="momentosActivos"/>.</summary>
        [Flags]
        public enum Momentos
        {
            Ninguno            = 0,
            CambioDeZona       = 1,
            CierreDeAplicacion = 2,
            Manual             = 4,
        }

        public static SaveManager Instance { get; private set; }

        [Header("Configuración")]
        [Tooltip("Segundos mínimos entre dos guardados seguidos. Caminar sobre el " +
                 "borde de dos zonas dispara cambios de zona en cadena; esto los " +
                 "agrupa. El cierre del juego se salta este freno.")]
        [Min(0f)]
        public float esperaMinima = 1f;

        [Tooltip("En qué momentos se vacía la cola hacia el backend. Quitar " +
                 "CierreDeAplicacion deja que solo se guarde al cambiar de zona; " +
                 "quitar los dos significa que no se guarda nunca, y se avisa al arrancar.\n\n" +
                 "Para que este campo sirva hay que poner el SaveManager en la escena: " +
                 "si no existe, se autocrea por código y manda el valor de aquí abajo.")]
        public Momentos momentosActivos = Momentos.CambioDeZona | Momentos.CierreDeAplicacion;

        [Tooltip("Segundos máximos que puede tardar un vaciado normal (cambio de zona).")]
        [Min(0f)]
        public float topeNormal = 8f;

        [Tooltip("Segundos máximos que se retiene el cierre del juego esperando a que " +
                 "suba todo. Pasados, se le pregunta al jugador si espera más o cierra.")]
        [Min(0f)]
        public float topeDeCierre = 10f;

        [Tooltip("Escribir en consola cada guardado y su motivo.")]
        public bool verboseLogs = true;

        /// <summary>Se dispara después de PEDIR el guardado, no después de que el
        /// backend conteste: las peticiones quedan en vuelo. Trae el motivo.</summary>
        public static event Action<Motivo> OnGuardado;

        /// <summary>Último motivo por el que se guardó. Para diagnóstico y pruebas.</summary>
        public Motivo UltimoMotivo { get; private set; } = Motivo.Manual;

        /// <summary>Cuántas veces se ha guardado en esta sesión.</summary>
        public int Guardados { get; private set; }

        /// <summary>Zona en la que estaba Otto en el último guardado. Ver la nota de
        /// <see cref="GuardarZona"/> sobre por qué todavía no viaja al backend.</summary>
        public string ZonaGuardada { get; private set; } = "";

        private float _ultimoGuardado = float.NegativeInfinity;

        /// <summary>Ya se vació (o el jugador renunció): el próximo `wantsToQuit` dice sí.</summary>
        private static bool _listoParaCerrar;
        /// <summary>Hay un vaciado de cierre en curso; no arrancar otro.</summary>
        private static bool _cerrando;

        // ── Ciclo de vida ─────────────────────────────────────────────────────

        // Los dos banderines del cierre son estáticos porque `wantsToQuit` es un evento
        // estático; se limpian aquí porque el estático sobrevive al recargado de dominio
        // del editor y si no, la segunda corrida arrancaría creyendo que ya cerró.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void LimpiarEstadoEstatico()
        {
            _listoParaCerrar = false;
            _cerrando = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            GetOrCreate();

            // El `-=` antes del `+=` no es paranoia: el evento es estático y sobrevive
            // al recargado de dominio, así que sin él nos suscribiríamos dos veces y
            // el cierre se retrasaría dos veces.
            Application.wantsToQuit -= QuiereCerrar;
            Application.wantsToQuit += QuiereCerrar;
        }

        /// <summary>
        /// Unity pregunta antes de cerrar. Devolver false cancela el cierre.
        ///
        /// Es lo que permite subir lo que queda en la cola ANTES de que el proceso
        /// muera, en vez del `OnApplicationQuit` de siempre, que corre cuando Unity ya
        /// está desmontando y no da tiempo a que salga una petición.
        ///
        /// La gran ventaja sobre parchear cada botón de salir: el aspa de la ventana y
        /// Alt+F4 pasan por aquí también, y esos no pasan por ningún botón nuestro.
        /// </summary>
        private static bool QuiereCerrar()
        {
            if (_listoParaCerrar) return true;   // ya vaciamos, o el jugador dijo que da igual
            if (_cerrando)        return false;  // segundo Alt+F4 mientras se vacía
            if (Instance == null) return true;   // sin quien vacíe, no hay nada que esperar

            _cerrando = true;
            Instance.StartCoroutine(Instance.CerrarCuandoTermine());
            return false;
        }

        /// <summary>
        /// Da el cierre por resuelto: el próximo <c>wantsToQuit</c> dirá que sí.
        ///
        /// Lo llama el menú de pausa cuando ya vació la cola y preguntó él mismo, para
        /// que al llamar a <c>Application.Quit()</c> no se arranque un segundo vaciado
        /// con su segundo diálogo.
        /// </summary>
        public static void MarcarCierreListo()
        {
            _listoParaCerrar = true;
            _cerrando = false;
        }

        /// <summary>
        /// Vacía la cola y recién entonces deja cerrar. Si se acaba el plazo con cambios
        /// sin subir, le pregunta al jugador en vez de decidir por él: puede que solo
        /// haga falta esperar un poco más, y puede que prefiera irse sabiendo lo que
        /// pierde. Lo que no vale es cerrar en silencio.
        /// </summary>
        private IEnumerator CerrarCuandoTermine()
        {
            while (true)
            {
                yield return GuardarYEsperar(Motivo.CierreDeAplicacion, topeDeCierre);

                var cola = ColaDeCambios.Instance;
                int pendientes = cola != null ? cola.UltimoResultado.Pendientes : 0;
                if (pendientes <= 0 || ColaDeCambios.Pendientes <= 0) break;

                // El menú de pausa es quien sabe dibujar; si no existe, no se puede
                // preguntar y cerrar en seco es mejor que dejar el juego colgado.
                var menu = Fishy.UI.MenuPausa.Instance;
                if (menu == null)
                {
                    Debug.LogError($"[SaveManager] Se cierra con {ColaDeCambios.Pendientes} " +
                                   "cambios sin guardar: no hay menú para preguntar.");
                    break;
                }

                bool seguirEsperando = false;
                bool respondido = false;
                menu.PreguntarSiEsperar(ColaDeCambios.Pendientes,
                    alEsperar: () => { seguirEsperando = true;  respondido = true; },
                    alCerrar:  () => { seguirEsperando = false; respondido = true; });

                while (!respondido) yield return null;
                if (!seguirEsperando)
                {
                    Debug.LogError($"[SaveManager] El jugador eligió cerrar con " +
                                   $"{ColaDeCambios.Pendientes} cambios sin guardar.");
                    break;
                }

                // Vuelve a intentarlo. `Preparar` exige que haya pasado `esperaMinima`
                // para los motivos que no son cierre; este lo es, así que no se frena.
                _ultimoGuardado = float.NegativeInfinity;
            }

            _listoParaCerrar = true;
            _cerrando = false;
            Application.Quit();
        }

        public static SaveManager GetOrCreate()
        {
            if (Instance != null) return Instance;

            var encontrado = FindAnyObjectByType<SaveManager>();
            if (encontrado != null) return encontrado;

            var go = new GameObject("SaveManager");
            return go.AddComponent<SaveManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Apagarlo todo es legítimo para depurar, pero descubrirlo por accidente
            // —con una partida entera ya jugada— no lo es.
            if (momentosActivos == Momentos.Ninguno)
                Debug.LogWarning("[SaveManager] momentosActivos está en Ninguno: " +
                                 "NO se va a guardar nada en toda la partida.", this);
        }

        private void OnEnable()
        {
            ZonaActual.OnZonaCambiada += AlCambiarDeZona;
        }

        private void OnDisable()
        {
            ZonaActual.OnZonaCambiada -= AlCambiarDeZona;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Los dos momentos ─────────────────────────────────────────────────

        private void AlCambiarDeZona(string anterior, string nueva)
        {
            Guardar(Motivo.CambioDeZona);
        }

        private void OnApplicationQuit()
        {
            // Si venimos de `wantsToQuit`, la cola ya está vacía y esto sería una
            // segunda ronda en el frame del cierre, justo cuando ya no da tiempo a que
            // salga nada.
            if (_listoParaCerrar) return;

            // Camino sin `wantsToQuit`: en el editor, parar el Play NO lo dispara. Aquí
            // ya no se puede retrasar nada, así que es best-effort, como siempre fue.
            Guardar(Motivo.CierreDeAplicacion);
        }

        private void OnApplicationPause(bool pausado)
        {
            // En móvil `OnApplicationQuit` muchas veces no llega: el sistema mata la
            // app pausada sin avisar. Esta es la única señal fiable de "se va".
            if (pausado) Guardar(Motivo.CierreDeAplicacion);
        }

        // ── API pública ──────────────────────────────────────────────────────

        /// <summary>
        /// Vacía la cola de cambios hacia el backend.
        ///
        /// Ya no le pide a cada sincronizador que suba lo suyo: los cambios llevan un
        /// rato esperando en <see cref="ColaDeCambios"/> y esto es lo que los suelta.
        /// Lo único que hay que marcar aquí es la posición de Otto, porque cambia todos
        /// los frames y no tiene un evento de "cambió" al que engancharse.
        ///
        /// No espera a que terminen las peticiones. Quien necesite eso —el cierre del
        /// juego— usa <see cref="GuardarYEsperar"/>.
        /// </summary>
        public void Guardar(Motivo motivo)
        {
            if (!Preparar(motivo)) return;
            StartCoroutine(VaciarCola(motivo, TopePara(motivo)));
        }

        /// <summary>
        /// Igual que <see cref="Guardar"/>, pero se puede esperar: termina cuando la
        /// cola está vacía o se acabó el plazo. Lo usa el cierre del juego.
        /// </summary>
        public IEnumerator GuardarYEsperar(Motivo motivo, float tope)
        {
            if (!Preparar(motivo)) yield break;
            yield return VaciarCola(motivo, tope);
        }

        /// <summary>Comprobaciones y marcado. Devuelve false si no hay que hacer nada.</summary>
        private bool Preparar(Motivo motivo)
        {
            // El interruptor va lo primero: si este momento está apagado, aquí no pasa
            // nada de nada, ni siquiera se anota la zona.
            if ((momentosActivos & (Momentos)motivo) == 0)
            {
                if (verboseLogs)
                    Debug.Log($"[SaveManager] {motivo} está desactivado en momentosActivos.", this);
                return false;
            }

            // El cierre nunca se frena: es la última oportunidad que hay.
            bool urgente = motivo == Motivo.CierreDeAplicacion;
            if (!urgente && Time.unscaledTime - _ultimoGuardado < esperaMinima) return false;

            var api = ApiManager.Instance;
            if (api == null || api.PartidaId == null)
            {
                // Sin partida no hay dónde guardar. No es un error: pasa en el menú y
                // en modo local.
                return false;
            }

            _ultimoGuardado = Time.unscaledTime;
            UltimoMotivo = motivo;
            Guardados++;

            GuardarZona();

            // La posición no tiene evento de cambio —cambia cada frame—, así que se
            // marca aquí. La mochila y los demás se marcan solos cuando pasa algo.
            PersonajeBackendSync.Instance?.MarcarSucio();

            if (verboseLogs)
                Debug.Log($"[SaveManager] Guardado #{Guardados} por {motivo}" +
                          (string.IsNullOrEmpty(ZonaGuardada) ? "" : $" (zona: {ZonaGuardada})") +
                          $" — {ColaDeCambios.Pendientes} cambios en la cola.", this);

            return true;
        }

        private IEnumerator VaciarCola(Motivo motivo, float tope)
        {
            var cola = ColaDeCambios.Instance;
            if (cola != null)
            {
                // Al cambiar de zona sí se reintenta: habrá otra oportunidad. Al cerrar
                // no la hay, y reintentar solo gastaría el plazo que queda.
                bool reintentar = motivo != Motivo.CierreDeAplicacion;
                yield return cola.Vaciar(motivo.ToString(), tope, reintentar);
            }

            OnGuardado?.Invoke(motivo);
        }

        private float TopePara(Motivo motivo)
            => motivo == Motivo.CierreDeAplicacion ? topeDeCierre : topeNormal;

        /// <summary>
        /// Anota en qué zona está Otto, y la deja lista para que viaje.
        ///
        /// No sube nada por su cuenta —como todo en esta clase—: quien la manda es
        /// <c>PersonajeBackendSync.GuardarPosicion()</c>, que la lee de
        /// <see cref="ZonaGuardada"/> y la mete en el mismo PATCH que la posición.
        /// Por eso esto corre justo antes de pedirle que suba, en <see cref="Guardar"/>.
        ///
        /// Va a la columna `zona_actual` de `PersonajeJugador` (migración 0012). Ojo:
        /// esa columna es la REGIÓN DEL MAPA (`zona_2`), y no tiene nada que ver con
        /// `ZonaProgreso.zona`, que es la temática del banco (`desconocidos`). Son dos
        /// vocabularios distintos y no hay tabla que los relacione.
        /// </summary>
        private void GuardarZona()
        {
            var zonas = ZonaActual.Instance;
            if (zonas == null) return;

            // Revisar en vez de leer Actual: entre tic y tic pueden pasar centésimas,
            // y guardar la zona de la que Otto acaba de salir sería justo el error.
            ZonaGuardada = zonas.Revisar();
        }
    }
}
