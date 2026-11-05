using System;
using System.Collections.Generic;
using Fishy.Mision;
using Fishy.Pausa;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Col = Fishy.UI.TecladoEnPantallaTheme.Colores;
using Med = Fishy.UI.TecladoEnPantallaTheme.Medidas;
using Fnt = Fishy.UI.TecladoEnPantallaTheme.Fuente;
using Txt = Fishy.UI.TecladoEnPantallaTheme.Textos;

namespace Fishy.UI
{
    /// <summary>
    /// La tarjeta de controles: enseña qué tecla hace qué. Sale en dos sitios, y la
    /// diferencia entre los dos es quién la pidió.
    ///
    /// <b>Abierta a mano</b>, desde el menú de pausa en Configuración ▸ Controles: se
    /// queda centrada todo lo que se quiera mirar y se va con «Volver» o con Esc.
    /// Antes de esto la tarjeta salía sola al empezar a jugar y se iba cuando se
    /// habían pulsado todas las teclas: tapaba el mundo justo en los primeros
    /// minutos, y una vez ida no había forma de volver a verla.
    ///
    /// <b>Como recordatorio</b>, cuando empieza una misión nueva: aparece 6 segundos
    /// en una esquina con las teclas que esa misión pide latiendo, y se va sola. Esto
    /// existe porque la primera misión dice «habla con Huemul» y nada más, y un
    /// niño/a de 9 años que no sabe que se interactúa con E no tiene de dónde
    /// sacarlo. No vuelve a ser un cartel permanente: seis segundos en una esquina se
    /// pueden ignorar, una tarjeta en medio de la pantalla no.
    ///
    /// <b>Las teclas se encienden al pulsarlas</b> aunque el juego esté en pausa: el
    /// niño/a puede probar la W y ver cuál es sin que Otto se mueva. No se pueden
    /// pulsar con el ratón ni con el dedo; enseña, no juega.
    ///
    /// <b>Las teclas no están escritas aquí.</b> Se le preguntan a quien las escucha
    /// —<see cref="OttoController"/>, <see cref="InteractionDetector"/>,
    /// <see cref="MenuPausa"/> y <see cref="MenuController"/>—, que es el criterio
    /// que ya seguía el cartel de la E. Un cartel que enseña teclas equivocadas es
    /// peor que no tener cartel.
    /// </summary>
    [DisallowMultipleComponent]
    public class TecladoEnPantalla : MonoBehaviour
    {
        /// <summary>Para qué se montó esta tarjeta. Cambia dónde se pone, si lleva
        /// botón de «Volver» y si se esconde sola.</summary>
        private enum Modo { Panel, Recordatorio }

        /// <summary>Las acciones que la tarjeta explica, en el orden de la tarjeta.
        /// Saltar no está: el juego no lo usa para nada.</summary>
        private enum Accion { Caminar, Hablar, Correr, Celular, Pausa }

        /// <summary>Cómo está pintada una tecla ahora mismo. Se guarda para no
        /// reasignar colores cada frame.</summary>
        private enum Pinta { Reposo, Pulsada, Destacada }

        /// <summary>Una tecla dibujada. Puede representar más de una —la W también es
        /// la flecha de arriba— porque para el niño/a son la misma cosa.</summary>
        private class Cara
        {
            public readonly List<Key> Teclas = new List<Key>();
            public Image Fondo;
            public TextMeshProUGUI Letra;

            /// <summary>El triangulito de dirección, solo en las teclas de caminar. Se
            /// guarda para recolorearlo con la tecla: una flecha que se queda del
            /// color de antes delata que el destacado está a medias.</summary>
            public Image Flecha;

            public Pinta Pinta = Pinta.Reposo;
        }

        /// <summary>Una fila: lo que hace y con qué teclas.</summary>
        private class Bloque
        {
            public Accion Accion;
            public readonly List<Cara> Caras = new List<Cara>();

            /// <summary>El nombre de lo que hace la fila («Hablar y seguir»). Se guarda
            /// para pintarlo del color de la misión junto a sus teclas: una tecla que
            /// late sin decir para qué sirve no enseña nada.</summary>
            public TextMeshProUGUI Etiqueta;

            /// <summary>Si la etiqueta está ya pintada de misión, para no reasignarle
            /// el color en cada frame.</summary>
            public bool EtiquetaDestacada;
        }

        private readonly List<Bloque> _bloques = new List<Bloque>();

        private Modo _modo = Modo.Panel;
        private GameObject _raiz;
        private RectTransform _tarjeta;
        private OttoController _otto;
        private MenuController _celular;
        private Action _alVolver;

        /// <summary>Lo que la misión activa necesita ahora mismo. Esas teclas se
        /// pintan del color de la misión y laten.</summary>
        private readonly HashSet<Accion> _loQuePideLaMision = new HashSet<Accion>();

        /// <summary>Hasta cuándo se enseña el recordatorio, en tiempo sin escalar.
        /// 0 = no hay ninguno en curso.</summary>
        private float _recordarHasta;

        private float _proximaRevision;
        private MissionManager _managerApuntado;
        private MissionTracker _rastreadorApuntado;

        public bool Abierto => _raiz != null && _raiz.activeSelf;

        // ── El recordatorio del mundo ─────────────────────────────────────────

        /// <summary>
        /// La tarjeta que vive en el mundo y sale sola. Es estática porque no puede
        /// haber dos, y la de la pausa no cuenta: esa la monta y la tira el menú.
        /// </summary>
        private static TecladoEnPantalla _recordatorio;

        /// <summary>El editor conserva los estáticos entre corridas de Play, y una
        /// referencia a un objeto destruido no es null: es un objeto "muerto" que
        /// hace que la siguiente corrida crea que ya hay recordatorio y no monte
        /// ninguno.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void LimpiarEstadoEstatico() => _recordatorio = null;

        /// <summary>
        /// Monta el recordatorio al cargar cualquier escena, sin cablear nada.
        ///
        /// El <c>DontDestroyOnLoad</c> va aquí y no en un Awake a propósito: fuera del
        /// modo Play tira excepción, y el arnés de pruebas crea el componente a mano.
        /// Por este camino solo se pasa jugando.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrearElRecordatorio()
        {
            if (_recordatorio != null) return;

            var go = new GameObject("TecladoRecordatorio");
            DontDestroyOnLoad(go);

            _recordatorio = go.AddComponent<TecladoEnPantalla>();
            _recordatorio._modo = Modo.Recordatorio;
        }

        // ── Crear, abrir y cerrar ─────────────────────────────────────────────

        /// <summary>
        /// Monta la tarjeta dentro de <paramref name="padre"/> —el canvas del menú de
        /// pausa—, apagada. <paramref name="alVolver"/> es lo que hace su botón
        /// «Volver».
        ///
        /// Lo llama la pausa la primera vez que se piden los controles y no al
        /// construirse: las teclas se le preguntan a Otto, y así seguro que ya armó
        /// sus atajos.
        /// </summary>
        public static TecladoEnPantalla CrearEn(Transform padre, Action alVolver)
        {
            var go = new GameObject("Controles", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            FishyUIKit.Estirar((RectTransform)go.transform);

            var ayuda = go.AddComponent<TecladoEnPantalla>();
            ayuda._modo = Modo.Panel;
            ayuda._alVolver = alVolver;
            ayuda._raiz = go;
            ayuda._otto = FindAnyObjectByType<OttoController>();
            ayuda.Construir();

            go.SetActive(false);
            return ayuda;
        }

        public void Abrir()
        {
            if (_raiz == null) return;

            // Se pregunta aquí y no cada frame: la tarjeta de la pausa se abre una vez
            // y lo que la misión pide no cambia mientras el juego está congelado.
            ApuntarseALasMisiones();
            MirarQuePideLaMision();

            // Ninguna tecla queda encendida de la vez anterior.
            Repintar(_ => false);
            _raiz.SetActive(true);
        }

        public void Cerrar()
        {
            if (_raiz != null) _raiz.SetActive(false);
        }

        /// <summary>
        /// Lo que la tarjeta está enseñando ahora mismo: para cada acción, todas las
        /// teclas que acepta.
        ///
        /// Existe para el arnés de pruebas, que compara esto con lo que el juego
        /// escucha de verdad. Es la forma de que «el cartel no miente» sea algo que
        /// se comprueba y no algo que se promete en un comentario.
        /// </summary>
        public Dictionary<string, List<Key>> LoQueEnsena()
        {
            var porAccion = new Dictionary<string, List<Key>>();

            foreach (Bloque bloque in _bloques)
            {
                var teclas = new List<Key>();
                foreach (Cara cara in bloque.Caras)
                    foreach (Key tecla in cara.Teclas)
                        if (!teclas.Contains(tecla)) teclas.Add(tecla);

                porAccion[bloque.Accion.ToString()] = teclas;
            }

            return porAccion;
        }

        private void Update()
        {
            if (_modo == Modo.Recordatorio && Time.unscaledTime >= _proximaRevision)
            {
                _proximaRevision = Time.unscaledTime + CadaCuanto;
                Revisar();
            }

            if (!Abierto) return;

            Repintar(EstaPulsada);
            Latir();
        }

        private void OnDestroy() => SoltarLasMisiones();

        /// <summary>Cada cuánto se revisa si toca mostrar el recordatorio, en segundos
        /// sin escalar (con la pausa puesta el tiempo normal no corre). La revisión
        /// busca cosas en la escena y no hace falta hacerlo cada frame.</summary>
        private const float CadaCuanto = 0.5f;

        /// <summary>Si esa tecla está pulsada ahora mismo. Es lo único que
        /// <see cref="Repintar"/> necesita saber del mundo, y por eso se le pasa
        /// desde fuera: así el arnés de pruebas puede fingir pulsaciones sin un
        /// teclado de verdad.</summary>
        private static bool EstaPulsada(Key tecla)
        {
            Keyboard teclado = Keyboard.current;
            return teclado != null && teclado[tecla].isPressed;
        }

        /// <summary>
        /// Enciende o apaga el recordatorio según si ahora toca enseñar algo.
        ///
        /// Se esconde cuando no aporta —en pausa, durante una cinemática o una carga,
        /// con el celular abierto encima— y cuando no hay teclado que enseñar: en una
        /// tablet, <c>Keyboard.current</c> es null y un dibujo de teclas sería un
        /// chiste malo.
        ///
        /// <b>Y se esconde mientras Otto no puede caminar</b>, que es como el resto
        /// del código pregunta «¿hay un diálogo, un chat o el Modo Detective en
        /// medio?» (ver <c>MenuController.PuedeAbrir</c>). Aquí no es por elegancia:
        /// la tarjeta es grande y el panel de diálogo le queda encima.
        /// </summary>
        private void Revisar()
        {
            if (_otto == null) _otto = FindAnyObjectByType<OttoController>();
            if (_celular == null) _celular = FindAnyObjectByType<MenuController>();

            ApuntarseALasMisiones();

            bool quiereVerse = Time.unscaledTime < _recordarHasta;

            // Se construye con Otto delante y no antes: las teclas se las preguntamos
            // a él, y en la escena del menú todavía no existe.
            if (quiereVerse && _raiz == null && _otto != null && Keyboard.current != null)
                Construir();

            bool mostrar = quiereVerse &&
                           _otto != null &&
                           _otto.movementEnabled &&
                           Keyboard.current != null &&
                           !EstadoDePausa.Pausado &&
                           EstadoDePausa.SePuedePausar() &&
                           (_celular == null || !_celular.EstaAbierto);

            if (_raiz == null || _raiz.activeSelf == mostrar) return;
            _raiz.SetActive(mostrar);
        }

        /// <summary>
        /// Pinta el estado de cada tecla: encendida mientras se la pulsa y del color
        /// de la misión si la misión activa la pide.
        ///
        /// El orden importa: pulsarla gana al destacado, porque es la respuesta a lo
        /// que el niño/a acaba de hacer, y sin eso la tecla que la misión pide no
        /// responde justo cuando se la prueba.
        ///
        /// Recibe de quién preguntar para poder probarse sin teclado.
        /// </summary>
        private void Repintar(Func<Key, bool> pulsada)
        {
            foreach (Bloque bloque in _bloques)
            {
                bool destacada = _loQuePideLaMision.Contains(bloque.Accion);

                foreach (Cara cara in bloque.Caras)
                {
                    bool ahora = false;
                    foreach (Key tecla in cara.Teclas)
                        if (pulsada(tecla)) { ahora = true; break; }

                    Pinta pinta = ahora     ? Pinta.Pulsada
                                : destacada ? Pinta.Destacada
                                            : Pinta.Reposo;

                    if (cara.Pinta == pinta) continue;
                    cara.Pinta = pinta;
                    Pintar(cara, pinta);
                }

                if (bloque.Etiqueta != null && bloque.EtiquetaDestacada != destacada)
                {
                    bloque.EtiquetaDestacada = destacada;
                    bloque.Etiqueta.color = destacada ? Col.Destacada : Col.Etiqueta;
                }
            }
        }

        private static void Pintar(Cara cara, Pinta pinta)
        {
            Color tapa, simbolo;

            switch (pinta)
            {
                case Pinta.Pulsada:
                    tapa = Col.TeclaPulsada; simbolo = Col.Letra; break;

                case Pinta.Destacada:
                    tapa = Col.Destacada; simbolo = Col.DestacadaLetra; break;

                default:
                    tapa = Col.Tecla; simbolo = Col.Letra; break;
            }

            if (cara.Fondo != null)
            {
                cara.Fondo.color = tapa;

                // El latido lo pone Latir() en cada frame mientras la tecla esté
                // destacada; al dejar de estarlo hay que devolverla a su tamaño o se
                // queda congelada a medio latido.
                if (pinta != Pinta.Destacada) cara.Fondo.rectTransform.localScale = Vector3.one;
            }

            if (cara.Letra != null) cara.Letra.color = simbolo;
            if (cara.Flecha != null) cara.Flecha.color = simbolo;
        }

        /// <summary>
        /// Hace latir las teclas que la misión pide. Es el mismo latido de la flecha
        /// que señala la zona, y con el mismo sentido: en esta pantalla, lo que se
        /// mueve es lo que la misión está pidiendo.
        ///
        /// En tiempo sin escalar, como todo lo de esta pieza: si alguna vez late con
        /// el juego a cámara lenta, no tiene por qué latir a cámara lenta.
        /// </summary>
        private void Latir()
        {
            if (_loQuePideLaMision.Count == 0) return;

            float latido = 1f + Med.AmplitudLatido *
                           Mathf.Sin(Time.unscaledTime * Med.VelocidadLatido);

            foreach (Bloque bloque in _bloques)
            {
                if (!_loQuePideLaMision.Contains(bloque.Accion)) continue;

                foreach (Cara cara in bloque.Caras)
                {
                    if (cara.Pinta != Pinta.Destacada || cara.Fondo == null) continue;
                    cara.Fondo.rectTransform.localScale = Vector3.one * latido;
                }
            }
        }

        // ── Lo que pide la misión activa ──────────────────────────────────────

        /// <summary>
        /// Cuánto se queda el recordatorio cuando sale, en segundos reales. Lo justo
        /// para mirar qué tecla late y seguir jugando; si se queda más, deja de ser un
        /// recordatorio y vuelve a ser el cartel permanente que ya nadie necesita.
        /// </summary>
        private const float Recordatorio = 6f;

        /// <summary>
        /// Se engancha a las misiones si todavía no lo estaba, y se reengancha si el
        /// gestor o el rastreador cambiaron —los dos son singletons de escena y la
        /// partida puede recargar—.
        ///
        /// Son los mismos tres avisos que escucha el cartel de misión de la esquina:
        /// el cambio de misión activa, el repintado del panel y el progreso de los
        /// objetivos. No se inventa nada nuevo ni se pregunta cada frame.
        ///
        /// Se mira desde <see cref="Revisar"/>, cada medio segundo, en vez de
        /// engancharse en el OnEnable: esta pieza vive desde la primera escena y los
        /// dos gestores aparecen más tarde. Pedirlos con GetOrCreate los crearía en
        /// el menú principal, donde no pinta nada tener misiones.
        /// </summary>
        private void ApuntarseALasMisiones()
        {
            MissionManager manager = MissionManager.Instance;
            if (manager != _managerApuntado)
            {
                if (_managerApuntado != null)
                {
                    _managerApuntado.onMisionActivaCambiada.RemoveListener(AlCambiarLaMisionActiva);
                    _managerApuntado.onPanelActualizado.RemoveListener(MirarQuePideLaMision);
                }

                _managerApuntado = manager;

                if (manager != null)
                {
                    manager.onMisionActivaCambiada.AddListener(AlCambiarLaMisionActiva);
                    manager.onPanelActualizado.AddListener(MirarQuePideLaMision);
                }

                MirarQuePideLaMision();
            }

            MissionTracker rastreador = MissionTracker.Instance;
            if (rastreador != _rastreadorApuntado)
            {
                if (_rastreadorApuntado != null)
                    _rastreadorApuntado.OnProgresoCambiado -= MirarQuePideLaMision;

                _rastreadorApuntado = rastreador;

                if (rastreador != null)
                    rastreador.OnProgresoCambiado += MirarQuePideLaMision;

                MirarQuePideLaMision();
            }
        }

        private void SoltarLasMisiones()
        {
            if (_managerApuntado != null)
            {
                _managerApuntado.onMisionActivaCambiada.RemoveListener(AlCambiarLaMisionActiva);
                _managerApuntado.onPanelActualizado.RemoveListener(MirarQuePideLaMision);
                _managerApuntado = null;
            }

            if (_rastreadorApuntado != null)
            {
                _rastreadorApuntado.OnProgresoCambiado -= MirarQuePideLaMision;
                _rastreadorApuntado = null;
            }
        }

        /// <summary>
        /// Una misión nueva: se mira qué pide y, si esta tarjeta es la del mundo,
        /// aparece un momento con esas teclas latiendo.
        ///
        /// La de la pausa no se abre sola por esto: la pausa la abre el niño/a cuando
        /// quiere, y una tarjeta que se despliega sola encima del menú sería un susto,
        /// no una ayuda.
        /// </summary>
        private void AlCambiarLaMisionActiva(DesafioRuntime _)
        {
            MirarQuePideLaMision();

            if (_modo == Modo.Recordatorio && _loQuePideLaMision.Count > 0)
                _recordarHasta = Time.unscaledTime + Recordatorio;
        }

        /// <summary>
        /// Qué teclas hacen falta para lo que queda de la misión activa.
        ///
        /// Se miran solo los objetivos PENDIENTES: una misión de tres pasos con dos
        /// hechos tiene que señalar lo que falta, no lo que ya se hizo.
        /// </summary>
        private void MirarQuePideLaMision()
        {
            _loQuePideLaMision.Clear();

            DesafioRuntime activa = MissionManager.Instance != null
                ? MissionManager.Instance.Activa
                : null;

            MissionTracker rastreador = MissionTracker.Instance;
            if (activa == null || rastreador == null) return;

            IReadOnlyList<ObjetivoMision> objetivos = rastreador.Objetivos(activa.Id);
            if (objetivos == null) return;

            foreach (ObjetivoMision objetivo in objetivos)
            {
                if (objetivo == null || objetivo.cumplido) continue;
                _loQuePideLaMision.Add(AccionQueLoCierra(objetivo.tipo));
            }
        }

        /// <summary>
        /// Con qué tecla se cierra un objetivo de cada tipo.
        ///
        /// <b>Caminar solo se destaca cuando el objetivo es llegar a un sitio.</b>
        /// Para los demás también hay que caminar —hasta el NPC, hasta el objeto,
        /// hasta la zona del chat—, pero si se destacara siempre no destacaría nada:
        /// estaría encendido en todas las misiones del juego. Lo que cambia de una
        /// misión a otra, y por tanto lo que vale la pena señalar, es la tecla que
        /// cierra el objetivo.
        ///
        /// Y esa tecla es la E en casi todos los demás, no por casualidad: recoger un
        /// objeto, hablar con un NPC, atender un chat —de celular o cara a cara— y
        /// abrir un caso del Modo Detective se hacen todos con el mismo gesto
        /// —acercarse e interactuar—, porque esas piezas del mundo implementan
        /// <c>IInteractable</c>. La excepción es abrir el panel del celular, que es
        /// justo lo que su tecla hace.
        /// </summary>
        private static Accion AccionQueLoCierra(TipoObjetivo tipo)
        {
            switch (tipo)
            {
                case TipoObjetivo.LlegarAZona:  return Accion.Caminar;
                case TipoObjetivo.AbrirElPanel: return Accion.Celular;
                default:                        return Accion.Hablar;
            }
        }

        // ── De dónde salen las teclas ─────────────────────────────────────────

        /// <summary>
        /// Las teclas de caminar, sacadas de la acción que Otto escucha de verdad.
        ///
        /// La acción lleva dos composites de cuatro partes —WASD primero, flechas
        /// después— más el stick y la cruceta del mando. Se recorren las partes de
        /// teclado y se agrupan por dirección, así que cada cara acaba sabiendo que
        /// "arriba" son la W <i>y</i> la flecha. Las del mando se saltan solas: su
        /// ruta no empieza por &lt;Keyboard&gt;.
        ///
        /// El respaldo es para cuando no hay Otto todavía (o alguien le quitó los
        /// atajos): WASD y flechas, que es lo que el controlador ata hoy. Hay una
        /// prueba del arnés que avisa si ese respaldo y los atajos de verdad dejan de
        /// coincidir.
        /// </summary>
        private static List<List<Key>> TeclasDeCaminar(OttoController otto)
        {
            var porDireccion = new List<List<Key>>
            {
                new List<Key>(), new List<Key>(), new List<Key>(), new List<Key>(),
            };

            InputAction accion = otto != null ? otto.AccionMover : null;
            if (accion != null)
            {
                foreach (InputBinding atadura in accion.bindings)
                {
                    int direccion = IndiceDeDireccion(atadura.name);
                    if (direccion < 0) continue;

                    if (TeclaDeRuta(atadura.path, out Key tecla))
                        porDireccion[direccion].Add(tecla);
                }
            }

            // Respaldo, dirección a dirección: si una se quedó sin teclas de teclado
            // se le pone la de siempre, y las que sí tienen no se tocan.
            Key[] respaldo = { Key.W, Key.A, Key.S, Key.D };
            Key[] flechas  = { Key.UpArrow, Key.LeftArrow, Key.DownArrow, Key.RightArrow };

            for (int i = 0; i < porDireccion.Count; i++)
            {
                if (porDireccion[i].Count > 0) continue;
                porDireccion[i].Add(respaldo[i]);
                porDireccion[i].Add(flechas[i]);
            }

            return porDireccion;
        }

        /// <summary>El orden en el que se dibujan: arriba, izquierda, abajo, derecha.
        /// Los nombres son los que el Input System le da a cada parte del composite
        /// 2DVector.</summary>
        private static int IndiceDeDireccion(string nombreDeLaParte)
        {
            if (string.IsNullOrEmpty(nombreDeLaParte)) return -1;

            switch (nombreDeLaParte.ToLowerInvariant())
            {
                case "up":    return 0;
                case "left":  return 1;
                case "down":  return 2;
                case "right": return 3;
                default:      return -1;
            }
        }

        /// <summary>Las teclas de una acción de un solo botón (correr), sin
        /// las del mando.</summary>
        private static List<Key> TeclasDe(InputAction accion, params Key[] respaldo)
        {
            var teclas = new List<Key>();

            if (accion != null)
                foreach (InputBinding atadura in accion.bindings)
                    if (TeclaDeRuta(atadura.path, out Key tecla)) teclas.Add(tecla);

            if (teclas.Count == 0) teclas.AddRange(respaldo);
            return teclas;
        }

        /// <summary>
        /// "&lt;Keyboard&gt;/leftShift" -&gt; <c>Key.LeftShift</c>. Devuelve false para
        /// cualquier ruta que no sea de teclado, que es como se descartan el stick y
        /// los botones del mando.
        ///
        /// El enum <c>Key</c> usa los mismos nombres que las rutas salvo por la
        /// mayúscula inicial, así que basta con ignorar las mayúsculas al convertir
        /// —el mismo truco, al revés, que usa <c>InteractionDetector</c> para armar
        /// su atadura desde <c>interactKey</c>.
        /// </summary>
        private static bool TeclaDeRuta(string ruta, out Key tecla)
        {
            tecla = Key.None;

            const string Prefijo = "<Keyboard>/";
            if (string.IsNullOrEmpty(ruta) || !ruta.StartsWith(Prefijo, StringComparison.Ordinal))
                return false;

            return Enum.TryParse(ruta.Substring(Prefijo.Length), ignoreCase: true, out tecla) &&
                   tecla != Key.None;
        }

        /// <summary>La tecla de interactuar, leída del detector que la escucha. Es
        /// pública y ya se usa así para escribir la letra del cartel de "para
        /// interactuar".</summary>
        private static Key TeclaDeInteractuar()
        {
            InteractionDetector detector = FindAnyObjectByType<InteractionDetector>();
            return detector != null ? detector.interactKey : Key.E;
        }

        /// <summary>Cómo se llama una tecla en la tarjeta. Las letras van solas; las
        /// demás llevan nombre en castellano y corto, que es lo que cabe en una
        /// tecla.</summary>
        private static string EtiquetaDe(Key tecla)
        {
            switch (tecla)
            {
                case Key.Space:      return Txt.Espacio;
                case Key.LeftShift:
                case Key.RightShift: return Txt.Shift;
                case Key.Escape:     return Txt.Escape;
                case Key.Tab:        return Txt.Tab;
                default:             return tecla.ToString();
            }
        }

        // ── La tarjeta ────────────────────────────────────────────────────────

        /// <summary>
        /// Arma la tarjeta entera. Todo a mano y en código, como el resto de la UI de
        /// este proyecto.
        ///
        /// Se coloca por coordenadas y no con grupos de layout porque cada fila tiene
        /// su propia forma —la cruz de caminar ocupa dos pisos y las demás uno— y
        /// porque así el arnés puede medir dónde queda cada cosa sin montar el canvas.
        ///
        /// Las dos diferencias entre los modos están aquí y no repartidas por la
        /// clase: de quién cuelga la tarjeta y dónde se pone (centrada y grande en la
        /// pausa, en una esquina y a tamaño 1 como recordatorio), y si lleva el botón
        /// de «Volver», que solo tiene sentido en algo que se cierra a mano.
        /// </summary>
        private void Construir()
        {
            Transform padre = _modo == Modo.Panel ? _raiz.transform : CanvasPropio();

            var tarjetaGO = new GameObject("Tarjeta", typeof(RectTransform), typeof(Image));
            tarjetaGO.transform.SetParent(padre, false);
            _tarjeta = (RectTransform)tarjetaGO.transform;

            if (_modo == Modo.Panel)
            {
                _tarjeta.anchorMin = _tarjeta.anchorMax = _tarjeta.pivot = new Vector2(0.5f, 0.5f);
                _tarjeta.anchoredPosition = Vector2.zero;
                _tarjeta.localScale = Vector3.one * Med.Escala;
            }
            else
            {
                _tarjeta.anchorMin = _tarjeta.anchorMax = _tarjeta.pivot = Vector2.zero;
                _tarjeta.anchoredPosition = Med.MargenRecordatorio;
            }

            var fondo = tarjetaGO.GetComponent<Image>();
            FishyUIKit.FondoRedondeado(fondo, Col.Panel, radio: Med.RadioPanel);

            // Como recordatorio es un cartel, no un botón: no debe robarle el clic a
            // nada del mundo. En la pausa sí lo para, que es lo que hace de telón.
            fondo.raycastTarget = _modo == Modo.Panel;

            float ancho = Med.AnchoPanel - 2f * Med.Padding;
            float y = -Med.Padding;

            Texto(_tarjeta, "Titulo", Txt.Titulo, Fnt.Titulo, Col.Titulo,
                FishyUIKit.Titulos, Med.Padding, y, ancho, Fnt.Titulo * 1.2f);

            y -= Fnt.Titulo * 1.2f + Med.Separacion;

            y = FilaDeCaminar(y);
            y = Fila(y, Accion.Hablar, new List<List<Key>> { new List<Key> { TeclaDeInteractuar() } });

            y = Fila(y, Accion.Correr, new List<List<Key>>
            {
                TeclasDe(_otto != null ? _otto.AccionCorrer : null, Key.LeftShift, Key.RightShift),
            });

            y = FilaDoble(y,
                Accion.Celular, new List<Key> { MenuController.TeclaDelCelular },
                Accion.Pausa,   new List<Key> { MenuPausa.TeclaDePausa });

            if (_modo == Modo.Panel)
            {
                // «Volver», a lo ancho como en la tarjeta de configuración.
                y -= Med.Separacion;
                Button volver = FishyUIKit.Boton(_tarjeta, Txt.Volver, Paleta.Verde,
                    Fnt.Boton, Med.AltoBoton, () => _alVolver?.Invoke());
                volver.navigation = new Navigation { mode = Navigation.Mode.None };

                var rtVolver = (RectTransform)volver.transform;
                rtVolver.anchorMin = rtVolver.anchorMax = rtVolver.pivot = new Vector2(0f, 1f);
                rtVolver.sizeDelta = new Vector2(ancho, Med.AltoBoton);
                rtVolver.anchoredPosition = new Vector2(Med.Padding, y);
                y -= Med.AltoBoton;
            }

            _tarjeta.sizeDelta = new Vector2(Med.AnchoPanel, -y + Med.Padding);

            // El recordatorio nace apagado: lo enciende Revisar cuando toca. El de la
            // pausa lo apaga CrearEn, que es quien sabe que todavía no se ha pedido.
            if (_modo == Modo.Recordatorio) _raiz.SetActive(false);
        }

        /// <summary>
        /// El canvas del recordatorio, que es suyo y de nadie más: no cuelga de
        /// ninguna escena, porque la tarjeta sobrevive a los cambios de zona.
        ///
        /// El de la pausa no pasa por aquí: se le da el canvas del menú ya hecho, con
        /// su velo y su orden de dibujo.
        /// </summary>
        private Transform CanvasPropio()
        {
            UiBootstrap.EnsureEventSystem();

            var canvasGO = new GameObject("TecladoRecordatorioCanvas",
                typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(transform, false);
            _raiz = canvasGO;

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = TecladoEnPantallaTheme.OrdenDeDibujo;

            var escalador = canvasGO.GetComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920f, 1080f);
            escalador.matchWidthOrHeight = 0.5f;

            return canvasGO.transform;
        }

        /// <summary>
        /// La cruz de caminar: la W arriba y A S D debajo, cada una con su
        /// triangulito. Es la única fila con forma propia, y se la gana: la cruz
        /// <i>es</i> la explicación, un niño/a que no lee rápido la entiende de un
        /// vistazo.
        /// </summary>
        private float FilaDeCaminar(float y)
        {
            var bloque = new Bloque { Accion = Accion.Caminar };
            RectTransform grupo = Grupo(bloque, "Caminar", y);

            List<List<Key>> teclas = TeclasDeCaminar(_otto);

            float paso = Med.LadoTecla + Med.Hueco;
            float anchoCruz = 3f * Med.LadoTecla + 2f * Med.Hueco;
            float altoCruz = 2f * Med.LadoTecla + Med.Hueco;

            // Columna y piso de cada dirección dentro de la cruz, en el orden en que
            // las devuelve TeclasDeCaminar: arriba, izquierda, abajo, derecha.
            int[] columnas = { 1, 0, 1, 2 };
            int[] pisos    = { 0, 1, 1, 1 };
            float[] giros  = { 90f, 180f, 270f, 0f };   // el triángulo nace mirando a la derecha

            for (int i = 0; i < teclas.Count; i++)
            {
                Cara cara = Tecla(grupo, teclas[i], EtiquetaDe(teclas[i][0]),
                    Med.Padding + columnas[i] * paso, -pisos[i] * paso, giros[i]);
                bloque.Caras.Add(cara);
            }

            float x = Med.Padding + anchoCruz + Med.Hueco * 2f;
            float ancho = Med.AnchoPanel - Med.Padding - x;

            bloque.Etiqueta = Texto(grupo, "Etiqueta", Txt.Caminar, Fnt.Etiqueta, Col.Etiqueta,
                FishyUIKit.Cuerpo, x, -altoCruz / 2f + 34f, ancho, 34f);

            Texto(grupo, "Pista", Txt.CaminarPista, Fnt.Pista, Col.Pista,
                FishyUIKit.Cuerpo, x, -altoCruz / 2f, ancho, 28f);

            _bloques.Add(bloque);
            return y - altoCruz - Med.Separacion;
        }

        /// <summary>Una fila corriente: sus teclas a la izquierda y lo que hacen a la
        /// derecha.</summary>
        private float Fila(float y, Accion accion, List<List<Key>> teclas)
        {
            var bloque = new Bloque { Accion = accion };
            RectTransform grupo = Grupo(bloque, accion.ToString(), y);

            float x = Dibujar(grupo, bloque, teclas, Med.Padding);
            bloque.Etiqueta = Texto(grupo, "Etiqueta", TextoDe(accion), Fnt.Etiqueta, Col.Etiqueta,
                FishyUIKit.Cuerpo, x, -Med.LadoTecla / 2f + 17f,
                Med.AnchoPanel - Med.Padding - x, 34f);

            _bloques.Add(bloque);
            return y - Med.LadoTecla - Med.Separacion;
        }

        /// <summary>
        /// Dos acciones en la misma fila. Las cuatro que quedan son de una tecla y un
        /// nombre corto, y apiladas de una en una la tarjeta se comía media pantalla
        /// de un equipo de 768 px de alto.
        /// </summary>
        private float FilaDoble(float y, Accion izquierda, List<Key> teclasIzquierda,
            Accion derecha, List<Key> teclasDerecha)
        {
            float ancho = Med.AnchoPanel - 2f * Med.Padding;
            float primera = ancho * Med.RepartoFilaDoble;

            MedioBloque(y, izquierda, teclasIzquierda, Med.Padding, primera);
            MedioBloque(y, derecha, teclasDerecha, Med.Padding + primera, ancho - primera);

            return y - Med.LadoTecla - Med.Separacion;
        }

        private void MedioBloque(float y, Accion accion, List<Key> teclas, float x, float ancho)
        {
            var bloque = new Bloque { Accion = accion };
            RectTransform grupo = Grupo(bloque, accion.ToString(), y);

            // Las alternativas de una misma acción —los dos Shift— son una sola cara:
            // enseñar dos teclas que hacen lo mismo solo confunde.
            float tras = Dibujar(grupo, bloque, new List<List<Key>> { teclas }, x);

            bloque.Etiqueta = Texto(grupo, "Etiqueta", TextoDe(accion), Fnt.Etiqueta, Col.Etiqueta,
                FishyUIKit.Cuerpo, tras, -Med.LadoTecla / 2f + 17f,
                x + ancho - tras, 34f);

            _bloques.Add(bloque);
        }

        /// <summary>Dibuja las caras de una fila una detrás de otra y devuelve dónde
        /// termina la última.</summary>
        private float Dibujar(RectTransform grupo, Bloque bloque, List<List<Key>> caras, float x)
        {
            foreach (List<Key> teclas in caras)
            {
                Cara cara = Tecla(grupo, teclas, EtiquetaDe(teclas[0]), x, 0f, null);
                bloque.Caras.Add(cara);
                x += cara.Fondo.rectTransform.sizeDelta.x + Med.Hueco;
            }

            // Un hueco más antes del nombre: pegado a la tecla se lee como si fuera
            // parte de ella.
            return x + Med.Hueco;
        }

        /// <summary>El contenedor de una fila.</summary>
        private RectTransform Grupo(Bloque bloque, string nombre, float y)
        {
            var go = new GameObject("Fila_" + nombre, typeof(RectTransform));
            go.transform.SetParent(_tarjeta, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(Med.AnchoPanel, 0f);

            return rt;
        }

        /// <summary>
        /// Una tecla: la cara redondeada, su letra y, si se le pasa un giro, el
        /// triangulito de la dirección.
        ///
        /// La flecha va dibujada y no escrita por lo mismo que el aspa de cerrar y el
        /// símbolo de pausa: ↑↓←→ no están en ninguna de las dos fuentes del juego y
        /// saldrían como cuadros huecos. El triángulo ya existe en el kit
        /// (<see cref="FishyUIKit.SpriteTriangulo"/>), nace mirando a la derecha y se
        /// gira.
        /// </summary>
        private Cara Tecla(RectTransform padre, List<Key> teclas, string etiqueta,
            float x, float y, float? giro)
        {
            var cara = new Cara();
            cara.Teclas.AddRange(teclas);

            var go = new GameObject("Tecla_" + etiqueta, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);

            float ancho = Mathf.Max(Med.LadoTecla,
                etiqueta.Length * Fnt.Letra * Med.AnchoPorLetra + Med.PaddingTecla);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(ancho, Med.LadoTecla);
            rt.anchoredPosition = new Vector2(x, y);

            cara.Fondo = go.GetComponent<Image>();
            FishyUIKit.FondoRedondeado(cara.Fondo, Col.Tecla, radio: Med.RadioTecla);
            cara.Fondo.raycastTarget = false;

            TextMeshProUGUI letra = FishyUIKit.Texto(go.transform, "Letra", etiqueta,
                Fnt.Letra, Col.Letra, TextAlignmentOptions.Center);
            letra.font = FishyUIKit.Cuerpo;   // "Espacio" en Mango no cabe ni se lee
            letra.textWrappingMode = TextWrappingModes.NoWrap;
            letra.raycastTarget = false;
            FishyUIKit.Estirar(letra.rectTransform);
            cara.Letra = letra;

            if (giro.HasValue)
            {
                // La letra sube un poco para hacerle sitio a la flecha.
                letra.rectTransform.anchoredPosition = new Vector2(0f, Med.BajadaFlecha / 2f);
                cara.Flecha = Flecha(go.transform, giro.Value);
            }

            return cara;
        }

        private Image Flecha(Transform padre, float giro)
        {
            var go = new GameObject("Flecha", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Med.LadoFlecha, Med.LadoFlecha);
            rt.anchoredPosition = new Vector2(0f, -Med.BajadaFlecha);
            rt.localRotation = Quaternion.Euler(0f, 0f, giro);

            var img = go.GetComponent<Image>();
            img.sprite = FishyUIKit.SpriteTriangulo();
            img.color = Col.Letra;
            img.raycastTarget = false;

            return img;
        }

        /// <summary>Un texto colocado por su esquina de arriba a la izquierda.</summary>
        private static TextMeshProUGUI Texto(RectTransform padre, string nombre, string contenido,
            float tamano, Color color, TMP_FontAsset fuente, float x, float y, float ancho,
            float alto)
        {
            TextMeshProUGUI texto = FishyUIKit.Texto(padre, nombre, contenido, tamano, color,
                TextAlignmentOptions.Left);

            if (fuente != null) texto.font = fuente;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.raycastTarget = false;

            RectTransform rt = texto.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(ancho, alto);
            rt.anchoredPosition = new Vector2(x, y);

            return texto;
        }

        private static string TextoDe(Accion accion)
        {
            switch (accion)
            {
                case Accion.Caminar: return Txt.Caminar;
                case Accion.Hablar:  return Txt.Hablar;
                case Accion.Correr:  return Txt.Correr;
                case Accion.Celular: return Txt.Celular;
                default:             return Txt.Pausa;
            }
        }
    }
}
