using System;
using System.Collections.Generic;
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
    /// La tarjeta de controles: enseña qué tecla hace qué. Se abre desde el menú de
    /// pausa, en Configuración ▸ Controles, y se queda ahí todo lo que se quiera
    /// mirar.
    ///
    /// <b>Antes salía sola</b> abajo a la derecha al empezar a jugar y se iba cuando
    /// se habían pulsado todas las teclas. Tapaba el mundo justo en los primeros
    /// minutos, y una vez ida no había forma de volver a verla. Ahora no aparece si
    /// no se pide, y no se va mientras esté abierta.
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
        /// <summary>Las acciones que la tarjeta explica, en el orden de la tarjeta.
        /// Saltar no está: el juego no lo usa para nada.</summary>
        private enum Accion { Caminar, Hablar, Correr, Celular, Pausa }

        /// <summary>Una tecla dibujada. Puede representar más de una —la W también es
        /// la flecha de arriba— porque para el niño/a son la misma cosa.</summary>
        private class Cara
        {
            public readonly List<Key> Teclas = new List<Key>();
            public Image Fondo;
            public bool Encendida;
        }

        /// <summary>Una fila: lo que hace y con qué teclas.</summary>
        private class Bloque
        {
            public Accion Accion;
            public readonly List<Cara> Caras = new List<Cara>();
        }

        private readonly List<Bloque> _bloques = new List<Bloque>();

        private GameObject _raiz;
        private RectTransform _tarjeta;
        private OttoController _otto;
        private Action _alVolver;

        /// <summary>
        /// Si la tarjeta trae su propio título «Controles» y su propio botón «Volver».
        ///
        /// La pausa los quiere: ahí la tarjeta sale sola encima del juego y tiene que
        /// decir qué es y cómo salir. Una pantalla de menú no, porque ya pone su título
        /// y su botón de volver con el aspecto del resto del menú, y repetirlos deja dos
        /// títulos y dos botones que hacen lo mismo.
        /// </summary>
        private bool _conTituloYVolver = true;

        public bool Abierto => _raiz != null && _raiz.activeSelf;

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
        public static TecladoEnPantalla CrearEn(Transform padre, Action alVolver,
                                               bool conTituloYVolver = true)
        {
            var go = new GameObject("Controles", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            FishyUIKit.Estirar((RectTransform)go.transform);

            var ayuda = go.AddComponent<TecladoEnPantalla>();
            ayuda._alVolver = alVolver;
            ayuda._conTituloYVolver = conTituloYVolver;
            ayuda._raiz = go;
            ayuda._otto = FindAnyObjectByType<OttoController>();
            ayuda.Construir();

            go.SetActive(false);
            return ayuda;
        }

        /// <summary>
        /// Cambia el tamaño de la tarjeta entera. <see cref="Medidas.Escala"/> (1.3) es
        /// lo que usa la pausa, donde la tarjeta sale sola sobre un velo oscuro; en la
        /// pantalla de opciones del menú, con los ajustes detrás, a ese tamaño tapa
        /// demasiado y se pide más chica.
        ///
        /// No se toca <c>Medidas.Escala</c> para esto: es un campo estático compartido,
        /// y cambiarlo desde el menú le cambiaría el tamaño también a la pausa.
        /// </summary>
        public void Escalar(float escala)
        {
            if (_tarjeta != null && escala > 0f)
                _tarjeta.localScale = Vector3.one * escala;
        }

        public void Abrir()
        {
            if (_raiz == null) return;

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
            if (!Abierto) return;
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

        /// <summary>Enciende cada tecla mientras se la pulsa y la apaga al soltarla.
        /// Nada más: la tarjeta no se apaga ni se va por mucho que se pulse.</summary>
        private void Repintar(Func<Key, bool> pulsada)
        {
            foreach (Bloque bloque in _bloques)
            {
                foreach (Cara cara in bloque.Caras)
                {
                    bool ahora = false;
                    foreach (Key tecla in cara.Teclas)
                        if (pulsada(tecla)) { ahora = true; break; }

                    if (cara.Encendida == ahora || cara.Fondo == null) continue;
                    cara.Encendida = ahora;
                    cara.Fondo.color = ahora
                        ? Col.TeclaPulsada
                        : Col.Tecla;
                }
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
        /// Arma la tarjeta entera, centrada sobre el velo de la pausa. Todo a mano y en
        /// código, como el resto de la UI de este proyecto.
        ///
        /// Se coloca por coordenadas y no con grupos de layout porque cada fila tiene
        /// su propia forma —la cruz de caminar ocupa dos pisos y las demás uno— y
        /// porque así el arnés puede medir dónde queda cada cosa sin montar el canvas.
        /// </summary>
        private void Construir()
        {
            var tarjetaGO = new GameObject("Tarjeta", typeof(RectTransform), typeof(Image));
            tarjetaGO.transform.SetParent(_raiz.transform, false);
            _tarjeta = (RectTransform)tarjetaGO.transform;

            _tarjeta.anchorMin = _tarjeta.anchorMax = _tarjeta.pivot = new Vector2(0.5f, 0.5f);
            _tarjeta.anchoredPosition = Vector2.zero;
            _tarjeta.localScale = Vector3.one * Med.Escala;

            FishyUIKit.FondoRedondeado(tarjetaGO.GetComponent<Image>(), Col.Panel,
                radio: Med.RadioPanel);

            float ancho = Med.AnchoPanel - 2f * Med.Padding;
            float y = -Med.Padding;

            if (_conTituloYVolver)
            {
                Texto(_tarjeta, "Titulo", Txt.Titulo, Fnt.Titulo, Col.Titulo,
                    FishyUIKit.Titulos, Med.Padding, y, ancho, Fnt.Titulo * 1.2f);

                y -= Fnt.Titulo * 1.2f + Med.Separacion;
            }

            y = FilaDeCaminar(y);
            y = Fila(y, Accion.Hablar, new List<List<Key>> { new List<Key> { TeclaDeInteractuar() } });

            y = Fila(y, Accion.Correr, new List<List<Key>>
            {
                TeclasDe(_otto != null ? _otto.AccionCorrer : null, Key.LeftShift, Key.RightShift),
            });

            y = FilaDoble(y,
                Accion.Celular, new List<Key> { MenuController.TeclaDelCelular },
                Accion.Pausa,   new List<Key> { MenuPausa.TeclaDePausa });

            if (_conTituloYVolver)
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
                case Accion.Celular: return Txt.Celular;
                default:             return Txt.Pausa;
            }
        }
    }
}
