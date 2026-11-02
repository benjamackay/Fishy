#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
    /// Pruebas headless del camino de guardado: <see cref="ColaDeCambios"/> y el
    /// diálogo de «¿esperas o te vas?».
    ///
    /// <b>Las primeras que tiene este camino.</b> Hasta ahora no había ninguna, y no
    /// por olvido: un .asmdef de pruebas no puede referenciar Assembly-CSharp, que es
    /// donde viven <c>SaveManager</c> y <c>ColaDeCambios</c>. Lo que el proyecto hizo
    /// tres veces para resolverlo —extraer el núcleo a su propio ensamblado, como
    /// Fishy.Album, Fishy.Mision y Fishy.Pausa— sigue siendo el camino bueno, y sigue
    /// pendiente. Un arnés de Editor no lo reemplaza, pero sí alcanza a fijar lo que
    /// se acaba de arreglar, y eso no podía quedar sin prueba.
    ///
    /// Qué fija: que un cambio que no puede subir se da por perdido en vez de
    /// reintentarse en bucle, y que el cartel de «¿esperas o te vas?» se deja de
    /// mostrar cuando esperar ya no sirve de nada. Los dos síntomas que se vieron
    /// jugando fueron diez clics y cien segundos para acabar perdiendo lo mismo que se
    /// habría perdido al primero, y una consola llena de errores repetidos.
    ///
    /// No toca la red: los envíos son dobles que contestan lo que la prueba les diga.
    ///
    /// Se corren desde el menú  Fishy ▸ Probar guardado, o sin abrir el editor:
    ///
    ///   Unity -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" \
    ///         -executeMethod Fishy.EditorTools.FishyPruebasGuardado.Ejecutar \
    ///         -logFile -
    ///
    /// Termina con código 0 si todo pasa y 1 si algo falla.
    /// </summary>
    public static class FishyPruebasGuardado
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        private const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Fishy/Probar guardado")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DEL GUARDADO (headless)");
            log.AppendLine(new string('=', 70));

            AsegurarApi();

            // ── Qué se reintenta y qué no ──
            ProbarLoQueSubeSaleDeLaCola(log);
            ProbarUnRechazoDelServidorNoSeReintenta(log);
            ProbarUnFalloSinRespuestaSeReintentaYSeRinde(log);
            ProbarUn429SiSeReintenta(log);
            ProbarLoPerdidoNoCuentaComoPendiente(log);

            // ── El diálogo que volvía a salir para siempre ──
            ProbarNoSePreguntaDosVecesSiEsperarNoSirve(log);

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── Qué se reintenta y qué no ────────────────────────────────────────────

        /// <summary>El caso feliz, para que las demás pruebas signifiquen algo: si esta
        /// fallara, «no quedó nada en la cola» no diría nada sobre rendirse.</summary>
        private static void ProbarLoQueSubeSaleDeLaCola(StringBuilder log)
        {
            ColaDeCambios cola = Reiniciar();

            var envio = new EnvioFalso(exito: true);
            ColaDeCambios.EncolarSnapshot("prueba.sube", envio.Enviar, "cambio que sube");

            Bombear(cola.Vaciar("Prueba", 5f, reintentarSiFalla: true));

            ColaDeCambios.Resultado r = cola.UltimoResultado;
            Comprobar(log, "un cambio que sube se va de la cola y el vaciado dice que todo bien",
                envio.Intentos == 1 && ColaDeCambios.Pendientes == 0 &&
                r.Subidos == 1 && r.TodoBien,
                $"{envio.Intentos} intento(s), {r.Subidos} subido(s), quedan {ColaDeCambios.Pendientes}");
        }

        /// <summary>
        /// Un 400 no se arregla insistiendo: el servidor va a rechazar el mismo cuerpo
        /// las veces que haga falta. Antes se reintentaba igual que un corte de red,
        /// porque <c>onError</c> recibe el cuerpo de la respuesta y el código se tiraba.
        /// </summary>
        private static void ProbarUnRechazoDelServidorNoSeReintenta(StringBuilder log)
        {
            ColaDeCambios cola = Reiniciar();
            cola.maxIntentos = 4;

            var envio = new EnvioFalso(codigo: 400);
            ColaDeCambios.EncolarSnapshot("prueba.400", envio.Enviar, "cambio rechazado");

            Bombear(cola.Vaciar("Prueba", 5f, reintentarSiFalla: true));

            Comprobar(log, "un cambio que el servidor rechaza (400) se intenta una sola vez",
                envio.Intentos == 1 && ColaDeCambios.Pendientes == 0 &&
                cola.UltimoResultado.Rendidos == 1,
                $"{envio.Intentos} intento(s), quedan {ColaDeCambios.Pendientes}");
        }

        /// <summary>
        /// Lo que no llegó a tener respuesta sí se reintenta —es para eso que existe el
        /// reintento—, pero con un tope.
        ///
        /// El tope es el arreglo de verdad: este envío falla SINCRÓNICAMENTE, en la
        /// misma línea en que se le llama, así que no gasta ni un milisegundo del plazo.
        /// Sin tope de intentos, el plazo —que es quien debería mandar— no se enteraba, y
        /// la ronda se repetía tan rápido como el bucle de Unity diera vueltas.
        /// </summary>
        private static void ProbarUnFalloSinRespuestaSeReintentaYSeRinde(StringBuilder log)
        {
            ColaDeCambios cola = Reiniciar();
            cola.maxIntentos = 3;

            var envio = new EnvioFalso(codigo: 0);
            ColaDeCambios.EncolarSnapshot("prueba.sinrespuesta", envio.Enviar, "cambio sin respuesta");

            Bombear(cola.Vaciar("Prueba", 5f, reintentarSiFalla: true));

            Comprobar(log, "un fallo sin respuesta se reintenta hasta maxIntentos y ahí se deja",
                envio.Intentos == 3 && ColaDeCambios.Pendientes == 0 &&
                cola.UltimoResultado.Rendidos == 1,
                $"{envio.Intentos} intento(s) con maxIntentos=3, quedan {ColaDeCambios.Pendientes}");
        }

        /// <summary>429 y 408 son 4xx pero significan «vuelve a intentarlo», así que la
        /// regla de «los 4xx no se reintentan» tiene que dejarlos pasar.</summary>
        private static void ProbarUn429SiSeReintenta(StringBuilder log)
        {
            ColaDeCambios cola = Reiniciar();
            cola.maxIntentos = 3;

            var envio = new EnvioFalso(codigo: 429);
            ColaDeCambios.EncolarSnapshot("prueba.429", envio.Enviar, "cambio con 429");

            Bombear(cola.Vaciar("Prueba", 5f, reintentarSiFalla: true));

            Comprobar(log, "un 429 se reintenta en vez de darse por perdido al primer fallo",
                envio.Intentos == 3,
                $"{envio.Intentos} intento(s) con maxIntentos=3");
        }

        /// <summary>
        /// La cuenta que corta el diálogo repetido.
        ///
        /// Lo que se dio por perdido NO es «pendiente»: pendiente es lo que todavía
        /// podría subir si se espera. Preguntarle al jugador si quiere esperar por algo
        /// que ya se descartó es pedirle que espere a algo que no va a pasar. Pero sí
        /// tiene que apagar <c>TodoBien</c>, que es lo que decide si se escribe en el log
        /// qué se perdió y lo que hace que la pantalla no diga «listo, ya se guardó».
        /// </summary>
        private static void ProbarLoPerdidoNoCuentaComoPendiente(StringBuilder log)
        {
            ColaDeCambios cola = Reiniciar();
            cola.maxIntentos = 2;

            var envio = new EnvioFalso(codigo: 400);
            ColaDeCambios.EncolarSnapshot("prueba.perdido", envio.Enviar, "cambio perdido");

            Bombear(cola.Vaciar("Prueba", 5f, reintentarSiFalla: true));

            ColaDeCambios.Resultado r = cola.UltimoResultado;
            Comprobar(log, "lo perdido no cuenta como pendiente, pero sí apaga TodoBien",
                r.Rendidos == 1 && r.Pendientes == 0 && !r.TodoBien &&
                ColaDeCambios.AlgoSeQuedoSinGuardar,
                $"rendidos {r.Rendidos}, pendientes {r.Pendientes}, todoBien {r.TodoBien}");
        }

        // ── El diálogo ───────────────────────────────────────────────────────────

        /// <summary>
        /// El síntoma que se vio jugando: «al salir de la partida, a cada rato pide
        /// confirmación».
        ///
        /// El escenario es un servidor colgado: los envíos salen y no contestan nunca.
        /// Con <c>paralelismo = 1</c>, el primero se queda con el único hueco y los otros
        /// no llegan a intentarse, así que al vencer el plazo la cola sigue teniendo
        /// cambios y el cartel sale. Eso está bien y tiene que seguir saliendo.
        ///
        /// Lo que se arregló es lo que pasaba DESPUÉS: el bucle solo miraba si quedaban
        /// cambios, así que volvía a sacar el mismo cartel indefinidamente, y cada
        /// «Seguir esperando» costaba el plazo entero. Ahora, si una ronda no bajó la
        /// cola, se deja de preguntar: la respuesta del niño/a no puede cambiar nada que
        /// no haya cambiado ya.
        /// </summary>
        private static void ProbarNoSePreguntaDosVecesSiEsperarNoSirve(StringBuilder log)
        {
            ColaDeCambios cola = Reiniciar();

            SaveManager save = NuevoSaveManager();
            MenuPausa menu = NuevoMenu();
            menu.Abrir();

            // Tres envíos que no contestan nunca. Con un solo hueco, el primero lo
            // ocupa para siempre y los otros dos se quedan en la cola.
            var colgado = new EnvioFalso(colgado: true);
            for (int i = 0; i < 3; i++)
                ColaDeCambios.EncolarSnapshot($"prueba.colgado.{i}", colgado.Enviar,
                                              $"cambio colgado {i}");

            int preguntas = 0;
            try
            {
                Bombear(((ISalidaAlMenu)menu).GuardarTodo(),
                    porPaso: () => { if (PulsarSeguirEsperando(menu)) preguntas++; });
            }
            finally
            {
                Soltar(menu, save);
            }

            // Preguntar una vez es correcto: puede que solo falte esperar un poco.
            // Preguntar una segunda vez, después de una ronda que no subió nada, es el
            // bucle que hacía dar diez clics para nada.
            Comprobar(log, "si esperar no bajó la cola, no se vuelve a preguntar lo mismo",
                preguntas == 1,
                $"{preguntas} pregunta(s) con MaxPreguntas=2");
        }

        // ── Andamios ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Un envío de mentira: cuenta los intentos y contesta lo que se le pida.
        ///
        /// Contestar el error en la misma llamada no es un atajo de la prueba: es lo que
        /// hace <c>ApiManager</c> de verdad cuando el envío no llega a salir —falta un id,
        /// no hay token—, y es justo el caso que no gasta plazo.
        /// </summary>
        private class EnvioFalso
        {
            public int Intentos;

            private readonly int _codigo;
            private readonly bool _exito;
            private readonly bool _colgado;

            public EnvioFalso(int codigo = 0, bool exito = false, bool colgado = false)
            {
                _codigo = codigo;
                _exito = exito;
                _colgado = colgado;
            }

            public void Enviar(Action ok, Action<string> error)
            {
                Intentos++;

                if (_colgado) return;           // ni ok ni error: se queda en el aire
                if (_exito) { ok(); return; }

                PonerCodigoDeError(_codigo);
                error($"fallo de prueba (código {_codigo})");
            }
        }

        /// <summary>Deja la cola vacía y con una instancia viva, y devuelve esa
        /// instancia. El almacén es estático: sin esto cada prueba vería lo que dejó la
        /// anterior.</summary>
        private static ColaDeCambios Reiniciar()
        {
            EstadoDePausa.Restablecer();
            Time.timeScale = 1f;

            ColaDeCambios.AlmacenParaPruebas.Limpiar();

            ColaDeCambios cola = ColaDeCambios.Instance;
            if (cola == null)
            {
                var go = new GameObject("ColaDePrueba") { hideFlags = HideFlags.HideAndDontSave };
                cola = go.AddComponent<ColaDeCambios>();
                PonerInstancia(typeof(ColaDeCambios), cola);
            }

            cola.verboseLogs = false;
            cola.paralelismo = 1;
            cola.maxIntentos = 4;
            return cola;
        }

        private static SaveManager NuevoSaveManager()
        {
            var go = new GameObject("SaveManagerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var save = go.AddComponent<SaveManager>();
            save.verboseLogs = false;
            save.esperaMinima = 0f;

            // Corto a propósito: la prueba mira cuántas veces se pregunta, no cuánto se
            // espera, y cada ronda cuesta este plazo en tiempo real de verdad.
            save.topeDeCierre = 0.3f;

            PonerInstancia(typeof(SaveManager), save);
            return save;
        }

        private static MenuPausa NuevoMenu()
        {
            var go = new GameObject("MenuPausaDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var menu = go.AddComponent<MenuPausa>();

            // En edición no hay Otto en la escena, que es como el menú distingue el
            // juego de un menú. Lo que se prueba acá no depende de eso.
            menu.soloDuranteElJuego = false;

            PonerInstancia(typeof(MenuPausa), menu);
            return menu;
        }

        private static void Soltar(MenuPausa menu, SaveManager save)
        {
            EstadoDePausa.Restablecer();
            Time.timeScale = 1f;

            QuitarInstancia(typeof(MenuPausa));
            QuitarInstancia(typeof(SaveManager));

            if (menu != null) UnityEngine.Object.DestroyImmediate(menu.gameObject);
            if (save != null) UnityEngine.Object.DestroyImmediate(save.gameObject);
        }

        /// <summary>
        /// Si el menú está preguntando «¿esperas o te vas?», pulsa «Seguir esperando» y
        /// dice que sí.
        ///
        /// Vale como detector de «se preguntó» porque el botón se apaga en cuanto se lo
        /// pulsa (<c>Congelar</c>): mientras el cartel sigue a la vista ya no está
        /// pulsable, así que no se cuenta dos veces el mismo cartel.
        /// </summary>
        private static bool PulsarSeguirEsperando(MenuPausa menu)
        {
            if (menu == null) return false;

            foreach (Button boton in menu.GetComponentsInChildren<Button>(true))
            {
                if (!boton.interactable) continue;

                var etiqueta = boton.GetComponentInChildren<TMP_Text>(true);
                if (etiqueta == null || etiqueta.text != "Seguir esperando") continue;

                boton.onClick.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Recorre una corrutina a mano, en modo edición, donde no hay quien la corra.
        ///
        /// Ojo con <c>WaitForSecondsRealtime</c>: hereda de
        /// <c>CustomYieldInstruction</c>, que <i>es</i> un IEnumerator, así que entra por
        /// la misma puerta que una corrutina anidada. Tratado como tal se giraría en
        /// vacío el rato entero que dura. Se aparta antes de preguntar por el anidado.
        /// </summary>
        private static void Bombear(IEnumerator rutina, Action porPaso = null, int tope = 200000)
        {
            var pila = new Stack<IEnumerator>();
            pila.Push(rutina);

            int vueltas = 0;
            while (pila.Count > 0)
            {
                if (++vueltas > tope)
                {
                    Debug.LogError($"[PruebasGuardado] La corrutina no terminó en {tope} " +
                                   "pasos: se corta.");
                    return;
                }

                IEnumerator actual = pila.Peek();
                bool siguio = actual.MoveNext();
                porPaso?.Invoke();

                if (!siguio) { pila.Pop(); continue; }

                if (actual.Current is CustomYieldInstruction) continue;
                if (actual.Current is IEnumerator anidada) pila.Push(anidada);
            }
        }

        /// <summary>
        /// Escribe el código HTTP del error, como lo haría <c>ApiManager</c> en la línea
        /// de antes de llamar al <c>onError</c>. El setter es privado, así que se escribe
        /// el campo de respaldo: el mismo recurso que usan los otros arneses para los
        /// singletons.
        /// </summary>
        private static void PonerCodigoDeError(int codigo)
        {
            var api = ApiManager.Instance;
            if (api == null) return;

            typeof(ApiManager)
                .GetField("<CodigoDelUltimoError>k__BackingField", Privado)
                ?.SetValue(api, codigo);
        }

        /// <summary>
        /// Un ApiManager en modo local y con partida.
        ///
        /// Las dos cosas hacen falta antes de encolar: sin <c>Instance</c>,
        /// <c>Vaciar</c> se va por la compuerta de «no hay quien suba»; y sin
        /// <c>PartidaId</c>, el sello de partida descarta cada cambio antes de
        /// intentarlo y ninguna de estas pruebas llegaría a ejercitar un envío.
        /// </summary>
        private static void AsegurarApi()
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

            typeof(ApiManager)
                .GetField("<PartidaId>k__BackingField", Privado)
                ?.SetValue(api, (int?)31337);
        }

        /// <summary>Deja el singleton apuntando a este objeto sin pasar por su
        /// <c>Awake</c>, que llama a <c>DontDestroyOnLoad</c> y fuera del modo Play
        /// tira InvalidOperationException.</summary>
        private static void PonerInstancia(Type tipo, MonoBehaviour quien)
        {
            tipo.GetField("<Instance>k__BackingField",
                     BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, quien);
        }

        private static void QuitarInstancia(Type tipo)
        {
            tipo.GetField("<Instance>k__BackingField",
                     BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, null);
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
