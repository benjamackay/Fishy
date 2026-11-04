#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Fishy.Pausa;
using Fishy.UI;
using Fishy.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless de lo que el niño/a lee y de las teclas que se le enseñan en
    /// los primeros minutos de partida.
    ///
    /// «El tutorial» no es una clase: es la secuencia de apertura —la misión inicial
    /// «Conoce el Bosque», el cartel de misión, el panel de diálogo de Huemul y el
    /// aviso de «para interactuar»— armada con piezas de uso general. Estas pruebas
    /// cubren las dos cosas que ahí se pedían:
    ///
    ///  A) que los textos se lean en el equipo escolar típico (1366x768), que es
    ///     donde el canvas de la escena los encogía a 17 px;
    ///  B) que el teclado de ayuda (<see cref="TecladoEnPantalla"/>) enseñe las
    ///     teclas que el juego escucha de verdad, y no unas escritas a mano.
    ///
    /// Como el resto de los arneses de este proyecto, esto vive en el editor y no en
    /// PlayMode: todo lo que se mide aquí está en Assembly-CSharp, y un .asmdef de
    /// pruebas no puede referenciarlo.
    ///
    /// Se corren desde el menú  Fishy ▸ Probar textos y teclas del tutorial,
    /// o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasTutorial.Ejecutar `
    ///             -logFile -
    ///
    /// Termina con código 0 si todo pasa y 1 si algo falla.
    /// </summary>
    public static class FishyPruebasTutorial
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        private const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivadoEstatico = BindingFlags.Static | BindingFlags.NonPublic;

        /// <summary>El equipo de la sala de computación del colegio. Todas las cuentas
        /// de legibilidad se hacen contra esta pantalla y no contra 1920x1080, que es
        /// donde nunca se ve el problema.</summary>
        private static readonly Vector2 PantallaDelColegio = new Vector2(1366f, 768f);

        private static readonly Vector2 Referencia = new Vector2(1920f, 1080f);

        /// <summary>Suelo de tamaño de letra, en píxeles de pantalla de verdad, para
        /// un cuerpo de texto que un niño/a de 9 a 13 años va a leer entero: el
        /// diálogo de los NPCs. Por debajo de esto se lee a trompicones.</summary>
        private const float MinimoLegible = 20f;

        /// <summary>Suelo para lo que se mira de reojo sin dejar de caminar: las
        /// líneas del cartel de misión. Se le pide menos que a un párrafo porque son
        /// frases de cuatro palabras que además se repiten en la página del Tab, pero
        /// no puede bajar de aquí o deja de servir como recordatorio.</summary>
        private const float MinimoDeOjeada = 18f;

        [MenuItem("Fishy/Probar textos y teclas del tutorial")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE TEXTOS Y TECLAS DEL TUTORIAL (headless)");
            log.AppendLine(new string('=', 70));

            // Lo que el teclado de ayuda tenga aprendido en ESTE equipo no es cosa de
            // las pruebas: se guarda, se arranca de cero y se devuelve al final.
            string aprendidasAntes = PlayerPrefs.GetString(TecladoEnPantalla.ClaveAprendidas, "");
            TecladoEnPantalla.Olvidar();

            // ── A) Lo que se lee ──
            ProbarElCanvasDeLaEscenaEscalaComoLosDemas(log);
            ProbarElCuerpoDelDialogoSeLeeEnElEquipoDelColegio(log);
            ProbarElTextoDelDialogoContrastaConSuPanel(log);
            ProbarElNombreYElCuerpoYaNoSePisan(log);
            ProbarNingunaLineaDeDialogoSeSaleDeLaCaja(log);
            ProbarElCartelDeMisionYElBotonDePausaNoSePisan(log);
            ProbarElCartelDeInteractuarRecibeFondo(log);

            // ── B) Las teclas ──
            ProbarElTecladoSeConstruyeSinExcepciones(log);
            ProbarEnsenaLasTeclasQueElJuegoEscucha(log);
            ProbarNingunaEtiquetaUsaUnGlifoQueFalta(log);
            ProbarLaTeclaSeEnciendeAlPulsarla(log);
            ProbarLoAprendidoSeAtenuaYLaTarjetaSeVa(log);
            ProbarNoSeVeCuandoNoToca(log);
            ProbarElDibujoCabeEnPantalla(log);
            ProbarLasFilasDelTecladoCuadran(log);
            ProbarLasFlechasVanEnCruz(log);
            ProbarNadaSeSaleDeLaTarjeta(log);

            EstadoDePausa.Restablecer();
            PlayerPrefs.SetString(TecladoEnPantalla.ClaveAprendidas, aprendidasAntes);
            PlayerPrefs.Save();

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── A) Lo que se lee ─────────────────────────────────────────────────────

        /// <summary>
        /// El canvas de MainScene escalaba SOLO por el ancho (match 0), mientras que
        /// los dieciséis que se construyen en código usan 0,5. En una ventana más
        /// alta que 16:9 el diálogo encogía y el cartel de misión no, y en cualquier
        /// pantalla de menos de 1920 de ancho el diálogo encogía de más.
        ///
        /// Se lee el archivo de la escena en vez de abrirla porque abrirla desde un
        /// arnés se llevaría por delante la escena que tenga abierta quien lo corre.
        /// </summary>
        private static void ProbarElCanvasDeLaEscenaEscalaComoLosDemas(StringBuilder log)
        {
            const string Ruta = "Assets/Scenes/MainScene.unity";

            if (!File.Exists(Ruta))
            {
                Comprobar(log, "el canvas de la escena escala por los dos lados", false,
                    $"no encontré {Ruta}");
                return;
            }

            MatchCollection valores = Regex.Matches(File.ReadAllText(Ruta),
                @"m_MatchWidthOrHeight: ([\d.]+)");

            bool todos = valores.Count > 0 && valores.Cast<Match>().All(m =>
                Mathf.Approximately(float.Parse(m.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture), 0.5f));

            Comprobar(log, "el canvas de la escena escala por los dos lados, como los de código",
                todos,
                valores.Count == 0 ? "no hay ningún CanvasScaler en la escena"
                                   : string.Join(", ", valores.Cast<Match>().Select(m => m.Groups[1].Value)));
        }

        /// <summary>
        /// El tamaño de letra del tema está en píxeles de 1920x1080; lo que el niño/a
        /// ve es eso multiplicado por el factor del escalador. En 1366x768 el factor
        /// es 0,71: los 24 pt que traía la escena eran 17 px reales.
        /// </summary>
        private static void ProbarElCuerpoDelDialogoSeLeeEnElEquipoDelColegio(StringBuilder log)
        {
            float factor = FactorDePantalla(PantallaDelColegio, 0.5f);
            float cuerpo = DialogoNeutroTheme.Fuente.Texto * factor;
            float nombre = DialogoNeutroTheme.Fuente.Nombre * factor;

            Comprobar(log, "en 1366x768 el diálogo se lee: pasa del mínimo de un texto corrido",
                cuerpo >= MinimoLegible && nombre >= cuerpo,
                $"factor {factor:0.00} · cuerpo {cuerpo:0.0} px, nombre {nombre:0.0} px " +
                $"(mínimo {MinimoLegible})");

            float guia     = MisionHudTheme.Fuente.Guia * factor;
            float objetivo = MisionHudTheme.Fuente.Objetivo * factor;
            float titulo   = MisionHudTheme.Fuente.Titulo * factor;

            Comprobar(log, "y el cartel de misión se mira de reojo sin tener que acercarse",
                guia >= MinimoDeOjeada && objetivo >= MinimoDeOjeada && titulo >= MinimoLegible,
                $"guía {guia:0.0} px, objetivo {objetivo:0.0} px, título {titulo:0.0} px " +
                $"(mínimo {MinimoDeOjeada})");
        }

        /// <summary>
        /// WCAG AA pide 4,5:1 para texto normal. El panel es translúcido, así que el
        /// contraste depende de lo que haya detrás: se mide contra el peor caso, que
        /// para un panel oscuro es un fondo blanco (la playa a mediodía).
        /// </summary>
        private static void ProbarElTextoDelDialogoContrastaConSuPanel(StringBuilder log)
        {
            Color sobreBlanco = Componer(DialogoNeutroTheme.Colores.Panel, Color.white);
            Color sobreNegro  = Componer(DialogoNeutroTheme.Colores.Panel, Color.black);

            float claro  = Contraste(DialogoNeutroTheme.Colores.Texto, sobreBlanco);
            float oscuro = Contraste(DialogoNeutroTheme.Colores.Texto, sobreNegro);
            float peor = Mathf.Min(claro, oscuro);

            Comprobar(log, "el texto del diálogo contrasta 4,5:1 con su panel, haya lo que haya detrás",
                peor >= 4.5f,
                $"sobre fondo claro {claro:0.00}:1, sobre fondo oscuro {oscuro:0.00}:1");
        }

        /// <summary>
        /// En la escena las cajas del nombre y del cuerpo se solapaban 16 px, así que
        /// un nombre largo —"Foca de Weddell"— caía encima del diálogo. Ahora las
        /// coloca el skin, y esto lo comprueba sobre una réplica del panel de la
        /// escena: el skin pisa la geometría que traiga, así que da igual de dónde
        /// venga el panel.
        /// </summary>
        private static void ProbarElNombreYElCuerpoYaNoSePisan(StringBuilder log)
        {
            DialogoNeutroSkin.Reiniciar();
            GameObject panel = PanelDeDialogoDePrueba(out TMP_Text nombre, out TMP_Text texto,
                out Image retrato);

            DialogoNeutroSkin.Aplicar(panel, nombre, texto, retrato);

            Rect caja = ((RectTransform)panel.transform).rect;
            Rect rNombre = EnElPadre((RectTransform)nombre.transform);
            Rect rTexto  = EnElPadre((RectTransform)texto.transform);
            Rect rCara   = EnElPadre((RectTransform)retrato.transform);

            bool separados = rTexto.yMax <= rNombre.yMin + 0.01f;
            bool dentro = caja.xMin <= rTexto.xMin && rTexto.xMax <= caja.xMax &&
                          caja.yMin <= rTexto.yMin && rNombre.yMax <= caja.yMax;
            bool sinPisarLaCara = rCara.xMax <= rTexto.xMin && rCara.xMax <= rNombre.xMin;

            Comprobar(log, "el nombre, el retrato y el cuerpo del diálogo no se pisan entre sí",
                separados && dentro && sinPisarLaCara,
                $"nombre y={rNombre.yMin:0}..{rNombre.yMax:0}, cuerpo y={rTexto.yMin:0}..{rTexto.yMax:0}, " +
                $"panel y={caja.yMin:0}..{caja.yMax:0}");

            UnityEngine.Object.DestroyImmediate(panel);
        }

        /// <summary>
        /// Ninguna línea de ningún NPCDialogue se puede salir de la caja del cuerpo.
        ///
        /// Es la prueba que sostiene la decisión de no encender el ajuste automático
        /// de tamaño: sin ajuste, una línea demasiado larga se derrama fuera del
        /// panel y nadie se entera hasta verlo en pantalla. Se mide con el medidor de
        /// TMP y con la fuente de verdad, no contando caracteres.
        /// </summary>
        private static void ProbarNingunaLineaDeDialogoSeSaleDeLaCaja(StringBuilder log)
        {
            Vector2 caja = CajaDelCuerpo();

            var medidor = new GameObject("MedidorDePrueba",
                typeof(RectTransform), typeof(TextMeshProUGUI))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var tmp = medidor.GetComponent<TextMeshProUGUI>();
            tmp.font = FishyUIKit.Cuerpo;
            tmp.fontSize = DialogoNeutroTheme.Fuente.Texto;

            string peor = "";
            float peorAlto = 0f;
            int lineas = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:NPCDialogue"))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                var dialogo = AssetDatabase.LoadAssetAtPath<NPCDialogue>(ruta);
                if (dialogo == null || dialogo.dialogueLines == null) continue;

                foreach (string linea in dialogo.dialogueLines)
                {
                    if (string.IsNullOrEmpty(linea)) continue;
                    lineas++;

                    float alto = tmp.GetPreferredValues(linea, caja.x, 0f).y;
                    if (alto <= peorAlto) continue;

                    peorAlto = alto;
                    peor = $"{Path.GetFileNameWithoutExtension(ruta)}: «{Recortar(linea)}»";
                }
            }

            UnityEngine.Object.DestroyImmediate(medidor);

            Comprobar(log, "ninguna línea de diálogo del juego se sale de la caja del panel",
                lineas > 0 && peorAlto <= caja.y,
                lineas == 0 ? "no encontré ningún NPCDialogue"
                            : $"{lineas} líneas · la más alta ocupa {peorAlto:0} de {caja.y:0} px · {peor}");
        }

        /// <summary>
        /// HDU-17 puso el botón de pausa arriba a la izquierda, que es donde ya vivía
        /// el cartel de misión, y además lo dibuja en el orden 9400 contra el 710 del
        /// cartel: el botón tapaba «MISIÓN» y el principio del título justo durante
        /// el tutorial, que es cuando ese cartel es lo único que dice a dónde ir.
        /// </summary>
        private static void ProbarElCartelDeMisionYElBotonDePausaNoSePisan(StringBuilder log)
        {
            float lado   = ConstanteDe<float>(typeof(BotonDePausaEnPantalla), "Lado");
            float margen = ConstanteDe<float>(typeof(BotonDePausaEnPantalla), "Margen");

            // Los dos cuelgan de la esquina de arriba a la izquierda, así que basta
            // con mirar hasta dónde baja el botón y desde dónde empieza el cartel.
            float abajoDelBoton = margen + lado;
            float arribaDelCartel = -MisionHudTheme.Medidas.MargenPanel.y;

            bool separados = arribaDelCartel >= abajoDelBoton;

            // Y el botón no puede empezar a la derecha del cartel y colarse por el
            // lado: los dos están pegados al borde izquierdo.
            bool mismaColumna = Mathf.Approximately(margen, MisionHudTheme.Medidas.MargenPanel.x);

            Comprobar(log, "el botón de pausa ya no tapa el cartel de misión",
                separados && mismaColumna,
                $"el botón ocupa hasta y={abajoDelBoton:0} y el cartel empieza en y={arribaDelCartel:0}");
        }

        /// <summary>
        /// El cartel de «para interactuar» se dibuja sobre el mapa, en crema y sin
        /// nada detrás. Ahora el skin le pone un fondo medido de lo que hay dentro.
        /// </summary>
        private static void ProbarElCartelDeInteractuarRecibeFondo(StringBuilder log)
        {
            CartelInteraccionSkin.Reiniciar();

            var cartel = new GameObject("InteractionPrompt", typeof(RectTransform))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var raiz = (RectTransform)cartel.transform;
            raiz.sizeDelta = new Vector2(500f, 150f);

            // Las mismas piezas, medidas y anclajes que trae la escena: la manito con
            // la letra y el texto al lado, que va pegado por su izquierda.
            Hijo(raiz, "Icono", new Vector2(75f, 75f), new Vector2(150f, 150f));
            RectTransform etiqueta = Hijo(raiz, "Label", new Vector2(160f, 73f),
                new Vector2(340f, 50f));
            etiqueta.pivot = new Vector2(0f, 0.5f);
            etiqueta.gameObject.AddComponent<TextMeshProUGUI>().text = "para interactuar";

            // Apagado, como está en la escena: el detector lo pinta en su Awake y el
            // cartel solo se enciende cuando hay alguien al alcance. Medir el tamaño
            // de algo apagado es justo lo que tiene que funcionar aquí.
            cartel.SetActive(false);

            CartelInteraccionSkin.Aplicar(cartel);

            Transform fondo = raiz.Find(CartelInteraccionSkin.NombreFondo);
            var rt = fondo as RectTransform;
            var img = fondo != null ? fondo.GetComponent<Image>() : null;

            // Llega del icono al final del texto, y ni un palmo más: medir de más lo
            // sacaba por el borde de la pantalla y le ponía un velo a media esquina.
            float padding = CartelInteraccionTheme.Medidas.Padding;
            bool cubre = rt != null &&
                         rt.rect.width  >= 500f && rt.rect.width  <= 500f + 2f * padding + 1f &&
                         rt.rect.height >= 150f && rt.rect.height <= 150f + 2f * padding + 1f;

            Comprobar(log, "el cartel de «para interactuar» lleva un fondo detrás del texto",
                fondo != null && fondo.GetSiblingIndex() == 0 && img != null &&
                !img.raycastTarget && cubre,
                fondo == null ? "no se pintó ningún fondo"
                              : $"fondo de {rt.rect.width:0}x{rt.rect.height:0}, " +
                                $"hermano nº {fondo.GetSiblingIndex()}");

            UnityEngine.Object.DestroyImmediate(cartel);
        }

        // ── B) Las teclas ────────────────────────────────────────────────────────

        /// <summary>
        /// La tarjeta se arma entera en código, así que lo primero es que se arme sin
        /// reventar. Se escucha además la consola: Unity se come las excepciones que
        /// ocurren dentro de un callback de layout y solo quedan ahí (mismo motivo y
        /// misma receta que en <c>FishyPruebasAlbum</c>).
        /// </summary>
        private static void ProbarElTecladoSeConstruyeSinExcepciones(StringBuilder log)
        {
            var ruidos = new List<string>();
            Application.LogCallback escucha = (condicion, pila, tipo) =>
            {
                if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
                    ruidos.Add($"{tipo}: {condicion}");
            };

            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();

            bool exploto = false;
            TecladoEnPantalla ayuda = null;

            Application.logMessageReceived += escucha;
            try
            {
                ayuda = NuevoTeclado();
                Invocar(ayuda, "Revisar");
            }
            catch (Exception ex)
            {
                exploto = true;
                ruidos.Add(ex.ToString());
            }
            finally
            {
                Application.logMessageReceived -= escucha;
            }

            Comprobar(log, "el teclado de ayuda se construye y se enciende sin una sola excepción",
                !exploto && ruidos.Count == 0 && Visible(ayuda),
                ruidos.Count > 0 ? ruidos[0]
                                 : (Visible(ayuda) ? "" : "se construyó pero no se mostró"));

            Soltar(ayuda, otto, teclado);
        }

        /// <summary>
        /// Lo que la tarjeta enseña tiene que ser lo que el juego escucha. Se compara
        /// contra las fuentes de verdad: las ataduras del <see cref="OttoController"/>,
        /// el <c>interactKey</c> del detector y las constantes declaradas del menú de
        /// pausa y del celular.
        ///
        /// Un cartel que enseña teclas equivocadas es peor que no tener cartel: el
        /// niño/a prueba, no pasa nada, y concluye que el juego está roto.
        /// </summary>
        private static void ProbarEnsenaLasTeclasQueElJuegoEscucha(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();
            Invocar(ayuda, "Revisar");

            Dictionary<string, List<Key>> ensenadas = ayuda.LoQueEnsena();

            List<Key> deVerdadCaminar = TeclasDeTeclado(otto.AccionMover);
            List<Key> deVerdadCorrer  = TeclasDeTeclado(otto.AccionCorrer);
            List<Key> deVerdadSaltar  = TeclasDeTeclado(otto.AccionSaltar);

            bool caminar = Coinciden(ensenadas, "Caminar", deVerdadCaminar);
            bool correr  = Coinciden(ensenadas, "Correr", deVerdadCorrer);
            bool saltar  = Coinciden(ensenadas, "Saltar", deVerdadSaltar);
            bool hablar  = Coinciden(ensenadas, "Hablar", new List<Key> { Key.E });
            bool celular = Coinciden(ensenadas, "Celular",
                new List<Key> { MenuController.TeclaDelCelular });
            bool pausa   = Coinciden(ensenadas, "Pausa",
                new List<Key> { MenuPausa.TeclaDePausa });

            Comprobar(log, "las teclas de la tarjeta son las que el juego escucha de verdad",
                caminar && correr && saltar && hablar && celular && pausa,
                string.Join(" · ", ensenadas.Select(p =>
                    $"{p.Key}: {string.Join("/", p.Value)}")));

            // El respaldo que se usa cuando todavía no hay Otto tiene que decir lo
            // mismo que las ataduras de verdad, o la tarjeta mentiría en el primer
            // frame de la partida.
            List<Key> respaldo = TeclasDeCaminarSinOtto();
            Comprobar(log, "y el respaldo de «sin Otto todavía» dice lo mismo que las ataduras",
                MismoConjunto(respaldo, deVerdadCaminar),
                $"respaldo {string.Join("/", respaldo)} · de verdad {string.Join("/", deVerdadCaminar)}");

            Soltar(ayuda, otto, teclado);
        }

        /// <summary>
        /// La fuente de cuerpo es estática y trae unos 250 glifos (Latin-1 y poco
        /// más): un carácter que falte sale como un cuadro hueco y nadie se entera
        /// hasta verlo. Las flechas ↑↓←→ son el caso clásico, y por eso en esta
        /// tarjeta van dibujadas con el triángulo del kit.
        /// </summary>
        private static void ProbarNingunaEtiquetaUsaUnGlifoQueFalta(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();
            Invocar(ayuda, "Revisar");

            var sinGlifo = new List<string>();
            var conFlecha = new List<string>();

            foreach (TMP_Text texto in ayuda.GetComponentsInChildren<TMP_Text>(true))
            {
                if (string.IsNullOrEmpty(texto.text)) continue;

                if (texto.font != null && !texto.font.HasCharacters(texto.text))
                    sinGlifo.Add($"«{texto.text}» en {texto.font.name}");

                if (texto.text.IndexOfAny(new[] { '↑', '↓', '←', '→', '✕', '⏸' }) >= 0)
                    conFlecha.Add(texto.text);
            }

            int triangulos = ayuda.GetComponentsInChildren<Image>(true)
                .Count(i => i.sprite != null && i.sprite.name.StartsWith("FishyTriangulo"));

            Comprobar(log, "ninguna etiqueta de la tarjeta usa un carácter que la fuente no tenga",
                sinGlifo.Count == 0, sinGlifo.Count == 0 ? "" : string.Join(", ", sinGlifo));

            Comprobar(log, "las cuatro flechas van dibujadas y no escritas",
                conFlecha.Count == 0 && triangulos == 4,
                conFlecha.Count > 0 ? $"hay flechas escritas: {string.Join(", ", conFlecha)}"
                                    : $"{triangulos} triángulos dibujados");

            Soltar(ayuda, otto, teclado);
        }

        /// <summary>
        /// Lo que convierte la tarjeta en algo que enseña y no en un adorno: se pulsa
        /// una tecla y la de la pantalla se enciende. El estado de las teclas se le
        /// pasa a <c>Repintar</c> desde fuera justamente para poder fingirlo aquí.
        /// </summary>
        private static void ProbarLaTeclaSeEnciendeAlPulsarla(StringBuilder log)
        {
            string guardado = PlayerPrefs.GetString(TecladoEnPantalla.ClaveAprendidas, "");
            TecladoEnPantalla.Olvidar();

            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();
            Invocar(ayuda, "Revisar");

            Image w = CaraDe(ayuda, "Tecla_W");
            Image e = CaraDe(ayuda, "Tecla_E");

            Repintar(ayuda, tecla => tecla == Key.W);
            bool seEnciende = w != null &&
                              Iguales(w.color, TecladoEnPantallaTheme.Colores.Pulsada) &&
                              e != null && Iguales(e.color, TecladoEnPantallaTheme.Colores.Marcada);

            Repintar(ayuda, tecla => false);
            bool seApaga = w != null && Iguales(w.color, TecladoEnPantallaTheme.Colores.Marcada);

            // La flecha de arriba enciende SU tecla, la del bloque de flechas, y no
            // la W. En el dibujo cada una está en su sitio —que es de lo que va
            // esto—, y las dos valen para caminar: lo dice el renglón de abajo.
            Image arriba = CaraDe(ayuda, "Tecla_UpArrow");
            Repintar(ayuda, tecla => tecla == Key.UpArrow);

            bool laFlechaTambien = arriba != null &&
                Iguales(arriba.color, TecladoEnPantallaTheme.Colores.Pulsada) &&
                w != null && !Iguales(w.color, TecladoEnPantallaTheme.Colores.Pulsada);

            Comprobar(log, "la tecla se enciende al pulsarla, se apaga al soltarla, y la flecha enciende la suya",
                seEnciende && seApaga && laFlechaTambien,
                $"enciende={seEnciende}, apaga={seApaga}, flecha={laFlechaTambien}");

            Soltar(ayuda, otto, teclado);
            PlayerPrefs.SetString(TecladoEnPantalla.ClaveAprendidas, guardado);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// La tarjeta se enseña sola a callarse: cada tecla —y su renglón de la
        /// lista— se apaga en cuanto se usa, y cuando están todas usadas el dibujo
        /// desaparece y no vuelve en la próxima partida.
        /// </summary>
        private static void ProbarLoAprendidoSeAtenuaYLaTarjetaSeVa(StringBuilder log)
        {
            string guardado = PlayerPrefs.GetString(TecladoEnPantalla.ClaveAprendidas, "");
            TecladoEnPantalla.Olvidar();

            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();
            Invocar(ayuda, "Revisar");

            TMP_Text renglon = ayuda.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(t => t.name == "Leyenda_Caminar");
            Image w = CaraDe(ayuda, "Tecla_W");

            float antes = renglon != null ? renglon.alpha : -1f;

            Repintar(ayuda, tecla => tecla == Key.W);
            Repintar(ayuda, tecla => false);

            float despues = renglon != null ? renglon.alpha : -1f;
            float alfaTecla = w != null ? w.color.a : -1f;

            Comprobar(log, "la tecla ya usada y su renglón se apagan, pero siguen ahí",
                Mathf.Approximately(antes, 1f) &&
                Mathf.Approximately(despues, TecladoEnPantallaTheme.Medidas.AlfaAprendida) &&
                Mathf.Approximately(alfaTecla, TecladoEnPantallaTheme.Medidas.AlfaAprendida),
                $"renglón {antes:0.00} → {despues:0.00}, tecla {alfaTecla:0.00}");

            // Y ahora todas: la tarjeta se va, aunque con un respiro de por medio.
            Repintar(ayuda, tecla => true);
            bool seguiaVisibleAlPrincipio = Visible(ayuda);

            Adelantar(ayuda, 5f);
            Repintar(ayuda, tecla => true);

            bool seFue = !Visible(ayuda);
            bool recordado = PlayerPrefs.GetString(TecladoEnPantalla.ClaveAprendidas, "")
                .Split(',').Length >= 6;

            // Y en la próxima sesión ni se construye.
            TecladoEnPantalla otra = NuevoTeclado();
            Invocar(otra, "LeerLoAprendido");
            Invocar(otra, "Revisar");
            bool noVuelve = !Visible(otra);

            Comprobar(log, "con todas las teclas usadas la tarjeta se va y no vuelve en la próxima partida",
                seguiaVisibleAlPrincipio && seFue && recordado && noVuelve,
                $"se queda un momento={seguiaVisibleAlPrincipio}, se va={seFue}, " +
                $"se recuerda={recordado}, no vuelve={noVuelve}");

            Soltar(otra, null, null);
            Soltar(ayuda, otto, teclado);
            PlayerPrefs.SetString(TecladoEnPantalla.ClaveAprendidas, guardado);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Una ayuda visible cuando no se puede jugar estorba y confunde. Se esconde
        /// en pausa, durante una cinemática o una carga, con el celular abierto
        /// encima y cuando no hay teclado que enseñar.
        /// </summary>
        private static void ProbarNoSeVeCuandoNoToca(StringBuilder log)
        {
            string guardado = PlayerPrefs.GetString(TecladoEnPantalla.ClaveAprendidas, "");
            TecladoEnPantalla.Olvidar();
            EstadoDePausa.Restablecer();

            const string Veto = "prueba: cinemática";

            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();

            Invocar(ayuda, "Revisar");
            bool enPartida = Visible(ayuda);

            EstadoDePausa.RegistrarBloqueo(Veto, () => true);
            Invocar(ayuda, "Revisar");
            bool enCinematica = !Visible(ayuda);
            EstadoDePausa.SoltarBloqueo(Veto);

            EstadoDePausa.Pausar();
            Invocar(ayuda, "Revisar");
            bool enPausa = !Visible(ayuda);
            EstadoDePausa.Reanudar();

            // Con un diálogo abierto Otto no se puede mover, y es la señal que usa
            // todo el juego para decir "hay algo en medio". El dibujo es grande y el
            // panel de diálogo le cae encima, así que se aparta.
            otto.DisableMovement();
            Invocar(ayuda, "Revisar");
            bool enDialogo = !Visible(ayuda);
            otto.EnableMovement();

            InputSystem.RemoveDevice(teclado);
            Invocar(ayuda, "Revisar");
            bool sinTeclado = !Visible(ayuda);
            teclado = AsegurarTeclado();

            Comprobar(log, "el dibujo solo se ve mientras se camina, y con teclado delante",
                enPartida && enCinematica && enPausa && enDialogo && sinTeclado,
                $"caminando={enPartida}, en cinemática oculto={enCinematica}, " +
                $"en pausa oculto={enPausa}, en diálogo oculto={enDialogo}, " +
                $"sin teclado oculto={sinTeclado}");

            Soltar(ayuda, otto, teclado);
            PlayerPrefs.SetString(TecladoEnPantalla.ClaveAprendidas, guardado);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// El dibujo es la pieza más grande que el juego pone en pantalla sin ser
        /// una ventana, y vive pegado a una esquina: lo que hay que vigilar es que
        /// quepa entero, porque un teclado con el borde cortado deja de parecer un
        /// teclado y deja de servir para ubicarse.
        /// </summary>
        private static void ProbarElDibujoCabeEnPantalla(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();
            Invocar(ayuda, "Revisar");

            RectTransform tarjeta = Tarjeta(ayuda);
            if (tarjeta == null)
            {
                Comprobar(log, "el dibujo del teclado cabe en pantalla", false,
                    "la tarjeta no llegó a construirse");
                Soltar(ayuda, otto, teclado);
                return;
            }

            Vector2 margen = TecladoEnPantallaTheme.Medidas.Margen;
            float ancho = tarjeta.sizeDelta.x;
            float alto  = tarjeta.sizeDelta.y;

            // En coordenadas de pantalla de la resolución de referencia.
            var rTarjeta = new Rect(Referencia.x - margen.x - ancho, margen.y, ancho, alto);

            // Y no puede comerse media pantalla: es una ayuda, no la pantalla de
            // ayuda. La mitad del alto es el techo a partir del cual estorba más de
            // lo que enseña.
            bool mesurado = alto <= Referencia.y / 2f;

            Comprobar(log, "el dibujo del teclado cabe en pantalla y no se come media",
                rTarjeta.xMin >= 0f && rTarjeta.yMin >= 0f && rTarjeta.yMax <= Referencia.y &&
                mesurado,
                $"{ancho:0}x{alto:0} en x={rTarjeta.xMin:0}..{rTarjeta.xMax:0} " +
                $"y={rTarjeta.yMin:0}..{rTarjeta.yMax:0}");

            Soltar(ayuda, otto, teclado);
        }

        /// <summary>
        /// El bloque principal tiene que salir rectangular: sus seis filas miden
        /// quince unidades cada una. Una fila que sume de más o de menos sale
        /// escalonada, y un teclado escalonado deja de parecerse al que el niño/a
        /// tiene delante —que es lo único que este dibujo tiene que conseguir—.
        ///
        /// Se mide sobre el dibujo ya construido y no sobre la tabla de posiciones,
        /// porque lo que importa es dónde acaban las teclas, no lo que diga la tabla.
        /// </summary>
        private static void ProbarLasFilasDelTecladoCuadran(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();
            Invocar(ayuda, "Revisar");

            float unidad = TecladoEnPantallaTheme.Medidas.Unidad;
            float padding = TecladoEnPantallaTheme.Medidas.Padding;
            float hueco = TecladoEnPantallaTheme.Medidas.Hueco;

            float finDelBloque = padding + 15f * unidad - hueco;

            // Agrupadas por el alto al que está cada una: eso es una fila. Las
            // flechas se dejan fuera —viven a la derecha del bloque y no tienen por
            // qué llegar tan lejos como él—; de ésas se ocupa la prueba de al lado.
            var porFila = new Dictionary<float, float>();

            foreach (RectTransform tapa in Tapas(ayuda))
            {
                if (tapa.anchoredPosition.x >= finDelBloque) continue;

                float fila = Mathf.Round(tapa.anchoredPosition.y);
                float borde = tapa.anchoredPosition.x + tapa.sizeDelta.x;

                if (!porFila.TryGetValue(fila, out float peor) || borde > peor)
                    porFila[fila] = borde;
            }

            var raras = new List<string>();
            foreach (KeyValuePair<float, float> fila in porFila)
                if (Mathf.Abs(fila.Value - finDelBloque) > 0.5f)
                    raras.Add($"y={fila.Key:0} acaba en x={fila.Value:0}");

            Comprobar(log, "las seis filas del teclado miden lo mismo y acaban a la par",
                porFila.Count == 6 && raras.Count == 0,
                raras.Count > 0 ? string.Join(" · ", raras)
                                : $"{porFila.Count} filas, todas hasta x={finDelBloque:0}");

            Soltar(ayuda, otto, teclado);
        }

        /// <summary>
        /// Y las flechas, en cruz y a la derecha del bloque, como en un teclado de
        /// verdad: la de arriba sola, encima de la de abajo, con la izquierda y la
        /// derecha a los lados. Puestas en fila —o en el orden equivocado— el dibujo
        /// enseñaría a buscarlas donde no están.
        /// </summary>
        private static void ProbarLasFlechasVanEnCruz(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();
            Invocar(ayuda, "Revisar");

            RectTransform arriba  = TapaDe(ayuda, "Tecla_UpArrow");
            RectTransform abajo   = TapaDe(ayuda, "Tecla_DownArrow");
            RectTransform izq     = TapaDe(ayuda, "Tecla_LeftArrow");
            RectTransform der     = TapaDe(ayuda, "Tecla_RightArrow");

            bool estanTodas = arriba != null && abajo != null && izq != null && der != null;

            bool enCruz = estanTodas &&
                // la de arriba, justo encima de la de abajo
                Mathf.Approximately(arriba.anchoredPosition.x, abajo.anchoredPosition.x) &&
                arriba.anchoredPosition.y > abajo.anchoredPosition.y &&
                // y las otras dos a sus lados, en la misma fila
                Mathf.Approximately(izq.anchoredPosition.y, abajo.anchoredPosition.y) &&
                Mathf.Approximately(der.anchoredPosition.y, abajo.anchoredPosition.y) &&
                izq.anchoredPosition.x < abajo.anchoredPosition.x &&
                der.anchoredPosition.x > abajo.anchoredPosition.x;

            float finDelBloque = TecladoEnPantallaTheme.Medidas.Padding +
                                 15f * TecladoEnPantallaTheme.Medidas.Unidad;
            bool aLaDerecha = estanTodas && izq.anchoredPosition.x >= finDelBloque;

            Comprobar(log, "las cuatro flechas van en cruz, a la derecha del teclado",
                enCruz && aLaDerecha,
                !estanTodas ? "faltan flechas en el dibujo"
                            : $"arriba x={arriba.anchoredPosition.x:0} y={arriba.anchoredPosition.y:0}, " +
                              $"abajo x={abajo.anchoredPosition.x:0} y={abajo.anchoredPosition.y:0}");

            Soltar(ayuda, otto, teclado);
        }

        /// <summary>
        /// Nada se derrama fuera de la tarjeta. Lo que más fácil se sale son los
        /// renglones de la lista: son frases en castellano, de largo distinto, y se
        /// colocan midiéndolas con TMP y saltando de línea cuando no caben. Si esa
        /// cuenta se tuerce, el texto se va por el borde y no se ve.
        /// </summary>
        private static void ProbarNadaSeSaleDeLaTarjeta(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();
            Invocar(ayuda, "Revisar");

            RectTransform tarjeta = Tarjeta(ayuda);
            float padding = TecladoEnPantallaTheme.Medidas.Padding;
            float ancho = tarjeta.sizeDelta.x;
            float alto  = tarjeta.sizeDelta.y;

            var fuera = new List<string>();

            foreach (RectTransform hijo in tarjeta)
            {
                // Cada hijo está colocado por su esquina de arriba a la izquierda, y
                // los textos llevan el ancho que midió TMP.
                float x = hijo.anchoredPosition.x;
                float y = hijo.anchoredPosition.y;
                float derecha = x + hijo.sizeDelta.x;
                float abajo   = y - hijo.sizeDelta.y;

                bool dentro = x >= padding - 0.5f && derecha <= ancho - padding + 0.5f &&
                              y <= -padding + 0.5f && abajo >= -(alto - padding) - 0.5f;

                if (!dentro)
                    fuera.Add($"{hijo.name} en x={x:0}..{derecha:0} y={y:0}..{abajo:0}");
            }

            Comprobar(log, "ni una tecla ni un renglón se sale de la tarjeta",
                fuera.Count == 0,
                fuera.Count == 0 ? $"tarjeta de {ancho:0}x{alto:0}"
                                 : string.Join(" · ", fuera.Take(3)));

            Soltar(ayuda, otto, teclado);
        }

        // ── Andamios ─────────────────────────────────────────────────────────────

        /// <summary>
        /// El factor por el que un CanvasScaler multiplica los tamaños de diseño.
        /// Es la fórmula de Unity para ScaleWithScreenSize + MatchWidthOrHeight:
        /// la media geométrica de los dos factores, ponderada por el match.
        /// </summary>
        private static float FactorDePantalla(Vector2 pantalla, float match)
        {
            float logAncho = Mathf.Log(pantalla.x / Referencia.x, 2f);
            float logAlto  = Mathf.Log(pantalla.y / Referencia.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logAncho, logAlto, match));
        }

        /// <summary>Lo que mide la caja del cuerpo del diálogo con las medidas del
        /// tema. Misma cuenta que hace el skin al colocarla.</summary>
        private static Vector2 CajaDelCuerpo()
        {
            float ancho = DialogoNeutroTheme.Medidas.TamanoPanel.x -
                          2f * DialogoNeutroTheme.Medidas.Margen -
                          DialogoNeutroTheme.Medidas.LadoRetrato -
                          DialogoNeutroTheme.Medidas.HuecoRetrato;

            float alto = DialogoNeutroTheme.Medidas.TamanoPanel.y -
                         2f * DialogoNeutroTheme.Medidas.Margen -
                         DialogoNeutroTheme.Medidas.AltoNombre -
                         DialogoNeutroTheme.Medidas.Separacion;
            return new Vector2(ancho, alto);
        }

        /// <summary>El rectángulo de un hijo en coordenadas de su padre.</summary>
        private static Rect EnElPadre(RectTransform rt)
        {
            Vector2 centro = rt.anchoredPosition;
            Vector2 tamano = rt.rect.size;
            return new Rect(centro.x - tamano.x / 2f, centro.y - tamano.y / 2f, tamano.x, tamano.y);
        }

        /// <summary>Una réplica del panel de diálogo de MainScene, con las medidas
        /// malas que traía: así la prueba comprueba que el skin las arregla y no que
        /// alguien ya las dejó bien.</summary>
        private static GameObject PanelDeDialogoDePrueba(out TMP_Text nombre, out TMP_Text texto,
            out Image retrato)
        {
            var panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var raiz = (RectTransform)panel.transform;
            raiz.sizeDelta = new Vector2(800f, 200f);

            RectTransform rNombre = Hijo(raiz, "NPCNameText", new Vector2(-125f, 24f),
                new Vector2(200f, 50f));
            nombre = rNombre.gameObject.AddComponent<TextMeshProUGUI>();
            nombre.fontSize = 36f;
            nombre.text = "Foca de Weddell";

            RectTransform rTexto = Hijo(raiz, "DialogueText", new Vector2(75f, -35f),
                new Vector2(600f, 100f));
            texto = rTexto.gameObject.AddComponent<TextMeshProUGUI>();
            texto.fontSize = 24f;

            RectTransform rCara = Hijo(raiz, "DialoguePortrait", new Vector2(-310f, 0f),
                new Vector2(128f, 128f));
            retrato = rCara.gameObject.AddComponent<Image>();

            return panel;
        }

        private static RectTransform Hijo(RectTransform padre, string nombre, Vector2 posicion,
            Vector2 tamano)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = posicion;
            rt.sizeDelta = tamano;
            return rt;
        }

        /// <summary>
        /// Un Otto mínimo con su ciclo de vida disparado a mano: en edición Unity no
        /// llama a Awake —que es donde se arman las ataduras de entrada que estas
        /// pruebas leen— ni a OnEnable ni a Start. Copiado de FishyPruebasPausa, y
        /// sin hideFlags por lo mismo: FindObjectsByType no devuelve lo escondido, y
        /// «hay un Otto en la escena» es justo lo que la tarjeta pregunta.
        /// </summary>
        private static OttoController NuevoOtto()
        {
            var go = new GameObject("OttoDePrueba");
            var otto = go.AddComponent<OttoController>();
            otto.enableOnStart = true;

            Invocar(otto, "Awake");
            Invocar(otto, "OnEnable");
            Invocar(otto, "Start");
            return otto;
        }

        /// <summary>
        /// La tarjeta, sin pasar por su Awake: ese Awake llama a DontDestroyOnLoad,
        /// que fuera del modo Play no avisa sino que TIRA InvalidOperationException.
        /// Lo que el Awake hace de útil —mirar si ya está todo aprendido— se repite
        /// aquí a mano.
        /// </summary>
        private static TecladoEnPantalla NuevoTeclado()
        {
            var go = new GameObject("TecladoDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var ayuda = go.AddComponent<TecladoEnPantalla>();
            Invocar(ayuda, "LeerLoAprendido");
            return ayuda;
        }

        /// <summary>
        /// En batch y sin gráficos no hay teclado conectado, y la tarjeta —con razón—
        /// no se muestra sin uno. Se añade uno virtual, que es para lo que el Input
        /// System trae AddDevice.
        /// </summary>
        private static Keyboard AsegurarTeclado()
        {
            return Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        }

        private static void Soltar(TecladoEnPantalla ayuda, OttoController otto, Keyboard teclado)
        {
            if (ayuda != null) UnityEngine.Object.DestroyImmediate(ayuda.gameObject);

            if (otto != null)
            {
                Invocar(otto, "OnDisable");
                UnityEngine.Object.DestroyImmediate(otto.gameObject);
            }

            if (teclado != null && teclado.added) InputSystem.RemoveDevice(teclado);

            EstadoDePausa.Restablecer();
        }

        private static bool Visible(TecladoEnPantalla ayuda)
        {
            var raiz = ayuda.GetType().GetField("_raiz", Privado).GetValue(ayuda) as GameObject;
            return raiz != null && raiz.activeSelf;
        }

        private static RectTransform Tarjeta(TecladoEnPantalla ayuda)
            => ayuda.GetType().GetField("_tarjeta", Privado).GetValue(ayuda) as RectTransform;

        /// <summary>Las tapas del dibujo del teclado: las del juego se llaman
        /// «Tecla_X» y las de relleno «Tapa».</summary>
        private static List<RectTransform> Tapas(TecladoEnPantalla ayuda)
        {
            var tapas = new List<RectTransform>();

            foreach (RectTransform hijo in Tarjeta(ayuda))
                if (hijo.name == "Tapa" || hijo.name.StartsWith("Tecla_")) tapas.Add(hijo);

            return tapas;
        }

        private static RectTransform TapaDe(TecladoEnPantalla ayuda, string nombre)
        {
            foreach (RectTransform tapa in Tapas(ayuda))
                if (tapa.name == nombre) return tapa;

            return null;
        }

        private static Image CaraDe(TecladoEnPantalla ayuda, string nombre)
            => ayuda.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == nombre);

        /// <summary>Le dice a la tarjeta qué teclas están pulsadas sin tener que
        /// pulsarlas: <c>Repintar</c> recibe de quién preguntar justo para esto.</summary>
        private static void Repintar(TecladoEnPantalla ayuda, Func<Key, bool> pulsada)
        {
            ayuda.GetType().GetMethod("Repintar", Privado).Invoke(ayuda, new object[] { pulsada });
        }

        /// <summary>Envejece el reloj interno de la despedida. El tiempo real no se
        /// puede adelantar, así que se retrasa el momento que la tarjeta recuerda.</summary>
        private static void Adelantar(TecladoEnPantalla ayuda, float segundos)
        {
            FieldInfo campo = ayuda.GetType().GetField("_momentoEnQueSeAprendioTodo", Privado);
            campo.SetValue(ayuda, (float)campo.GetValue(ayuda) - segundos);
        }

        /// <summary>Las teclas de teclado de una acción, en el orden en que están
        /// atadas y sin repetir. Es la verdad contra la que se compara la tarjeta.</summary>
        private static List<Key> TeclasDeTeclado(InputAction accion)
        {
            var teclas = new List<Key>();
            if (accion == null) return teclas;

            foreach (InputBinding atadura in accion.bindings)
            {
                const string Prefijo = "<Keyboard>/";
                if (string.IsNullOrEmpty(atadura.path) ||
                    !atadura.path.StartsWith(Prefijo, StringComparison.Ordinal)) continue;

                if (Enum.TryParse(atadura.path.Substring(Prefijo.Length), true, out Key tecla) &&
                    !teclas.Contains(tecla))
                    teclas.Add(tecla);
            }

            return teclas;
        }

        /// <summary>El respaldo que usa la tarjeta mientras no hay Otto, sacado de
        /// ella misma: se le pide lo que enseñaría sin controlador.</summary>
        private static List<Key> TeclasDeCaminarSinOtto()
        {
            MethodInfo metodo = typeof(TecladoEnPantalla)
                .GetMethod("TeclasDeCaminar", PrivadoEstatico);

            return (List<Key>)metodo.Invoke(null, new object[] { null });
        }

        private static bool Coinciden(Dictionary<string, List<Key>> ensenadas, string accion,
            List<Key> esperadas)
        {
            return ensenadas.TryGetValue(accion, out List<Key> teclas) &&
                   MismoConjunto(teclas, esperadas);
        }

        private static bool MismoConjunto(List<Key> unas, List<Key> otras)
            => unas != null && otras != null && unas.Count == otras.Count &&
               !unas.Except(otras).Any();

        private static T ConstanteDe<T>(Type tipo, string nombre)
        {
            FieldInfo campo = tipo.GetField(nombre, PrivadoEstatico);
            return campo == null ? default : (T)campo.GetValue(null);
        }

        private static bool Iguales(Color una, Color otra)
            => Mathf.Approximately(una.r, otra.r) && Mathf.Approximately(una.g, otra.g) &&
               Mathf.Approximately(una.b, otra.b);

        /// <summary>Un color translúcido puesto encima de otro.</summary>
        private static Color Componer(Color encima, Color debajo)
        {
            return new Color(
                encima.r * encima.a + debajo.r * (1f - encima.a),
                encima.g * encima.a + debajo.g * (1f - encima.a),
                encima.b * encima.a + debajo.b * (1f - encima.a),
                1f);
        }

        /// <summary>Contraste WCAG entre dos colores opacos: de 1:1 (invisible) a
        /// 21:1 (negro sobre blanco). AA pide 4,5 para texto normal.</summary>
        private static float Contraste(Color una, Color otra)
        {
            float l1 = Luminancia(una);
            float l2 = Luminancia(otra);
            if (l1 < l2) (l1, l2) = (l2, l1);

            return (l1 + 0.05f) / (l2 + 0.05f);
        }

        private static float Luminancia(Color color)
        {
            return 0.2126f * Lineal(color.r) + 0.7152f * Lineal(color.g) + 0.0722f * Lineal(color.b);
        }

        private static float Lineal(float canal)
            => canal <= 0.03928f ? canal / 12.92f : Mathf.Pow((canal + 0.055f) / 1.055f, 2.4f);

        private static string Recortar(string texto)
            => texto.Length <= 40 ? texto : texto.Substring(0, 40) + "…";

        private static void Invocar(MonoBehaviour comp, string metodo)
            => comp.GetType().GetMethod(metodo, Privado)?.Invoke(comp, null);

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
