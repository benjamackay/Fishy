#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Fishy.Chat;
using Fishy.Finales;
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless de los finales (HDU-09) y de los testimonios de la Misión 3.
    ///
    /// Los finales: qué final corresponde a cada porcentaje (umbrales del banco: A desde
    /// 70 %, B desde 40 %, C debajo), que se disparen solo al cerrar la Misión 6 y que
    /// cada final tenga su objeto de recompensa. Los testimonios: que Coipo avance de su
    /// presentación al testimonio, y que terminar la presentación no cumpla el objetivo
    /// del testimonio.
    ///
    /// Menú: Fishy ▸ Probar finales y testimonios. Sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasFinales.Ejecutar `
    ///             -logFile -
    /// </summary>
    public static class FishyPruebasFinales
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        [MenuItem("Fishy/Probar finales y testimonios")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();
            BancoPreguntasLoader.Recargar();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE FINALES Y TESTIMONIOS (headless)");
            log.AppendLine(new string('=', 70));

            ProbarUmbrales(log);
            ProbarMaximoDelBanco(log);
            ProbarPorcentajeConPisoYTope(log);
            ProbarSinPorcentajeVaElDelMedio(log);
            ProbarDisparadores(log);
            ProbarRecompensasExisten(log);
            ProbarHablante(log);
            ProbarCoipoAvanzaAlTestimonio(log);
            ProbarLaPresentacionNoCumpleElTestimonio(log);

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        private static List<FinalNarrativo> Finales()
            => BancoPreguntasLoader.Load().finales_narrativos.finales;

        // ── Finales ───────────────────────────────────────────────────────────

        /// <summary>Los bordes son donde se equivoca un "&gt;" por un "&gt;=".</summary>
        private static void ProbarUmbrales(StringBuilder log)
        {
            var casos = new (float pct, string esperado)[]
            {
                (100f, "FINAL_A"), (70f, "FINAL_A"), (69.9f, "FINAL_B"),
                (40f, "FINAL_B"), (39.9f, "FINAL_C"), (0f, "FINAL_C"),
            };

            var finales = Finales();
            bool todos = finales.Count == 3;
            string detalle = $"{finales.Count} finales en el banco";
            foreach (var (pct, esperado) in casos)
            {
                string obtenido = SistemaDeFinales.ElegirFinal(finales, pct)?.id;
                if (obtenido != esperado)
                {
                    todos = false;
                    detalle = $"con {pct} % salió {obtenido}, se esperaba {esperado}";
                    break;
                }
            }
            Comprobar(log, "A desde 70 %, B desde 40 %, C debajo", todos, todos ? "" : detalle);
        }

        /// <summary>
        /// La mejor ruta de los 8 chats con decisión suma 18 (M1 y M4 fase 1 dan 3 por
        /// el seguimiento; las dos variantes de la Misión 6 cuentan una vez). El backend
        /// aplica la misma regla y tiene una prueba con el mismo número.
        /// </summary>
        private static void ProbarMaximoDelBanco(StringBuilder log)
        {
            int maximo = SistemaDeFinales.PuntosMaximos(BancoPreguntasLoader.Load());
            Comprobar(log, "El máximo posible, calculado desde el banco, es 18", maximo == 18, $"{maximo}");
        }

        private static void ProbarPorcentajeConPisoYTope(StringBuilder log)
        {
            float? negativo = SistemaDeFinales.Porcentaje(-4, 18);
            float? ejemplo = SistemaDeFinales.Porcentaje(16, 18);
            float? pasado = SistemaDeFinales.Porcentaje(25, 18);
            float? sinMaximo = SistemaDeFinales.Porcentaje(5, 0);
            bool bien = negativo == 0f && Mathf.Abs(ejemplo.Value - 88.89f) < 0.01f
                        && pasado == 100f && sinMaximo == null;
            Comprobar(log, "Negativo cuenta como 0, 16 de 18 es 89 %, tope en 100", bien,
                $"{negativo} / {ejemplo} / {pasado} / {(sinMaximo.HasValue ? sinMaximo.ToString() : "null")}");
        }

        private static void ProbarSinPorcentajeVaElDelMedio(StringBuilder log)
        {
            string id = SistemaDeFinales.ElegirFinal(Finales(), null)?.id;
            Comprobar(log, "Sin porcentaje se muestra el Final B", id == "FINAL_B", id);
        }

        /// <summary>
        /// Las dos variantes de la Misión 6, por cualquier rama, disparan el final; un
        /// nodo de otra misión o uno que no cierra, no.
        /// </summary>
        private static void ProbarDisparadores(StringBuilder log)
        {
            var banco = BancoPreguntasLoader.Load();
            var si = new[] { "HDU4_NPC06_FIN_SEGURO", "HDU4_NPC06_FIN_INSEGURO",
                             "HDU4_NPC06B_FIN_SEGURO", "HDU4_NPC06B_FIN_INSEGURO" };
            var no = new[] { "HDU4_NPC06_Q01", "HDU4_M5B_FIN_SEGURO", "HDU2_NPC01_FIN_SEGURO", "NO_EXISTE" };

            string malo = null;
            foreach (var id in si)
                if (!SistemaDeFinales.TerminaLaAventura(banco, id)) { malo = $"{id} no dispara"; break; }
            if (malo == null)
                foreach (var id in no)
                    if (SistemaDeFinales.TerminaLaAventura(banco, id)) { malo = $"{id} dispara"; break; }

            Comprobar(log, "Solo los FIN de la Misión 6 (las dos variantes) disparan el final",
                malo == null, malo);
        }

        private static void ProbarRecompensasExisten(StringBuilder log)
        {
            CatalogoItems.Recargar();
            string falta = null;
            foreach (var f in Finales())
                if (CatalogoItems.Buscar(f.recompensa_item_id) == null)
                {
                    falta = $"{f.id} → '{f.recompensa_item_id}' no está en Resources/Items";
                    break;
                }
            Comprobar(log, "Cada final tiene su objeto de recompensa", falta == null, falta);
        }

        private static void ProbarHablante(StringBuilder log)
        {
            string radio = SistemaDeFinales.Hablante(new LineaDeFinal { npc_nombre = "Huemul", por_radio = true });
            string cara = SistemaDeFinales.Hablante(new LineaDeFinal { npc_nombre = "Coipo", por_radio = false });
            Comprobar(log, "Las líneas por radio lo dicen en el nombre",
                radio == "Huemul (por radio)" && cara == "Coipo", $"'{radio}' / '{cara}'");
        }

        // ── Testimonios ───────────────────────────────────────────────────────

        private static DialogoNeutroNPC CoipoDePrueba(out GameObject go)
        {
            go = new GameObject("Coipo de prueba");
            var npc = go.AddComponent<DialogoNeutroNPC>();
            npc.dialogoId = "HDU1_NPC_COIPO";
            npc.dialogosSiguientes.Add("HDU3_M3_TESTIMONIO_COIPO");
            return npc;
        }

        /// <summary>
        /// Se presenta, la vez siguiente da el testimonio y ahí se queda. Se llama a
        /// EndDialogue directo: es lo que pasa al cerrar la última línea.
        /// </summary>
        private static void ProbarCoipoAvanzaAlTestimonio(StringBuilder log)
        {
            var npc = CoipoDePrueba(out var go);
            try
            {
                string antes = npc.DialogoActualId;
                npc.EndDialogue();
                string trasPresentarse = npc.UltimoDialogoTerminado;
                string siguiente = npc.DialogoActualId;
                string primeraLinea = npc.dialogueData != null && npc.dialogueData.dialogueLines.Length > 0
                    ? npc.dialogueData.dialogueLines[0] : "";
                npc.EndDialogue();
                string trasTestimonio = npc.UltimoDialogoTerminado;
                string queda = npc.DialogoActualId;

                bool bien = antes == "HDU1_NPC_COIPO"
                    && trasPresentarse == "HDU1_NPC_COIPO"
                    && siguiente == "HDU3_M3_TESTIMONIO_COIPO"
                    && primeraLinea.StartsWith("¡Para nada!")
                    && trasTestimonio == "HDU3_M3_TESTIMONIO_COIPO"
                    && queda == "HDU3_M3_TESTIMONIO_COIPO";

                Comprobar(log, "Coipo pasa de su presentación a su testimonio y ahí se queda", bien,
                    $"{antes} → {siguiente} → {queda}; línea: '{primeraLinea}'");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// onDialogueEnded salta al terminar cualquiera de los diálogos de Coipo. El
        /// objetivo del testimonio solo puede aceptar el del testimonio.
        /// </summary>
        private static void ProbarLaPresentacionNoCumpleElTestimonio(StringBuilder log)
        {
            var npc = CoipoDePrueba(out var go);
            try
            {
                var testimonio = new ObjetivoMision
                {
                    tipo = TipoObjetivo.HablarConNpc,
                    dialogoNpcId = "HDU3_M3_TESTIMONIO_COIPO",
                    npc = npc,
                };
                var presentacion = new ObjetivoMision
                {
                    tipo = TipoObjetivo.HablarConNpc,
                    dialogoNpcId = "HDU1_NPC_COIPO",
                    npc = npc,
                };

                npc.EndDialogue();   // terminó la presentación
                bool tras1 = !testimonio.AceptaElEvento() && presentacion.AceptaElEvento();
                npc.EndDialogue();   // terminó el testimonio
                bool tras2 = testimonio.AceptaElEvento();

                Comprobar(log, "Terminar la presentación de Coipo no cumple el objetivo del testimonio",
                    tras1 && tras2, $"tras presentarse={tras1}, tras el testimonio={tras2}");
            }
            finally { Object.DestroyImmediate(go); }
        }

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
