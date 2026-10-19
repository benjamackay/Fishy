using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Fishy.Chat;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless de la acumulación de riesgo por zona (HDU-2 / HDU-8).
    ///
    /// Comprueban la parte que vive solo en Unity y que ningún test del backend
    /// puede cubrir: que las conversaciones que arma <see cref="BancoPreguntasLoader"/>
    /// arrastren el id de cada opción del banco. Sin ese id el backend recibe la
    /// respuesta pero no puede puntuarla, y el riesgo por zona queda en cero sin
    /// que nada falle de forma visible — justo el modo de fallar más difícil de
    /// notar jugando.
    ///
    /// Se corren desde el menú  Fishy ▸ Probar riesgo por zona,
    /// o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasRiesgo.Ejecutar `
    ///             -logFile -
    ///
    /// En batchmode termina con código 0 si todo pasa y 1 si algo falla, así que
    /// sirve tal cual como puerta de control antes de dar la feature por lista.
    /// </summary>
    public static class FishyPruebasRiesgo
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        [MenuItem("Fishy/Probar riesgo por zona")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE RIESGO POR ZONA (headless)");
            log.AppendLine(new string('=', 70));

            ProbarBancoSeCarga(log);
            ProbarOpcionesLlevanSuId(log);
            ProbarIdsCoincidenConElBanco(log);
            ProbarImpactosSonLosEsperados(log);
            ProbarZonasConocidas(log);

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── Pruebas ────────────────────────────────────────────────────────────

        private static void ProbarBancoSeCarga(StringBuilder log)
        {
            var banco = BancoPreguntasLoader.Load();
            Comprobar(log, "el banco se carga desde Resources",
                banco != null && banco.preguntas != null && banco.preguntas.Count > 0,
                () => "banco vacío o nulo");

            if (banco?.preguntas == null) return;
            int conOpciones = 0;
            foreach (var p in banco.preguntas)
                if (p.opciones_respuesta != null && p.opciones_respuesta.Count > 0) conOpciones++;

            log.AppendLine($"         {banco.preguntas.Count} preguntas, {conOpciones} con opciones");
        }

        private static void ProbarOpcionesLlevanSuId(StringBuilder log)
        {
            foreach (var (nombre, hdu, convs) in Conversaciones())
            {
                // Cero opciones significa dos cosas muy distintas: que el banco no
                // trae contenido de esa HDU (nada que probar) o que sí lo trae y el
                // loader perdió los ids por el camino (eso sí es una falla). El
                // banco v1.8 reorganizó HDU-8 en HDU-3 y HDU-4, así que la primera
                // situación es real y no debe pintarse de rojo.
                if (!BancoTienePreguntasDe(hdu))
                {
                    log.AppendLine($"  [--   ] {nombre}: el banco no trae preguntas {hdu}, no hay nada que probar");
                    continue;
                }

                int total = 0, sinId = 0;
                foreach (var conv in convs)
                    foreach (var nodo in conv.nodes)
                    {
                        if (nodo.options == null) continue;
                        foreach (var op in nodo.options)
                        {
                            total++;
                            if (string.IsNullOrEmpty(op.bancoOptionId)) sinId++;
                        }
                    }

                Comprobar(log, $"{nombre}: las {total} opciones llevan bancoOptionId",
                    total > 0 && sinId == 0,
                    () => total == 0 ? $"el banco trae preguntas {hdu} pero no se generó ninguna opción"
                                     : $"{sinId} de {total} opciones sin id");
            }
        }

        private static void ProbarIdsCoincidenConElBanco(StringBuilder log)
        {
            var delBanco = new HashSet<string>();
            foreach (var p in BancoPreguntasLoader.Load().preguntas)
            {
                if (p.opciones_respuesta == null) continue;
                foreach (var op in p.opciones_respuesta)
                    if (!string.IsNullOrEmpty(op.id)) delBanco.Add(op.id);
            }

            foreach (var (nombre, hdu, convs) in Conversaciones())
            {
                if (!BancoTienePreguntasDe(hdu)) continue;

                var desconocidos = new List<string>();
                foreach (var conv in convs)
                    foreach (var nodo in conv.nodes)
                    {
                        if (nodo.options == null) continue;
                        foreach (var op in nodo.options)
                            if (!string.IsNullOrEmpty(op.bancoOptionId) && !delBanco.Contains(op.bancoOptionId))
                                desconocidos.Add(op.bancoOptionId);
                    }

                // Un id que no exista en el banco viajaría al backend y caería en
                // "sin_clasificar": la respuesta se guarda pero no puntúa.
                Comprobar(log, $"{nombre}: todos los ids existen en el banco",
                    desconocidos.Count == 0,
                    () => $"{desconocidos.Count} id(s) desconocido(s): " +
                          string.Join(", ", desconocidos.GetRange(0, Mathf.Min(3, desconocidos.Count))));
            }
        }

        private static void ProbarImpactosSonLosEsperados(StringBuilder log)
        {
            // El banco se desvía de la tabla a propósito en las sub-decisiones
            // (cierre / reacción / rectificación), donde el documento oficial de
            // diálogos da +1 a la opción óptima en lugar de +2, y lo deja escrito
            // en `formato_respuesta.nota_subdecisiones` con los nodos afectados.
            // La lista sale de ahí y no de acá: si el banco declara una excepción
            // nueva, esta prueba la sigue sin que nadie la edite. Lo que ya no
            // tolera es una desviación que el banco NO haya declarado.
            string nota = NotaDeSubdecisiones();
            var excepciones = new List<string>();
            var malos = new List<string>();

            foreach (var p in BancoPreguntasLoader.Load().preguntas)
            {
                if (p.opciones_respuesta == null) continue;
                foreach (var op in p.opciones_respuesta)
                {
                    int esperado;
                    switch (op.tipo)
                    {
                        case "insegura":      esperado = -1; break;
                        case "segura_basica": esperado =  1; break;
                        case "segura_optima": esperado =  2; break;
                        default: continue;
                    }
                    if (op.impacto_puntuacion == esperado) continue;

                    if (!string.IsNullOrEmpty(op.id) && nota.Contains(op.id))
                        excepciones.Add($"{op.id}={op.impacto_puntuacion}");
                    else
                        malos.Add($"{op.id} ({op.tipo}={op.impacto_puntuacion}, se esperaba {esperado})");
                }
            }

            // Si el banco se desviara de la tabla sin declararlo, el puntaje del
            // backend seguiría siendo "correcto" pero ya no significaría lo que
            // dice la HDU.
            Comprobar(log, "el impacto de cada opción calza con su tipo (-1 / +1 / +2) "
                         + "o es una excepción declarada por el banco",
                malos.Count == 0,
                () => string.Join("; ", malos.GetRange(0, Mathf.Min(3, malos.Count))));

            if (excepciones.Count > 0)
                log.AppendLine($"         {excepciones.Count} excepción(es) declarada(s) en el banco: "
                             + string.Join(", ", excepciones));
        }

        private static void ProbarZonasConocidas(StringBuilder log)
        {
            var zonas = new SortedSet<string>();
            foreach (var p in BancoPreguntasLoader.Load().preguntas)
                if (!string.IsNullOrEmpty(p.zona)) zonas.Add(p.zona);

            // Antes esto exigía que existieran zonas concretas, y se puso en rojo
            // cuando el banco v1.8 repartió `chat_simulado` en `ciberacoso` y
            // `reto_viral`. Lo que importa de verdad no es qué zonas hay, sino que
            // no aparezca una que el juego no sepa manejar: una zona desconocida
            // (o un typo) llega igual al backend y termina agrupando el riesgo en
            // un casillero que ninguna pantalla muestra.
            var desconocidas = new List<string>();
            foreach (var z in zonas)
                if (!ZonasQueElJuegoManeja.Contains(z)) desconocidas.Add(z);

            Comprobar(log, "todas las zonas del banco son zonas que el juego maneja",
                zonas.Count > 0 && desconocidas.Count == 0,
                () => zonas.Count == 0
                    ? "el banco no declara ninguna zona"
                    : "zona(s) que el juego no maneja: " + string.Join(", ", desconocidas)
                      + " (conocidas: " + string.Join(", ", ZonasQueElJuegoManeja) + ")");

            log.AppendLine($"         zonas: {string.Join(", ", zonas)}");
        }

        // ── Infraestructura ────────────────────────────────────────────────────

        /// <summary>
        /// Las zonas que el juego sabe manejar. Salen de
        /// <c>BancoPreguntasLoader.EscenarioToCategoria</c>, que es el único lugar
        /// donde el juego traduce contenido del banco a una zona. Si allá se
        /// agrega una zona, hay que agregarla acá.
        /// </summary>
        private static readonly SortedSet<string> ZonasQueElJuegoManeja =
            new SortedSet<string> { "ciberacoso", "desconocidos", "reto_viral" };

        /// <summary>Texto de `formato_respuesta.nota_subdecisiones`, o "" si no está.</summary>
        private static string NotaDeSubdecisiones()
        {
            // Se lee del JSON crudo a propósito: `BancoRaiz` solo modela `version` y
            // `preguntas`, y no vale la pena tocar un tipo de producción para que
            // una prueba de editor pueda leer un campo de metadatos.
            var asset = Resources.Load<TextAsset>("banco_preguntas");
            if (asset == null) return "";

            const string clave = "\"nota_subdecisiones\"";
            int i = asset.text.IndexOf(clave, System.StringComparison.Ordinal);
            if (i < 0) return "";

            int abre = asset.text.IndexOf('"', i + clave.Length + 1);   // tras los dos puntos
            if (abre < 0) return "";
            int cierra = asset.text.IndexOf('"', abre + 1);             // la nota no lleva comillas dentro
            return cierra < 0 ? "" : asset.text.Substring(abre + 1, cierra - abre - 1);
        }

        private static bool BancoTienePreguntasDe(string hdu)
        {
            foreach (var p in BancoPreguntasLoader.Load().preguntas)
                if (p.hdu == hdu) return true;
            return false;
        }

        private static List<(string, string, List<ChatConversation>)> Conversaciones()
        {
            return new List<(string, string, List<ChatConversation>)>
            {
                ("HDU-2 (desconocidos)",  "HDU-2", BancoPreguntasLoader.CreateHDU2Conversations()),
                ("HDU-8 (chat simulado)", "HDU-8", BancoPreguntasLoader.CreateHDU8Conversations()),
            };
        }

        private static void Comprobar(StringBuilder log, string queSePrueba, bool pasa,
            System.Func<string> detalle)
        {
            if (pasa)
            {
                _ok++;
                log.AppendLine($"  [OK   ] {queSePrueba}");
            }
            else
            {
                string texto = $"  [FALLA] {queSePrueba} -> {detalle()}";
                _fallas.Add(texto);
                log.AppendLine(texto);
            }
        }
    }
}
