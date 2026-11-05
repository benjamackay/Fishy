#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Fishy.Mision;
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
    ///  B) que la tarjeta de controles de la pausa (<see cref="TecladoEnPantalla"/>)
    ///     enseñe las teclas que el juego escucha de verdad, y no unas escritas a mano.
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
            ProbarLaTarjetaNoSeVaAunqueSePulsenTodas(log);
            ProbarNingunaFilaSeSaleDeLaTarjeta(log);
            ProbarElCartelDeInteractuarNoSePisaConElRecordatorio(log);

            // ── C) Lo que pide la misión activa ──
            ProbarLaMisionDestacaLaTeclaQueLaCierra(log);
            ProbarAbrirElPanelDestacaLaTeclaDelCelular(log);

            // ── D) Las misiones del tutorial, una por paso ──
            ProbarElTutorialSePartEnMisionesDeUnObjetivo(log);
            ProbarTerminarUnaMisionEntregaLaSiguiente(log);
            ProbarElChatDeLaFlamencaCierraSuObjetivo(log);

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

            Comprobar(log, "la tarjeta de controles se construye y se abre sin una sola excepción",
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

            Dictionary<string, List<Key>> ensenadas = ayuda.LoQueEnsena();

            List<Key> deVerdadCaminar = TeclasDeTeclado(otto.AccionMover);
            List<Key> deVerdadCorrer  = TeclasDeTeclado(otto.AccionCorrer);

            bool caminar = Coinciden(ensenadas, "Caminar", deVerdadCaminar);
            bool correr  = Coinciden(ensenadas, "Correr", deVerdadCorrer);
            bool sinSaltar = !ensenadas.ContainsKey("Saltar");
            bool hablar  = Coinciden(ensenadas, "Hablar", new List<Key> { Key.E });
            bool celular = Coinciden(ensenadas, "Celular",
                new List<Key> { MenuController.TeclaDelCelular });
            bool pausa   = Coinciden(ensenadas, "Pausa",
                new List<Key> { MenuPausa.TeclaDePausa });

            Comprobar(log, "las teclas de la tarjeta son las que el juego escucha de verdad",
                caminar && correr && sinSaltar && hablar && celular && pausa,
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
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();

            Image w = CaraDe(ayuda, "Tecla_W");
            Image e = CaraDe(ayuda, "Tecla_E");

            Repintar(ayuda, tecla => tecla == Key.W);
            bool seEnciende = w != null &&
                              Iguales(w.color, TecladoEnPantallaTheme.Colores.TeclaPulsada) &&
                              e != null && Iguales(e.color, TecladoEnPantallaTheme.Colores.Tecla);

            Repintar(ayuda, tecla => false);
            bool seApaga = w != null && Iguales(w.color, TecladoEnPantallaTheme.Colores.Tecla);

            // La flecha de arriba enciende la MISMA tecla que la W: para el niño/a
            // son la misma cosa, y así se enseña la equivalencia sin decirla.
            Repintar(ayuda, tecla => tecla == Key.UpArrow);
            bool laFlechaTambien = w != null &&
                                   Iguales(w.color, TecladoEnPantallaTheme.Colores.TeclaPulsada);

            Comprobar(log, "la tecla se enciende al pulsarla, se apaga al soltarla, y la flecha vale igual",
                seEnciende && seApaga && laFlechaTambien,
                $"enciende={seEnciende}, apaga={seApaga}, flecha={laFlechaTambien}");

            Soltar(ayuda, otto, teclado);
        }

        /// <summary>
        /// La tarjeta es una consulta y no una lección: pulsar todas las teclas no la
        /// apaga ni la atenúa. Antes se iba sola y no había forma de volver a verla.
        /// </summary>
        private static void ProbarLaTarjetaNoSeVaAunqueSePulsenTodas(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();

            Repintar(ayuda, tecla => true);
            Repintar(ayuda, tecla => false);
            Repintar(ayuda, tecla => true);

            bool sigue = Visible(ayuda) && ayuda.Abierto;

            ayuda.Cerrar();
            bool cierra = !Visible(ayuda);

            ayuda.Abrir();
            Image w = CaraDe(ayuda, "Tecla_W");
            bool vuelveLimpia = Visible(ayuda) && w != null &&
                                Iguales(w.color, TecladoEnPantallaTheme.Colores.Tecla);

            Comprobar(log, "la tarjeta de controles no se va aunque se pulsen todas, y se puede volver a abrir",
                sigue && cierra && vuelveLimpia,
                $"sigue={sigue}, cierra={cierra}, vuelve sin teclas encendidas={vuelveLimpia}");

            Soltar(ayuda, otto, teclado);
        }

        /// <summary>
        /// El ancho de cada tecla se calcula a ojo de buen cubero (tantos píxeles por
        /// carácter). Esto mide con TMP lo que de verdad ocupa cada fila y comprueba
        /// que no se derrama fuera de la tarjeta, que es lo que esa aproximación
        /// puede romper al cambiar un texto.
        /// </summary>
        private static void ProbarNingunaFilaSeSaleDeLaTarjeta(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();

            float limite = TecladoEnPantallaTheme.Medidas.AnchoPanel -
                           TecladoEnPantallaTheme.Medidas.Padding;

            string peor = "";
            float peorBorde = 0f;

            foreach (TMP_Text texto in ayuda.GetComponentsInChildren<TMP_Text>(true))
            {
                // Las letras de las teclas van estiradas dentro de su cara, así que
                // su posición no dice nada: la que cuenta es la de los nombres.
                if (texto.name != "Etiqueta" && texto.name != "Pista" && texto.name != "Titulo")
                    continue;

                float borde = texto.rectTransform.anchoredPosition.x +
                              texto.GetPreferredValues().x;

                if (borde <= peorBorde) continue;
                peorBorde = borde;
                peor = $"«{texto.text}»";
            }

            Comprobar(log, "ningún nombre de la tarjeta se derrama fuera de ella",
                peorBorde > 0f && peorBorde <= limite,
                $"el más largo llega a x={peorBorde:0} de {limite:0} · {peor}");

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

        /// <summary>La tarjeta de controles, montada en un padre de prueba como la
        /// monta la pausa en su canvas, y ya abierta.</summary>
        private static TecladoEnPantalla NuevoTeclado()
        {
            var padre = new GameObject("PausaDePrueba", typeof(RectTransform))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            TecladoEnPantalla ayuda = TecladoEnPantalla.CrearEn(padre.transform, null);
            ayuda.Abrir();
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
            if (ayuda != null) UnityEngine.Object.DestroyImmediate(ayuda.transform.root.gameObject);

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

        private static Image CaraDe(TecladoEnPantalla ayuda, string nombre)
            => ayuda.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == nombre);

        /// <summary>Le dice a la tarjeta qué teclas están pulsadas sin tener que
        /// pulsarlas: <c>Repintar</c> recibe de quién preguntar justo para esto.</summary>
        private static void Repintar(TecladoEnPantalla ayuda, Func<Key, bool> pulsada)
        {
            ayuda.GetType().GetMethod("Repintar", Privado).Invoke(ayuda, new object[] { pulsada });
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

            var porDireccion = (IEnumerable<List<Key>>)metodo.Invoke(null, new object[] { null });

            var todas = new List<Key>();
            foreach (List<Key> direccion in porDireccion)
                foreach (Key tecla in direccion)
                    if (!todas.Contains(tecla)) todas.Add(tecla);

            return todas;
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

        /// <summary>
        /// El cartel de «para interactuar» vivía abajo a la izquierda, que es donde
        /// ahora sale el recordatorio. Se corrió al lado desde su tema, y esto
        /// comprueba que de verdad no se pisan: si alguien engorda la tarjeta y no
        /// mueve el cartel, el cartel se dibujaría debajo (orden 0 contra 800) y el
        /// niño/a no vería el aviso justo cuando tiene a alguien delante.
        ///
        /// Se mide con la tarjeta de la pausa porque el contenido es el mismo y su
        /// <c>sizeDelta</c> viene sin escalar; como recordatorio va a tamaño 1 en la
        /// esquina, así que esas son las medidas que acaba ocupando.
        /// </summary>
        private static void ProbarElCartelDeInteractuarNoSePisaConElRecordatorio(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();

            RectTransform tarjeta = Tarjeta(ayuda);
            Vector2 margen = TecladoEnPantallaTheme.Medidas.MargenRecordatorio;
            var rTeclado = new Rect(margen.x, margen.y, tarjeta.sizeDelta.x, tarjeta.sizeDelta.y);

            CartelInteraccionSkin.Reiniciar();
            GameObject cartel = CartelDePrueba();
            CartelInteraccionSkin.Aplicar(cartel);

            var raiz = (RectTransform)cartel.transform;
            var fondo = (RectTransform)raiz.Find(CartelInteraccionSkin.NombreFondo);

            // El fondo, en coordenadas de pantalla: el cartel está anclado a la
            // esquina de abajo a la izquierda y el fondo colocado dentro de él.
            Vector2 centro = raiz.anchoredPosition + fondo.anchoredPosition;
            var rCartel = new Rect(centro.x - fondo.sizeDelta.x / 2f,
                                   centro.y - fondo.sizeDelta.y / 2f,
                                   fondo.sizeDelta.x, fondo.sizeDelta.y);

            Vector2 pedida = CartelInteraccionTheme.Medidas.Posicion;
            bool dondeSePidio = Mathf.Abs(rCartel.xMin - pedida.x) < 0.5f &&
                                Mathf.Abs(rCartel.yMin - pedida.y) < 0.5f;

            Comprobar(log, "el cartel de «para interactuar» se aparta del recordatorio de teclas",
                dondeSePidio && !rCartel.Overlaps(rTeclado),
                $"cartel x={rCartel.xMin:0}..{rCartel.xMax:0} y={rCartel.yMin:0}..{rCartel.yMax:0} · " +
                $"tarjeta hasta x={rTeclado.xMax:0}");

            UnityEngine.Object.DestroyImmediate(cartel);
            Soltar(ayuda, otto, teclado);
        }

        // ── C) Lo que pide la misión activa ──────────────────────────────────────

        /// <summary>
        /// Lo que pidió el equipo: que las teclas que hacen falta para la misión
        /// activa se remarquen. La tarjeta se engancha a los mismos avisos que el
        /// cartel de misión, y el tipo de cada objetivo pendiente dice qué tecla
        /// señalar.
        ///
        /// Las dos mitades de la regla: «llegar a una zona» señala las de caminar, y
        /// todo lo demás —recoger, hablar, chatear, abrir un caso— señala la de
        /// interactuar, porque las cuatro piezas del mundo se abren con la misma
        /// tecla.
        /// </summary>
        private static void ProbarLaMisionDestacaLaTeclaQueLaCierra(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();

            var objetivos = new List<ObjetivoMision>
            {
                new ObjetivoMision { tipo = TipoObjetivo.LlegarAZona, zonaDestino = "zona_9" },
            };
            DarMisionActiva("MISION_DE_PRUEBA", objetivos);

            // Abrir es quien se apunta a las misiones y mira qué piden: es el camino
            // que recorre el niño/a al entrar en Configuración ▸ Controles.
            ayuda.Abrir();
            Repintar(ayuda, tecla => false);

            List<string> pideAlPrincipio = LoQuePide(ayuda);
            Image w = CaraDe(ayuda, "Tecla_W");
            Image e = CaraDe(ayuda, "Tecla_E");

            bool caminarDestacado = pideAlPrincipio.Contains("Caminar") &&
                                    pideAlPrincipio.Count == 1 &&
                                    Iguales(w.color, TecladoEnPantallaTheme.Colores.Destacada) &&
                                    !Iguales(e.color, TecladoEnPantallaTheme.Colores.Destacada);

            // Cumplido ese objetivo y pendiente uno de hablar: cambia la tecla que se
            // señala. Es el mismo camino que recorre una misión de verdad.
            objetivos[0].cumplido = true;
            objetivos.Add(new ObjetivoMision { tipo = TipoObjetivo.HablarConNpc, dialogoNpcId = "X" });

            Invocar(ayuda, "MirarQuePideLaMision");
            Repintar(ayuda, tecla => false);

            List<string> pideAhora = LoQuePide(ayuda);
            bool hablarDestacado = pideAhora.Contains("Hablar") && pideAhora.Count == 1 &&
                                   Iguales(e.color, TecladoEnPantallaTheme.Colores.Destacada) &&
                                   !Iguales(w.color, TecladoEnPantallaTheme.Colores.Destacada);

            Comprobar(log, "la misión activa destaca la tecla que cierra lo que queda por hacer",
                caminarDestacado && hablarDestacado,
                $"con «llegar a zona» pide [{string.Join(", ", pideAlPrincipio)}], " +
                $"con «hablar» pide [{string.Join(", ", pideAhora)}]");

            // Y el nombre de la fila acompaña: una tecla que late sin decir para qué
            // sirve no enseña nada.
            TMP_Text nombre = EtiquetaDeLaFila(ayuda, "Hablar");

            Comprobar(log, "y el nombre de su fila se pinta del mismo color",
                nombre != null &&
                Iguales(nombre.color, TecladoEnPantallaTheme.Colores.Destacada),
                nombre == null ? "no encontré la etiqueta de la fila" : $"color {nombre.color}");

            QuitarMision();
            Soltar(ayuda, otto, teclado);
        }

        /// <summary>
        /// El tipo de objetivo nuevo para abrir el celular tiene que llevar a la
        /// tarjeta a destacar el Tab y no la E, que es la respuesta por defecto de
        /// todos los demás.
        /// </summary>
        private static void ProbarAbrirElPanelDestacaLaTeclaDelCelular(StringBuilder log)
        {
            OttoController otto = NuevoOtto();
            Keyboard teclado = AsegurarTeclado();
            TecladoEnPantalla ayuda = NuevoTeclado();

            DarMisionActiva("MISION_DE_PRUEBA", new List<ObjetivoMision>
            {
                new ObjetivoMision { tipo = TipoObjetivo.AbrirElPanel },
            });

            ayuda.Abrir();
            Repintar(ayuda, tecla => false);

            List<string> pide = LoQuePide(ayuda);
            Image tab = CaraDe(ayuda, "Tecla_Tab");
            Image e = CaraDe(ayuda, "Tecla_E");

            Comprobar(log, "una misión de abrir el celular destaca el Tab, no la E",
                pide.Count == 1 && pide.Contains("Celular") &&
                Iguales(tab.color, TecladoEnPantallaTheme.Colores.Destacada) &&
                !Iguales(e.color, TecladoEnPantallaTheme.Colores.Destacada),
                $"pide [{string.Join(", ", pide)}]");

            QuitarMision();
            Soltar(ayuda, otto, teclado);
        }

        // ── D) Las misiones del tutorial ─────────────────────────────────────────

        /// <summary>
        /// Lo que se pidió: una misión por paso del guión, cada una con un solo
        /// objetivo. Con los cuatro objetivos en una sola misión, el cartel los
        /// enseñaría todos y la tarjeta destacaría todas sus teclas a la vez; así, en
        /// cada momento hay exactamente una cosa que hacer.
        /// </summary>
        private static void ProbarElTutorialSePartEnMisionesDeUnObjetivo(StringBuilder log)
        {
            MisionesDelTutorial cadena = NuevaCadenaDelTutorial(out _);

            List<object> eslabones = Eslabones(cadena);
            var ids = new List<string>();
            var tipos = new List<string>();
            bool todosDeUno = true;

            foreach (object eslabon in eslabones)
            {
                var ficha = (DesafioData)Campo(eslabon, "Ficha");
                var objetivo = (ObjetivoMision)Campo(eslabon, "Objetivo");

                ids.Add(ficha.desafioId);
                tipos.Add(objetivo.tipo.ToString());

                // Un objetivo por misión, y numerado: sin número no se puede cerrar
                // desde fuera ni guardar su avance.
                todosDeUno &= objetivo.ordenCatalogo == 1;
            }

            Comprobar(log, "el tutorial se parte en tres misiones encadenadas de un objetivo cada una",
                ids.Count == 3 && todosDeUno &&
                ids[0] == MisionesDelTutorial.IdFlamenca &&
                ids[1] == MisionesDelTutorial.IdPanel &&
                ids[2] == MisionesDelTutorial.IdCaso &&
                tipos[0] == TipoObjetivo.ChatearCaraACara.ToString() &&
                tipos[1] == TipoObjetivo.AbrirElPanel.ToString() &&
                tipos[2] == TipoObjetivo.CompletarCasoDetective.ToString(),
                string.Join(" → ", ids) + "  [" + string.Join(", ", tipos) + "]");

            SoltarCadena(cadena);
        }

        /// <summary>
        /// Terminar una abre la siguiente, y solo la siguiente: una misión que no es
        /// la que tocaba —otra del juego, o la misma dos veces— no adelanta la
        /// cadena.
        /// </summary>
        private static void ProbarTerminarUnaMisionEntregaLaSiguiente(StringBuilder log)
        {
            MisionesDelTutorial cadena = NuevaCadenaDelTutorial(out DesafioData primera);

            // Una que no es la suya: no pasa nada.
            Completar(cadena, "OTRA_MISION_CUALQUIERA");
            bool noSeAdelanta = Entregados(cadena) == 0;

            // La del huemul: entra la de la flamenca.
            Completar(cadena, primera.desafioId);
            bool entregoLaPrimera = Entregados(cadena) == 1 &&
                MissionTracker.Instance.Objetivos(MisionesDelTutorial.IdFlamenca).Count == 1;

            // Y la de la flamenca abre la del Tab.
            Completar(cadena, MisionesDelTutorial.IdFlamenca);
            bool entregoLaSegunda = Entregados(cadena) == 2 &&
                MissionTracker.Instance.Objetivos(MisionesDelTutorial.IdPanel).Count == 1;

            Comprobar(log, "terminar una misión del tutorial entrega la siguiente, y solo la siguiente",
                noSeAdelanta && entregoLaPrimera && entregoLaSegunda,
                $"sin adelantarse={noSeAdelanta}, flamenca={entregoLaPrimera}, panel={entregoLaSegunda}");

            SoltarCadena(cadena);
        }

        /// <summary>
        /// El disparador que hay que traer a mano: el chat con opciones avisa por un
        /// evento de C#, no por uno de Unity, así que su objetivo no se puede cerrar
        /// solo. Lo cierra la cadena del tutorial con <c>MissionTracker.Cumplir</c>,
        /// que es la puerta nueva del rastreador.
        /// </summary>
        private static void ProbarElChatDeLaFlamencaCierraSuObjetivo(StringBuilder log)
        {
            MisionesDelTutorial cadena = NuevaCadenaDelTutorial(out DesafioData primera);
            Completar(cadena, primera.desafioId);   // entra la misión de la flamenca

            IReadOnlyList<ObjetivoMision> objetivos =
                MissionTracker.Instance.Objetivos(MisionesDelTutorial.IdFlamenca);

            bool empiezaPendiente = objetivos.Count == 1 && !objetivos[0].cumplido;

            // Lo que hace el evento del chat al cerrarse.
            cadena.GetType().GetMethod("AlCerrarElChat", Privado)
                .Invoke(cadena, new object[] { 1f });

            bool seCierra = objetivos[0].cumplido;
            string porQue = objetivos[0].PorQue ?? "";

            Comprobar(log, "cerrar el chat de la flamenca cierra su objetivo, y deja dicho quién fue",
                empiezaPendiente && seCierra && porQue.Contains("tutorial"),
                $"pendiente al empezar={empiezaPendiente}, cumplido={seCierra}, porqué «{porQue}»");

            SoltarCadena(cadena);
        }

        // ── Andamios de las misiones ─────────────────────────────────────────────

        /// <summary>El nombre de una fila de la tarjeta («Hablar y seguir»), que es el
        /// texto llamado «Etiqueta» dentro del grupo de esa fila.</summary>
        private static TMP_Text EtiquetaDeLaFila(TecladoEnPantalla ayuda, string fila)
        {
            Transform grupo = Tarjeta(ayuda).Find("Fila_" + fila);
            if (grupo == null) return null;

            Transform etiqueta = grupo.Find("Etiqueta");
            return etiqueta != null ? etiqueta.GetComponent<TMP_Text>() : null;
        }

        /// <summary>Las acciones que la tarjeta está destacando ahora mismo. El campo
        /// es un conjunto de un enum privado, así que se leen por su nombre.</summary>
        private static List<string> LoQuePide(TecladoEnPantalla ayuda)
        {
            var pide = new List<string>();

            var conjunto = ayuda.GetType().GetField("_loQuePideLaMision", Privado)
                ?.GetValue(ayuda) as System.Collections.IEnumerable;

            if (conjunto != null)
                foreach (object accion in conjunto) pide.Add(accion.ToString());

            pide.Sort();
            return pide;
        }

        /// <summary>Una réplica del cartel de «para interactuar» de la escena: la
        /// manito con la letra y el texto al lado, apagado como está allí.</summary>
        private static GameObject CartelDePrueba()
        {
            var cartel = new GameObject("InteractionPrompt", typeof(RectTransform))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var raiz = (RectTransform)cartel.transform;
            raiz.anchorMin = raiz.anchorMax = Vector2.zero;
            raiz.anchoredPosition = new Vector2(270f, 85f);
            raiz.sizeDelta = new Vector2(500f, 150f);

            Hijo(raiz, "Icono", new Vector2(75f, 75f), new Vector2(150f, 150f));
            RectTransform etiqueta = Hijo(raiz, "Label", new Vector2(160f, 73f),
                new Vector2(340f, 50f));
            etiqueta.pivot = new Vector2(0f, 0.5f);
            etiqueta.gameObject.AddComponent<TextMeshProUGUI>().text = "para interactuar";

            cartel.SetActive(false);
            return cartel;
        }

        /// <summary>
        /// Una misión activa con los objetivos que se le pasen, montada como la monta
        /// el juego: el rastreador la sigue y el gestor la pone de activa.
        ///
        /// Los dos singletons se ponen por reflexión en vez de por GetOrCreate: su
        /// Awake llama a DontDestroyOnLoad, que fuera del modo Play no avisa sino que
        /// TIRA excepción. Mismo truco que FishyPruebasPausa con el SaveManager.
        /// </summary>
        private static void DarMisionActiva(string id, List<ObjetivoMision> objetivos)
        {
            var desafio = ScriptableObject.CreateInstance<DesafioData>();
            desafio.desafioId = id;
            desafio.titulo = "Misión de prueba";

            var rastreadorGO = new GameObject("MissionTrackerDePrueba")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var rastreador = rastreadorGO.AddComponent<MissionTracker>();
            PonerInstancia(typeof(MissionTracker), rastreador);
            rastreador.Seguir(desafio, objetivos);

            var managerGO = new GameObject("MissionManagerDePrueba")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var manager = managerGO.AddComponent<MissionManager>();
            PonerInstancia(typeof(MissionManager), manager);

            typeof(MissionManager)
                .GetField("<Activa>k__BackingField", Privado)
                ?.SetValue(manager, new DesafioRuntime { data = desafio });
        }

        private static void QuitarMision()
        {
            foreach (MissionTracker rastreador in UnityEngine.Object.FindObjectsByType<MissionTracker>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(rastreador.gameObject);

            foreach (MissionManager manager in UnityEngine.Object.FindObjectsByType<MissionManager>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(manager.gameObject);

            PonerInstancia(typeof(MissionTracker), null);
            PonerInstancia(typeof(MissionManager), null);
        }

        private static void PonerInstancia(Type tipo, MonoBehaviour quien)
        {
            tipo.GetField("<Instance>k__BackingField", PrivadoEstatico)?.SetValue(null, quien);
        }

        /// <summary>
        /// La cadena de misiones del tutorial, con el mínimo de escena que necesita:
        /// un TutorialPasos del que saca sus piezas, un MissionGiver con la ficha de
        /// la misión del huemul y una foca a la que apuntar.
        ///
        /// El Start se invoca a mano, como todo en edición. El MissionManager y el
        /// MissionTracker se crean con GetOrCreate: su Awake no corre fuera de Play,
        /// que es justamente lo que evita el DontDestroyOnLoad que tiraría excepción.
        /// </summary>
        private static MisionesDelTutorial NuevaCadenaDelTutorial(out DesafioData primera)
        {
            QuitarMision();

            var tutorialGO = new GameObject("TutorialPasosDePrueba");
            var tutorial = tutorialGO.AddComponent<TutorialPasos>();
            tutorial.npcDetective = new GameObject("FocaDePrueba")
                .AddComponent<Fishy.Detective.DetectiveLauncher>();

            primera = ScriptableObject.CreateInstance<DesafioData>();
            primera.desafioId = "TUTORIAL_PRIMEROS_PASOS_DE_PRUEBA";
            primera.titulo = "Primeros pasos";

            var giro = new GameObject("MissionGiverDePrueba").AddComponent<MissionGiver>();
            giro.desafio = primera;

            MissionManager.GetOrCreate();
            MissionTracker.GetOrCreate();

            var cadenaGO = new GameObject("MisionesDelTutorialDePrueba");
            var cadena = cadenaGO.AddComponent<MisionesDelTutorial>();
            Invocar(cadena, "Start");
            return cadena;
        }

        /// <summary>Le cuenta a la cadena que se completó esa misión, que es lo que
        /// hace el evento del MissionManager.</summary>
        private static void Completar(MisionesDelTutorial cadena, string desafioId)
        {
            var ficha = ScriptableObject.CreateInstance<DesafioData>();
            ficha.desafioId = desafioId;

            cadena.GetType().GetMethod("AlCompletarUna", Privado)
                .Invoke(cadena, new object[] { new DesafioRuntime { data = ficha } });
        }

        private static List<object> Eslabones(MisionesDelTutorial cadena)
        {
            var lista = new List<object>();
            var campo = cadena.GetType().GetField("_cadena", Privado)
                .GetValue(cadena) as System.Collections.IEnumerable;

            foreach (object eslabon in campo) lista.Add(eslabon);
            return lista;
        }

        private static int Entregados(MisionesDelTutorial cadena)
            => (int)cadena.GetType().GetField("_entregados", Privado).GetValue(cadena);

        private static object Campo(object quien, string nombre)
            => quien.GetType().GetField(nombre).GetValue(quien);

        private static void SoltarCadena(MisionesDelTutorial cadena)
        {
            foreach (TutorialPasos tutorial in UnityEngine.Object.FindObjectsByType<TutorialPasos>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tutorial.npcDetective != null)
                    UnityEngine.Object.DestroyImmediate(tutorial.npcDetective.gameObject);

                UnityEngine.Object.DestroyImmediate(tutorial.gameObject);
            }

            foreach (MissionGiver giver in UnityEngine.Object.FindObjectsByType<MissionGiver>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(giver.gameObject);

            if (cadena != null) UnityEngine.Object.DestroyImmediate(cadena.gameObject);

            QuitarMision();
        }

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
