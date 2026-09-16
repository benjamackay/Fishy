using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fishy.Net
{
    /// <summary>
    /// Los cambios de la partida esperan aqui hasta que toca subirlos.
    ///
    /// Antes cada sincronizador mandaba su peticion en cuanto pasaba algo: un POST por
    /// objeto recogido, otro por NPC terminado, uno por cada linea de chat. Contra
    /// Supabase cada peticion cuesta 600-800 ms, asi que el juego se pasaba la partida
    /// goteando trafico. Ahora se acumulan aqui y salen todas juntas en los momentos que
    /// decide <c>SaveManager</c>: al cambiar de zona y al cerrar el juego.
    ///
    /// <b>La cola vive solo en memoria y no se persiste.</b> Es una decision tomada: un
    /// cierre sucio (crash, bateria, matar el proceso) pierde lo acumulado desde el
    /// ultimo vaciado, o sea lo de la zona actual. El cierre normal no pierde nada,
    /// porque <c>SaveManager</c> retiene el cierre hasta que esto termina.
    ///
    /// Se crea sola y sobrevive entre escenas.
    /// </summary>
    [DisallowMultipleComponent]
    public class ColaDeCambios : MonoBehaviour
    {
        /// <summary>
        /// Como se comporta un cambio cuando ya hay otro con su misma clave, y como se
        /// manda. Cada familia necesita trato distinto y mezclarlas pierde datos.
        /// </summary>
        public enum Familia
        {
            /// <summary>
            /// Solo importa el ultimo estado: posicion, mochila, progreso. No guardan
            /// datos — su <c>Enviar</c> lee el estado vivo al ejecutarse—, asi que
            /// encolar uno es marcar una clave como sucia y siempre sube lo de ahora.
            /// </summary>
            Snapshot,

            /// <summary>
            /// Un hecho que ocurrio, idempotente por su clave: este objeto se recogio,
            /// esta mision se completo. Reencolar pisa con el valor nuevo.
            /// </summary>
            Append,

            /// <summary>
            /// Varias peticiones encadenadas que dependen unas de otras (el chat: el id
            /// de la conversacion no existe hasta que contesta la primera). Se mandan de
            /// una en una y esperando, nunca en paralelo con nada.
            /// </summary>
            Cadena,
        }

        /// <summary>Un cambio esperando su turno.</summary>
        public class CambioPendiente
        {
            public string Clave;
            public Familia Familia;
            public string Descripcion;

            /// <summary>Partida a la que pertenece, para no subirla a la de otro menor.</summary>
            public int? Partida;

            /// <summary>Orden de PRIMERA aparicion de la clave. Reencolar no lo cambia.</summary>
            public int Orden;

            /// <summary>Veces que ya se intento y fallo.</summary>
            public int Intentos;

            /// <summary>La llamada, ya lista. Recibe (cuandoSalgaBien, cuandoFalle).</summary>
            public Action<Action, Action<string>> Enviar;

            /// <summary>
            /// Para los que fusionan (zona, progreso): el valor acumulado, y como
            /// construir el envio a partir de el. Se rehace al vaciar para que salga el
            /// valor ya fusionado y no el primero que llego.
            /// </summary>
            public object Valor;
            public Func<object, Action<Action, Action<string>>> HacerEnviar;

            public Action<Action, Action<string>> Resolver()
                => HacerEnviar != null ? HacerEnviar(Valor) : Enviar;
        }

        /// <summary>
        /// El almacen con las reglas de coalescencia. Es C# plano a proposito, sin nada
        /// de UnityEngine, para que las pruebas headless lo ejerciten sin escena.
        /// </summary>
        public class Almacen
        {
            private readonly Dictionary<string, CambioPendiente> _porClave =
                new Dictionary<string, CambioPendiente>();
            private readonly List<string> _orden = new List<string>();
            private int _secuencia;

            public int Cuenta => _porClave.Count;

            /// <summary>
            /// Mete o actualiza un cambio. Reencolar una clave reemplaza el contenido
            /// pero CONSERVA su posicion original: si no, un snapshot que se repite
            /// mucho se iria colando por delante de hechos mas viejos.
            /// </summary>
            public void Poner(CambioPendiente cambio, Func<object, object, object> fusion = null)
            {
                if (_porClave.TryGetValue(cambio.Clave, out var previo))
                {
                    cambio.Orden = previo.Orden;
                    cambio.Intentos = previo.Intentos;
                    if (fusion != null) cambio.Valor = fusion(previo.Valor, cambio.Valor);
                }
                else
                {
                    cambio.Orden = _secuencia++;
                    _orden.Add(cambio.Clave);
                }
                _porClave[cambio.Clave] = cambio;
            }

            public bool Quitar(string clave)
            {
                if (!_porClave.Remove(clave)) return false;
                _orden.Remove(clave);
                return true;
            }

            /// <summary>Copia en orden de llegada. Se itera sobre esto y no sobre el
            /// diccionario, porque el vaciado quita y reencola mientras recorre.</summary>
            public List<CambioPendiente> Instantanea()
            {
                var lista = new List<CambioPendiente>(_porClave.Count);
                foreach (string clave in _orden)
                    if (_porClave.TryGetValue(clave, out var c)) lista.Add(c);
                lista.Sort((a, b) => a.Orden.CompareTo(b.Orden));
                return lista;
            }

            public List<string> Claves() => new List<string>(_orden);

            public void Limpiar()
            {
                _porClave.Clear();
                _orden.Clear();
            }
        }

        /// <summary>Como termino un vaciado.</summary>
        public struct Resultado
        {
            public int Subidos;

            /// <summary>Se intentaron y el servidor dijo que no.</summary>
            public int Fallidos;

            /// <summary>
            /// Salieron y el plazo vencio sin que contestaran, ni bien ni mal. Se dan por
            /// perdidos y no se reencolan: puede que hayan llegado, y repetirlos duplicaria
            /// el cambio. Estan aparte de <see cref="Fallidos"/> porque no son lo mismo —
            /// de un fallo se sabe que no entro; de esto no se sabe nada.
            /// </summary>
            public int SinRespuesta;

            /// <summary>Se quedaron en la cola sin llegar a salir.</summary>
            public int SinIntentar;

            public float Segundos;

            public bool TodoBien => Fallidos == 0 && SinRespuesta == 0 && SinIntentar == 0;
            public int Pendientes => Fallidos + SinRespuesta + SinIntentar;
        }

        // ── Configuracion ─────────────────────────────────────────────────────

        [Header("Configuración")]
        [Tooltip("Cuántas peticiones se mandan a la vez al vaciar. Las cadenas (el chat) " +
                 "siempre van de una en una, sin importar este valor.")]
        [Min(1)] public int paralelismo = 4;

        [Tooltip("Veces que se reintenta un cambio que falló, cuando el vaciado admite " +
                 "reintento (el del cambio de zona). El del cierre no reintenta: no hay tiempo.")]
        [Min(0)] public int maxIntentos = 3;

        [Tooltip("Avisar por consola si la cola crece por encima de esto sin vaciarse. " +
                 "Señal de que algún vaciado dejó de ocurrir.")]
        [Min(0)] public int avisarPorEncimaDe = 200;

        [Tooltip("Escribir en consola cada cambio que entra y cada vaciado.")]
        public bool verboseLogs = true;

        /// <summary>
        /// Margen sobre el plazo antes de dar por perdida una peticion. `req.timeout` es
        /// entero en segundos, asi que al acotarlo se redondea; sin este margen una
        /// peticion que iba a fallar justo en el limite se contaria como "sin intentar".
        /// </summary>
        private const float Gracia = 0.5f;

        // ── Estado ────────────────────────────────────────────────────────────

        public static ColaDeCambios Instance { get; private set; }

        private static readonly Almacen _almacen = new Almacen();

        private bool _vaciando;
        private int _enVueloCola;
        private int _subidos;
        private int _fallidos;
        private int _intentados;
        private int _descartados;

        /// <summary>Claves que salieron y todavia no contestaron. Solo sirve para poder
        /// nombrarlas si el plazo vence con alguna en vuelo.</summary>
        private readonly List<string> _enVueloClaves = new List<string>();

        /// <summary>Cuantos cambios esperan ahora mismo.</summary>
        public static int Pendientes => _almacen.Cuenta;

        /// <summary>True mientras se esta subiendo.</summary>
        public bool Vaciando => _vaciando;

        /// <summary>Como fue el ultimo vaciado.</summary>
        public Resultado UltimoResultado { get; private set; }

        /// <summary>El almacen, para las pruebas. En juego no hace falta tocarlo.</summary>
        public static Almacen AlmacenParaPruebas => _almacen;

        // ── Ciclo de vida ─────────────────────────────────────────────────────

        // El estatico sobrevive al recargado de dominio del editor: sin esto, los
        // cambios de una corrida se arrastrarian a la siguiente.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void LimpiarEstadoEstatico()
        {
            _almacen.Limpiar();
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear() => GetOrCreate();

        public static ColaDeCambios GetOrCreate()
        {
            if (Instance != null) return Instance;

            var encontrado = FindAnyObjectByType<ColaDeCambios>();
            if (encontrado != null) return encontrado;

            return new GameObject("ColaDeCambios").AddComponent<ColaDeCambios>();
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

        // ── Encolar ───────────────────────────────────────────────────────────

        /// <summary>
        /// Marca un snapshot como sucio. <paramref name="enviar"/> tiene que LEER EL
        /// ESTADO VIVO cuando se ejecute, no capturar los datos ahora: es lo que hace
        /// que siempre suba lo ultimo y que repetir la llamada salga gratis.
        /// </summary>
        public static void EncolarSnapshot(string clave, Action<Action, Action<string>> enviar,
            string descripcion = null)
            => Meter(clave, Familia.Snapshot, enviar, descripcion);

        /// <summary>Un hecho idempotente por clave. Reencolar pisa con lo nuevo.</summary>
        public static void EncolarAppend(string clave, Action<Action, Action<string>> enviar,
            string descripcion = null)
            => Meter(clave, Familia.Append, enviar, descripcion);

        /// <summary>Una secuencia que se manda entera y sola (hoy: una conversacion).</summary>
        public static void EncolarCadena(string clave, Action<Action, Action<string>> enviar,
            string descripcion = null)
            => Meter(clave, Familia.Cadena, enviar, descripcion);

        /// <summary>
        /// Progreso de una zona. <b>Fusiona por OR, no pisa.</b>
        ///
        /// Hay dos escritores con significados opuestos: MisionBackendSync manda
        /// `completada: false` al desbloquearla, y BosqueDesconocidosManager manda
        /// `true` al terminarla. Con "gana el ultimo", un desbloqueo que llegara despues
        /// de un completado degradaria la zona en el reporte del adulto. Completar es un
        /// camino de ida.
        /// </summary>
        public static void EncolarZona(string slug, bool completada)
        {
            if (string.IsNullOrEmpty(slug)) return;

            Meter($"zona:{slug}", Familia.Append, null, $"zona {slug}",
                valor: completada,
                fusion: (viejo, nuevo) => (viejo is bool v && v) || (nuevo is bool n && n),
                hacerEnviar: v => (ok, error) =>
                {
                    var api = ApiManager.Instance;
                    if (api == null) { error("No hay ApiManager."); return; }
                    api.RegistrarProgresoZona(slug, (bool)v,
                        onSuccess: _ => ok(), onError: error);
                });
        }

        /// <summary>
        /// Progreso de la partida (0-100). <b>Fusiona por maximo, no pisa.</b>
        ///
        /// Cada zona manda un valor absoluto al completarse. Si dos se cerraran entre
        /// dos vaciados y la segunda tuviera un valor menor, el progreso retrocederia.
        /// </summary>
        public static void EncolarProgreso(float progreso)
        {
            Meter("partida.progreso", Familia.Snapshot, null, $"progreso {progreso:F0}%",
                valor: progreso,
                fusion: (viejo, nuevo) => Mathf.Max(
                    viejo is float a ? a : float.MinValue,
                    nuevo is float b ? b : float.MinValue),
                hacerEnviar: v => (ok, error) =>
                {
                    var api = ApiManager.Instance;
                    if (api == null) { error("No hay ApiManager."); return; }
                    api.ActualizarPartida(progreso: (float)v,
                        onSuccess: _ => ok(), onError: error);
                });
        }

        /// <summary>Saca un cambio de la cola sin mandarlo. Para deshacer un encolado.</summary>
        public static void Olvidar(string clave) => _almacen.Quitar(clave);

        private static void Meter(string clave, Familia familia,
            Action<Action, Action<string>> enviar, string descripcion,
            object valor = null,
            Func<object, object, object> fusion = null,
            Func<object, Action<Action, Action<string>>> hacerEnviar = null)
        {
            if (string.IsNullOrEmpty(clave)) return;

            var cambio = new CambioPendiente
            {
                Clave = clave,
                Familia = familia,
                Descripcion = string.IsNullOrEmpty(descripcion) ? clave : descripcion,
                // El sello de partida es lo que impide que el avance de un hermano se le
                // aparezca al otro si se cambia de perfil con la cola a medias.
                Partida = ApiManager.Instance != null ? ApiManager.Instance.PartidaId : null,
                Enviar = enviar,
                Valor = valor,
                HacerEnviar = hacerEnviar,
            };

            _almacen.Poner(cambio, fusion);

            var cola = Instance;
            if (cola == null) return;

            if (cola.verboseLogs)
                Debug.Log($"[Cola] +{clave} ({familia}) — {_almacen.Cuenta} pendientes.");

            if (cola.avisarPorEncimaDe > 0 && _almacen.Cuenta == cola.avisarPorEncimaDe)
                Debug.LogWarning($"[Cola] Llevo {_almacen.Cuenta} cambios sin vaciar. " +
                                 "¿Dejó de ocurrir algún vaciado?");
        }

        // ── Vaciar ────────────────────────────────────────────────────────────

        /// <summary>
        /// Sube todo lo pendiente, con un plazo.
        ///
        /// <paramref name="reintentarSiFalla"/> distingue los dos momentos: al cambiar de
        /// zona un fallo se devuelve a la cola y habra otra oportunidad, que es lo que
        /// conserva el reintento que antes hacian por su cuenta ObjetosRecogidosSync y
        /// NpcTematicaSync. Al cerrar no: no habra otra vez, y lo que no salga se pierde.
        /// </summary>
        public IEnumerator Vaciar(string motivo, float topeSegundos, bool reintentarSiFalla)
        {
            // Reentrante: un segundo llamador espera al que ya corre en vez de arrancar
            // otro. Dos vaciados a la vez se pisarian el tope de tiempo y podrian mandar
            // la misma entrada dos veces.
            if (_vaciando)
            {
                while (_vaciando) yield return null;
                yield break;
            }

            var api = ApiManager.Instance;
            if (api == null)
            {
                UltimoResultado = new Resultado { SinIntentar = _almacen.Cuenta };
                yield break;
            }

            if (_almacen.Cuenta == 0)
            {
                UltimoResultado = new Resultado();
                yield break;
            }

            _vaciando = true;
            _enVueloCola = 0;
            _enVueloClaves.Clear();
            _subidos = 0;
            _fallidos = 0;
            _intentados = 0;
            _descartados = 0;

            // Todo el tiempo va en realtime: MenuPausa pone Time.timeScale = 0 al
            // abrirse, y un WaitForSeconds aqui dejaria el juego colgado para siempre.
            float arranque = Time.realtimeSinceStartup;
            float limite = arranque + Mathf.Max(0f, topeSegundos);

            int alEmpezar = _almacen.Cuenta;
            if (verboseLogs)
                Debug.Log($"[Cola] Vaciando por {motivo}: {alEmpezar} cambios.");

            try
            {
                foreach (var cambio in _almacen.Instantanea())
                {
                    if (Time.realtimeSinceStartup >= limite) break;

                    // El sello: si la cola sobrevivio a un cambio de perfil, esto es lo
                    // unico que evita escribirle el avance de un hermano al otro.
                    if (cambio.Partida.HasValue && cambio.Partida != api.PartidaId)
                    {
                        _almacen.Quitar(cambio.Clave);
                        _descartados++;
                        Debug.LogWarning(
                            $"[Cola] Descartado '{cambio.Descripcion}' de la partida " +
                            $"{cambio.Partida}: ahora se juega la {api.PartidaId}.");
                        continue;
                    }

                    // Una cadena no puede solaparse con nada: el chat usa NpcId y ChatId
                    // de ApiManager, que son estado global y se pisarian.
                    bool esCadena = cambio.Familia == Familia.Cadena;
                    int hueco = esCadena ? 1 : Mathf.Max(1, paralelismo);

                    while (_enVueloCola >= hueco && Time.realtimeSinceStartup < limite)
                        yield return null;
                    if (Time.realtimeSinceStartup >= limite) break;

                    api.TopeDeTiempoParaPeticiones = SegundosHasta(limite);

                    _almacen.Quitar(cambio.Clave);
                    _enVueloCola++;
                    _enVueloClaves.Add(cambio.Clave);
                    _intentados++;

                    var c = cambio;
                    bool reintentar = reintentarSiFalla;
                    c.Resolver()(
                        () =>
                        {
                            _enVueloCola--;
                            _enVueloClaves.Remove(c.Clave);
                            _subidos++;
                        },
                        error =>
                        {
                            _enVueloCola--;
                            _enVueloClaves.Remove(c.Clave);
                            _fallidos++;
                            if (!reintentar || c.Intentos + 1 >= maxIntentos)
                            {
                                if (reintentar)
                                    Debug.LogWarning($"[Cola] '{c.Descripcion}' falló " +
                                                     $"{c.Intentos + 1} veces, se abandona: {error}");
                                return;
                            }
                            c.Intentos++;
                            _almacen.Poner(c);
                        });

                    // La cadena se espera entera antes de seguir.
                    if (esCadena)
                        while (_enVueloCola > 0 && Time.realtimeSinceStartup < limite)
                            yield return null;
                }

                // Que no quede nada en el aire antes de decir que terminamos. Se mira
                // tambien el contador de ApiManager porque una operacion puede haber
                // llamado a su callback con otra peticion encadenada todavia viva.
                float conGracia = limite + Gracia;
                while ((_enVueloCola > 0 || api.PeticionesEnVuelo > 0) &&
                       Time.realtimeSinceStartup < conGracia)
                    yield return null;
            }
            finally
            {
                // Si esto no se restaura, el resto de la sesion se queda con timeouts
                // cortos y las peticiones normales empiezan a fallar sin motivo.
                api.TopeDeTiempoParaPeticiones = null;
                _vaciando = false;
            }

            UltimoResultado = new Resultado
            {
                Subidos = _subidos,
                Fallidos = _fallidos,

                // Los que seguian en vuelo al vencer el plazo. Sin esta cuenta, un envio
                // que no llama nunca a su callback no aparecia en ningun lado: el
                // resultado decia "todo bien" y el cierre daba por guardado un cambio
                // que se acababa de perder.
                SinRespuesta = Mathf.Max(0, _enVueloCola),

                // De los que habia al empezar, los que ni salieron. No se mira el tamano
                // del almacen: los fallidos que se reencolan para la proxima estan ahi
                // dentro y se contarian dos veces, una como fallo y otra como no intento.
                SinIntentar = Mathf.Max(0, alEmpezar - _intentados - _descartados),

                Segundos = Time.realtimeSinceStartup - arranque,
            };

            Informar(motivo, UltimoResultado);
        }

        /// <summary>Segundos que quedan, redondeados hacia arriba y nunca menos de 1:
        /// `req.timeout` es entero y un 0 significaria "sin limite".</summary>
        private static int SegundosHasta(float limite)
            => Mathf.Max(1, Mathf.CeilToInt(limite - Time.realtimeSinceStartup));

        private void Informar(string motivo, Resultado r)
        {
            if (r.TodoBien)
            {
                if (verboseLogs)
                    Debug.Log($"[Cola] Vaciada en {r.Segundos:F1} s: {r.Subidos} subidos.");
                return;
            }

            // LogError y no Warning a proposito: en un build esto es lo unico que queda
            // en Player.log, y es la diferencia entre "no se guardó y nadie sabe por qué"
            // y poder decir exactamente qué se perdió.
            var claves = _almacen.Claves();
            foreach (string enVuelo in _enVueloClaves)
                if (!claves.Contains(enVuelo)) claves.Add(enVuelo);

            Debug.LogError(
                $"[Cola] Vaciada por {motivo} en {r.Segundos:F1} s con cambios sin guardar: " +
                $"{r.Subidos} subidos, {r.Fallidos} fallidos, " +
                $"{r.SinRespuesta} sin respuesta, {r.SinIntentar} sin intentar." +
                (claves.Count > 0 ? $"\nQuedan: {string.Join(", ", claves)}" : ""));
        }
    }
}
