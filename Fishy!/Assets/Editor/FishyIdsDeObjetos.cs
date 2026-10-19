#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fishy.Zonas.BosqueDesconocidos;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Le pone un <c>objetoId</c> a cada <see cref="WorldItem"/> de la escena abierta,
    /// para que el juego pueda recordar cuáles ya se recogieron.
    ///
    /// Uso: abre la escena del mapa y llama
    /// Fishy → Asignar ids a los objetos del mapa.
    ///
    /// <b>Solo rellena los vacíos.</b> Un id ya asignado no se toca nunca, ni aunque
    /// alguien renombre el GameObject: ese id está escrito en la base de datos de
    /// cada niño/a, y cambiarlo haría reaparecer objetos que ya habían recogido. Por
    /// eso el id se deriva del nombre una única vez y después es independiente de él.
    ///
    /// El id sale de la escena más el nombre del objeto (<c>SAMPLESCENE_CONCHA</c>),
    /// con un número si hay varios iguales. Es legible a propósito: se va a ver en el
    /// panel de admin de Django, donde un GUID no diría nada.
    /// </summary>
    public static class FishyIdsDeObjetos
    {
        private const string MenuAsignar = "Fishy/Asignar ids de escena (objetos y NPCs)";
        private const string MenuRevisar = "Fishy/Revisar ids de escena (objetos y NPCs)";

        [MenuItem(MenuAsignar)]
        public static void Asignar()
        {
            var items = ItemsDeLaEscena();
            if (items.Count == 0 && NpcsDeLaEscena().Count == 0) { NadaQueHacer(); return; }

            // Los ids que ya existen se reservan primero, para que un id nuevo no
            // choque con uno viejo al numerar.
            var usados = new HashSet<string>(
                items.Where(i => !string.IsNullOrWhiteSpace(i.objetoId))
                     .Select(i => i.objetoId.Trim()));
            foreach (var npc in NpcsDeLaEscena())
                if (!string.IsNullOrWhiteSpace(npc.npcId)) usados.Add(npc.npcId.Trim());

            string escena = Prefijo(SceneManager.GetActiveScene().name);
            var log = new StringBuilder();
            int asignados = 0;

            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.objetoId)) continue;

                string baseId = $"{escena}_{Prefijo(item.name)}";
                string id = baseId;
                int n = 1;
                while (usados.Contains(id)) id = $"{baseId}_{++n:00}";

                Undo.RecordObject(item, "Asignar objetoId");
                item.objetoId = id;
                EditorUtility.SetDirty(item);
                usados.Add(id);
                asignados++;

                log.AppendLine($"    {item.name}  →  {id}");
            }

            // Los NPCs de temática necesitan lo mismo y por la misma razón: sin id, su
            // avance no se recuerda y la temática empieza de cero cada sesión.
            int npcsAsignados = 0;
            foreach (var npc in NpcsDeLaEscena())
            {
                if (!string.IsNullOrWhiteSpace(npc.npcId)) continue;

                string baseId = $"{escena}_NPC_{Prefijo(npc.name)}";
                string id = baseId;
                int n = 1;
                while (usados.Contains(id)) id = $"{baseId}_{++n:00}";

                Undo.RecordObject(npc, "Asignar npcId");
                npc.npcId = id;
                EditorUtility.SetDirty(npc);
                usados.Add(id);
                npcsAsignados++;
                log.AppendLine($"    {npc.name}  →  {id}");
            }
            asignados += npcsAsignados;

            if (asignados > 0)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            int total = items.Count + NpcsDeLaEscena().Count;
            int yaTenian = total - asignados;
            var resumen = new StringBuilder();
            resumen.AppendLine($"[Ids de escena] {items.Count} objeto(s) y {NpcsDeLaEscena().Count} NPC(s) en '{SceneManager.GetActiveScene().name}'.");
            resumen.AppendLine($"  {asignados} id(s) nuevos, {yaTenian} que ya tenían (no se tocan).");
            if (asignados > 0) resumen.Append(log);

            Debug.Log(resumen.ToString());

            EditorUtility.DisplayDialog("Fishy — Ids de objetos del mapa",
                asignados == 0
                    ? $"Los {total} objetos y NPCs ya tenían id. No se cambió nada."
                    : $"Se asignaron {asignados} id(s) nuevos a los objetos sin id.\n" +
                      $"{yaTenian} ya tenían y no se tocaron.\n\n" +
                      "ACUÉRDATE DE GUARDAR LA ESCENA (Ctrl+S): los ids viven en la escena.",
                "OK");
        }

        [MenuItem(MenuRevisar)]
        public static void Revisar()
        {
            var items = ItemsDeLaEscena();
            if (items.Count == 0) { NadaQueHacer(); return; }

            var npcs = NpcsDeLaEscena();

            var sinId = items.Where(i => string.IsNullOrWhiteSpace(i.objetoId))
                             .Select(i => i.name)
                             .Concat(npcs.Where(n => string.IsNullOrWhiteSpace(n.npcId))
                                         .Select(n => $"{n.name} (NPC)"))
                             .ToList();
            var repetidos = items
                .Where(i => !string.IsNullOrWhiteSpace(i.objetoId))
                .Select(i => (id: i.objetoId.Trim(), nombre: i.name))
                .Concat(npcs.Where(n => !string.IsNullOrWhiteSpace(n.npcId))
                            .Select(n => (id: n.npcId.Trim(), nombre: $"{n.name} (NPC)")))
                .GroupBy(x => x.id)
                .Where(g => g.Count() > 1)
                .ToList();

            var log = new StringBuilder();
            log.AppendLine($"[Ids de escena] {items.Count} objeto(s) y {npcs.Count} NPC(s) revisados.");
            foreach (var i in items.OrderBy(i => i.objetoId))
                log.AppendLine($"    {(string.IsNullOrWhiteSpace(i.objetoId) ? "(SIN ID)" : i.objetoId),-34} {i.name}");
            foreach (var n in npcs.OrderBy(n => n.npcId))
                log.AppendLine($"    {(string.IsNullOrWhiteSpace(n.npcId) ? "(SIN ID)" : n.npcId),-34} {n.name} (NPC)");

            if (sinId.Count > 0)
            {
                log.AppendLine();
                log.AppendLine($"  SIN ID ({sinId.Count}) — van a reaparecer en el mapa cada vez:");
                foreach (var nombre in sinId) log.AppendLine($"    {nombre}");
            }

            if (repetidos.Count > 0)
            {
                log.AppendLine();
                log.AppendLine($"  REPETIDOS ({repetidos.Count}) — recoger uno haría desaparecer al otro:");
                foreach (var g in repetidos)
                    log.AppendLine($"    '{g.Key}': {string.Join(", ", g.Select(x => x.nombre))}");
            }

            bool hayProblemas = sinId.Count > 0 || repetidos.Count > 0;
            if (hayProblemas) Debug.LogError(log.ToString());
            else Debug.Log(log.ToString());

            EditorUtility.DisplayDialog("Fishy — Ids de objetos del mapa",
                hayProblemas
                    ? $"Hay problemas:\n\n• {sinId.Count} objeto(s) sin id\n" +
                      $"• {repetidos.Count} id(s) repetido(s)\n\nEl detalle está en la consola."
                    : $"Los {items.Count} objetos y {npcs.Count} NPC(s) tienen un id único.",
                "OK");
        }

        // ── Auxiliares ─────────────────────────────────────────────────────────

        /// <summary>
        /// Incluye los desactivados: un objeto apagado en la escena sigue siendo parte
        /// del mapa y puede encenderse desde un script, así que también necesita id.
        /// </summary>
        private static List<WorldItem> ItemsDeLaEscena()
        {
            return Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include)
                .OrderBy(i => i.name)
                .ToList();
        }

        /// <summary>Los NPCs de temática de la escena, incluidos los desactivados.</summary>
        private static List<BosqueDesconocidosNPC> NpcsDeLaEscena()
        {
            return Object.FindObjectsByType<BosqueDesconocidosNPC>(FindObjectsInactive.Include)
                .OrderBy(n => n.name)
                .ToList();
        }

        private static void NadaQueHacer()
        {
            EditorUtility.DisplayDialog("Fishy — Ids de objetos del mapa",
                $"No hay ningún WorldItem en la escena abierta " +
                $"('{SceneManager.GetActiveScene().name}').\n\n" +
                "Abre la escena del mapa y vuelve a intentarlo.", "OK");
        }

        /// <summary>
        /// Deja el texto en MAYÚSCULAS sin tildes ni espacios, para que el id sea del
        /// mismo estilo que `desafioId`, `itemId` y los del banco.
        /// </summary>
        private static string Prefijo(string texto)
        {
            var sb = new StringBuilder();
            foreach (char c in texto.Trim().ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
                else if (c == ' ' || c == '_' || c == '-') sb.Append('_');
                else sb.Append(Desacentuar(c));
            }
            // Sin guiones bajos repetidos ni en los extremos: "MI  OBJETO (1)" no puede
            // dar "MI__OBJETO_1_".
            string limpio = string.Join("_", sb.ToString().Split('_')
                .Where(p => !string.IsNullOrEmpty(p)));
            return string.IsNullOrEmpty(limpio) ? "OBJETO" : limpio;
        }

        private static string Desacentuar(char c)
        {
            switch (c)
            {
                case 'Á': return "A";
                case 'É': return "E";
                case 'Í': return "I";
                case 'Ó': return "O";
                case 'Ú': return "U";
                case 'Ñ': return "N";
                case 'Ü': return "U";
                default:  return "";
            }
        }
    }
}
#endif
