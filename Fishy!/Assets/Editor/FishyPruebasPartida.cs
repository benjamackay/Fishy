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
    /// Pruebas headless del guardado de partida (HDU-15): inventario y posicion de Otto.
    ///
    /// Cubren la parte que vive solo en Unity y que ningún test del backend puede
    /// alcanzar: que el catálogo encuentre los assets por su <c>itemId</c>, y que la
    /// rama de modo local del <see cref="ApiManager"/> se comporte igual que el
    /// endpoint. Esa segunda es la que más importa: si las dos ramas divergen, el
    /// bug aparece recién al reconectar, que es el peor momento para descubrirlo.
    ///
    /// Se corren desde el menú  Fishy ▸ Probar guardado de partida,
    /// o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasPartida.Ejecutar `
    ///             -logFile -
    ///
    /// Termina con código 0 si todo pasa y 1 si algo falla.
    /// </summary>
    public static class FishyPruebasPartida
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        /// <summary>Partida ficticia. Se limpia su PlayerPrefs al terminar.</summary>
        private const int PartidaDePrueba = 999999;

        [MenuItem("Fishy/Probar guardado de partida")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE GUARDADO DE PARTIDA (headless)");
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

            ProbarPersonajeSinPosicion(log, api);
            ProbarPersonajeGuardaYRestaura(log, api);
            ProbarPersonajeEnElOrigen(log, api);
            ProbarPersonajeSeparaPorPartida(log, api);

            ProbarRedaccionDeSecretos(log);

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

        // ── El personaje (dónde quedó Otto) ────────────────────────────────────

        private static void ProbarPersonajeSinPosicion(StringBuilder log, ApiManager api)
        {
            LimpiarPrefs();

            PersonajeDto dto = null;
            api.ObtenerPersonaje(PartidaDePrueba, onSuccess: d => dto = d);

            Comprobar(log, "personaje: sin guardar, tiene_posicion es false",
                dto != null && !dto.tiene_posicion,
                dto == null ? "null" : $"tiene_posicion={dto.tiene_posicion}");
        }

        private static void ProbarPersonajeGuardaYRestaura(StringBuilder log, ApiManager api)
        {
            LimpiarPrefs();

            // Sin callback a proposito: guardar tiene que ocurrir igual.
            api.GuardarPersonaje("SampleScene", 12.5f, -3.25f, PartidaDePrueba);

            PersonajeDto dto = null;
            api.ObtenerPersonaje(PartidaDePrueba, onSuccess: d => dto = d);

            Comprobar(log, "personaje: guarda y devuelve la misma posicion",
                dto != null && dto.tiene_posicion
                    && Mathf.Approximately(dto.pos_x ?? 0f, 12.5f)
                    && Mathf.Approximately(dto.pos_y ?? 0f, -3.25f)
                    && dto.escena == "SampleScene",
                dto == null ? "null" : $"{dto.escena} ({dto.pos_x}, {dto.pos_y})");
        }

        private static void ProbarPersonajeEnElOrigen(StringBuilder log, ApiManager api)
        {
            LimpiarPrefs();

            api.GuardarPersonaje("SampleScene", 0f, 0f, PartidaDePrueba);

            PersonajeDto dto = null;
            api.ObtenerPersonaje(PartidaDePrueba, onSuccess: d => dto = d);

            // El (0,0) es un lugar del mapa. Si se confundiera con "no hay posicion",
            // a un nino que guarda ahi lo mandaria de vuelta al spawnPoint.
            Comprobar(log, "personaje: el (0,0) NO es 'sin posicion'",
                dto != null && dto.tiene_posicion,
                dto == null ? "null" : $"tiene_posicion={dto.tiene_posicion}");
        }

        private static void ProbarPersonajeSeparaPorPartida(StringBuilder log, ApiManager api)
        {
            LimpiarPrefs();

            api.GuardarPersonaje("SampleScene", 5f, 5f, PartidaDePrueba);

            PersonajeDto otra = null;
            api.ObtenerPersonaje(PartidaDePrueba + 1, onSuccess: d => otra = d);

            Comprobar(log, "personaje: otra partida no ve esta posicion",
                otra != null && !otra.tiene_posicion,
                otra == null ? "null" : $"tiene_posicion={otra.tiene_posicion}");

            PlayerPrefs.DeleteKey($"fishy.personaje.{PartidaDePrueba + 1}");
        }

        // ── Que no se filtren credenciales por consola ─────────────────────────

        private static void ProbarRedaccionDeSecretos(StringBuilder log)
        {
            // `Redactar` es privado: es detalle interno del ApiManager, no API publica.
            // Se llega por reflexion en vez de abrirlo solo para esta prueba.
            var metodo = typeof(ApiManager).GetMethod("Redactar",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (metodo == null)
            {
                Comprobar(log, "existe ApiManager.Redactar", false, "no se encontro el metodo");
                return;
            }

            string Redactar(string j) => (string)metodo.Invoke(null, new object[] { j });

            // El caso que reporto Oscar: el login mandaba la contrasena en claro.
            string login = Redactar("{\"nombre\":\"Oscar\",\"password\":\"clave-secreta-123\"}");
            Comprobar(log, "la contrasena no sale por consola",
                !login.Contains("clave-secreta-123") && login.Contains("Oscar"), login);

            // La respuesta del login trae el token, que es igual de grave: es una
            // credencial portadora y no expira.
            string respuesta = Redactar("{\"token\":\"9f8a7b6c5d4e3f2a1b\",\"adulto_id\":3}");
            Comprobar(log, "el token tampoco sale",
                !respuesta.Contains("9f8a7b6c5d4e3f2a1b") && respuesta.Contains("3"), respuesta);

            // Anidado: un secreto adentro de otro objeto tambien se tapa.
            string anidado = Redactar("{\"datos\":{\"password\":\"otra-clave\"},\"ok\":true}");
            Comprobar(log, "tambien tapa los secretos anidados",
                !anidado.Contains("otra-clave"), anidado);

            // En una lista, que es como vienen inventario y objetos recogidos.
            string lista = Redactar("[{\"item_id\":\"ITEM_BRUJULA\"},{\"token\":\"abc123\"}]");
            Comprobar(log, "tapa dentro de listas",
                !lista.Contains("abc123") && lista.Contains("ITEM_BRUJULA"), lista);

            // Lo que NO es secreto tiene que seguir viendose, o el log deja de servir.
            string normal = Redactar("{\"item_id\":\"ITEM_FLOR_01\",\"cantidad\":3}");
            Comprobar(log, "lo que no es secreto se sigue viendo",
                normal.Contains("ITEM_FLOR_01") && normal.Contains("3"), normal);

            // Falla cerrado: si no se puede leer, no se imprime por si trae algo.
            string basura = Redactar("esto no es json y trae password=hola");
            Comprobar(log, "un cuerpo no-JSON se omite entero",
                !basura.Contains("hola"), basura);
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
            PlayerPrefs.DeleteKey($"fishy.personaje.{PartidaDePrueba}");
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
