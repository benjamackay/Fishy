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
            foreach (var (nombre, convs) in Conversaciones())
            {
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
                    () => total == 0 ? "no se generó ninguna opción"
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

            foreach (var (nombre, convs) in Conversaciones())
            {
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
                    if (op.impacto_puntuacion != esperado)
                        malos.Add($"{op.id} ({op.tipo}={op.impacto_puntuacion}, se esperaba {esperado})");
                }
            }

            // Si el banco se desviara de la tabla, el puntaje del backend seguiría
            // siendo "correcto" pero ya no significaría lo que dice la HDU.
            Comprobar(log, "el impacto de cada opción calza con su tipo (-1 / +1 / +2)",
                malos.Count == 0,
                () => string.Join("; ", malos.GetRange(0, Mathf.Min(3, malos.Count))));
        }

        private static void ProbarZonasConocidas(StringBuilder log)
        {
            var zonas = new SortedSet<string>();
            foreach (var p in BancoPreguntasLoader.Load().preguntas)
                if (!string.IsNullOrEmpty(p.zona)) zonas.Add(p.zona);

            Comprobar(log, "las zonas del banco son las que espera el juego",
                zonas.Contains("desconocidos") && zonas.Contains("chat_simulado"),
                () => "zonas encontradas: " + string.Join(", ", zonas));

            log.AppendLine($"         zonas: {string.Join(", ", zonas)}");
        }

        // ── Infraestructura ────────────────────────────────────────────────────

        private static List<(string, List<ChatConversation>)> Conversaciones()
        {
            return new List<(string, List<ChatConversation>)>
            {
                ("HDU-2 (desconocidos)", BancoPreguntasLoader.CreateHDU2Conversations()),
                ("HDU-8 (chat simulado)", BancoPreguntasLoader.CreateHDU8Conversations()),
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
