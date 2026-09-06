#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Revisa que todos los <see cref="ItemData"/> del proyecto tengan un
    /// <c>itemId</c> válido y único.
    ///
    /// Uso: Fishy → Revisar ids de objetos. El detalle sale por consola.
    ///
    /// <b>Por qué existe:</b> el <c>itemId</c> es lo que se escribe en la base de datos
    /// cuando se guarda el inventario. Un id vacío o repetido no rompe nada en el
    /// editor —el juego corre igual, porque en memoria el inventario usa referencias,
    /// no ids— y revienta recién al guardar y volver a cargar, mezclando dos objetos
    /// distintos o perdiendo uno. Es el mismo tipo de desajuste silencioso que ya
    /// costó caro con <c>caso_01</c> contra <c>DC_CASO_01</c>, así que conviene tener
    /// dónde preguntarlo antes de que muerda.
    ///
    /// Se corre a mano en vez de en cada importación: son 9 assets y el chequeo tiene
    /// sentido al agregar objetos nuevos, no cada vez que alguien toca un sprite.
    /// </summary>
    public static class FishyRevisarItemIds
    {
        private const string MenuRuta = "Fishy/Revisar ids de objetos";

        [MenuItem(MenuRuta)]
        public static void Revisar()
        {
            var items = AssetDatabase.FindAssets("t:ItemData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(ruta => (ruta, item: AssetDatabase.LoadAssetAtPath<ItemData>(ruta)))
                .Where(par => par.item != null)
                .OrderBy(par => par.ruta)
                .ToList();

            if (items.Count == 0)
            {
                Debug.LogWarning("[Items] No encontré ningún ItemData en el proyecto.");
                return;
            }

            var sinId = items.Where(p => string.IsNullOrWhiteSpace(p.item.itemId)).ToList();

            var repetidos = items
                .Where(p => !string.IsNullOrWhiteSpace(p.item.itemId))
                .GroupBy(p => p.item.itemId.Trim())
                .Where(g => g.Count() > 1)
                .ToList();

            // Un id con espacios o minúsculas no está "mal" —funciona—, pero se sale de
            // la convención del resto del proyecto y es justo lo que después nadie
            // recuerda al escribirlo a mano en el banco o en una prueba.
            var raros = items
                .Where(p => !string.IsNullOrWhiteSpace(p.item.itemId))
                .Where(p => p.item.itemId != p.item.itemId.Trim().ToUpperInvariant()
                            || p.item.itemId.Contains(' '))
                .ToList();

            var informe = new StringBuilder();
            informe.AppendLine($"[Items] {items.Count} ItemData revisados.");

            foreach (var (ruta, item) in items)
                informe.AppendLine($"    {item.itemId,-20} {System.IO.Path.GetFileName(ruta)}");

            if (sinId.Count > 0)
            {
                informe.AppendLine();
                informe.AppendLine($"  SIN ID ({sinId.Count}) — no se pueden guardar:");
                foreach (var (ruta, _) in sinId) informe.AppendLine($"    {ruta}");
            }

            if (repetidos.Count > 0)
            {
                informe.AppendLine();
                informe.AppendLine($"  IDS REPETIDOS ({repetidos.Count}) — dos objetos distintos se pisarían:");
                foreach (var grupo in repetidos)
                {
                    informe.AppendLine($"    '{grupo.Key}':");
                    foreach (var (ruta, _) in grupo) informe.AppendLine($"        {ruta}");
                }
            }

            if (raros.Count > 0)
            {
                informe.AppendLine();
                informe.AppendLine($"  FUERA DE CONVENCIÓN ({raros.Count}) — se esperan MAYÚSCULAS sin espacios:");
                foreach (var (ruta, item) in raros)
                    informe.AppendLine($"    '{item.itemId}'  en  {ruta}");
            }

            bool hayProblemas = sinId.Count > 0 || repetidos.Count > 0;

            if (hayProblemas) Debug.LogError(informe.ToString());
            else if (raros.Count > 0) Debug.LogWarning(informe.ToString());
            else Debug.Log(informe.ToString());

            string resumen = hayProblemas
                ? $"Hay problemas que impiden guardar el inventario:\n\n" +
                  $"• {sinId.Count} objeto(s) sin id\n" +
                  $"• {repetidos.Count} id(s) repetido(s)\n\n" +
                  "El detalle está en la consola."
                : raros.Count > 0
                    ? $"Los {items.Count} objetos tienen id único, pero {raros.Count} se " +
                      "salen de la convención (MAYÚSCULAS sin espacios). Detalle en la consola."
                    : $"Los {items.Count} objetos tienen un id único y bien formado.";

            EditorUtility.DisplayDialog("Fishy — Ids de objetos", resumen, "OK");
        }
    }
}
#endif
