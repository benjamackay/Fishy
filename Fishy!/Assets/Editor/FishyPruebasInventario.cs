#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fishy.Net;
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless del guardado de inventario (HDU-15).
    ///
    /// Cubren la parte que vive solo en Unity y que ningún test del backend puede
    /// alcanzar: que el catálogo encuentre los assets por su <c>itemId</c>, y que la
    /// rama de modo local del <see cref="ApiManager"/> se comporte igual que el
    /// endpoint. Esa segunda es la que más importa: si las dos ramas divergen, el
    /// bug aparece recién al reconectar, que es el peor momento para descubrirlo.
    ///
    /// Se corren desde el menú  Fishy ▸ Probar guardado de inventario,
    /// o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasInventario.Ejecutar `
    ///             -logFile -
    ///
    /// Termina con código 0 si todo pasa y 1 si algo falla.
    /// </summary>
    public static class FishyPruebasInventario
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        /// <summary>Partida ficticia. Se limpia su PlayerPrefs al terminar.</summary>
        private const int PartidaDePrueba = 999999;

        [MenuItem("Fishy/Probar guardado de inventario")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE GUARDADO DE INVENTARIO (headless)");
            log.AppendLine(new string('=', 70));

            ProbarCatalogoSeCarga(log);
            ProbarIdsUnicosYNoVacios(log);
            ProbarBusquedaPorId(log);

            // Un solo ApiManager para todas: es un singleton con DontDestroyOnLoad, y
            // el segundo que se cree se suicida en Awake. Crear uno por prueba era
            // pelearse con el contrato de la propia clase.
            var api = ApiLocal();
            ProbarModoLocalGuardaYLee(log, api);
            ProbarModoLocalReemplaza(log, api);
            ProbarModoLocalSumaRepetidos(log, api);
            ProbarModoLocalDescartaCantidadCero(log, api);
            ProbarModoLocalSeparaPorPartida(log, api);

            LimpiarPrefs();

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── El catálogo ────────────────────────────────────────────────────────

        private static void ProbarCatalogoSeCarga(StringBuilder log)
        {
            CatalogoItems.Recargar();
            int n = CatalogoItems.Todos.Count;
            Comprobar(log, "el catálogo se carga desde Resources/Items",
                n > 0, $"encontró {n} objeto(s)");
        }

        private static void ProbarIdsUnicosYNoVacios(StringBuilder log)
        {
            // Se leen los assets directo, no el catálogo: el catálogo ya descarta los
            // que están mal, así que preguntarle a él no probaría nada.
            var items = AssetDatabase.FindAssets("t:ItemData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                .Where(i => i != null)
                .ToList();

            var vacios = items.Where(i => string.IsNullOrWhiteSpace(i.itemId)).ToList();
            Comprobar(log, "ningún ItemData quedó sin itemId",
                vacios.Count == 0,
                vacios.Count == 0 ? $"{items.Count} revisados"
                                  : string.Join(", ", vacios.Select(i => i.name)));

            var repes = items.Where(i => !string.IsNullOrWhiteSpace(i.itemId))
                .GroupBy(i => i.itemId.Trim())
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            Comprobar(log, "no hay itemId repetidos",
                repes.Count == 0,
                repes.Count == 0 ? "todos únicos" : string.Join(", ", repes));
        }

        private static void ProbarBusquedaPorId(StringBuilder log)
        {
            var primero = CatalogoItems.Todos.Values.FirstOrDefault();
            if (primero == null) { Comprobar(log, "buscar por id", false, "catálogo vacío"); return; }

            var hallado = CatalogoItems.Buscar(primero.itemId);
            Comprobar(log, $"Buscar('{primero.itemId}') devuelve su asset",
                hallado == primero, hallado != null ? hallado.name : "null");

            Comprobar(log, "un id que no existe devuelve null",
                CatalogoItems.Buscar("ITEM_QUE_NO_EXISTE_JAMAS") == null, "");

            Comprobar(log, "un id vacío devuelve null sin reventar",
                CatalogoItems.Buscar("") == null && CatalogoItems.Buscar(null) == null, "");
        }

        // ── La rama de modo local ──────────────────────────────────────────────

        private static void ProbarModoLocalGuardaYLee(StringBuilder log, ApiManager api)
        {
            LimpiarPrefs();

            Comprobar(log, "el ApiManager de prueba quedó en modo local",
                api.IsLocalMode, $"IsLocalMode={api.IsLocalMode}");

            string error = null;
            api.GuardarInventario(Envio(("ITEM_BRUJULA", 1), ("ITEM_FLOR_01", 3)), PartidaDePrueba,
                onError: e => error = e);
            Comprobar(log, "guardar no devuelve error", error == null, error ?? "sin error");

            // Sin callback a propósito: guardar tiene que ocurrir aunque a quien llama
            // no le interese la respuesta. Así se descubrió que el `?.Invoke(Guardar())`
            // se saltaba el guardado entero cuando onSuccess era null.
            var leido = Leer(api);
            Comprobar(log, "modo local: guarda y vuelve a leer",
                leido.Count == 2 && leido["ITEM_BRUJULA"] == 1 && leido["ITEM_FLOR_01"] == 3,
                Describir(leido));
        }

        private static void ProbarModoLocalReemplaza(StringBuilder log, ApiManager api)
        {
            LimpiarPrefs();

            api.GuardarInventario(Envio(("ITEM_BRUJULA", 1), ("ITEM_FLOR_01", 2)), PartidaDePrueba);
            api.GuardarInventario(Envio(("ITEM_BRUJULA", 1)), PartidaDePrueba);

            var leido = Leer(api);
            Comprobar(log, "modo local: lo que no viene se borra (igual que el PUT)",
                leido.Count == 1 && leido.ContainsKey("ITEM_BRUJULA"), Describir(leido));

            api.GuardarInventario(new List<ItemInventarioEnvio>(), PartidaDePrueba);
            Comprobar(log, "modo local: lista vacía vacía la mochila",
                Leer(api).Count == 0, Describir(Leer(api)));
        }

        private static void ProbarModoLocalSumaRepetidos(StringBuilder log, ApiManager api)
        {
            LimpiarPrefs();

            api.GuardarInventario(Envio(("ITEM_FLOR_02", 2), ("ITEM_FLOR_02", 3)), PartidaDePrueba);

            var leido = Leer(api);
            Comprobar(log, "modo local: repetidos se suman (igual que el backend)",
                leido.Count == 1 && leido["ITEM_FLOR_02"] == 5, Describir(leido));
        }

        private static void ProbarModoLocalDescartaCantidadCero(StringBuilder log, ApiManager api)
        {
            LimpiarPrefs();

            api.GuardarInventario(Envio(("ITEM_ROCA", 0), ("ITEM_SURF", -2), ("ITEM_SILBATO", 1)),
                PartidaDePrueba);

            var leido = Leer(api);
            Comprobar(log, "modo local: cantidad <= 0 es no tenerlo",
                leido.Count == 1 && leido.ContainsKey("ITEM_SILBATO"), Describir(leido));
        }

        private static void ProbarModoLocalSeparaPorPartida(StringBuilder log, ApiManager api)
        {
            LimpiarPrefs();

            api.GuardarInventario(Envio(("ITEM_BRUJULA", 1)), PartidaDePrueba);
            var otra = Leer(api, PartidaDePrueba + 1);

            Comprobar(log, "modo local: otra partida no ve esta mochila",
                otra.Count == 0, Describir(otra));

            PlayerPrefs.DeleteKey($"fishy.inventario.{PartidaDePrueba + 1}");
        }

        // ── Auxiliares ─────────────────────────────────────────────────────────

        /// <summary>
        /// ApiManager suelto en modo local. No se usa <c>ApiManager.Instance</c> para
        /// no tocar la sesión real si alguien corre esto con el juego andando.
        ///
        /// `useLocalMode` y `verboseLogs` son [SerializeField] privados, así que se
        /// escriben con SerializedObject en vez de abrirlos con un setter público que
        /// solo existiría para esta prueba. Es código de editor: tiene permitido
        /// hurgar en la serialización, y así el componente no gana API de más.
        /// </summary>
        private static ApiManager ApiLocal()
        {
            var go = new GameObject("ApiManagerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var api = go.AddComponent<ApiManager>();

            var so = new SerializedObject(api);
            so.FindProperty("useLocalMode").boolValue = true;
            so.FindProperty("verboseLogs").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            return api;
        }

        private static List<ItemInventarioEnvio> Envio(params (string id, int cantidad)[] items)
        {
            return items.Select(i => new ItemInventarioEnvio(i.id, i.cantidad)).ToList();
        }

        private static Dictionary<string, int> Leer(ApiManager api, int? partida = null)
        {
            var resultado = new Dictionary<string, int>();
            api.ObtenerInventario(partida ?? PartidaDePrueba,
                onSuccess: filas =>
                {
                    foreach (var f in filas) resultado[f.item_id] = f.cantidad;
                });
            return resultado;
        }

        private static string Describir(Dictionary<string, int> inv)
        {
            if (inv.Count == 0) return "(vacía)";
            return string.Join(", ", inv.OrderBy(p => p.Key).Select(p => $"{p.Key} x{p.Value}"));
        }

        private static void LimpiarPrefs()
        {
            PlayerPrefs.DeleteKey($"fishy.inventario.{PartidaDePrueba}");
            PlayerPrefs.Save();
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
