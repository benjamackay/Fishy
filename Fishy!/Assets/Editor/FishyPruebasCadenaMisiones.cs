#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fishy.Chat;
using Fishy.Mision;
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless de la cadena de misiones del orden de narración: que el
    /// catálogo real (<c>Resources/misiones.json</c>) esté bien enlazado y que la
    /// bifurcación del segundo reto decida lo que tiene que decidir.
    ///
    /// El catálogo es un JSON escrito a mano con ids que se cruzan entre sí y con el
    /// banco. Un error de tipeo en un <c>desbloquea_mision</c> no da error en ningún
    /// lado: la cadena se corta en silencio y el niño/a se queda sin objetivo a mitad
    /// de zona. Estas pruebas son lo que lo detecta.
    ///
    /// Se corren desde  Fishy ▸ Probar cadena de misiones,  o sin abrir el editor:
    ///
    ///   Unity -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///         -executeMethod Fishy.EditorTools.FishyPruebasCadenaMisiones.Ejecutar `
    ///         -logFile -
    /// </summary>
    public static class FishyPruebasCadenaMisiones
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        [MenuItem("Fishy/Probar cadena de misiones")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE LA CADENA DE MISIONES (headless)");
            log.AppendLine(new string('=', 70));

            // Las otras pruebas reemplazan el catálogo estático por uno de juguete.
            CatalogoMisiones.Recargar();

            ProbarQueTodoDestinoExiste(log);
            ProbarQueTodaLaCadenaEsAlcanzable(log);
            ProbarQueLaCadenaSiempreAvanza(log);
            ProbarQueCadaPasoTieneUnaSolaLinea(log);
            ProbarQueLoReferenciadoExisteEnElBanco(log);

            ProbarSinBifurcacionSiempreLaMisma(log);
            ProbarBifurcacionSegunComoTermino(log);
            ProbarRamaYaEntregadaNoSeRedecide(log);
            ProbarSinDatosEsperaSiSeRestaura(log);
            ProbarSinDatosNiNadaQueEsperarVaPorAcepta(log);

            ProbarComoSeAnotaElResultado(log);

            CatalogoMisiones.Recargar();

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── El catálogo real ──────────────────────────────────────────────────

        private static IEnumerable<MisionRegistro> Principales()
            => CatalogoMisiones.Todas.Values.Where(m => m.tipo == "principal");

        private static IEnumerable<string> Destinos(MisionRegistro m)
        {
            if (!string.IsNullOrWhiteSpace(m.desbloquea_mision))    yield return m.desbloquea_mision.Trim();
            if (!string.IsNullOrWhiteSpace(m.desbloquea_si_acepta)) yield return m.desbloquea_si_acepta.Trim();
        }

        private static void ProbarQueTodoDestinoExiste(StringBuilder log)
        {
            var rotos = new List<string>();
            foreach (MisionRegistro m in CatalogoMisiones.Todas.Values)
                foreach (string destino in Destinos(m))
                    if (CatalogoMisiones.Buscar(destino) == null)
                        rotos.Add($"{m.mision_id} → {destino}");

            Comprobar(log, "Toda misión encadenada existe en el catálogo",
                rotos.Count == 0, string.Join(", ", rotos));
        }

        /// <summary>Desde la primera misión, siguiendo las dos ramas de cada
        /// bifurcación, se llega a todas las principales. Una que no se alcanza nunca
        /// se entrega: es contenido muerto o un enlace roto.</summary>
        private static void ProbarQueTodaLaCadenaEsAlcanzable(StringBuilder log)
        {
            MisionRegistro inicio = Principales().OrderBy(m => m.orden).FirstOrDefault();
            if (inicio == null)
            {
                Comprobar(log, "Hay misiones principales en el catálogo", false, "ninguna");
                return;
            }

            var vistos = new HashSet<string>();
            var pendientes = new Stack<string>();
            pendientes.Push(inicio.mision_id);
            while (pendientes.Count > 0)
            {
                string id = pendientes.Pop();
                if (!vistos.Add(id)) continue;
                MisionRegistro m = CatalogoMisiones.Buscar(id);
                if (m == null) continue;
                foreach (string destino in Destinos(m)) pendientes.Push(destino);
            }

            var sueltas = Principales().Select(m => m.mision_id).Where(id => !vistos.Contains(id)).ToList();
            Comprobar(log, $"Toda principal se alcanza desde '{inicio.mision_id}'",
                sueltas.Count == 0, sueltas.Count == 0 ? $"{vistos.Count} alcanzadas" : string.Join(", ", sueltas));
        }

        /// <summary>El panel muestra la misión disponible de menor orden: si un paso
        /// tuviera menor orden que el anterior, una misión vieja todavía disponible le
        /// quitaría el sitio en el panel.</summary>
        private static void ProbarQueLaCadenaSiempreAvanza(StringBuilder log)
        {
            var mal = new List<string>();
            foreach (MisionRegistro m in Principales())
                foreach (string destino in Destinos(m))
                {
                    MisionRegistro d = CatalogoMisiones.Buscar(destino);
                    if (d != null && d.orden <= m.orden) mal.Add($"{m.mision_id}({m.orden}) → {destino}({d.orden})");
                }

            int maxPrincipal = Principales().Max(m => m.orden);
            var secundariasAntes = CatalogoMisiones.Todas.Values
                .Where(m => m.tipo == "secundaria" && m.orden <= maxPrincipal)
                .Select(m => m.mision_id).ToList();

            Comprobar(log, "Cada paso tiene mayor orden que el anterior",
                mal.Count == 0, string.Join(", ", mal));
            Comprobar(log, "Ninguna secundaria puede quitarle el panel a un paso principal",
                secundariasAntes.Count == 0, string.Join(", ", secundariasAntes));
        }

        /// <summary>"Un solo objetivo activo a la vez": varios objetivos solo se
        /// permiten si comparten descripción, que el panel junta en una línea con
        /// contador ("Reúne los tres testimonios (0/3)").</summary>
        private static void ProbarQueCadaPasoTieneUnaSolaLinea(StringBuilder log)
        {
            var mal = new List<string>();
            foreach (MisionRegistro m in Principales())
            {
                int lineas = m.ObjetivosEnOrden()
                    .Select(o => string.IsNullOrWhiteSpace(o.descripcion) ? $"\0{o.orden}" : o.descripcion.Trim())
                    .Distinct().Count();
                if (lineas != 1) mal.Add($"{m.mision_id} ({lineas} líneas)");
            }

            Comprobar(log, "Cada paso principal ocupa una sola línea del panel",
                mal.Count == 0, string.Join(", ", mal));
        }

        private static void ProbarQueLoReferenciadoExisteEnElBanco(StringBuilder log)
        {
            var faltan = new List<string>();
            foreach (MisionRegistro m in Principales())
                foreach (ObjetivoRegistro o in m.ObjetivosEnOrden())
                {
                    switch ((o.tipo ?? "").Trim())
                    {
                        case "hablar_npc":
                            if (DialogoNpcLoader.BuscarEnBanco(o.dialogo_id) == null)
                                faltan.Add($"{m.mision_id}: diálogo {o.dialogo_id}");
                            break;
                        case "chatear_telefono":
                            var convs = BancoPreguntasLoader.CreateConversationForEscenarioId(o.escenario_ids);
                            if (convs == null || convs.Count == 0)
                                faltan.Add($"{m.mision_id}: escenario {o.escenario_ids}");
                            break;
                    }
                }

            Comprobar(log, "Todo diálogo y escenario que pide la cadena está en el banco",
                faltan.Count == 0, string.Join(", ", faltan));
        }

        // ── La bifurcación ────────────────────────────────────────────────────

        private static MisionRegistro Reto(string siRechaza, string siAcepta) => new MisionRegistro
        {
            mision_id = "RETO",
            desbloquea_mision = siRechaza,
            desbloquea_si_acepta = siAcepta,
        };

        private static readonly System.Func<string, bool> Nada = _ => false;

        private static void ProbarSinBifurcacionSiempreLaMisma(StringBuilder log)
        {
            var simple = Reto("SIGUIENTE", null);
            bool bien =
                ConexionAutomaticaMisiones.ElegirSiguiente(simple, Nada, true,  false) == "SIGUIENTE" &&
                ConexionAutomaticaMisiones.ElegirSiguiente(simple, Nada, false, false) == "SIGUIENTE" &&
                ConexionAutomaticaMisiones.ElegirSiguiente(simple, Nada, null,  true)  == "SIGUIENTE";

            Comprobar(log, "Sin 'desbloquea_si_acepta' se encadena siempre igual, sin esperar", bien, "");
        }

        private static void ProbarBifurcacionSegunComoTermino(StringBuilder log)
        {
            var reto = Reto("SEGUNDO_RETO", "RETO_FINAL");
            string siRechazo = ConexionAutomaticaMisiones.ElegirSiguiente(reto, Nada, true,  false);
            string siAcepto  = ConexionAutomaticaMisiones.ElegirSiguiente(reto, Nada, false, false);

            Comprobar(log, "Rechazó el reto → sigue el segundo reto",
                siRechazo == "SEGUNDO_RETO", $"eligió {siRechazo}");
            Comprobar(log, "Aceptó o dudó → salta al reto final",
                siAcepto == "RETO_FINAL", $"eligió {siAcepto}");
        }

        /// <summary>La decisión se toma una vez. Si ya se entregó una rama, se sigue
        /// esa aunque lo que se sepa ahora diga otra cosa: si no, al restaurar se
        /// entregarían las dos.</summary>
        private static void ProbarRamaYaEntregadaNoSeRedecide(StringBuilder log)
        {
            var reto = Reto("SEGUNDO_RETO", "RETO_FINAL");
            string a = ConexionAutomaticaMisiones.ElegirSiguiente(reto, id => id == "RETO_FINAL", true, false);
            string b = ConexionAutomaticaMisiones.ElegirSiguiente(reto, id => id == "SEGUNDO_RETO", false, true);

            Comprobar(log, "Con una rama ya entregada no se vuelve a decidir",
                a == "RETO_FINAL" && b == "SEGUNDO_RETO", $"{a} / {b}");
        }

        private static void ProbarSinDatosEsperaSiSeRestaura(StringBuilder log)
        {
            string elegida = ConexionAutomaticaMisiones.ElegirSiguiente(
                Reto("SEGUNDO_RETO", "RETO_FINAL"), Nada, null, true);

            Comprobar(log, "Sin saber cómo terminó y restaurando la partida, espera",
                elegida == null, $"eligió {elegida ?? "nada"}");
        }

        /// <summary>Las escenas de la rama del rechazo lo dan por hecho ("ya van dos
        /// veces que dices que no"); sin datos, mejor la que no presupone nada.</summary>
        private static void ProbarSinDatosNiNadaQueEsperarVaPorAcepta(StringBuilder log)
        {
            string elegida = ConexionAutomaticaMisiones.ElegirSiguiente(
                Reto("SEGUNDO_RETO", "RETO_FINAL"), Nada, null, false);

            Comprobar(log, "Sin saber cómo terminó y sin nada por llegar, rama de 'no rechazó'",
                elegida == "RETO_FINAL", $"eligió {elegida}");
        }

        // ── El registro de cómo terminó cada chat ─────────────────────────────

        /// <summary>
        /// Misma regla que el contador de presión social del backend: termina en segura
        /// = rechazo; insegura = aceptó; dudosa = no cuenta como rechazo. Y cerrar sin
        /// elegir no anota nada, no lo convierte en aceptación.
        ///
        /// Ids propios de la prueba: sin partida el registro vive solo en memoria y no
        /// se puede vaciar desde aquí.
        /// </summary>
        private static void ProbarComoSeAnotaElResultado(StringBuilder log)
        {
            RechazosEnChats.Anotar(new[] { "PRUEBA_SEGURA" },  OptionSafety.Safe);
            RechazosEnChats.Anotar(new[] { "PRUEBA_INSEGURA" }, OptionSafety.Unsafe);
            RechazosEnChats.Anotar(new[] { "PRUEBA_DUDOSA" },  OptionSafety.Neutral);
            RechazosEnChats.Anotar(new[] { "PRUEBA_SIN_ELEGIR" }, null);

            bool? segura   = RechazosEnChats.Rechazo("PRUEBA_SEGURA");
            bool? insegura = RechazosEnChats.Rechazo("PRUEBA_INSEGURA");
            bool? dudosa   = RechazosEnChats.Rechazo("PRUEBA_DUDOSA");
            bool? nada     = RechazosEnChats.Rechazo("PRUEBA_SIN_ELEGIR");
            bool? enLista  = RechazosEnChats.Rechazo("OTRO, PRUEBA_SEGURA");

            Comprobar(log, "Terminar en una respuesta segura cuenta como rechazo",
                segura == true, $"{segura}");
            Comprobar(log, "Terminar en insegura o dudosa no cuenta como rechazo",
                insegura == false && dudosa == false, $"{insegura} / {dudosa}");
            Comprobar(log, "Cerrar sin elegir deja el resultado como desconocido",
                nada == null, $"{nada?.ToString() ?? "null"}");
            Comprobar(log, "Se encuentra dentro de una lista de escenarios separada por comas",
                enLista == true, $"{enLista}");
        }

        // ── Andamiaje ─────────────────────────────────────────────────────────

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
