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

            /// <summary>
            /// Antes de este instante (realtime) no se vuelve a intentar. Es el retardo
            /// creciente: contra un servidor caido, reintentar cada diez segundos para
            /// siempre solo llena el log.
            ///
            /// No se anota en el diario a proposito: si el juego se reinicio, lo sensato
            /// es probar otra vez de inmediato.
            /// </summary>
            public float NoAntesDe;

            /// <summary>
            /// Fallo tantas veces que se deja de reintentar solo. <b>No se pierde</b>:
            /// sigue en el diario, sale en el aviso al jugador y vuelve a la carga con
            /// "Intentar ahora" o al volver a entrar al juego.
            /// </summary>
            public bool Atascado;

            /// <summary>
            /// La llamada, ya lista. Recibe (cuandoSalgaBien, cuandoFalle).
            ///
            /// <b>Solo la usan los dos snapshots de verdad</b> —la mochila y la posicion
            /// de Otto—, que leen el estado vivo al ejecutarse. Un closure no se puede
            /// anotar en el diario, asi que todo lo demas va por <see cref="Receta"/>.
            /// </summary>
            public Action<Action, Action<string>> Enviar;

            /// <summary>
            /// Nombre de la receta que sabe hacer esta llamada (ver
            /// <see cref="RecetasDeCola"/>), o null si va por <see cref="Enviar"/>.
            ///
            /// Tenerlo por nombre y no por delegado es lo que permite escribir el cambio
            /// en el diario y rehacerlo al volver a entrar al juego.
            /// </summary>
            public string Receta;

            /// <summary>
            /// Los argumentos de la receta, serializables. Para las claves que fusionan
            /// (zona por OR, progreso por maximo) es el valor ya acumulado: se rehace el
            /// envio al vaciar para que salga el fusionado y no el primero que llego.
            /// </summary>
            public object Valor;

            /// <summary>Se puede anotar en el diario y sobrevivir al cierre.</summary>
            public bool Anotable => !string.IsNullOrEmpty(Receta);

            /// <summary>Cómo se combinan dos valores de esta clave (viejo, nuevo). Null =
            /// gana el nuevo. Se guarda aquí para poder fusionar también cuando un envío
            /// fallido vuelve a la cola y ya hay uno más nuevo esperando.</summary>
            public Func<object, object, object> Fusion;

            public Action<Action, Action<string>> Resolver()
                => Anotable ? RecetasDeCola.Hacer(Receta, Valor, Partida) : Enviar;
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

            public bool Contiene(string clave) => !string.IsNullOrEmpty(clave) && _porClave.ContainsKey(clave);

            /// <summary>
            /// Vuelve a meter un cambio que viene del diario, <b>conservando su orden
            /// original</b>: lo que quedó de la sesión anterior es más viejo que lo de
            /// ahora y tiene que salir antes. La secuencia se adelanta para que un
            /// cambio nuevo no reciba un orden que ya está usado.
            ///
            /// Si la clave ya está ocupada gana la de ahora, que es la que tiene el dato
            /// más reciente.
            /// </summary>
            public void Reponer(CambioPendiente cambio)
            {
                if (cambio == null || string.IsNullOrEmpty(cambio.Clave)) return;

                if (_porClave.TryGetValue(cambio.Clave, out var vivo))
                {
                    // Gana el de ahora, que tiene el dato más reciente... pero si la
                    // clave fusiona hay que combinarlos, igual que al reencolar un fallo:
                    // una zona que quedó "completada" la sesión pasada no la puede borrar
                    // un "desbloqueada" de esta. El repuesto es el viejo de los dos.
                    if (cambio.Fusion != null)
                        vivo.Valor = cambio.Fusion(cambio.Valor, vivo.Valor);
                    return;
                }

                _porClave[cambio.Clave] = cambio;
                _orden.Add(cambio.Clave);
                if (cambio.Orden >= _secuencia) _secuencia = cambio.Orden + 1;
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
            /// Se saltaron a proposito: esperaban su retardo tras un fallo, o estan
            /// atascados. No son una perdida — siguen anotados en el diario — asi que no
            /// cuentan como "sin guardar" para el aviso de cierre.
            /// </summary>
            public int Esperando;

            public float Segundos;

            public bool TodoBien => Fallidos == 0 && SinRespuesta == 0 && SinIntentar == 0;
            public int Pendientes => Fallidos + SinRespuesta + SinIntentar;
        }

        // ── Configuracion ─────────────────────────────────────────────────────

        [Header("Configuración")]
        [Tooltip("Cuántas peticiones se mandan a la vez al vaciar. Las cadenas (el chat) " +
                 "siempre van de una en una, sin importar este valor.")]
        [Min(1)] public int paralelismo = 4;

        [Tooltip("Veces que se reintenta un cambio que falla antes de dejar de insistir " +
                 "solo. Pasadas, el cambio NO se pierde: queda anotado en el diario, sale " +
                 "en el aviso al jugador y vuelve a intentarse con «Intentar ahora» o al " +
                 "volver a entrar al juego.")]
        [Min(1)] public int maxIntentos = 6;

        [Tooltip("Segundos entre dos vaciados de fondo. La cola se va subiendo mientras " +
                 "se juega, en vez de solo al cambiar de zona; dentro de este rato los " +
                 "cambios siguen agrupándose, así que el número de peticiones es el mismo.")]
        [Min(1f)] public float intervaloDeFondo = 10f;

        [Tooltip("Segundos máximos que puede tardar un vaciado de fondo o un reintento " +
                 "pedido por el jugador.")]
        [Min(1f)] public float topeDeFondo = 15f;

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

        /// <summary>Los que se saltaron porque todavia esperaban su retardo, o porque
        /// estan atascados. No son un fallo: no se intentaron a proposito.</summary>
        private int _esperando;

        /// <summary>
        /// Cuanto se espera antes de volver a intentar, segun las veces que ya fallo.
        /// Contra un servidor caido, reintentar cada diez segundos para siempre no
        /// arregla nada y llena el log.
        /// </summary>
        private static float RetardoTras(int intentos)
        {
            switch (intentos)
            {
                case 1:  return 10f;
                case 2:  return 30f;
                case 3:  return 60f;
                default: return 120f;
            }
        }

        /// <summary>
        /// Hay algo que fallo tantas veces que se dejo de reintentar solo. Es lo que
        /// enciende el aviso al jugador: no se perdio nada, pero no va a salir sin ayuda.
        /// </summary>
        public static bool HayAtasco
        {
            get
            {
                foreach (var cambio in _almacen.Instantanea())
                    if (cambio.Atascado) return true;
                return false;
            }
        }

        /// <summary>
        /// Todo lo que queda por subir esta anotado en el diario, o sea que cerrar ahora
        /// no pierde nada: se sube al volver a entrar.
        ///
        /// Es lo que decide si al jugador se le dice "se perdera el progreso" o "se
        /// guardara la proxima vez". Confundir las dos cosas fue justo lo que hizo creer
        /// durante semanas que el guardado estaba roto.
        /// </summary>
        public static bool TodoAnotado
        {
            get
            {
                foreach (var cambio in _almacen.Instantanea())
                    if (!cambio.Anotable) return false;
                return true;
            }
        }

        /// <summary>Que falta por subir, en palabras, para poder decirselo al jugador.</summary>
        public static List<string> Descripciones()
        {
            var lista = new List<string>();
            foreach (var cambio in _almacen.Instantanea())
                lista.Add(string.IsNullOrEmpty(cambio.Descripcion) ? cambio.Clave : cambio.Descripcion);
            return lista;
        }

        /// <summary>
        /// Lo que el jugador pulsa en "Intentar ahora": se olvidan los retardos y los
        /// atascos, y se vacia enseguida.
        /// </summary>
        public static void ReintentarAhora()
        {
            foreach (var cambio in _almacen.Instantanea())
            {
                cambio.Atascado = false;
                cambio.NoAntesDe = 0f;
                cambio.Intentos = 0;
            }

            // Va directo a vaciar y no por SaveManager.Guardar a propósito: el
            // interruptor `momentosActivos` es para los momentos AUTOMÁTICOS, y ahí
            // `Manual` viene apagado de fábrica, así que pedirlo por ese camino no haría
            // nada. Además un reintento no necesita marcar de sucios la mochila ni la
            // posición: lo que hay que sacar es lo que ya está en la cola.
            var cola = Instance;
            if (cola == null || cola._vaciando) return;
            cola.StartCoroutine(cola.Vaciar("PedidoDelJugador", cola.topeDeFondo, reintentarSiFalla: true));
        }

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
        private static void AutoCrear()
        {
            GetOrCreate();
            Reproducir();
        }

        /// <summary>
        /// Devuelve a la cola lo que quedó anotado de sesiones anteriores.
        ///
        /// Cada entrada vuelve con <b>su</b> partida, no con la que se está jugando: los
        /// endpoints de escritura son todos por partida y el servidor comprueba que sea
        /// de este adulto, así que un objetivo que se cumplió en la partida 3 se sube
        /// bien mientras se juega la 4. Es lo que permite que el diario no tenga que
        /// descartar nada por haber cambiado de perfil.
        /// </summary>
        public static void Reproducir()
        {
            var entradas = DiarioDeCambios.Leer();
            if (entradas.Count == 0) return;

            int repuestos = 0;
            foreach (var entrada in entradas)
            {
                object args = DiarioDeCambios.ArgsDe(entrada);
                if (args == null)
                {
                    DiarioDeCambios.Confirmar(entrada.Clave);   // ilegible: sacarlo del diario
                    continue;
                }

                if (!Enum.TryParse(entrada.Familia, out Familia familia))
                    familia = Familia.Append;

                _almacen.Reponer(new CambioPendiente
                {
                    Clave = entrada.Clave,
                    Familia = familia,
                    Descripcion = entrada.Descripcion,
                    Partida = entrada.Partida,
                    Orden = entrada.Orden,

                    // A cero, y no el del diario, a propósito: una sesión nueva merece
                    // sus intentos completos. Si se arrastrara la cuenta, algo que se
                    // atascó ayer se atascaría hoy al primer fallo, aunque el problema
                    // (el servidor caído, el wifi del colegio) ya no exista.
                    Intentos = 0,

                    Receta = entrada.Receta,
                    Valor = args,
                    Fusion = RecetasDeCola.FusionDe(entrada.Receta),
                });
                repuestos++;
            }

            if (repuestos > 0)
                Debug.Log($"[Cola] {repuestos} cambio(s) quedaron sin subir la última vez " +
                          "y vuelven a la cola.");
        }

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

        private void Start() => StartCoroutine(GotearDeFondo());

        /// <summary>
        /// Va subiendo la cola cada pocos segundos mientras se juega.
        ///
        /// <b>Por qué, si ya había dos momentos.</b> Los dos momentos son instantes, y
        /// entre uno y otro puede pasar media partida: el niño/a que se queda en la misma
        /// zona cuarenta minutos no guarda nada en cuarenta minutos. Así la ventana deja
        /// de ser una lista de instantes y pasa a ser "siempre, unos segundos por
        /// detrás".
        ///
        /// <b>No vuelve al goteo de una petición por evento</b>, que es lo que esta cola
        /// vino a quitar: dentro del intervalo los cambios se siguen agrupando por clave,
        /// así que el número de peticiones es el mismo, solo reparte. El cambio de zona
        /// sigue vaciando de inmediato y el cierre sigue vaciando con plazo.
        ///
        /// <b>Espera en realtime, no en tiempo de juego.</b> <c>MenuPausa</c> pone
        /// <c>Time.timeScale = 0</c> al abrirse, y un <c>WaitForSeconds</c> aquí dejaría
        /// el goteo parado para siempre en cuanto alguien abriera la pausa.
        /// </summary>
        private IEnumerator GotearDeFondo()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(1f, intervaloDeFondo));

                if (_vaciando || _almacen.Cuenta == 0) continue;

                // Cualificado: SaveManager vive en Fishy.World y esto en Fishy.Net.
                var save = Fishy.World.SaveManager.Instance;
                if (save != null &&
                    (save.momentosActivos & Fishy.World.SaveManager.Momentos.EnSegundoPlano) == 0)
                    continue;

                // No se exige PartidaId. Lo anotado lleva su propia partida, así que en
                // cuanto hay sesión se puede ir subiendo lo que quedó de la vez anterior,
                // incluso desde el menú y antes de elegir con qué partida se sigue.
                var api = ApiManager.Instance;
                if (api == null || api.IsLocalMode || !api.IsLoggedIn) continue;

                yield return Vaciar("EnSegundoPlano", topeDeFondo, reintentarSiFalla: true);
            }
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

            EncolarReceta($"zona:{slug}", Familia.Append, RecetasDeCola.Zona,
                new RecetasDeCola.ArgsZona { Zona = slug, Completada = completada },
                $"zona {slug}");
        }

        /// <summary>
        /// Progreso de la partida (0-100). <b>Fusiona por maximo, no pisa.</b>
        ///
        /// Cada zona manda un valor absoluto al completarse. Si dos se cerraran entre
        /// dos vaciados y la segunda tuviera un valor menor, el progreso retrocederia.
        /// </summary>
        public static void EncolarProgreso(float progreso)
            => EncolarReceta("partida.progreso", Familia.Snapshot, RecetasDeCola.Progreso,
                new RecetasDeCola.ArgsProgreso { Progreso = progreso },
                $"progreso {progreso:F0}%");

        /// <summary>Una misión que quedó disponible o que se completó.</summary>
        public static void EncolarMision(string misionId, bool completada)
        {
            if (string.IsNullOrEmpty(misionId)) return;

            EncolarReceta($"mision:{misionId}", Familia.Append, RecetasDeCola.Mision,
                new RecetasDeCola.ArgsMision { MisionId = misionId, Completada = completada },
                $"misión {misionId}");
        }

        /// <summary>Un objetivo concreto de una misión, por su orden en el catálogo.</summary>
        public static void EncolarObjetivo(string misionId, int orden)
        {
            if (string.IsNullOrEmpty(misionId)) return;

            EncolarReceta($"objetivo:{misionId}:{orden}", Familia.Append, RecetasDeCola.Objetivo,
                new RecetasDeCola.ArgsObjetivo { MisionId = misionId, Orden = orden },
                $"objetivo {misionId} #{orden}");
        }

        /// <summary>Un objeto que el niño/a recogió del mapa.</summary>
        public static void EncolarObjeto(string objetoId)
        {
            if (string.IsNullOrEmpty(objetoId)) return;

            EncolarReceta($"objeto:{objetoId}", Familia.Append, RecetasDeCola.Objeto,
                new RecetasDeCola.ArgsObjeto { ObjetoId = objetoId },
                $"objeto {objetoId}");
        }

        /// <summary>Un NPC de temática terminado, con o sin éxito.</summary>
        public static void EncolarNpc(string npcId, bool exito)
        {
            if (string.IsNullOrEmpty(npcId)) return;

            EncolarReceta($"npc:{npcId}", Familia.Append, RecetasDeCola.Npc,
                new RecetasDeCola.ArgsNpc { NpcId = npcId, Exito = exito },
                $"NPC {npcId}");
        }

        /// <summary>Un caso del Modo Detective jugado, aprobado o no.</summary>
        public static void EncolarDetective(string casoId, IEnumerable<string> marcados,
            int aciertos, int totalRiesgo, float porcentaje)
        {
            if (string.IsNullOrEmpty(casoId)) return;

            EncolarReceta($"detective:{casoId}", Familia.Append, RecetasDeCola.Detective,
                new RecetasDeCola.ArgsDetective
                {
                    CasoId = casoId,
                    // Copia: el jugador puede reabrir el caso antes de que esto salga.
                    Marcados = marcados != null ? new List<string>(marcados) : new List<string>(),
                    Aciertos = aciertos,
                    TotalRiesgo = totalRiesgo,
                    Porcentaje = porcentaje,
                },
                $"caso {casoId}");
        }

        /// <summary>
        /// Una conversación entera. La clave la pone quien llama, porque un mismo
        /// contacto puede tener varias conversaciones en una partida y pisarlas las
        /// perdería.
        /// </summary>
        public static void EncolarChat(string clave, string contacto, string zona,
            string categoria, List<RecetasDeCola.MensajeDeChat> mensajes, string cierre)
        {
            if (string.IsNullOrEmpty(clave)) return;

            EncolarReceta(clave, Familia.Cadena, RecetasDeCola.Chat,
                new RecetasDeCola.ArgsChat
                {
                    Contacto = contacto,
                    Zona = zona,
                    Categoria = categoria,
                    Cierre = cierre,
                    Mensajes = mensajes,
                },
                $"conversación con {contacto} ({(mensajes != null ? mensajes.Count : 0)} mensajes)");
        }

        /// <summary>
        /// El camino general: un cambio que sabe rehacerse a partir de su nombre de
        /// receta y sus argumentos, y que por eso puede anotarse en el diario.
        /// </summary>
        public static void EncolarReceta(string clave, Familia familia, string receta,
            object args, string descripcion = null)
            => Meter(clave, familia, null, descripcion,
                receta: receta,
                valor: args,
                fusion: RecetasDeCola.FusionDe(receta));

        /// <summary>Saca un cambio de la cola sin mandarlo. Para deshacer un encolado.</summary>
        public static void Olvidar(string clave) => _almacen.Quitar(clave);

        private static void Meter(string clave, Familia familia,
            Action<Action, Action<string>> enviar, string descripcion,
            string receta = null,
            object valor = null,
            Func<object, object, object> fusion = null)
        {
            if (string.IsNullOrEmpty(clave)) return;

            var cambio = new CambioPendiente
            {
                Clave = clave,
                Familia = familia,
                Descripcion = string.IsNullOrEmpty(descripcion) ? clave : descripcion,
                // La partida en la que ocurrió el cambio. Ya no sirve para descartar
                // —el diario puede subir a la partida de antes, porque los endpoints son
                // por partida— sino para subirlo a la correcta.
                Partida = ApiManager.Instance != null ? ApiManager.Instance.PartidaId : null,
                Enviar = enviar,
                Receta = receta,
                Valor = valor,
                Fusion = fusion,
            };

            _almacen.Poner(cambio, fusion);

            // Después de Poner, no antes: lo que se anota tiene que ser el valor ya
            // fusionado —una zona que pasó a completada, un progreso que subió— y no el
            // que llegó suelto.
            if (cambio.Anotable)
                DiarioDeCambios.Anotar(cambio.Clave, cambio.Familia.ToString(), cambio.Receta,
                    cambio.Valor, cambio.Partida, cambio.Orden, cambio.Intentos,
                    cambio.Descripcion, cambio.Fusion);

            var cola = Instance;
            if (cola == null) return;

            if (cola.verboseLogs)
                Debug.Log($"[Cola] +{clave} ({familia}) — {_almacen.Cuenta} pendientes." +
                          (cambio.Anotable ? "" : " Sin diario: no sobrevive al cierre."));

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
            _esperando = 0;

            // Al cerrar no se respeta el retardo: es la última oportunidad de esta
            // sesión, así que se intenta todo, incluido lo atascado.
            bool respetarRetardo = reintentarSiFalla;

            // Todo el tiempo va en realtime: MenuPausa pone Time.timeScale = 0 al
            // abrirse, y un WaitForSeconds aqui dejaria el juego colgado para siempre.
            float arranque = Time.realtimeSinceStartup;
            float limite = arranque + Mathf.Max(0f, topeSegundos);

            int alEmpezar = _almacen.Cuenta;
            if (verboseLogs)
                Debug.Log($"[Cola] Vaciando por {motivo}: {alEmpezar} cambios.");

            try
            {
                foreach (var previsto in _almacen.Instantanea())
                {
                    if (Time.realtimeSinceStartup >= limite) break;

                    // Lo que espera su retardo, o lo que se atascó, no se toca... salvo
                    // al cerrar, que es la última oportunidad de esta sesión y entonces
                    // se intenta todo. `respetarRetardo` distingue los dos momentos sin
                    // necesidad de otro parámetro.
                    if (respetarRetardo &&
                        (previsto.Atascado || Time.realtimeSinceStartup < previsto.NoAntesDe))
                    {
                        _esperando++;
                        continue;
                    }

                    // Una cadena no puede solaparse con nada: el chat usa NpcId y ChatId
                    // de ApiManager, que son estado global y se pisarian.
                    bool esCadena = previsto.Familia == Familia.Cadena;
                    int hueco = esCadena ? 1 : Mathf.Max(1, paralelismo);

                    while (_enVueloCola >= hueco && Time.realtimeSinceStartup < limite)
                        yield return null;
                    if (Time.realtimeSinceStartup >= limite) break;

                    // Se toma lo que hay AHORA bajo esta clave, no la copia de arriba: si
                    // mientras se esperaba un hueco entró un dato más nuevo, es ese el
                    // que tiene que salir. Va DESPUÉS de esperar y justo antes de mandar,
                    // para que un plazo vencido no deje un cambio sacado y perdido.
                    // Null = ya lo mandó otro camino.
                    var cambio = _almacen.Tomar(previsto.Clave);
                    if (cambio == null) continue;

                    // Antes, un cambio de otra partida se tiraba: era lo único que
                    // evitaba escribirle el avance de un hermano al otro. Ya no hace
                    // falta tirar nada. Lo anotado lleva SU partida y se sube a ella,
                    // porque los endpoints de escritura son todos por partida.
                    //
                    // Lo que no está anotado sí se descarta, porque un snapshot lee el
                    // estado vivo y ese ya es de la partida de ahora.
                    if (!cambio.Anotable && cambio.Partida.HasValue && cambio.Partida != api.PartidaId)
                    {
                        _descartados++;
                        Debug.LogWarning(
                            $"[Cola] Descartado '{cambio.Descripcion}' de la partida " +
                            $"{cambio.Partida}: ahora se juega la {api.PartidaId}.");
                        continue;
                    }

                    api.TopeDeTiempoParaPeticiones = SegundosHasta(limite);

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

                            // Antes cada sincronizador escribía su propio "guardado".
                            // Decirlo aquí lo dice de todos igual, y en el sitio que
                            // sabe de verdad que el servidor contestó que sí.
                            if (verboseLogs) Debug.Log($"[Cola] ✓ {c.Descripcion}");

                            // Recién ahora se borra del diario. Si hay otro cambio con
                            // esta misma clave esperando, el diario tiene que conservarlo:
                            // lo que se acaba de confirmar es el valor viejo.
                            if (c.Anotable && !_almacen.Contiene(c.Clave))
                                DiarioDeCambios.Confirmar(c.Clave);
                        },
                        error =>
                        {
                            _enVueloCola--;
                            _enVueloClaves.Remove(c.Clave);
                            _fallidos++;

                            // Lo que no se puede anotar tampoco se puede reintentar más
                            // allá de esta sesión, y al cerrar no hay otra vez.
                            if (!reintentar && !c.Anotable) return;

                            c.Intentos++;

                            // Ya no se abandona nunca. Tras unos cuantos intentos se deja
                            // de insistir solo —"atascado"—, pero sigue en el diario, sale
                            // en el aviso al jugador y vuelve a intentarse con "Intentar
                            // ahora" o en la sesión siguiente.
                            if (c.Intentos >= Mathf.Max(1, maxIntentos))
                            {
                                c.Atascado = true;
                                Debug.LogWarning($"[Cola] '{c.Descripcion}' falló {c.Intentos} " +
                                                 $"veces y deja de reintentarse solo. No se pierde: " +
                                                 $"queda guardado para el próximo intento. Último error: {error}");
                            }
                            else
                            {
                                c.NoAntesDe = Time.realtimeSinceStartup + RetardoTras(c.Intentos);
                            }

                            _almacen.Reencolar(c);
                            if (c.Anotable)
                                DiarioDeCambios.Anotar(c.Clave, c.Familia.ToString(), c.Receta,
                                    c.Valor, c.Partida, c.Orden, c.Intentos, c.Descripcion, c.Fusion);
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
                // Los que esperaban su retardo se restan aparte: no es que no se
                // hayan podido mandar, es que no era su momento.
                SinIntentar = Mathf.Max(0, alEmpezar - _intentados - _descartados - _esperando),

                Esperando = _esperando,

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
                    Debug.Log($"[Cola] Vaciada en {r.Segundos:F1} s: {r.Subidos} subidos." +
                              (r.Esperando > 0 ? $" {r.Esperando} esperan su turno." : ""));
                return;
            }

            var claves = _almacen.Claves();
            foreach (string enVuelo in _enVueloClaves)
                if (!claves.Contains(enVuelo)) claves.Add(enVuelo);

            // Cuánto de lo que queda sobrevive al cierre. Es la diferencia entre "se
            // subirá solo la próxima vez" y "esto se perdió", y confundirlas fue
            // justamente lo que hizo creer durante semanas que el guardado estaba roto.
            int anotados = 0;
            foreach (var cambio in _almacen.Instantanea())
                if (cambio.Anotable) anotados++;

            // Lo que salió y no contestó a tiempo sigue anotado, así que también vuelve.
            // Puede que hubiera llegado y se mande dos veces: todas las escrituras son
            // idempotentes a propósito, así que repetir una no duplica nada.
            int perdidos = r.Pendientes - anotados - r.SinRespuesta;

            string resumen =
                $"[Cola] Vaciada por {motivo} en {r.Segundos:F1} s: {r.Subidos} subidos, " +
                $"{r.Fallidos} fallidos, {r.SinRespuesta} sin respuesta, " +
                $"{r.SinIntentar} sin intentar." +
                (claves.Count > 0 ? $"\nQuedan: {string.Join(", ", claves)}" : "");

            if (perdidos <= 0)
            {
                // Nada que lamentar: está todo en el diario y se sube al volver a entrar.
                Debug.LogWarning($"{resumen}\nTodo lo que queda está anotado: se sube solo " +
                                 "en cuanto haya conexión, o al volver a entrar al juego.");
                return;
            }

            // LogError solo para lo que de verdad se pierde. En un build esto es lo único
            // que queda en Player.log.
            Debug.LogError($"{resumen}\n{perdidos} cambio(s) NO están anotados y se pierden " +
                           "(la mochila y la posición de Otto, que se recalculan solas al " +
                           "siguiente guardado).");
        }
    }
}
