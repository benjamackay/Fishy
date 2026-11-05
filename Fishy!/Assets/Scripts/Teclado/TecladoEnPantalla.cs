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
    /// El dibujo de un teclado, abajo a la derecha, con las teclas del juego
    /// iluminadas en el sitio exacto en el que están en el teclado de verdad.
    ///
    /// <b>Por qué hace falta.</b> La primera misión dice «habla con Huemul» y nada
    /// más. Para cumplirla hay que descubrir solo tres cosas: que se camina con WASD
    /// o las flechas, que hay que llegar hasta él y que se interactúa con E. La única
    /// pista que existía era el cartel de «E para interactuar», que aparece cuando ya
    /// estás pegado al NPC: las teclas de caminar no se enseñaban en ninguna parte.
    ///
    /// <b>Por qué el teclado entero y no una lista de teclas sueltas.</b> Un niño/a
    /// de 9 años que no sabe escribir a máquina no busca «la tecla W»: busca un sitio
    /// en un objeto que tiene delante. Una ficha con una W no le dice dónde mirar; el
    /// teclado dibujado entero sí, porque reconoce la forma —la barra larga de abajo,
    /// la esquina de arriba a la izquierda, el bloque de flechas— y desde ahí ubica
    /// lo demás. Por eso se dibujan también las teclas que el juego no usa: apagadas,
    /// sin competir, pero ahí, que son las que dan el mapa. Debajo del dibujo va la
    /// lista de qué hace cada una.
    ///
    /// <b>Enseña, no juega.</b> Las teclas se iluminan cuando se las pulsa, pero no
    /// se pueden pulsar con el ratón ni con el dedo. Es a propósito: el público son
    /// salas de computación con teclado, y mandar entrada desde aquí obligaría a
    /// apuntarse a la <see cref="PuertaDeEntrada"/> y a pensar qué pasa con cada
    /// bloqueo de los que ya existen. El juego táctil tiene su propia pieza,
    /// <see cref="OttoOnScreenButton"/>, que sigue sin cablearse en ninguna escena;
    /// encenderla es otra decisión y merece su propia historia de usuario.
    ///
    /// <b>Se va solo.</b> Cada tecla se apaga en cuanto el niño/a la usa, y cuando
    /// las ha usado todas el dibujo desaparece y no vuelve: lo aprendido queda en
    /// PlayerPrefs, del aparato y no de la cuenta, igual que
    /// <see cref="ConfiguracionDelJuego"/>. Una ayuda que no se sabe cuándo callarse
    /// termina siendo adorno que se tapa con la mano.
    ///
    /// <b>Las teclas no están escritas aquí.</b> Se le preguntan a quien las escucha
    /// —<see cref="OttoController"/>, <see cref="InteractionDetector"/>,
    /// <see cref="MenuPausa"/> y <see cref="MenuController"/>—, que es el criterio
    /// que ya seguía el cartel de la E. Un dibujo que enseña teclas equivocadas es
    /// peor que no tener dibujo.
    /// </summary>
    [DisallowMultipleComponent]
    public class TecladoEnPantalla : MonoBehaviour
    {
        /// <summary>Dónde se recuerda lo que el niño/a ya se sabe. Comparte prefijo
        /// con los ajustes para que todo lo de este aparato se encuentre junto.</summary>
        public const string ClaveAprendidas = ConfiguracionDelJuego.Prefijo + "TeclasAprendidas";

        /// <summary>Cada cuánto se revisa si toca mostrarse, en segundos sin escalar
        /// (con la pausa puesta el tiempo normal no corre). Mismo motivo y mismo
        /// valor que en <see cref="BotonDePausaEnPantalla"/>: la revisión busca cosas
        /// en la escena y no hace falta hacerlo cada frame.</summary>
        private const float CadaCuanto = 0.5f;

        /// <summary>
        /// Lo que se espera, en segundos reales, desde que se apaga la última tecla
        /// hasta que el dibujo se va. Sin esta pausa el niño/a nunca ve apagarse la
        /// que acaba de aprender: desaparecería todo en el mismo frame y parecería
        /// que se rompió algo.
        ///
        /// Son segundos <b>a la vista</b>: la cuenta vuelve a empezar cada vez que el
        /// dibujo reaparece (ver <see cref="Revisar"/>). Si no, pasaba esto: la última
        /// tecla que queda por usar suele ser Esc —abre el menú de pausa—, así que al
        /// pulsarla el dibujo se escondía, el reloj seguía corriendo con el menú
        /// abierto y al reanudar la despedida ya estaba vencida. El niño/a veía
        /// desaparecer el teclado sin haber visto nada: pulsó Esc y lo liquidó.
        /// </summary>
        private const float Despedida = 2.5f;

        // ── Lo que se enseña ──────────────────────────────────────────────────

        /// <summary>Las acciones que el dibujo explica. El orden es el de la lista de
        /// abajo y el id es lo que se guarda en PlayerPrefs, así que no se renombran
        /// a la ligera: un id que cambia hace que quien ya aprendió vuelva a verlo.</summary>
        private enum Accion { Caminar, Hablar, Correr, Saltar, Celular, Pausa }

        /// <summary>Cómo está pintada una tecla ahora mismo. Se guarda para no
        /// reescribir los colores en cada frame.</summary>
        private enum Pinta { Sin, Encendida, Aprendida, Destacada, Pulsada }

        /// <summary>Una tecla iluminada del dibujo: su tapa, lo que lleva escrito o
        /// dibujado encima, y qué tecla de verdad es.</summary>
        private class Cara
        {
            public Key Tecla;
            public Image Fondo;
            public Graphic Simbolo;      // la letra, o el triángulo de las flechas
            public Pinta Pinta = Pinta.Sin;
        }

        /// <summary>Una acción: lo que hace, con qué teclas, su renglón en la lista y
        /// si ya se aprendió.</summary>
        private class Bloque
        {
            public Accion Accion;
            public readonly List<Key> Teclas = new List<Key>();
            public readonly List<Cara> Caras = new List<Cara>();
            public TMP_Text Leyenda;
            public Pinta PintaDeLaLeyenda = Pinta.Sin;
            public bool Aprendida;
        }

        private readonly List<Bloque> _bloques = new List<Bloque>();

        /// <summary>De tecla a la acción que la usa, para saber al dibujar el teclado
        /// cuáles hay que encender.</summary>
        private readonly Dictionary<Key, Bloque> _porTecla = new Dictionary<Key, Bloque>();

        /// <summary>Dónde quedó cada tecla en el dibujo: fila y columna. Sirve para
        /// nombrarlas en la lista de abajo en el orden en que se ven, que es el único
        /// que un niño/a puede seguir con el dedo.</summary>
        private readonly Dictionary<Key, Vector2> _sitio = new Dictionary<Key, Vector2>();

        private GameObject _raiz;
        private RectTransform _tarjeta;
        private OttoController _otto;
        private MenuController _celular;
        private float _proximaRevision;
        private bool _todoAprendido;
        private float _momentoEnQueSeAprendioTodo;
        private bool _yaNoHaceFalta;
        private string _ultimoDiagnostico;

        /// <summary>Lo que la misión activa necesita ahora mismo. Esas teclas se
        /// pintan del color de la misión y laten.</summary>
        private readonly HashSet<Accion> _loQuePideLaMision = new HashSet<Accion>();

        /// <summary>Hasta cuándo se enseña el recordatorio de una misión nueva, en
        /// tiempo sin escalar. 0 = no hay ninguno en curso.</summary>
        private float _recordarHasta;

        private MissionManager _managerApuntado;
        private MissionTracker _rastreadorApuntado;

        // ── Ciclo de vida ─────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (FindAnyObjectByType<TecladoEnPantalla>() != null) return;
            new GameObject("TecladoEnPantalla").AddComponent<TecladoEnPantalla>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            LeerLoAprendido();
        }

        /// <summary>
        /// Mira si en este aparato ya se aprendieron todas: entonces no hay nada que
        /// enseñar y el dibujo no llega ni a construirse.
        ///
        /// Está fuera del Awake para que el arnés de pruebas pueda llamarlo: el Awake
        /// hace además <c>DontDestroyOnLoad</c>, que fuera del modo Play no avisa
        /// sino que tira excepción.
        /// </summary>
        private void LeerLoAprendido() => _yaNoHaceFalta = TodoAprendido(Leer());

        /// <summary>
        /// Lo que el dibujo está enseñando ahora mismo: para cada acción, todas las
        /// teclas que acepta.
        ///
        /// Existe para el arnés de pruebas, que compara esto con lo que el juego
        /// escucha de verdad. Es la forma de que «el dibujo no miente» sea algo que
        /// se comprueba y no algo que se promete en un comentario.
        /// </summary>
        public Dictionary<string, List<Key>> LoQueEnsena()
        {
            var porAccion = new Dictionary<string, List<Key>>();
            foreach (Bloque bloque in _bloques)
                porAccion[bloque.Accion.ToString()] = new List<Key>(bloque.Teclas);

            return porAccion;
        }

        private void Update()
        {
            if (Time.unscaledTime >= _proximaRevision)
            {
                _proximaRevision = Time.unscaledTime + CadaCuanto;
                Revisar();
            }

            if (_raiz == null || !_raiz.activeSelf) return;

            Repintar(EstaPulsada);
            Latir();
        }

        private void OnDestroy() => SoltarLasMisiones();

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
        /// Enciende o apaga el dibujo según si ahora toca enseñar algo.
        ///
        /// Se esconde cuando no aporta —en pausa, durante una cinemática o una carga,
        /// con el celular abierto encima— y cuando no hay teclado que enseñar: en una
        /// tablet, <c>Keyboard.current</c> es null y un dibujo de teclas sería un
        /// chiste malo.
        ///
        /// <b>Y se esconde mientras Otto no puede caminar</b>, que es como el resto
        /// del código pregunta «¿hay un diálogo, un chat o el Modo Detective en
        /// medio?» (ver <c>MenuController.PuedeAbrir</c>). Aquí no es por elegancia:
        /// el dibujo es grande y el panel de diálogo le queda encima. Se pierde poco,
        /// porque para cuando hay un diálogo abierto la E ya se pulsó —es la que lo
        /// empezó— y su renglón ya está apagado.
        /// </summary>
        private void Revisar()
        {
            if (_otto == null) _otto = FindAnyObjectByType<OttoController>();
            if (_celular == null) _celular = FindAnyObjectByType<MenuController>();

            ApuntarseALasMisiones();

            // Con todo aprendido el dibujo ya no sale, salvo mientras dure el
            // recordatorio de una misión recién empezada.
            bool quiereVerse = !_yaNoHaceFalta || Time.unscaledTime < _recordarHasta;

            // Se construye con Otto delante y no en el Awake: las teclas se las
            // preguntamos a él, y en la escena del menú todavía no existe.
            if (quiereVerse && _raiz == null && _otto != null && Keyboard.current != null)
                Construir();

            bool mostrar = quiereVerse &&
                           _otto != null &&
                           _otto.movementEnabled &&
                           Keyboard.current != null &&
                           !EstadoDePausa.Pausado &&
                           EstadoDePausa.SePuedePausar() &&
                           (_celular == null || !_celular.EstaAbierto);

            EmitirDiagnostico(quiereVerse, mostrar);

            if (_raiz == null || _raiz.activeSelf == mostrar) return;

            _raiz.SetActive(mostrar);

            // Al volver a la vista, la cuenta para despedirse empieza de nuevo: lo
            // que pasó mientras estaba escondido —el menú de pausa, el celular, una
            // conversación— no cuenta como despedida, porque no se vio.
            if (mostrar) _momentoEnQueSeAprendioTodo = Time.unscaledTime;
        }

        private void EmitirDiagnostico(bool quiereVerse, bool mostrar)
        {
            bool puedePausar = EstadoDePausa.SePuedePausar(out string motivoPausa);
            bool celularAbierto = _celular != null && _celular.EstaAbierto;
            string diagnostico =
                $"quiereVerse={quiereVerse}, " +
                $"yaNoHaceFalta={_yaNoHaceFalta}, " +
                $"otto={_otto != null}, " +
                $"movimiento={_otto != null && _otto.movementEnabled}, " +
                $"teclado={Keyboard.current != null}, " +
                $"pausado={EstadoDePausa.Pausado}, " +
                $"puedePausar={puedePausar}, " +
                $"motivoPausa='{motivoPausa}', " +
                $"celularAbierto={celularAbierto}, " +
                $"raiz={(_raiz == null ? "null" : _raiz.activeSelf.ToString())}, " +
                $"mostrar={mostrar}";

            if (diagnostico == _ultimoDiagnostico) return;
            _ultimoDiagnostico = diagnostico;
            Debug.Log($"[Teclado] {diagnostico}", this);
        }

        /// <summary>
        /// Pinta el estado de cada tecla: encendida mientras se la pulsa, del color
        /// de la misión si la misión activa la pide, y apagada —ella y su renglón de
        /// la lista— en cuanto se usó por primera vez.
        ///
        /// El orden importa: pulsarla gana a todo, porque es la respuesta a lo que el
        /// niño/a acaba de hacer; y lo que la misión pide gana a «ya te la sabes»,
        /// porque saberse una tecla no quita que ahora mismo sea la que hace falta.
        ///
        /// Recibe de quién preguntar para poder probarse sin teclado.
        /// </summary>
        private void Repintar(Func<Key, bool> pulsada)
        {
            bool todas = _bloques.Count > 0;

            foreach (Bloque bloque in _bloques)
            {
                bool algunaPulsada = false;

                foreach (Cara cara in bloque.Caras)
                    if (pulsada(cara.Tecla)) { algunaPulsada = true; break; }

                if (algunaPulsada && !bloque.Aprendida) Aprender(bloque);

                bool destacada = _loQuePideLaMision.Contains(bloque.Accion);

                foreach (Cara cara in bloque.Caras)
                {
                    Pinta pinta = pulsada(cara.Tecla) ? Pinta.Pulsada
                                : destacada           ? Pinta.Destacada
                                : bloque.Aprendida    ? Pinta.Aprendida
                                                      : Pinta.Encendida;

                    if (cara.Pinta == pinta) continue;
                    cara.Pinta = pinta;
                    Pintar(cara, pinta);
                }

                PintarLeyenda(bloque, destacada);

                todas &= bloque.Aprendida;
            }

            if (!todas) { _todoAprendido = false; return; }

            // Mientras dure el recordatorio de una misión nueva el dibujo se queda,
            // aunque esté todo aprendido: justo para eso volvió a salir.
            if (Time.unscaledTime < _recordarHasta) return;

            // Un respiro antes de irse, para que se vea apagarse la última. El
            // "¿ya empezó la cuenta?" va en su propio banderín y no en un 0 que
            // signifique "todavía no": Time.unscaledTime vale 0 fuera del modo Play,
            // y con el 0 de centinela la cuenta no arrancaba nunca.
            if (!_todoAprendido)
            {
                _todoAprendido = true;
                _momentoEnQueSeAprendioTodo = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _momentoEnQueSeAprendioTodo < Despedida) return;

            _yaNoHaceFalta = true;
            if (_raiz != null) _raiz.SetActive(false);
        }

        private static void Pintar(Cara cara, Pinta pinta)
        {
            Color tapa, simbolo;

            switch (pinta)
            {
                case Pinta.Pulsada:
                    tapa = Col.Pulsada; simbolo = Col.PulsadaLetra; break;

                case Pinta.Destacada:
                    tapa = Col.Destacada; simbolo = Col.DestacadaLetra; break;

                default:
                    tapa = Col.Marcada; simbolo = Col.MarcadaLetra; break;
            }

            if (pinta == Pinta.Aprendida)
            {
                tapa.a    *= Med.AlfaAprendida;
                simbolo.a *= Med.AlfaAprendida;
            }

            if (cara.Fondo != null)
            {
                cara.Fondo.color = tapa;

                // El latido lo pone Latir() en cada frame mientras la tecla esté
                // destacada; al dejar de estarlo hay que devolverla a su tamaño o se
                // queda congelada a medio latido.
                if (pinta != Pinta.Destacada) cara.Fondo.rectTransform.localScale = Vector3.one;
            }

            if (cara.Simbolo != null) cara.Simbolo.color = simbolo;
        }

        /// <summary>
        /// El renglón de la lista, del color de la misión cuando esa tecla es la que
        /// hace falta ahora. Sin esto solo latería la tecla, y el niño/a vería moverse
        /// algo sin saber para qué sirve.
        /// </summary>
        private static void PintarLeyenda(Bloque bloque, bool destacada)
        {
            Pinta pinta = destacada        ? Pinta.Destacada
                        : bloque.Aprendida ? Pinta.Aprendida
                                           : Pinta.Encendida;

            if (bloque.PintaDeLaLeyenda == pinta || bloque.Leyenda == null) return;
            bloque.PintaDeLaLeyenda = pinta;

            bloque.Leyenda.color = destacada ? Col.Destacada : Col.Leyenda;
            bloque.Leyenda.alpha = pinta == Pinta.Aprendida ? Med.AlfaAprendida : 1f;
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

        private void Aprender(Bloque bloque)
        {
            bloque.Aprendida = true;   // de apagarlo se encarga PintarLeyenda

            HashSet<string> sabidas = Leer();
            sabidas.Add(bloque.Accion.ToString());
            Guardar(sabidas);
        }

        // ── Lo aprendido, entre sesiones ──────────────────────────────────────

        private static HashSet<string> Leer()
        {
            var sabidas = new HashSet<string>();
            string guardado = PlayerPrefs.GetString(ClaveAprendidas, "");

            foreach (string trozo in guardado.Split(','))
                if (!string.IsNullOrWhiteSpace(trozo)) sabidas.Add(trozo.Trim());

            return sabidas;
        }

        private static void Guardar(HashSet<string> sabidas)
        {
            PlayerPrefs.SetString(ClaveAprendidas, string.Join(",", sabidas));
            PlayerPrefs.Save();
        }

        private static bool TodoAprendido(HashSet<string> sabidas)
        {
            foreach (Accion accion in Enum.GetValues(typeof(Accion)))
                if (!sabidas.Contains(accion.ToString())) return false;

            return true;
        }

        /// <summary>Vuelve a enseñarlo todo desde cero. Para el arnés de pruebas y
        /// para poder mirar el dibujo otra vez sin borrar los PlayerPrefs enteros,
        /// que se llevarían por delante el volumen y la pantalla completa.</summary>
        public static void Olvidar()
        {
            PlayerPrefs.DeleteKey(ClaveAprendidas);
            PlayerPrefs.Save();
        }

        // ── Lo que pide la misión activa ──────────────────────────────────────

        /// <summary>
        /// Cuánto se queda el dibujo cuando vuelve a salir porque empezó una misión
        /// nueva, en segundos reales. Lo justo para mirar qué tecla late y seguir
        /// jugando; si se queda más, deja de ser un recordatorio y vuelve a ser un
        /// cartel permanente que ya nadie necesita.
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
        /// Una misión nueva: se mira qué pide y, si el dibujo ya se había retirado
        /// por sabido, vuelve a salir un momento con esas teclas latiendo.
        ///
        /// Es el único caso en el que algo que ya se fue vuelve, y está pensado para
        /// el niño/a que lleva media hora jugando y de pronto recibe una misión de un
        /// tipo que no había tocado: «esto se hace con la E» sin tener que adivinarlo.
        /// </summary>
        private void AlCambiarLaMisionActiva(DesafioRuntime _)
        {
            MirarQuePideLaMision();

            if (_yaNoHaceFalta && _loQuePideLaMision.Count > 0)
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
        /// después— más el stick y la cruceta del mando. Las del mando se saltan
        /// solas: su ruta no empieza por &lt;Keyboard&gt;.
        ///
        /// El respaldo es para cuando no hay Otto todavía (o alguien le quitó los
        /// atajos): WASD y flechas, que es lo que el controlador ata hoy. Hay una
        /// prueba del arnés que avisa si ese respaldo y los atajos de verdad dejan de
        /// coincidir.
        /// </summary>
        private static List<Key> TeclasDeCaminar(OttoController otto)
        {
            return TeclasDe(otto != null ? otto.AccionMover : null,
                Key.W, Key.A, Key.S, Key.D,
                Key.UpArrow, Key.LeftArrow, Key.DownArrow, Key.RightArrow);
        }

        /// <summary>Las teclas de teclado de una acción, sin repetir y sin las del
        /// mando. Si no hay ninguna —porque no hay Otto— se usa el respaldo.</summary>
        private static List<Key> TeclasDe(InputAction accion, params Key[] respaldo)
        {
            var teclas = new List<Key>();

            if (accion != null)
                foreach (InputBinding atadura in accion.bindings)
                    if (TeclaDeRuta(atadura.path, out Key tecla) && !teclas.Contains(tecla))
                        teclas.Add(tecla);

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

        /// <summary>Cómo se llama una tecla cuando hay que escribir su nombre. Las
        /// letras van solas; las demás llevan nombre corto, que es lo que cabe en una
        /// tapa.</summary>
        private static string NombreDe(Key tecla)
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

        private static bool EsFlecha(Key tecla)
            => tecla == Key.UpArrow || tecla == Key.DownArrow ||
               tecla == Key.LeftArrow || tecla == Key.RightArrow;

        // ── La forma del teclado ──────────────────────────────────────────────

        /// <summary>
        /// Una tapa del dibujo: lo que lleva escrito, cuánto mide en unidades de
        /// tecla y, si es una de las del juego, qué tecla es.
        ///
        /// Con <see cref="Texto"/> en null es un hueco y no se dibuja nada: así se
        /// separan el bloque de las F y el de las flechas sin inventar otra tabla.
        /// </summary>
        private readonly struct Tapa
        {
            public readonly string Texto;
            public readonly float Ancho;
            public readonly Key Tecla;

            public Tapa(string texto, float ancho = 1f, Key tecla = Key.None)
            {
                Texto = texto; Ancho = ancho; Tecla = tecla;
            }

            public bool EsHueco => Texto == null;
        }

        private static Tapa Hueco(float ancho) => new Tapa(null, ancho);
        private static Tapa Vacia(float ancho = 1f) => new Tapa("", ancho);

        /// <summary>
        /// El teclado, fila por fila, en unidades de tecla. Es un teclado español de
        /// los que hay en las salas de computación: la Ñ está donde tiene que estar y
        /// el Shift de la izquierda es corto porque a su lado va la tecla del menor
        /// que. Las medidas (1,5 para el tabulador, 6,25 para la barra espaciadora…)
        /// son las de un teclado de verdad, que es de lo que va todo esto.
        ///
        /// Lo que no se dibuja: el bloque de teclas de edición y el numérico. No hay
        /// ninguna tecla del juego ahí, y se llevarían la mitad del ancho.
        ///
        /// Las quince unidades de cada fila tienen que cuadrar entre sí o el teclado
        /// sale escalonado; hay una prueba del arnés que lo comprueba.
        /// </summary>
        private static readonly Tapa[][] Filas =
        {
            new[]
            {
                new Tapa(Txt.Escape, 1f, Key.Escape), Hueco(1f),
                Vacia(), Vacia(), Vacia(), Vacia(), Hueco(0.5f),
                Vacia(), Vacia(), Vacia(), Vacia(), Hueco(0.5f),
                Vacia(), Vacia(), Vacia(), Vacia(),
            },
            new[]
            {
                new Tapa("º"),
                new Tapa("1", 1f, Key.Digit1), new Tapa("2", 1f, Key.Digit2),
                new Tapa("3", 1f, Key.Digit3), new Tapa("4", 1f, Key.Digit4),
                new Tapa("5", 1f, Key.Digit5), new Tapa("6", 1f, Key.Digit6),
                new Tapa("7", 1f, Key.Digit7), new Tapa("8", 1f, Key.Digit8),
                new Tapa("9", 1f, Key.Digit9), new Tapa("0", 1f, Key.Digit0),
                new Tapa("'"), new Tapa("¡"), Vacia(2f),
            },
            new[]
            {
                new Tapa(Txt.Tab, 1.5f, Key.Tab),
                new Tapa("Q", 1f, Key.Q), new Tapa("W", 1f, Key.W), new Tapa("E", 1f, Key.E),
                new Tapa("R", 1f, Key.R), new Tapa("T", 1f, Key.T), new Tapa("Y", 1f, Key.Y),
                new Tapa("U", 1f, Key.U), new Tapa("I", 1f, Key.I), new Tapa("O", 1f, Key.O),
                new Tapa("P", 1f, Key.P), new Tapa("`"), new Tapa("+"),
                Vacia(1.5f),
            },
            new[]
            {
                Vacia(1.75f),
                new Tapa("A", 1f, Key.A), new Tapa("S", 1f, Key.S), new Tapa("D", 1f, Key.D),
                new Tapa("F", 1f, Key.F), new Tapa("G", 1f, Key.G), new Tapa("H", 1f, Key.H),
                new Tapa("J", 1f, Key.J), new Tapa("K", 1f, Key.K), new Tapa("L", 1f, Key.L),
                new Tapa("Ñ"), new Tapa("´"),
                Vacia(2.25f),
            },
            new[]
            {
                new Tapa(Txt.Shift, 1.25f, Key.LeftShift), new Tapa("<"),
                new Tapa("Z", 1f, Key.Z), new Tapa("X", 1f, Key.X), new Tapa("C", 1f, Key.C),
                new Tapa("V", 1f, Key.V), new Tapa("B", 1f, Key.B), new Tapa("N", 1f, Key.N),
                new Tapa("M", 1f, Key.M), new Tapa(","), new Tapa("."), new Tapa("-"),
                new Tapa(Txt.Shift, 2.75f, Key.RightShift),
            },
            new[]
            {
                new Tapa("Ctrl", 1.25f, Key.LeftCtrl), Vacia(1.25f),
                new Tapa("Alt", 1.25f, Key.LeftAlt),
                new Tapa(Txt.Espacio, 6.25f, Key.Space),
                new Tapa("Alt", 1.25f, Key.RightAlt), Vacia(1.25f), Vacia(1.25f),
                new Tapa("Ctrl", 1.25f, Key.RightCtrl),
            },
        };

        /// <summary>Ancho del bloque principal, en unidades. Todas las filas de arriba
        /// suman esto.</summary>
        private const float AnchoBloque = 15f;

        /// <summary>Hueco entre el bloque principal y el de flechas, en unidades.</summary>
        private const float SeparacionFlechas = 0.5f;

        /// <summary>Las cuatro flechas, con su sitio: columna (en unidades, contando
        /// desde el borde del bloque de flechas) y fila del teclado. Van donde van en
        /// un teclado de verdad: la de arriba sola, encima de las otras tres.</summary>
        private static readonly (Key tecla, float columna, int fila, float giro)[] Flechas =
        {
            (Key.UpArrow,    1f, 4,  90f),
            (Key.LeftArrow,  0f, 5, 180f),
            (Key.DownArrow,  1f, 5, 270f),
            (Key.RightArrow, 2f, 5,   0f),
        };

        // ── La tarjeta ────────────────────────────────────────────────────────

        /// <summary>
        /// Arma la tarjeta entera: título, dibujo del teclado y lista de abajo. Todo
        /// a mano y en código, como el resto de la UI de este proyecto: nada de
        /// prefabs ni de tocar la escena.
        ///
        /// Se coloca por coordenadas y no con grupos de layout porque un teclado es
        /// una tabla de posiciones, no una pila de cosas; y porque así el arnés puede
        /// medir dónde queda cada tecla sin montar el canvas.
        /// </summary>
        private void Construir()
        {
            UiBootstrap.EnsureEventSystem();

            PrepararBloques();

            var canvasGO = new GameObject("TecladoEnPantallaCanvas",
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

            var tarjetaGO = new GameObject("Tarjeta", typeof(RectTransform), typeof(Image));
            tarjetaGO.transform.SetParent(canvasGO.transform, false);
            _tarjeta = (RectTransform)tarjetaGO.transform;

            _tarjeta.anchorMin = _tarjeta.anchorMax = _tarjeta.pivot = new Vector2(0f, 0f);
            _tarjeta.anchoredPosition = Med.Margen;

            var fondo = tarjetaGO.GetComponent<Image>();
            FishyUIKit.FondoRedondeado(fondo, Col.Panel, radio: Med.RadioPanel);
            // Es un cartel, no un botón: no debe robarle el clic a nada del mundo.
            fondo.raycastTarget = false;

            // El ancho lo manda el dibujo: el teclado mide lo que mide.
            float anchoTeclado = (AnchoBloque + SeparacionFlechas + 3f) * Med.Unidad;
            float ancho = anchoTeclado + 2f * Med.Padding;

            float y = -Med.Padding;

            Texto(_tarjeta, "Titulo", Txt.Titulo, Fnt.Titulo, Col.Titulo, FishyUIKit.Titulos,
                Med.Padding, y, anchoTeclado, Fnt.Titulo * 1.2f);

            y -= Fnt.Titulo * 1.2f + Med.Separacion;

            y = DibujarTeclado(y);
            y -= Med.Separacion;
            y = DibujarLeyenda(y, anchoTeclado);

            _tarjeta.sizeDelta = new Vector2(ancho, -y + Med.Padding);

            // Lo ya sabido de otras partidas entra apagado: el dibujo se abre
            // enseñando solo lo que falta.
            HashSet<string> sabidas = Leer();
            foreach (Bloque bloque in _bloques)
            {
                if (!sabidas.Contains(bloque.Accion.ToString())) continue;

                bloque.Aprendida = true;
                PintarLeyenda(bloque, destacada: false);

                foreach (Cara cara in bloque.Caras)
                {
                    cara.Pinta = Pinta.Aprendida;
                    Pintar(cara, Pinta.Aprendida);
                }
            }

            _raiz.SetActive(false);   // lo enciende Revisar si toca
        }

        /// <summary>Pregunta las teclas de cada acción y arma el índice que el dibujo
        /// consulta tecla por tecla.</summary>
        private void PrepararBloques()
        {
            _bloques.Clear();
            _porTecla.Clear();

            Apuntar(Accion.Caminar, TeclasDeCaminar(_otto));
            Apuntar(Accion.Hablar, new List<Key> { TeclaDeInteractuar() });
            Apuntar(Accion.Correr, TeclasDe(_otto != null ? _otto.AccionCorrer : null,
                Key.LeftShift, Key.RightShift));
            Apuntar(Accion.Saltar, TeclasDe(_otto != null ? _otto.AccionSaltar : null, Key.Space));
            Apuntar(Accion.Celular, new List<Key> { MenuController.TeclaDelCelular });
            Apuntar(Accion.Pausa, new List<Key> { MenuPausa.TeclaDePausa });
        }

        private void Apuntar(Accion accion, List<Key> teclas)
        {
            var bloque = new Bloque { Accion = accion };
            bloque.Teclas.AddRange(teclas);
            _bloques.Add(bloque);

            // Si dos acciones compartieran tecla mandaría la primera, pero no pasa:
            // sería un conflicto de controles y se vería aquí antes que en el juego.
            foreach (Key tecla in teclas)
                if (!_porTecla.ContainsKey(tecla)) _porTecla[tecla] = bloque;
        }

        /// <summary>Dibuja el teclado entero y devuelve por dónde queda el borde de
        /// abajo.</summary>
        private float DibujarTeclado(float y)
        {
            for (int fila = 0; fila < Filas.Length; fila++)
            {
                float columna = 0f;

                foreach (Tapa tapa in Filas[fila])
                {
                    if (!tapa.EsHueco)
                    {
                        if (tapa.Tecla != Key.None) _sitio[tapa.Tecla] = new Vector2(columna, fila);
                        DibujarTapa(tapa, columna, fila, y);
                    }

                    columna += tapa.Ancho;
                }
            }

            float izquierdaFlechas = AnchoBloque + SeparacionFlechas;
            foreach ((Key tecla, float columna, int fila, float giro) in Flechas)
            {
                _sitio[tecla] = new Vector2(izquierdaFlechas + columna, fila);
                DibujarFlecha(tecla, izquierdaFlechas + columna, fila, y, giro);
            }

            return y - Filas.Length * Med.Unidad;
        }

        /// <summary>
        /// Una tecla del dibujo. Las que el juego usa salen claras y con su letra
        /// grande; las demás, apagadas y con la letra pequeña: están para reconocer
        /// la forma del teclado, no para leerlas.
        /// </summary>
        private void DibujarTapa(Tapa tapa, float columna, int fila, float arriba)
        {
            bool marcada = tapa.Tecla != Key.None && _porTecla.ContainsKey(tapa.Tecla);

            // Las del juego se llaman por su tecla —"Tecla_W"— para poder buscarlas
            // desde el arnés de pruebas; las de relleno, todas igual.
            RectTransform rt = Rectangulo(marcada ? "Tecla_" + tapa.Tecla : "Tapa",
                columna, fila, arriba, tapa.Ancho);

            var fondo = rt.gameObject.AddComponent<Image>();
            FishyUIKit.FondoRedondeado(fondo, marcada ? Col.Marcada : Col.Tapa,
                radio: Med.RadioTecla);
            fondo.raycastTarget = false;

            if (string.IsNullOrEmpty(tapa.Texto)) return;

            float tamano = marcada ? Fnt.Letra
                         : tapa.Texto.Length > 1 ? Fnt.Nombre
                                                 : Fnt.Relleno;

            TextMeshProUGUI letra = FishyUIKit.Texto(rt, "Letra", tapa.Texto, tamano,
                marcada ? Col.MarcadaLetra : Col.TapaLetra, TextAlignmentOptions.Center);
            letra.font = FishyUIKit.Cuerpo;   // "Espacio" en Mango no cabe ni se lee
            letra.textWrappingMode = TextWrappingModes.NoWrap;
            letra.raycastTarget = false;
            FishyUIKit.Estirar(letra.rectTransform, Med.MargenLetra, Med.MargenLetra);

            // Que encoja si no cabe, nunca que crezca: "Shift" en una tapa de 1,25
            // unidades y "Esc" en una de 1 se salían por los dos lados y el dibujo
            // parecía roto. Es el mismo arreglo que el nombre del NPC en el panel de
            // diálogo, y por el mismo motivo: el texto es variable y la caja no.
            letra.enableAutoSizing = true;
            letra.fontSizeMax = tamano;
            letra.fontSizeMin = Med.LetraMinima;

            if (marcada) Apuntar(tapa.Tecla, fondo, letra);
        }

        /// <summary>
        /// Una tecla de dirección. La flecha va dibujada y no escrita por lo mismo
        /// que el aspa de cerrar y el símbolo de pausa: ↑↓←→ no están en ninguna de
        /// las dos fuentes del juego y saldrían como cuadros huecos. El triángulo ya
        /// existe en el kit (<see cref="FishyUIKit.SpriteTriangulo"/>), nace mirando
        /// a la derecha y se gira.
        /// </summary>
        private void DibujarFlecha(Key tecla, float columna, int fila, float arriba, float giro)
        {
            bool marcada = _porTecla.ContainsKey(tecla);

            RectTransform rt = Rectangulo("Tecla_" + tecla, columna, fila, arriba, 1f);

            var fondo = rt.gameObject.AddComponent<Image>();
            FishyUIKit.FondoRedondeado(fondo, marcada ? Col.Marcada : Col.Tapa,
                radio: Med.RadioTecla);
            fondo.raycastTarget = false;

            var go = new GameObject("Flecha", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rt, false);

            var flechaRT = (RectTransform)go.transform;
            flechaRT.anchorMin = flechaRT.anchorMax = flechaRT.pivot = new Vector2(0.5f, 0.5f);
            flechaRT.sizeDelta = new Vector2(Med.LadoFlecha, Med.LadoFlecha);
            flechaRT.localRotation = Quaternion.Euler(0f, 0f, giro);

            var img = go.GetComponent<Image>();
            img.sprite = FishyUIKit.SpriteTriangulo();
            img.color = marcada ? Col.MarcadaLetra : Col.TapaLetra;
            img.raycastTarget = false;

            if (marcada) Apuntar(tecla, fondo, img);
        }

        /// <summary>El rectángulo de una tecla, colocado por su esquina de arriba a la
        /// izquierda. El hueco entre teclas se descuenta del tamaño, así que las
        /// columnas siguen cayendo en múltiplos limpios de la unidad.</summary>
        private RectTransform Rectangulo(string nombre, float columna, int fila, float arriba,
            float ancho)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(_tarjeta, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(ancho * Med.Unidad - Med.Hueco, Med.Unidad - Med.Hueco);
            rt.anchoredPosition = new Vector2(Med.Padding + columna * Med.Unidad,
                arriba - fila * Med.Unidad);
            return rt;
        }

        private void Apuntar(Key tecla, Image fondo, Graphic simbolo)
        {
            if (!_porTecla.TryGetValue(tecla, out Bloque bloque)) return;

            bloque.Caras.Add(new Cara { Tecla = tecla, Fondo = fondo, Simbolo = simbolo });
        }

        /// <summary>
        /// La lista de abajo: qué hace cada tecla, en renglones que se van llenando
        /// de izquierda a derecha y saltan de línea cuando no cabe la siguiente.
        ///
        /// Cada entrada es un texto suyo, y no un renglón entero con todas, porque
        /// cada una se apaga por su cuenta cuando esa tecla ya se usó. El ancho se
        /// mide con TMP en vez de calcularlo a ojo: son frases en castellano, con
        /// acentos y de largo distinto, y una cuenta aproximada se pasa de largo
        /// justo en la más larga.
        /// </summary>
        private float DibujarLeyenda(float y, float ancho)
        {
            string tinte = ColorUtility.ToHtmlStringRGB(Col.LeyendaTecla);

            float x = Med.Padding;
            float derecha = Med.Padding + ancho;

            foreach (Bloque bloque in _bloques)
            {
                TextMeshProUGUI texto = Texto(_tarjeta, "Leyenda_" + bloque.Accion,
                    EntradaDeLeyenda(bloque, tinte), Fnt.Leyenda, Col.Leyenda,
                    FishyUIKit.Cuerpo, x, y, ancho, Med.AltoLeyenda);

                float suyo = texto.GetPreferredValues().x;

                // Si no cabe, a la línea siguiente. Nunca con la primera del renglón:
                // una entrada más ancha que la tarjeta se sale igual, y empujarla
                // dejaría además un renglón vacío.
                if (x > Med.Padding && x + suyo > derecha)
                {
                    y -= Med.AltoLeyenda;
                    x = Med.Padding;
                    texto.rectTransform.anchoredPosition = new Vector2(x, y);
                }

                texto.rectTransform.sizeDelta = new Vector2(suyo, Med.AltoLeyenda);
                x += suyo + Med.HuecoLeyenda;

                bloque.Leyenda = texto;
            }

            return y - Med.AltoLeyenda;
        }

        /// <summary>
        /// «W A S D o las flechas · Caminar», con el nombre de las teclas del mismo
        /// color que las teclas iluminadas del dibujo: ese color es todo el vínculo
        /// entre la lista y el teclado.
        ///
        /// Las flechas no se nombran una a una —serían cuatro palabras largas— sino
        /// con la coletilla de siempre, y solo si de verdad están atadas.
        /// </summary>
        private string EntradaDeLeyenda(Bloque bloque, string tinte)
        {
            var nombres = new List<string>();
            bool hayFlechas = false;

            foreach (Key tecla in EnOrdenDeDibujo(bloque.Teclas))
            {
                if (EsFlecha(tecla)) { hayFlechas = true; continue; }

                string nombre = NombreDe(tecla);
                if (!nombres.Contains(nombre)) nombres.Add(nombre);
            }

            string teclas = string.Join(" ", nombres);
            string coletilla = hayFlechas ? Txt.OLasFlechas : "";

            return $"<color=#{tinte}>{teclas}</color>{coletilla}{Txt.Separador}{TextoDe(bloque.Accion)}";
        }

        /// <summary>
        /// Las teclas ordenadas como se ven en el dibujo: de arriba abajo y de
        /// izquierda a derecha.
        ///
        /// Importa porque el orden en que vienen atadas no es el orden en que se
        /// miran: el composite de movimiento va arriba-abajo-izquierda-derecha, y la
        /// lista decía «W S A D», que no es como nadie nombra esas cuatro teclas. Se
        /// ordena por dónde quedaron en el dibujo y sale «W A S D» sin escribirlo a
        /// mano en ninguna parte.
        /// </summary>
        private List<Key> EnOrdenDeDibujo(List<Key> teclas)
        {
            var ordenadas = new List<Key>(teclas);

            ordenadas.Sort((una, otra) =>
            {
                bool hayUna  = _sitio.TryGetValue(una, out Vector2 sitioUna);
                bool hayOtra = _sitio.TryGetValue(otra, out Vector2 sitioOtra);

                // Una tecla que no esté dibujada —no debería haberla— se va al final
                // en vez de colarse en medio del orden de las demás.
                if (!hayUna || !hayOtra) return hayUna == hayOtra ? 0 : (hayUna ? -1 : 1);

                int porFila = sitioUna.y.CompareTo(sitioOtra.y);
                return porFila != 0 ? porFila : sitioUna.x.CompareTo(sitioOtra.x);
            });

            return ordenadas;
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
                case Accion.Saltar:  return Txt.Saltar;
                case Accion.Celular: return Txt.Celular;
                default:             return Txt.Pausa;
            }
        }
    }
}
