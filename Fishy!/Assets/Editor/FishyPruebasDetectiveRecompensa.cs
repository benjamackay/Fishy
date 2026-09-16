#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Fishy.Detective;
using Fishy.Net;
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless de las recompensas del Modo Detective (HDU-11) y del
    /// álbum mínimo que se adelantó de HDU-12.
    ///
    /// Cubren la parte que un test del backend no puede alcanzar: que
    /// CatalogoRecompensasDetective y CatalogoItems encuentren el contenido
    /// nuevo, y que DetectiveCaseManager entregue el pin correcto sin
    /// duplicarlo al repetir el caso (InventoryManager.AddItem no es
    /// idempotente por sí solo, así que este guard es justo lo que hay que
    /// vigilar).
    ///
    /// Se corren desde el menú  Fishy ▸ Probar recompensas Detective,
    /// o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasDetectiveRecompensa.Ejecutar `
    ///             -logFile -
    ///
    /// Termina con código 0 si todo pasa y 1 si algo falla.
    /// </summary>
    public static class FishyPruebasDetectiveRecompensa
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        [MenuItem("Fishy/Probar recompensas Detective")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE RECOMPENSAS DEL MODO DETECTIVE (headless)");
            log.AppendLine(new string('=', 70));

            AsegurarApiLocal();

            ProbarCatalogoRecompensasEncuentraLosTresCasos(log);
            ProbarCatalogoRecompensasCasoInexistenteEsNull(log);
            ProbarCatalogoItemsEncuentraLosCuatroNuevos(log);

            ProbarOtorgaElPinAlLlegarAlUmbral(log);
            ProbarNoOtorgaNadaBajoElUmbral(log);
            ProbarRepetirElCasoNoDuplica(log);
            ProbarAlbumLlegaConElPrimerPinYNoSeDuplica(log);
            ProbarCasoSinRecompensaNoRevienta(log);
            ProbarUsaRecompensaDelBackendSiElCasoLaTrae(log);

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── El catálogo de recompensas ───────────────────────────────────────────

        private static void ProbarCatalogoRecompensasEncuentraLosTresCasos(StringBuilder log)
        {
            foreach (var caseId in new[] { "DC_CASO_01", "DC_CASO_02", "DC_CASO_03" })
            {
                var r = CatalogoRecompensasDetective.Buscar(caseId);
                Comprobar(log, $"CatalogoRecompensasDetective encuentra {caseId}",
                    r != null && !string.IsNullOrEmpty(r.itemId), r?.itemId ?? "null");
            }
        }

        private static void ProbarCatalogoRecompensasCasoInexistenteEsNull(StringBuilder log)
        {
            Comprobar(log, "un caseId que no existe devuelve null",
                CatalogoRecompensasDetective.Buscar("CASO_QUE_NO_EXISTE") == null, "");
        }

        private static void ProbarCatalogoItemsEncuentraLosCuatroNuevos(StringBuilder log)
        {
            CatalogoItems.Recargar();
            var ids = new[]
            {
                "PIN_VIGIA_SILENCIOSO", "PIN_TESTIGO_SOLIDARIO", "PIN_ANALISTA_CORRIENTES",
                AlbumEvidenciasUI.ItemIdAlbum,
            };
            foreach (var id in ids)
                Comprobar(log, $"CatalogoItems encuentra {id}", CatalogoItems.Buscar(id) != null, "");
        }

        // ── DetectiveCaseManager: otorgar / no duplicar ──────────────────────────

        private static void ProbarOtorgaElPinAlLlegarAlUmbral(StringBuilder log)
        {
            InventoryManager.Instance.Vaciar();

            var manager = NuevoManager();
            manager.CargarCaso(CasoDePrueba("DC_CASO_01", 2));
            manager.ToggleMarca("MSG_RIESGO_0");
            manager.ToggleMarca("MSG_RIESGO_1");   // 2/2 = 100% >= umbral 0.5
            manager.CalcularResultado();

            var pin = CatalogoItems.Buscar("PIN_VIGIA_SILENCIOSO");
            Comprobar(log, "otorga el pin al llegar al umbral",
                InventoryManager.Instance.GetQuantity(pin) == 1,
                $"cantidad={InventoryManager.Instance.GetQuantity(pin)}");

            Object.DestroyImmediate(manager.gameObject);
        }

        private static void ProbarNoOtorgaNadaBajoElUmbral(StringBuilder log)
        {
            InventoryManager.Instance.Vaciar();

            var manager = NuevoManager();
            manager.CargarCaso(CasoDePrueba("DC_CASO_01", 2));
            // Nada marcado: 0/2 = 0% < umbral 0.5.
            manager.CalcularResultado();

            var pin = CatalogoItems.Buscar("PIN_VIGIA_SILENCIOSO");
            Comprobar(log, "no otorga nada por debajo del umbral",
                InventoryManager.Instance.GetQuantity(pin) == 0,
                $"cantidad={InventoryManager.Instance.GetQuantity(pin)}");

            Object.DestroyImmediate(manager.gameObject);
        }

        private static void ProbarRepetirElCasoNoDuplica(StringBuilder log)
        {
            InventoryManager.Instance.Vaciar();

            var manager = NuevoManager();
            manager.CargarCaso(CasoDePrueba("DC_CASO_01", 2));
            manager.ToggleMarca("MSG_RIESGO_0");
            manager.ToggleMarca("MSG_RIESGO_1");

            manager.CalcularResultado();
            manager.CalcularResultado();   // "repetir" el caso ya aprobado

            var pin = CatalogoItems.Buscar("PIN_VIGIA_SILENCIOSO");
            Comprobar(log, "repetir el caso no duplica el pin",
                InventoryManager.Instance.GetQuantity(pin) == 1,
                $"cantidad={InventoryManager.Instance.GetQuantity(pin)}");

            Object.DestroyImmediate(manager.gameObject);
        }

        private static void ProbarAlbumLlegaConElPrimerPinYNoSeDuplica(StringBuilder log)
        {
            InventoryManager.Instance.Vaciar();
            var album = CatalogoItems.Buscar(AlbumEvidenciasUI.ItemIdAlbum);

            var manager1 = NuevoManager();
            manager1.CargarCaso(CasoDePrueba("DC_CASO_01", 1));
            manager1.ToggleMarca("MSG_RIESGO_0");
            manager1.CalcularResultado();

            Comprobar(log, "el álbum aparece junto con el primer pin",
                InventoryManager.Instance.GetQuantity(album) == 1,
                $"cantidad={InventoryManager.Instance.GetQuantity(album)}");

            var manager2 = NuevoManager();
            manager2.CargarCaso(CasoDePrueba("DC_CASO_02", 1));
            manager2.ToggleMarca("MSG_RIESGO_0");
            manager2.CalcularResultado();

            Comprobar(log, "un segundo pin no duplica el álbum",
                InventoryManager.Instance.GetQuantity(album) == 1,
                $"cantidad={InventoryManager.Instance.GetQuantity(album)}");

            Object.DestroyImmediate(manager1.gameObject);
            Object.DestroyImmediate(manager2.gameObject);
        }

        private static void ProbarCasoSinRecompensaNoRevienta(StringBuilder log)
        {
            InventoryManager.Instance.Vaciar();

            var manager = NuevoManager();
            manager.CargarCaso(CasoDePrueba("CASO_QUE_NO_EXISTE_EN_EL_CATALOGO", 1));
            manager.ToggleMarca("MSG_RIESGO_0");

            bool exploto = false;
            try { manager.CalcularResultado(); }
            catch { exploto = true; }

            Comprobar(log, "un caso sin recompensa en el catálogo no revienta", !exploto, "");

            Object.DestroyImmediate(manager.gameObject);
        }

        /// <summary>Si el caso ya trae su propia recompensa (como la mandaría el
        /// backend el día que HDU-11 se implemente ahí), esa gana por sobre la
        /// del catálogo local — aunque sean casos distintos.</summary>
        private static void ProbarUsaRecompensaDelBackendSiElCasoLaTrae(StringBuilder log)
        {
            InventoryManager.Instance.Vaciar();

            var caso = CasoDePrueba("DC_CASO_01", 1);   // el catálogo local le daría PIN_VIGIA_SILENCIOSO
            caso.recompensaItemId = "PIN_TESTIGO_SOLIDARIO";   // "el backend" manda otra cosa
            caso.recompensaNombre = "Pin del Testigo Solidario";
            caso.recompensaUmbralAciertos = 0.5f;
            caso.recompensaNoDuplicaAlRepetir = true;

            var manager = NuevoManager();
            manager.CargarCaso(caso);
            manager.ToggleMarca("MSG_RIESGO_0");
            manager.CalcularResultado();

            var delBackend = CatalogoItems.Buscar("PIN_TESTIGO_SOLIDARIO");
            var delCatalogoLocal = CatalogoItems.Buscar("PIN_VIGIA_SILENCIOSO");
            Comprobar(log, "si el caso ya trae recompensa, se usa esa y no la del catálogo local",
                InventoryManager.Instance.GetQuantity(delBackend) == 1 &&
                InventoryManager.Instance.GetQuantity(delCatalogoLocal) == 0,
                $"backend={InventoryManager.Instance.GetQuantity(delBackend)} " +
                $"local={InventoryManager.Instance.GetQuantity(delCatalogoLocal)}");

            Object.DestroyImmediate(manager.gameObject);
        }

        // ── Auxiliares ────────────────────────────────────────────────────────

        private static DetectiveCaseManager NuevoManager()
        {
            var go = new GameObject("DetectiveCaseManagerDePrueba");
            return go.AddComponent<DetectiveCaseManager>();
        }

        /// <summary>Caso mínimo con <paramref name="cantidadRiesgo"/> mensajes de
        /// riesgo no ambiguos, ids "MSG_RIESGO_0".."MSG_RIESGO_N-1".</summary>
        private static DetectiveCase CasoDePrueba(string caseId, int cantidadRiesgo)
        {
            var mensajes = new List<DetectiveMessage>();
            for (int i = 0; i < cantidadRiesgo; i++)
            {
                mensajes.Add(new DetectiveMessage
                {
                    id = $"MSG_RIESGO_{i}",
                    autor = "NPC de prueba",
                    texto = "mensaje de prueba",
                    esRiesgo = true,
                    esAmbiguo = false,
                });
            }

            return new DetectiveCase
            {
                caseId = caseId,
                npcObservado1 = "NPC1",
                npcObservado2 = "NPC2",
                permisoPlayerText = "",
                permisoNpcNombre = "",
                permisoNpcResponse = "",
                mensajes = mensajes,
                explicacionGuiada = new List<ExplicacionEntry>(),
            };
        }

        /// <summary>Reusa el ApiManager singleton si ya existe (re-ejecutar el
        /// menú en la misma sesión de editor), o crea uno en modo local. Así
        /// ReportarProgreso corta antes de pegarle a la red.</summary>
        private static void AsegurarApiLocal()
        {
            var api = ApiManager.Instance;
            if (api == null)
            {
                var go = new GameObject("ApiManagerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
                api = go.AddComponent<ApiManager>();
            }

            var so = new SerializedObject(api);
            so.FindProperty("useLocalMode").boolValue = true;
            so.FindProperty("verboseLogs").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
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
