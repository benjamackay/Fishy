using System;
using System.Collections.Generic;
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
    /// El teclado de ayuda: una tarjeta abajo a la derecha que enseña qué tecla hace
    /// qué, pensada para la primera partida de un niño/a de 9 a 13 años.
    ///
    /// <b>Por qué hace falta.</b> La primera misión dice «habla con Huemul» y nada
    /// más. Para cumplirla hay que descubrir solo tres cosas: que se camina con WASD
    /// o las flechas, que hay que llegar hasta él y que se interactúa con E. La única
    /// pista que existía era el cartel de «E para interactuar», que aparece cuando ya
    /// estás pegado al NPC: las teclas de caminar no se enseñaban en ninguna parte.
    ///
    /// <b>Enseña, no juega.</b> Las teclas se iluminan cuando se las pulsa, pero no
    /// se pueden pulsar con el ratón ni con el dedo. Es a propósito: el público son
    /// salas de computación con teclado, y mandar entrada desde aquí obligaría a
    /// apuntarse a la <see cref="PuertaDeEntrada"/> y a pensar qué pasa con cada
    /// bloqueo de los que ya existen. El juego táctil tiene su propia pieza,
    /// <see cref="OttoOnScreenButton"/>, que sigue sin cablearse en ninguna escena;
    /// encenderla es otra decisión y merece su propia historia de usuario.
    ///
    /// <b>Se va sola.</b> Cada fila se apaga en cuanto el niño/a usa esa tecla, y
    /// cuando las ha usado todas la tarjeta desaparece y no vuelve: lo aprendido
    /// queda en PlayerPrefs, del aparato y no de la cuenta, igual que
    /// <see cref="ConfiguracionDelJuego"/>. Una ayuda que no se sabe cuándo callarse
    /// termina siendo adorno que se tapa con la mano.
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
        /// <summary>Dónde se recuerda lo que el niño/a ya se sabe. Comparte prefijo
        /// con los ajustes para que todo lo de este aparato se encuentre junto.</summary>
        public const string ClaveAprendidas = ConfiguracionDelJuego.Prefijo + "TeclasAprendidas";

        /// <summary>Cada cuánto se revisa si toca mostrarse, en segundos sin escalar
        /// (con la pausa puesta el tiempo normal no corre). Mismo motivo y mismo
        /// valor que en <see cref="BotonDePausaEnPantalla"/>: la revisión busca cosas
        /// en la escena y no hace falta hacerlo cada frame.</summary>
        private const float CadaCuanto = 0.5f;

        /// <summary>Lo que se espera, en segundos reales, desde que se apaga la última
        /// fila hasta que la tarjeta se va. Sin esta pausa el niño/a nunca ve
        /// apagarse la que acaba de aprender: la tarjeta desaparecería en el mismo
        /// frame y parecería que se rompió algo.</summary>
        private const float Despedida = 1.5f;

        // ── Lo que se enseña ──────────────────────────────────────────────────

        /// <summary>Las acciones que la tarjeta explica. El orden es el de la tarjeta
        /// y el id es lo que se guarda en PlayerPrefs, así que no se renombran a la
        /// ligera: un id que cambia hace que quien ya aprendió vuelva a ver la fila.</summary>
        private enum Accion { Caminar, Hablar, Correr, Saltar, Celular, Pausa }

        /// <summary>Una tecla dibujada. Puede representar más de una —la W también es
        /// la flecha de arriba— porque para el niño/a son la misma cosa.</summary>
        private class Cara
        {
            public readonly List<Key> Teclas = new List<Key>();
            public Image Fondo;
            public bool Encendida;
        }

        /// <summary>Una fila: lo que hace, con qué teclas, y si ya se aprendió.</summary>
        private class Bloque
        {
            public Accion Accion;
            public readonly List<Cara> Caras = new List<Cara>();
            public CanvasGroup Grupo;
            public bool Aprendida;
        }

        private readonly List<Bloque> _bloques = new List<Bloque>();

        private GameObject _raiz;
        private RectTransform _tarjeta;
        private OttoController _otto;
        private MenuController _celular;
        private float _proximaRevision;
        private bool _todoAprendido;
        private float _momentoEnQueSeAprendioTodo;
        private bool _yaNoHaceFalta;

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
        /// enseñar y la tarjeta no llega ni a construirse.
        ///
        /// Está fuera del Awake para que el arnés de pruebas pueda llamarlo: el Awake
        /// hace además <c>DontDestroyOnLoad</c>, que fuera del modo Play no avisa
        /// sino que tira excepción.
        /// </summary>
        private void LeerLoAprendido() => _yaNoHaceFalta = TodoAprendido(Leer());

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
            if (Time.unscaledTime >= _proximaRevision)
            {
                _proximaRevision = Time.unscaledTime + CadaCuanto;
                Revisar();
            }

            if (_raiz == null || !_raiz.activeSelf) return;

            Repintar(EstaPulsada);
        }

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
        /// Enciende o apaga la tarjeta según si ahora toca enseñar algo.
        ///
        /// Se esconde cuando ya no aporta —durante una cinemática o una carga, con el
        /// juego en pausa, con el celular abierto encima— y cuando no hay teclado que
        /// enseñar: en una tablet, <c>Keyboard.current</c> es null y una tarjeta de
        /// teclas sería un chiste malo.
        ///
        /// <b>Sí se queda durante los diálogos</b>, aunque Otto no pueda moverse: es
        /// justo cuando el niño/a necesita saber que la misma tecla que empezó la
        /// conversación es la que la hace avanzar.
        /// </summary>
        private void Revisar()
        {
            if (_otto == null) _otto = FindAnyObjectByType<OttoController>();
            if (_celular == null) _celular = FindAnyObjectByType<MenuController>();

            // Se construye con Otto delante y no en el Awake: las teclas se las
            // preguntamos a él, y en la escena del menú todavía no existe.
            if (!_yaNoHaceFalta && _raiz == null && _otto != null && Keyboard.current != null)
                Construir();

            bool mostrar = !_yaNoHaceFalta &&
                           _otto != null &&
                           Keyboard.current != null &&
                           !EstadoDePausa.Pausado &&
                           EstadoDePausa.SePuedePausar() &&
                           (_celular == null || !_celular.EstaAbierto);

            if (_raiz != null && _raiz.activeSelf != mostrar) _raiz.SetActive(mostrar);
        }

        /// <summary>
        /// Pinta el estado de cada tecla: encendida mientras se la pulsa, y la fila
        /// entera atenuada en cuanto esa tecla se usó por primera vez.
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
                {
                    bool ahora = false;
                    foreach (Key tecla in cara.Teclas)
                        if (pulsada(tecla)) { ahora = true; break; }

                    algunaPulsada |= ahora;

                    if (cara.Encendida == ahora || cara.Fondo == null) continue;
                    cara.Encendida = ahora;
                    cara.Fondo.color = ahora
                        ? Col.TeclaPulsada
                        : Col.Tecla;
                }

                if (algunaPulsada && !bloque.Aprendida) Aprender(bloque);
                todas &= bloque.Aprendida;
            }

            if (!todas) { _todoAprendido = false; return; }

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

        private void Aprender(Bloque bloque)
        {
            bloque.Aprendida = true;
            if (bloque.Grupo != null)
                bloque.Grupo.alpha = Med.AlfaAprendida;

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
        /// para poder mirar la tarjeta otra vez sin borrar los PlayerPrefs enteros,
        /// que se llevarían por delante el volumen y la pantalla completa.</summary>
        public static void Olvidar()
        {
            PlayerPrefs.DeleteKey(ClaveAprendidas);
            PlayerPrefs.Save();
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

        /// <summary>Las teclas de una acción de un solo botón (correr, saltar), sin
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
        /// este proyecto: nada de prefabs ni de tocar la escena.
        ///
        /// Se coloca por coordenadas y no con grupos de layout porque cada fila tiene
        /// su propia forma —la cruz de caminar ocupa dos pisos y las demás uno— y
        /// porque así el arnés puede medir dónde queda cada cosa sin montar el canvas.
        /// </summary>
        private void Construir()
        {
            UiBootstrap.EnsureEventSystem();

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

            _tarjeta.anchorMin = _tarjeta.anchorMax = _tarjeta.pivot = new Vector2(1f, 0f);
            _tarjeta.anchoredPosition = new Vector2(-Med.Margen.x, Med.Margen.y);

            var fondo = tarjetaGO.GetComponent<Image>();
            FishyUIKit.FondoRedondeado(fondo, Col.Panel, radio: Med.RadioPanel);
            // Es un cartel, no un botón: no debe robarle el clic a nada del mundo.
            fondo.raycastTarget = false;

            float ancho = Med.AnchoPanel - 2f * Med.Padding;
            float y = -Med.Padding;

            Texto(_tarjeta, "Titulo", Txt.Titulo, Fnt.Titulo, Col.Titulo,
                FishyUIKit.Titulos, Med.Padding, y, ancho, Fnt.Titulo * 1.2f);

            y -= Fnt.Titulo * 1.2f + Med.Separacion;

            y = FilaDeCaminar(y);
            y = Fila(y, Accion.Hablar, new List<List<Key>> { new List<Key> { TeclaDeInteractuar() } });

            y = FilaDoble(y,
                Accion.Correr, TeclasDe(_otto != null ? _otto.AccionCorrer : null,
                    Key.LeftShift, Key.RightShift),
                Accion.Saltar, TeclasDe(_otto != null ? _otto.AccionSaltar : null, Key.Space));

            y = FilaDoble(y,
                Accion.Celular, new List<Key> { MenuController.TeclaDelCelular },
                Accion.Pausa,   new List<Key> { MenuPausa.TeclaDePausa });

            _tarjeta.sizeDelta = new Vector2(Med.AnchoPanel, -y + Med.Padding);

            // Lo ya sabido de otras partidas entra apagado: la tarjeta se abre
            // enseñando solo lo que falta.
            HashSet<string> sabidas = Leer();
            foreach (Bloque bloque in _bloques)
            {
                if (!sabidas.Contains(bloque.Accion.ToString())) continue;

                bloque.Aprendida = true;
                if (bloque.Grupo != null) bloque.Grupo.alpha = Med.AlfaAprendida;
            }

            _raiz.SetActive(false);   // lo enciende Revisar si toca
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

            Texto(grupo, "Etiqueta", Txt.Caminar, Fnt.Etiqueta, Col.Etiqueta,
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
            Texto(grupo, "Etiqueta", TextoDe(accion), Fnt.Etiqueta, Col.Etiqueta,
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

            Texto(grupo, "Etiqueta", TextoDe(accion), Fnt.Etiqueta, Col.Etiqueta,
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

        /// <summary>El contenedor de una fila. Lleva un CanvasGroup para poder
        /// atenuarla entera de una vez cuando esa tecla ya se aprendió.</summary>
        private RectTransform Grupo(Bloque bloque, string nombre, float y)
        {
            var go = new GameObject("Fila_" + nombre, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_tarjeta, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(Med.AnchoPanel, 0f);

            bloque.Grupo = go.GetComponent<CanvasGroup>();
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

            if (giro.HasValue)
            {
                // La letra sube un poco para hacerle sitio a la flecha.
                letra.rectTransform.anchoredPosition = new Vector2(0f, Med.BajadaFlecha / 2f);
                Flecha(go.transform, giro.Value);
            }

            return cara;
        }

        private void Flecha(Transform padre, float giro)
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
