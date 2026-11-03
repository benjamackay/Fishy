#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Fishy.Detective;
using Fishy.Net;
using Fishy.Pausa;
using Fishy.UI;
using Fishy.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless del menú de pausa (HDU-17).
    ///
    /// Lo que se puede probar con NUnit en PlayMode está en Fishy.Pausa.Tests, al
    /// lado del núcleo: congelar, callar la entrada, los vetos y el orden
    /// guardar-antes-de-cargar, todo contra dobles. Acá está lo que esas pruebas no
    /// alcanzan a ver, porque vive en Assembly-CSharp y un .asmdef de pruebas no
    /// puede referenciarlo: el <see cref="MenuPausa"/> de verdad, el
    /// <see cref="SaveManager"/> de verdad y un caso del Modo Detective a medio
    /// resolver.
    ///
    /// Lo del álbum en el celular vive en <c>FishyPruebasAlbum</c>, que es su sitio.
    ///
    /// Se corren desde el menú  Fishy ▸ Probar menú de pausa,
    /// o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasPausa.Ejecutar `
    ///             -logFile -
    ///
    /// Termina con código 0 si todo pasa y 1 si algo falla.
    /// </summary>
    public static class FishyPruebasPausa
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        private const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Fishy/Probar menú de pausa")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DEL MENÚ DE PAUSA (headless)");
            log.AppendLine(new string('=', 70));

            AsegurarApiLocal();

            // ── La pantalla y la pausa de verdad ──
            ProbarElMenuSeAbreConLosTresBotones(log);
            ProbarPausarCongelaYLeQuitaElControlAOtto(log);
            ProbarReanudarLeDevuelveElControlAOtto(log);
            ProbarNoSeAbreSiAlgoLoVeta(log);
            ProbarLaConfiguracionTapaLaBotoneraYVuelve(log);
            ProbarLaConfiguracionGuardaElVolumen(log);
            ProbarElBotonEnPantallaSeEscondeCuandoNoTocaPausar(log);

            // ── El camino real de salida ──
            ProbarSalirPideElGuardadoAntesDeCargarElMenu(log);

            // ── Lo que no se puede perder al pausar ──
            ProbarUnCasoDetectiveAMedioResolverSobreviveALaPausa(log);


            EstadoDePausa.Restablecer();

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── La pantalla ──────────────────────────────────────────────────────────

        /// <summary>CA2 — El menú ofrece las tres opciones que pide la historia.</summary>
        private static void ProbarElMenuSeAbreConLosTresBotones(StringBuilder log)
        {
            Reiniciar();

            OttoController otto = NuevoOtto();
            MenuPausa menu = NuevoMenu();
            menu.Abrir();

            List<string> botones = EtiquetasDeBotones(menu);

            Comprobar(log, "el menú de pausa ofrece Continuar, Configuración y Salir al menú",
                botones.Any(t => t.Contains("Seguir jugando")) &&
                botones.Any(t => t.Contains("Configuración")) &&
                botones.Any(t => t.Contains("volver al menú")),
                string.Join(" | ", botones));

            Soltar(menu, otto);
        }

        /// <summary>CA3 — Al pausar se congela la acción y se desactiva el input.</summary>
        private static void ProbarPausarCongelaYLeQuitaElControlAOtto(StringBuilder log)
        {
            Reiniciar();

            OttoController otto = NuevoOtto();
            bool podiaMoverse = otto.movementEnabled;

            MenuPausa menu = NuevoMenu();
            menu.Abrir();

            Comprobar(log, "al pausar se congela el juego y Otto deja de responder",
                podiaMoverse && EstadoDePausa.Pausado && Mathf.Approximately(Time.timeScale, 0f) &&
                !otto.movementEnabled && menu.Abierto,
                $"timeScale={Time.timeScale}, Otto se mueve={otto.movementEnabled}");

            Soltar(menu, otto);
        }

        /// <summary>CA4 — Al continuar, el juego vuelve a donde estaba.</summary>
        private static void ProbarReanudarLeDevuelveElControlAOtto(StringBuilder log)
        {
            Reiniciar();

            OttoController otto = NuevoOtto();
            MenuPausa menu = NuevoMenu();

            menu.Abrir();
            menu.Cerrar();

            Comprobar(log, "al continuar vuelve el tiempo y vuelve el control",
                !EstadoDePausa.Pausado && Mathf.Approximately(Time.timeScale, 1f) &&
                otto.movementEnabled && !menu.Abierto,
                $"timeScale={Time.timeScale}, Otto se mueve={otto.movementEnabled}");

            Soltar(menu, otto);
        }

        /// <summary>
        /// CA1 — Con una cinemática o una carga en curso, Esc no abre nada.
        ///
        /// El veto se pone a mano en vez de arrancar una <c>ZoneUnlockCinematic</c>
        /// de verdad: lo que esta prueba tiene que ver es que <see cref="MenuPausa"/>
        /// le hace caso al núcleo, no que la cinemática sepa decir que está corriendo
        /// —eso es cosa suya y ya lo hacía—. Quién veta de verdad lo decide
        /// <see cref="BloqueosDePausa"/>.
        /// </summary>
        private static void ProbarNoSeAbreSiAlgoLoVeta(StringBuilder log)
        {
            Reiniciar();

            const string Veto = "prueba: cinemática";
            OttoController otto = NuevoOtto();
            MenuPausa menu = NuevoMenu();

            EstadoDePausa.RegistrarBloqueo(Veto, () => true);
            menu.Abrir();

            bool noAbrio = !menu.Abierto && !EstadoDePausa.Pausado &&
                           Mathf.Approximately(Time.timeScale, 1f) && otto.movementEnabled;

            EstadoDePausa.SoltarBloqueo(Veto);
            menu.Abrir();

            Comprobar(log, "con una cinemática en curso no se pausa, y en cuanto termina sí",
                noAbrio && menu.Abierto && EstadoDePausa.Pausado,
                noAbrio ? "" : "el menú se abrió encima de la cinemática");

            Soltar(menu, otto);
        }

        /// <summary>CA2 — «Configuración» tapa la botonera y «Volver» la devuelve, sin
        /// descongelar el juego: configurar es parte de estar en pausa.</summary>
        private static void ProbarLaConfiguracionTapaLaBotoneraYVuelve(StringBuilder log)
        {
            Reiniciar();

            OttoController otto = NuevoOtto();
            MenuPausa menu = NuevoMenu();
            menu.Abrir();

            menu.AbrirConfiguracion();
            bool tapada = !Botonera(menu).gameObject.activeSelf && ConfigAbierta(menu) &&
                          EstadoDePausa.Pausado;

            menu.CerrarConfiguracion();
            bool volvio = Botonera(menu).gameObject.activeSelf && !ConfigAbierta(menu) &&
                          EstadoDePausa.Pausado;

            Comprobar(log, "la configuración se abre encima del menú y vuelve sin reanudar el juego",
                tapada && volvio,
                tapada ? (volvio ? "" : "no volvió al menú") : "no tapó la botonera");

            Soltar(menu, otto);
        }

        /// <summary>CA2 — El volumen elegido se queda guardado para la próxima vez.</summary>
        private static void ProbarLaConfiguracionGuardaElVolumen(StringBuilder log)
        {
            float original = ConfiguracionDelJuego.Volumen;

            ConfiguracionDelJuego.Volumen = 0.37f;
            bool seGuardo = Mathf.Approximately(ConfiguracionDelJuego.Volumen, 0.37f) &&
                            Mathf.Approximately(AudioListener.volume, 0.37f);

            ConfiguracionDelJuego.Volumen = 2f;
            bool seAcota = Mathf.Approximately(ConfiguracionDelJuego.Volumen, 1f);

            ConfiguracionDelJuego.Volumen = original;

            Comprobar(log, "el volumen se guarda, se aplica y no se sale de 0..1",
                seGuardo && seAcota, $"volumen restaurado a {original:0.00}");
        }

        /// <summary>
        /// CA1 — El botón táctil de pausa está mientras se pueda pausar, y no está
        /// cuando no: sin partida, con el juego ya pausado, o durante una cinemática.
        ///
        /// Un botón visible que no hace nada es peor que no tener botón: el niño/a lo
        /// pulsa tres veces y concluye que el juego se colgó.
        /// </summary>
        private static void ProbarElBotonEnPantallaSeEscondeCuandoNoTocaPausar(StringBuilder log)
        {
            Reiniciar();

            const string Veto = "prueba: cinemática";
            var go = new GameObject("BotonDePausaDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var boton = go.AddComponent<BotonDePausaEnPantalla>();
            Invocar(boton, "Construir");

            // Sin Otto en la escena no hay partida que pausar.
            Invocar(boton, "Revisar");
            bool ocultoSinPartida = !Visible(boton);

            OttoController otto = NuevoOtto();
            Invocar(boton, "Revisar");
            bool visibleEnPartida = Visible(boton);

            EstadoDePausa.RegistrarBloqueo(Veto, () => true);
            Invocar(boton, "Revisar");
            bool ocultoEnCinematica = !Visible(boton);
            EstadoDePausa.SoltarBloqueo(Veto);

            EstadoDePausa.Pausar();
            Invocar(boton, "Revisar");
            bool ocultoEnPausa = !Visible(boton);

            Comprobar(log, "el botón táctil de pausa está solo cuando se puede pausar",
                ocultoSinPartida && visibleEnPartida && ocultoEnCinematica && ocultoEnPausa,
                $"sin partida oculto={ocultoSinPartida}, en partida visible={visibleEnPartida}, " +
                $"en cinemática oculto={ocultoEnCinematica}, en pausa oculto={ocultoEnPausa}");

            UnityEngine.Object.DestroyImmediate(go);
            Soltar(null, otto);
        }

        private static bool Visible(BotonDePausaEnPantalla boton)
        {
            var raiz = boton.GetType().GetField("_raiz", Privado).GetValue(boton) as GameObject;
            return raiz != null && raiz.activeSelf;
        }

        // ── Salir al menú (CA5) ──────────────────────────────────────────────────

        /// <summary>
        /// Un destino que usa el guardado DE VERDAD de <see cref="MenuPausa"/> pero
        /// que no carga ninguna escena: anota que se lo pidieron y sigue.
        ///
        /// Cargar la escena del menú desde un arnés de edición se llevaría por
        /// delante la escena que el desarrollador tiene abierta. Lo que importa acá
        /// es el orden, y el orden se ve igual.
        /// </summary>
        private class SalidaEspiada : ISalidaAlMenu
        {
            private readonly ISalidaAlMenu _real;
            public readonly List<string> Pasos = new List<string>();

            public SalidaEspiada(MenuPausa menu) => _real = menu;

            public IEnumerator GuardarTodo()
            {
                Pasos.Add("guardar:empieza");
                IEnumerator real = _real.GuardarTodo();
                while (real.MoveNext()) yield return real.Current;
                Pasos.Add("guardar:termina");
            }

            public void CargarMenu() => Pasos.Add("cargar");
        }

        private static void ProbarSalirPideElGuardadoAntesDeCargarElMenu(StringBuilder log)
        {
            Reiniciar();

            OttoController otto = NuevoOtto();
            MenuPausa menu = NuevoMenu();
            menu.Abrir();

            SaveManager save = NuevoSaveManager();
            DarPartida(42424);

            var guardados = new List<SaveManager.Motivo>();
            Action<SaveManager.Motivo> escucha = m => guardados.Add(m);
            SaveManager.OnGuardado += escucha;

            var espia = new SalidaEspiada(menu);
            try
            {
                // En edición no hay ColaDeCambios viva que vacíe lo que el SaveManager
                // encola, así que la cola se queda con cambios pendientes y
                // `GuardarTodo` entra en su bucle de «¿esperas o te vas?», que espera
                // un botón que nadie va a pulsar. Se vacía a mano en cada paso: lo
                // que esta prueba mira es el orden, no el transporte al backend.
                Bombear(SecuenciaDeSalida.Ejecutar(espia), porPaso: () =>
                {
                    if (ColaDeCambios.Pendientes > 0)
                        ColaDeCambios.DescartarTodo("arnés de pruebas: no hay backend");

                    ResponderSiPregunta(menu);
                });
            }
            finally
            {
                SaveManager.OnGuardado -= escucha;
            }

            int termina = espia.Pasos.IndexOf("guardar:termina");
            int carga = espia.Pasos.IndexOf("cargar");

            Comprobar(log, "salir al menú termina el guardado antes de pedir la escena",
                termina >= 0 && carga >= 0 && termina < carga,
                string.Join(" → ", espia.Pasos));

            Comprobar(log, "y ese guardado es el del SaveManager, con motivo de cierre",
                guardados.Contains(SaveManager.Motivo.CierreDeAplicacion),
                guardados.Count == 0 ? "el SaveManager no guardó nada"
                                     : string.Join(", ", guardados));

            Comprobar(log, "al salir el juego queda descongelado para la escena que viene",
                Mathf.Approximately(Time.timeScale, 1f) && !EstadoDePausa.Pausado,
                $"timeScale={Time.timeScale}");

            QuitarPartida();
            UnityEngine.Object.DestroyImmediate(save.gameObject);
            QuitarInstancia(typeof(SaveManager));
            Soltar(menu, otto);
        }

        // ── Lo que no se puede perder (CA4) ──────────────────────────────────────

        /// <summary>
        /// CA4 — Un caso del Modo Detective a medio resolver sigue igual tras la
        /// pausa: las señales ya marcadas siguen marcadas y se puede seguir marcando.
        ///
        /// Es el criterio que más caro saldría equivocar: el niño/a lleva tres
        /// mensajes revisados, pausa para ir a comer y al volver el caso está en
        /// blanco. No pasa porque la pausa solo toca el reloj y la entrada; esto lo
        /// deja comprobado contra el manager de verdad y un caso del banco.
        /// </summary>
        private static void ProbarUnCasoDetectiveAMedioResolverSobreviveALaPausa(StringBuilder log)
        {
            Reiniciar();

            DetectiveCase caso = DetectiveCaseLoader.LoadLocal("detective_caso_01");
            if (caso == null || caso.mensajes == null || caso.mensajes.Count < 3)
            {
                Comprobar(log, "un caso a medio resolver sobrevive a la pausa", false,
                    "no se pudo cargar detective_caso_01 del respaldo local");
                return;
            }

            OttoController otto = NuevoOtto();
            DetectiveCaseManager manager = NuevoManager();
            manager.CargarCaso(caso);

            string primero = caso.mensajes[0].id;
            string segundo = caso.mensajes[1].id;
            string tercero = caso.mensajes[2].id;

            manager.ToggleMarca(primero);
            manager.ToggleMarca(segundo);

            MenuPausa menu = NuevoMenu();
            menu.Abrir();

            bool durante = manager.EstaMarcado(primero) && manager.EstaMarcado(segundo);

            menu.Cerrar();

            bool despues = manager.EstaMarcado(primero) && manager.EstaMarcado(segundo) &&
                           !manager.EstaMarcado(tercero);

            // Y se puede seguir donde se dejó.
            manager.ToggleMarca(tercero);
            bool sigueVivo = manager.EstaMarcado(tercero);

            Comprobar(log, "un caso a medio resolver conserva sus marcas al pausar y reanudar",
                durante && despues && sigueVivo,
                $"durante={durante}, después={despues}, se puede seguir={sigueVivo}");

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            Soltar(menu, otto);
        }

        // ── Andamios ─────────────────────────────────────────────────────────────

        /// <summary>Cada prueba arranca sin pausa y con el tiempo corriendo: el estado
        /// es estático y, si no, una prueba vería lo que dejó la anterior.</summary>
        private static void Reiniciar()
        {
            EstadoDePausa.Restablecer();
            Time.timeScale = 1f;
        }

        /// <summary>
        /// Un Otto mínimo, con su ciclo de vida disparado a mano.
        ///
        /// En edición Unity no llama a Awake, OnEnable ni Start, y los tres importan
        /// acá: Awake arma las acciones de entrada, OnEnable es donde Otto se apunta
        /// a la <c>PuertaDeEntrada</c> y Start es donde se enciende el movimiento.
        /// </summary>
        private static OttoController NuevoOtto()
        {
            // Sin hideFlags, al revés que los demás andamios de acá.
            // FindObjectsByType no devuelve lo marcado con HideAndDontSave ni con
            // DontSave, y «hay un Otto en la escena» es justo lo que el menú de pausa
            // y el botón táctil preguntan para saber si hay partida que pausar:
            // escondido, este Otto no existiría para ellos. Se destruye en `Soltar`,
            // y la escena no se guarda nunca desde acá.
            var go = new GameObject("OttoDePrueba");
            var otto = go.AddComponent<OttoController>();
            otto.enableOnStart = true;

            Invocar(otto, "Awake");
            Invocar(otto, "OnEnable");
            Invocar(otto, "Start");
            return otto;
        }

        private static MenuPausa NuevoMenu()
        {
            var go = new GameObject("MenuPausaDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var menu = go.AddComponent<MenuPausa>();

            // El menú normalmente solo se abre si hay un OttoController en la escena,
            // que es como el resto del código distingue el juego de un menú. Acá se
            // apaga esa comprobación: así las pruebas que no necesitan a Otto no
            // tienen que crear uno, y las que sí lo crean comprueban lo suyo —que se
            // le quita el control— sin depender de este atajo.
            menu.soloDuranteElJuego = false;

            PonerInstancia(typeof(MenuPausa), menu);
            return menu;
        }

        private static SaveManager NuevoSaveManager()
        {
            var go = new GameObject("SaveManagerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var save = go.AddComponent<SaveManager>();
            save.verboseLogs = false;
            save.esperaMinima = 0f;
            save.topeDeCierre = 0.1f;

            PonerInstancia(typeof(SaveManager), save);
            return save;
        }

        /// <summary>
        /// Deja el singleton apuntando a este objeto, sin pasar por su <c>Awake</c>.
        ///
        /// El <c>Awake</c> de estos dos hace dos cosas: fijar <c>Instance</c> y
        /// llamar a <c>DontDestroyOnLoad</c>, que fuera del modo Play no avisa sino
        /// que <b>tira</b> InvalidOperationException. Lo que las pruebas necesitan es
        /// lo primero; lo segundo solo tiene sentido con escenas de verdad.
        /// </summary>
        private static void PonerInstancia(Type tipo, MonoBehaviour quien)
        {
            tipo.GetField("<Instance>k__BackingField",
                     BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, quien);
        }

        /// <summary>Suelta el singleton al terminar: es estático y sobreviviría a la
        /// prueba apuntando a un objeto ya destruido.</summary>
        private static void QuitarInstancia(Type tipo)
        {
            tipo.GetField("<Instance>k__BackingField",
                     BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, null);
        }

        private static DetectiveCaseManager NuevoManager()
        {
            var go = new GameObject("DetectiveCaseManagerDePrueba")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            return go.AddComponent<DetectiveCaseManager>();
        }

        private static void Soltar(MenuPausa menu, OttoController otto)
        {
            if (menu != null)
            {
                Invocar(menu, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(menu.gameObject);
                QuitarInstancia(typeof(MenuPausa));
            }

            if (otto != null)
            {
                Invocar(otto, "OnDisable");
                UnityEngine.Object.DestroyImmediate(otto.gameObject);
            }

            EstadoDePausa.Restablecer();
            Time.timeScale = 1f;
        }

        private static void Invocar(MonoBehaviour comp, string metodo)
            => comp.GetType().GetMethod(metodo, Privado)?.Invoke(comp, null);

        private static Transform Botonera(MenuPausa menu)
            => (Transform)menu.GetType().GetField("_botonera", Privado).GetValue(menu);

        private static bool ConfigAbierta(MenuPausa menu)
        {
            var config = menu.GetType().GetField("_config", Privado).GetValue(menu)
                as PanelConfiguracionUI;
            return config != null && config.Abierto;
        }

        private static List<string> EtiquetasDeBotones(MenuPausa menu)
        {
            return menu.GetComponentsInChildren<Button>(true)
                .Select(b => b.GetComponentInChildren<TMP_Text>(true))
                .Where(t => t != null)
                .Select(t => t.text ?? "")
                .ToList();
        }

        /// <summary>
        /// Recorre una corrutina hasta el final, en edición y sin StartCoroutine.
        ///
        /// Entra en las corrutinas anidadas —<c>GuardarTodo</c> espera al
        /// <c>SaveManager</c>, que a su vez espera a la cola— y se salta las esperas
        /// de reloj: acá no hay bucle de Unity que las haga avanzar, y lo que esta
        /// prueba mira es el ORDEN de los pasos, no cuánto tardan. El tope de vueltas
        /// está para que un bucle que no termine falle en vez de colgar el editor.
        ///
        /// <b>Ojo con <c>WaitForSecondsRealtime</c></b>, que hay que apartar a mano:
        /// hereda de <c>CustomYieldInstruction</c>, que <i>es</i> un IEnumerator, así
        /// que entra por la misma puerta que una corrutina anidada. Tratado como tal,
        /// esto lo giraba en vacío los 0,4 s reales que dura —cientos de miles de
        /// vueltas— y la salida se cortaba por el tope antes de llegar a cargar.
        /// </summary>
        private static void Bombear(IEnumerator rutina, Action porPaso = null, int tope = 10000)
        {
            var pila = new Stack<IEnumerator>();
            pila.Push(rutina);

            int vueltas = 0;
            while (pila.Count > 0)
            {
                if (++vueltas > tope)
                {
                    Debug.LogError("[PruebasPausa] La corrutina no terminó en " +
                                   $"{tope} pasos: se corta.");
                    return;
                }

                IEnumerator actual = pila.Peek();
                bool siguio = actual.MoveNext();
                porPaso?.Invoke();

                if (!siguio) { pila.Pop(); continue; }

                // El orden importa: una espera de reloj también es IEnumerator.
                if (actual.Current is CustomYieldInstruction) continue;
                if (actual.Current is IEnumerator anidada) pila.Push(anidada);
                // Lo demás (null, YieldInstruction…) se da por cumplido.
            }
        }

        /// <summary>Le pone una partida al ApiManager para que el SaveManager no corte
        /// antes de guardar: sin partida no hay dónde guardar y `Preparar` devuelve
        /// false. El setter es privado, así que se escribe el campo de respaldo.</summary>
        private static void DarPartida(int id)
        {
            var api = ApiManager.Instance;
            if (api == null) return;

            typeof(ApiManager)
                .GetField("<PartidaId>k__BackingField", Privado)
                ?.SetValue(api, (int?)id);
        }

        private static void QuitarPartida()
        {
            var api = ApiManager.Instance;
            if (api == null) return;

            typeof(ApiManager)
                .GetField("<PartidaId>k__BackingField", Privado)
                ?.SetValue(api, null);
        }

        /// <summary>
        /// Reusa el ApiManager singleton si ya existe, o crea uno en modo local, para
        /// que nada de esto le pegue a la red.
        ///
        /// Y lo deja además en <c>Instance</c>: en edición el <c>Awake</c> que lo
        /// pondría no corre, y sin <c>Instance</c> el <see cref="SaveManager"/> corta
        /// antes de guardar —«sin partida no hay dónde guardar»— y la prueba del
        /// orden de salida no vería ningún guardado que ordenar.
        /// </summary>
        private static void AsegurarApiLocal()
        {
            var api = ApiManager.Instance;
            if (api == null)
            {
                var go = new GameObject("ApiManagerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
                api = go.AddComponent<ApiManager>();
                PonerInstancia(typeof(ApiManager), api);
            }

            var so = new SerializedObject(api);
            so.FindProperty("useLocalMode").boolValue = true;
            so.FindProperty("verboseLogs").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Si el menú está preguntando «el guardado tarda, ¿esperas o te vas?»,
        /// contesta que se va.
        ///
        /// En el juego ese cartel sale cuando quedan cambios sin subir y espera a que
        /// alguien pulse un botón. Acá no hay nadie: sin esto la salida se queda
        /// esperando para siempre una respuesta que no va a llegar. Contestar «Salir
        /// ahora» es lo que haría un niño/a con prisa, y es el camino en el que el
        /// orden guardar-antes-de-cargar importa más.
        /// </summary>
        private static void ResponderSiPregunta(MenuPausa menu)
        {
            if (menu == null) return;

            foreach (Button boton in menu.GetComponentsInChildren<Button>(true))
            {
                if (!boton.interactable) continue;

                var etiqueta = boton.GetComponentInChildren<TMP_Text>(true);
                if (etiqueta == null || etiqueta.text != "Salir ahora") continue;

                boton.onClick.Invoke();
                return;
            }
        }

        private static void Comprobar(StringBuilder log, string que, bool paso, string detalle)
        {
            if (paso)
            {
                _ok++;
                log.AppendLine($"  OK    {que}" + (string.IsNullOrEmpty(detalle) ? "" : $"  [{detalle}]"));
            }
            else
            {
                _fallas.Add(que);
                log.AppendLine($"  FALLA {que}" + (string.IsNullOrEmpty(detalle) ? "" : $"  [{detalle}]"));
            }
        }
    }
}
#endif
