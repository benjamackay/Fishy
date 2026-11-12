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

            /// <summary>Cómo se combinan dos valores de esta clave (viejo, nuevo). Null =
            /// gana el nuevo. Se guarda aquí para poder fusionar también cuando un envío
            /// fallido vuelve a la cola y ya hay uno más nuevo esperando.</summary>
            public Func<object, object, object> Fusion;

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

            /// <summary>
            /// Saca de la cola lo que HAY AHORA bajo esa clave, o null si ya no está.
            ///
            /// El vaciado recorre una copia y puede esperar entre un cambio y otro; en
            /// ese rato el juego puede haber reencolado la misma clave con datos más
            /// nuevos. Mandar la copia vieja y quitar la nueva perdía el dato nuevo.
            /// </summary>
            public CambioPendiente Tomar(string clave)
            {
                if (!_porClave.TryGetValue(clave, out var actual)) return null;
                Quitar(clave);
                return actual;
            }

            /// <summary>
            /// Devuelve a la cola un cambio que falló. Si mientras tanto entró uno más
            /// nuevo con la misma clave, el nuevo manda: no se le pisa con el viejo. Si la
            /// clave fusiona (zona por OR, progreso por máximo) se combinan los dos, para
            /// que un "desbloqueada" viejo que falló no borre un "completada" más nuevo.
            /// </summary>
            public void Reencolar(CambioPendiente fallido)
            {
                if (_porClave.TryGetValue(fallido.Clave, out var nuevo))
                {
                    nuevo.Intentos = Math.Max(nuevo.Intentos, fallido.Intentos);
                    if (fallido.Fusion != null)
                        nuevo.Valor = fallido.Fusion(fallido.Valor, nuevo.Valor);
                    return;
                }
                Poner(fallido);
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

            /// <summary>
            /// Se dieron por perdidos y SE SACARON de la cola: el servidor los rechazo
            /// con un codigo que no cambia por insistir (400, 404...), o agotaron
            /// <see cref="maxIntentos"/>.
            ///
            /// No cuentan en <see cref="Pendientes"/> a proposito: pendiente es lo que
            /// todavia podria subir si se espera, y esto no. Preguntarle al jugador si
            /// quiere esperar por uno de estos es pedirle que espere a algo que no va a
            /// pasar. Si cuentan en <see cref="TodoBien"/>, que es lo que decide si se
            /// escribe el informe de lo que se perdio.
            /// </summary>
            public int Rendidos;

            public float Segundos;

            /// <summary>
            /// No queda nada por subir.
            ///
            /// <b>Los fallos no cuentan.</b> Con la insistencia, un cambio que falla
            /// vuelve a la cola y se reintenta dentro del mismo vaciado: si al final
            /// entro, que haya fallado tres veces en el camino no es un problema, y si no
            /// entro ya esta contado en <see cref="SinIntentar"/>. Sumar las dos cosas
            /// contaria el mismo cambio dos veces.
            /// </summary>
            public bool TodoBien => SinRespuesta == 0 && SinIntentar == 0 && Rendidos == 0;

            public int Pendientes => SinRespuesta + SinIntentar;
        }

        // ── Configuracion ─────────────────────────────────────────────────────

        [Header("Configuración")]
        [Tooltip("Cuántas peticiones se mandan a la vez al vaciar. Las cadenas (el chat) " +
                 "siempre van de una en una, sin importar este valor.\n\n" +
                 "En 1 porque el servidor de pruebas atiende una petición a la vez: con " +
                 "más, las que mandamos juntas esperan en su fila y vencen en grupo. Subirlo " +
                 "solo sirve con un servidor que atienda varias a la vez.")]
        [Min(1)] public int paralelismo = 1;

        [Tooltip("Avisar por consola si la cola crece por encima de esto sin vaciarse. " +
                 "Señal de que algún vaciado dejó de ocurrir.")]
        [Min(0)] public int avisarPorEncimaDe = 200;

        [Tooltip("Veces que se intenta un mismo cambio antes de darlo por perdido.\n\n" +
                 "El plazo manda CUÁNTO RATO se insiste, pero no puede mandar CUÁNTAS " +
                 "veces: un envío que falla sin llegar a salir —falta un id, no hay " +
                 "token— llama a su callback de error en la misma línea, sin gastar " +
                 "plazo, así que sin este tope se reintenta en bucle hasta agotarlo.")]
        [Min(1)] public int maxIntentos = 4;

        [Tooltip("Escribir en consola cada cambio que entra y cada vaciado.")]
        public bool verboseLogs = true;

        /// <summary>
        /// Margen sobre el plazo antes de dar por perdida una peticion. `req.timeout` es
        /// entero en segundos, asi que al acotarlo se redondea; sin este margen una
        /// peticion que iba a fallar justo en el limite se contaria como "sin intentar".
        /// </summary>
        private const float Gracia = 0.5f;

        /// <summary>
        /// Lo que se espera antes de repetir una ronda que no consiguio subir nada.
        /// Contra un servidor que no contesta, insistir sin pausa solo quema frames.
        /// </summary>
        private const float EsperaEntreRondas = 0.5f;

        /// <summary>
        /// Plazo que tiene que quedar para que valga la pena mandar otra peticion.
        ///
        /// Sin esto, con tres decimas de plazo restante la cola todavia mandaba un cambio
        /// —`req.timeout` es entero, asi que le tocaba 1 s— y abandonaba la espera en el
        /// frame siguiente, al vencer el plazo. El cambio salia de verdad, probablemente
        /// llegaba, y se contaba como "sin respuesta": al jugador se le preguntaba si
        /// queria esperar por algo que ya estaba guardado.
        /// </summary>
        private const float MargenParaMandar = 1f;

        // ── Estado ────────────────────────────────────────────────────────────

        public static ColaDeCambios Instance { get; private set; }

        private static readonly Almacen _almacen = new Almacen();

        private bool _vaciando;

        /// <summary>
        /// Hasta cuándo puede seguir el vaciado en curso. Es un campo y no una variable
        /// local porque el último que pide guardar lo fija (ver <see cref="Vaciar"/>).
        /// </summary>
        private float _limite;
        private int _enVueloCola;
        private int _subidos;
        private int _fallidos;
        private int _intentados;
        private int _descartados;

        /// <summary>Los que se dieron por perdidos en este vaciado, y por que. Se nombran
        /// en el informe: "no se guardo todo" sin decir que es lo que no se guardo no
        /// sirve para arreglar nada.</summary>
        private readonly List<string> _rendidos = new List<string>();

        /// <summary>El ultimo error de red del vaciado, para poder decir por que no
        /// termino en vez de solo cuantos quedan.</summary>
        private string _ultimoError;

        /// <summary>Por que no se pudo terminar el ultimo vaciado, o cadena vacia.</summary>
        public static string UltimoError => Instance != null ? Instance._ultimoError : null;

        /// <summary>Claves que salieron y todavia no contestaron. Solo sirve para poder
        /// nombrarlas si el plazo vence con alguna en vuelo.</summary>
        private readonly List<string> _enVueloClaves = new List<string>();

        /// <summary>Cuantos cambios esperan ahora mismo.</summary>
        public static int Pendientes => _almacen.Cuenta;

        /// <summary>
        /// El ultimo vaciado dejo algo sin guardar, contando tambien lo que se dio por
        /// perdido y por eso ya NO esta en la cola.
        ///
        /// Hace falta porque <see cref="Pendientes"/> solo ve el almacen: tras rendirse
        /// con un cambio, la cola queda en cero y mirar solo eso haria que la pantalla
        /// dijera "listo, ya se guardo" justo cuando algo se acaba de perder.
        /// </summary>
        public static bool AlgoSeQuedoSinGuardar
            => Instance != null && !Instance.UltimoResultado.TodoBien;

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
        /// El final con el que termino la aventura, para que llegue al album del nino.
        ///
        /// Va por la cola y no con un POST directo porque es un hecho que ocurre UNA
        /// vez, justo al final de la partida: si en ese momento no hay red, un POST
        /// suelto se perderia y el nino se quedaria sin su medalla en el album sin que
        /// nadie se enterara. El servidor es idempotente, asi que reintentar no la
        /// duplica.
        ///
        /// <b>Clave por final, no una sola para "el final".</b> Rejugar la Mision 6 y
        /// sacar otro final entrega OTRA medalla —el album no quita lo ya ganado—, asi
        /// que con una clave compartida el segundo final pisaria al primero antes de
        /// que llegara a subir.
        /// </summary>
        public static void EncolarFinal(string finalId)
        {
            if (string.IsNullOrWhiteSpace(finalId)) return;
            string id = finalId.Trim();

            Meter($"album.final:{id}", Familia.Append, null, $"final {id}",
                valor: id,
                fusion: (viejo, nuevo) => nuevo,
                hacerEnviar: v => (ok, error) =>
                {
                    var api = ApiManager.Instance;
                    if (api == null) { error("No hay ApiManager."); return; }
                    api.RegistrarFinalDelAlbum((string)v,
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

        /// <summary>
        /// Tira todo lo pendiente, dejando escrito qué. Es para cuando el jugador eligió
        /// irse sin esperar —"Se perderán datos"— y la aplicación sigue abierta: si se
        /// quedara en la cola, el cierre desde el menú, donde ya no hay partida a la que
        /// subirlo, volvería a preguntar una y otra vez por cambios que nunca pueden salir.
        /// </summary>
        public static void DescartarTodo(string porQue)
        {
            List<string> claves = _almacen.Claves();
            if (claves.Count == 0) return;

            _almacen.Limpiar();
            Debug.LogError($"[Cola] Descartados {claves.Count} cambio(s) sin subir ({porQue}): " +
                           string.Join(", ", claves));
        }

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
                Fusion = fusion,
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
            // otro. Dos vaciados a la vez podrian mandar la misma entrada dos veces.
            //
            // Pero el plazo pasa a ser el suyo, contado desde ahora: manda el ultimo que
            // pidio guardar. El cambio de zona insiste mucho rato de fondo; sin esto,
            // cerrar el juego en medio se quedaba esperando ese rato entero, callado, en
            // vez de los segundos del cierre y luego preguntar. Y al reves, otro cambio de
            // zona a mitad de un vaciado le da su plazo completo en vez de las sobras.
            if (_vaciando)
            {
                _limite = Time.realtimeSinceStartup + Mathf.Max(0f, topeSegundos);
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
            _rendidos.Clear();
            _ultimoError = null;

            // Todo el tiempo va en realtime: MenuPausa pone Time.timeScale = 0 al
            // abrirse, y un WaitForSeconds aqui dejaria el juego colgado para siempre.
            float arranque = Time.realtimeSinceStartup;
            _limite = arranque + Mathf.Max(0f, topeSegundos);

            // El margen no puede pasarse del plazo entero, o un vaciado con un tope
            // cortito no mandaria absolutamente nada.
            float margen = Mathf.Min(MargenParaMandar, Mathf.Max(0f, topeSegundos));

            int alEmpezar = _almacen.Cuenta;
            if (verboseLogs)
                Debug.Log($"[Cola] Vaciando por {motivo}: {alEmpezar} cambios.");

            try
            {
              // Se insiste hasta VACIAR la cola, o hasta que se acabe el plazo.
              //
              // Antes se daba una sola pasada y lo que fallaba se quedaba esperando al
              // siguiente momento de guardado. Como los momentos son solo dos, ese
              // siguiente podia estar a media partida de distancia: un fallo de red de un
              // segundo dejaba el cambio sin subir durante media hora.
              while (_almacen.Cuenta > 0 && Time.realtimeSinceStartup < _limite)
              {
                int subidosAlEmpezarLaRonda = _subidos;

                foreach (var previsto in _almacen.Instantanea())
                {
                    if (Time.realtimeSinceStartup >= _limite) break;

                    // Una cadena no puede solaparse con nada: el chat usa NpcId y ChatId
                    // de ApiManager, que son estado global y se pisarian.
                    bool esCadena = previsto.Familia == Familia.Cadena;
                    int hueco = esCadena ? 1 : Mathf.Max(1, paralelismo);

                    while (_enVueloCola >= hueco && Time.realtimeSinceStartup < _limite)
                        yield return null;

                    // Ver `MargenParaMandar`: lo que no se alcanza a esperar no se manda.
                    if (Time.realtimeSinceStartup + margen > _limite) break;

                    // Se toma lo que hay AHORA bajo esta clave, no la copia de arriba: si
                    // mientras se esperaba un hueco entró un dato más nuevo, es ese el
                    // que tiene que salir. Va DESPUÉS de esperar y justo antes de mandar,
                    // para que un plazo vencido no deje un cambio sacado y perdido.
                    // Null = ya lo mandó otro camino.
                    var cambio = _almacen.Tomar(previsto.Clave);
                    if (cambio == null) continue;

                    // El sello: si la cola sobrevivio a un cambio de perfil, esto es lo
                    // unico que evita escribirle el avance de un hermano al otro.
                    if (cambio.Partida.HasValue && cambio.Partida != api.PartidaId)
                    {
                        _descartados++;
                        Debug.LogWarning(
                            $"[Cola] Descartado '{cambio.Descripcion}' de la partida " +
                            $"{cambio.Partida}: ahora se juega la {api.PartidaId}.");
                        continue;
                    }

                    _enVueloCola++;
                    _enVueloClaves.Add(cambio.Clave);
                    _intentados++;

                    var c = cambio;
                    bool reintentar = reintentarSiFalla;

                    // El plazo recorta solo ESTA peticion: se pone justo antes de mandarla
                    // y se quita en cuanto sale. ApiManager lee el timeout al crear la
                    // peticion, dentro de esta misma llamada, asi que lo que se pida en
                    // cualquier otro momento —las lecturas de los sincronizadores, un
                    // dialogo— conserva su timeout normal. Antes el recorte duraba todo el
                    // vaciado y les cortaba las lecturas a los demas.
                    api.TopeDeTiempoParaPeticiones = SegundosHasta(_limite);
                    try
                    {
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
                            _ultimoError = error;

                            if (!reintentar) return;

                            // El codigo se lee AQUI, dentro del callback: ApiManager lo
                            // deja puesto en la linea de antes de llamarnos y lo pisa la
                            // siguiente peticion que falle.
                            int codigo = api.CodigoDelUltimoError;

                            // Insistir solo arregla lo que fallo por el camino. Un cuerpo
                            // que el servidor rechaza lo va a rechazar igual la vez 20, y
                            // mientras tanto bloquea el cierre y llena el log.
                            if (EsCulpaNuestra(codigo))
                            {
                                Rendirse(c, $"el servidor lo rechazo con {codigo} y no " +
                                            "cambia por insistir", error);
                                return;
                            }

                            c.Intentos++;

                            // El tope de intentos es el que frena los fallos que no gastan
                            // plazo. Un envio que ni sale —falta un id, no hay token—
                            // llama a este callback sincronicamente, en la misma linea del
                            // `Resolver()` de arriba: la ronda entera termina en cero
                            // milisegundos y el plazo, que es quien deberia mandar, no se
                            // entera. Con 8 claves asi, un plazo de 10 s daba ~20 rondas
                            // de reintentos instantaneos: 160 errores en consola y ni un
                            // cambio subido.
                            if (c.Intentos >= maxIntentos)
                            {
                                Rendirse(c, $"{c.Intentos} intentos fallidos", error);
                                return;
                            }

                            _almacen.Reencolar(c);
                        });
                    }
                    finally
                    {
                        api.TopeDeTiempoParaPeticiones = null;
                    }

                    // La cadena se espera entera antes de seguir.
                    if (esCadena)
                        while (_enVueloCola > 0 && Time.realtimeSinceStartup < _limite)
                            yield return null;
                }

                // La ronda no ha terminado hasta que contesten todas sus peticiones: sin
                // esto, la siguiente ronda volveria a mandar lo que todavia esta en el
                // aire. Se mira tambien el contador de ApiManager porque una operacion
                // puede haber llamado a su callback con otra peticion encadenada viva.
                while ((_enVueloCola > 0 || api.PeticionesEnVuelo > 0) &&
                       Time.realtimeSinceStartup < _limite + Gracia)
                    yield return null;

                if (_almacen.Cuenta == 0 || !reintentarSiFalla) break;

                // Una ronda entera sin subir nada significa que el servidor no esta
                // contestando. Repetir sin pausa solo quemaria frames hasta agotar el
                // plazo, asi que se espera un poco -en realtime, que MenuPausa pone el
                // timeScale en cero- y se vuelve a probar.
                if (_subidos == subidosAlEmpezarLaRonda)
                {
                    float queda = _limite - Time.realtimeSinceStartup;
                    if (queda <= 0f) break;
                    yield return new WaitForSecondsRealtime(Mathf.Min(EsperaEntreRondas, queda));
                }
              }
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

                // Lo que sigue en la cola al terminar. Antes se calculaba restando los
                // intentos a los que habia al empezar, porque un fallido reencolado se
                // habria contado dos veces; con la insistencia esa resta ya no vale -se
                // reintenta dentro del mismo vaciado, asi que los intentos pueden ser mas
                // que los cambios- y lo que queda es mirar el almacen directamente.
                SinIntentar = _almacen.Cuenta,

                Rendidos = _rendidos.Count,

                Segundos = Time.realtimeSinceStartup - arranque,
            };

            Informar(motivo, UltimoResultado);
        }

        /// <summary>Segundos que quedan, redondeados hacia arriba y nunca menos de 1:
        /// `req.timeout` es entero y un 0 significaria "sin limite".</summary>
        private static int SegundosHasta(float limite)
            => Mathf.Max(1, Mathf.CeilToInt(limite - Time.realtimeSinceStartup));

        /// <summary>
        /// El servidor contesto culpando al cuerpo que le mandamos, no a la red.
        ///
        /// Los 4xx son del cliente: el cuerpo esta mal (400), el token no vale (401), la
        /// partida no es de este adulto (403), el caso no esta cargado en la base (404).
        /// Ninguno se arregla repitiendo la misma peticion.
        ///
        /// Las dos excepciones son 408 y 429: tambien son 4xx, pero los dos significan
        /// literalmente "vuelve a intentarlo". Y el 0 no es un codigo: es un timeout o un
        /// corte de red, el caso para el que el reintento existe.
        /// </summary>
        private static bool EsCulpaNuestra(int codigo)
            => codigo >= 400 && codigo < 500 && codigo != 408 && codigo != 429;

        /// <summary>
        /// Da un cambio por perdido: lo deja FUERA de la cola y lo escribe.
        ///
        /// Sacarlo es el punto. Mientras siguiera en la cola, el cierre del juego lo
        /// contaria como pendiente y le preguntaria al jugador si quiere esperar a algo
        /// que no va a llegar nunca — que es exactamente lo que hacia que el dialogo de
        /// "¿esperas o te vas?" volviera a salir una y otra vez.
        /// </summary>
        private void Rendirse(CambioPendiente c, string porQue, string error)
        {
            _rendidos.Add($"{c.Clave} ({porQue})");

            Debug.LogError($"[Cola] Se da por perdido '{c.Descripcion}' [{c.Clave}]: " +
                           $"{porQue}.\nUltimo error: {error}");
        }

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
                $"[Cola] Vaciada por {motivo} en {r.Segundos:F1} s SIN terminar: " +
                $"{r.Subidos} subidos, {r.Fallidos} intento(s) fallido(s), " +
                $"{r.SinRespuesta} sin respuesta, {r.SinIntentar} todavia en la cola" +
                $" ({_intentados} intento(s) en total" +
                (_descartados > 0 ? $", {_descartados} descartado(s) de otra partida" : "") + ")." +
                (claves.Count > 0 ? $"\nQuedan: {string.Join(", ", claves)}" : "") +
                (_rendidos.Count > 0 ? $"\nPerdidos: {string.Join(", ", _rendidos)}" : "") +
                (string.IsNullOrEmpty(_ultimoError) ? "" : $"\nUltimo error: {_ultimoError}"));
        }
    }
}
