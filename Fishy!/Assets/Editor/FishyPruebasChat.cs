#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Fishy.Chat;
using Fishy.Net;
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless del armado de conversaciones a partir del banco de preguntas.
    ///
    /// Existen por un bug que costó una tarde y que ninguna prueba habría pillado: las
    /// conversaciones con los personajes sospechosos <b>empezaban por su propio final</b>
    /// cuando el banco venía de la BD local. El nodo inicial se tomaba del primer
    /// elemento de la lista, y el orden lo decide el backend
    /// (<c>ordering = ["zona", "npc_id", "fase", …]</c>) con <c>fase</c> nullable: en
    /// SQLite los NULL ordenan primero y los nodos FIN no tienen fase. Contra PostgreSQL
    /// ordenan últimos, así que el bug era invisible en Supabase.
    ///
    /// Lo que se prueba aquí es justamente eso: que el armado <b>no dependa del orden en
    /// que lleguen las preguntas</b>. Se le pasa el banco en el orden malo a propósito.
    ///
    /// Se corren desde  Fishy ▸ Probar armado de chats,  o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasChat.Ejecutar `
    ///             -logFile -
    /// </summary>
    public static class FishyPruebasChat
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        [MenuItem("Fishy/Probar armado de chats")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE ARMADO DE CHATS (headless)");
            log.AppendLine(new string('=', 70));

            ProbarArranqueConLosFinPrimero(log);
            ProbarArranqueConLosFinAlFinal(log);
            ProbarDosNpcsEnUnEscenario(log);
            ProbarLaConversacionNoSeCierraDeInmediato(log);
            ProbarCategoriaDeRiesgo(log);
            ProbarCategoriaDeUnaConversacionNeutra(log);

            // Devolver el banco de verdad: las pruebas metieron uno de cuatro preguntas
            // en la caché estática, y si se corren desde el menú el siguiente Play se lo
            // encontraría puesto.
            BancoPreguntasLoader.Recargar();

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── Las pruebas ───────────────────────────────────────────────────────

        /// <summary>El orden que sirve SQLite: los FIN (fase NULL) delante.</summary>
        private static void ProbarArranqueConLosFinPrimero(StringBuilder log)
        {
            var conv = Armar(FinSeguro(), FinInseguro(), Q01(), Q02());

            Comprobar(log, "Con los nodos FIN primero (como los sirve SQLite) arranca en Q01",
                conv != null && conv.startNodeId == "Q01",
                $"arrancó en '{conv?.startNodeId}'");
        }

        /// <summary>El orden que sirve PostgreSQL. Tiene que dar lo mismo.</summary>
        private static void ProbarArranqueConLosFinAlFinal(StringBuilder log)
        {
            var conv = Armar(Q01(), Q02(), FinSeguro(), FinInseguro());

            Comprobar(log, "Con los FIN al final (como los sirve PostgreSQL) arranca igual en Q01",
                conv != null && conv.startNodeId == "Q01",
                $"arrancó en '{conv?.startNodeId}'");
        }

        /// <summary>
        /// El otro caso, que fallaba también contra Supabase: `npc_id` pesa más que
        /// `fase` en el ordering, así que un escenario de dos NPCs llega encabezado por
        /// el del nombre alfabéticamente menor, que no es por donde empieza la historia.
        /// </summary>
        private static void ProbarDosNpcsEnUnEscenario(StringBuilder log)
        {
            var segunda = Q02();
            segunda.npc_id = "NPC_01";          // alfabéticamente antes que el del arranque
            var primera = Q01();
            primera.npc_id = "NPC_TESTIMONIOS";

            var conv = Armar(segunda, primera, FinSeguro());

            Comprobar(log, "Un escenario con dos NPCs arranca por la fase 1, no por el npc_id menor",
                conv != null && conv.startNodeId == "Q01",
                $"arrancó en '{conv?.startNodeId}'");
        }

        /// <summary>
        /// La consecuencia visible del bug: el chat se abría y se cerraba de una, porque
        /// el nodo FIN tiene closesChat. Si el arranque es correcto, el primer nodo NO
        /// cierra y además ofrece opciones que el niño/a pueda responder.
        /// </summary>
        private static void ProbarLaConversacionNoSeCierraDeInmediato(StringBuilder log)
        {
            var conv = Armar(FinSeguro(), FinInseguro(), Q01(), Q02());
            var nodo = conv?.GetNode(conv.startNodeId);

            Comprobar(log, "El primer nodo no cierra el chat y trae opciones",
                nodo != null && !nodo.closesChat && nodo.HasOptions,
                nodo == null ? "no se encontró el nodo inicial"
                             : $"closesChat={nodo.closesChat}, opciones={nodo.options.Count}");
        }

        /// <summary>
        /// La categoría tiene que ser la del riesgo, no la del nodo de cierre.
        ///
        /// Los FIN son `neutral` por definición, así que tomarla del primer elemento de
        /// la lista etiquetaba como neutra una conversación de acoso entera. Se vio en la
        /// partida 2: dos chats de grooming con sus cuatro respuestas bien guardadas y
        /// `categoria_riesgo = 'neutral'`, que es por donde filtra el reporte del adulto.
        /// </summary>
        private static void ProbarCategoriaDeRiesgo(StringBuilder log)
        {
            var conv = Armar(FinSeguro(), FinInseguro(), Q01(), Q02());

            Comprobar(log, "La categoría sale del mensaje de riesgo, no del nodo FIN",
                conv != null && conv.categoriaRiesgo == "ciberacoso_exclusion",
                $"quedó en '{conv?.categoriaRiesgo}'");
        }

        /// <summary>
        /// Y una conversación que de verdad no tiene riesgo —el cierre de zona, por
        /// ejemplo— tiene que seguir siendo neutra. Si no, el arreglo de arriba se
        /// llevaría por delante la distinción que el reporte necesita.
        /// </summary>
        private static void ProbarCategoriaDeUnaConversacionNeutra(StringBuilder log)
        {
            var conv = Armar(FinSeguro());

            Comprobar(log, "Una conversación sin mensajes de riesgo sigue siendo neutra",
                conv != null && conv.categoriaRiesgo == "neutral",
                $"quedó en '{conv?.categoriaRiesgo}'");
        }

        // ── Andamiaje ─────────────────────────────────────────────────────────

        private const string Escenario = "PRUEBA_CHAT01";

        /// <summary>
        /// Mete estas preguntas en el banco —en el orden dado— y arma la conversación.
        ///
        /// Se inyectan por <see cref="BancoPreguntasLoader.AplicarDesdeBackend"/>, que es
        /// el mismo camino que usa el juego cuando baja el banco de la base, así que la
        /// prueba ejercita también la traducción de DTOs.
        /// </summary>
        private static ChatConversation Armar(params PreguntaBancoDto[] preguntas)
        {
            BancoPreguntasLoader.AplicarDesdeBackend(new List<PreguntaBancoDto>(preguntas));
            var convs = BancoPreguntasLoader.CreateConversationForEscenarioId(Escenario);
            return convs != null && convs.Count > 0 ? convs[0] : null;
        }

        private static PreguntaBancoDto Base(string id, int? fase, int? orden, string mensaje)
            => new PreguntaBancoDto
            {
                pregunta_id = id,
                hdu = "HDU-3",
                zona = "ciberacoso",
                npc_id = "NPC_03",
                npc_nombre = "Flamenco",
                fase = fase,
                orden_en_fase = orden,
                escenario_id = Escenario,
                escenario_nombre = "Escenario de prueba",
                categoria = "ciberacoso_exclusion",
                nivel_riesgo = 3,
                mensaje_npc = mensaje,
                opciones = new List<OpcionBancoDto>(),
            };

        private static PreguntaBancoDto Q01()
        {
            var p = Base("Q01", 1, 1, "Te sacamos del grupo, jaja.");
            p.es_mensaje_riesgo = true;
            p.opciones.Add(new OpcionBancoDto
            {
                opcion_id = "Q01_R1", texto = "Eso no se hace. Reporto.",
                tipo = "segura_optima", impacto_puntuacion = 2,
                siguiente_pregunta = "FIN_SEGURO", orden = 0,
            });
            p.opciones.Add(new OpcionBancoDto
            {
                opcion_id = "Q01_R2", texto = "Da igual, no digo nada.",
                tipo = "insegura", impacto_puntuacion = -1,
                siguiente_pregunta = "Q02", orden = 1,
            });
            return p;
        }

        private static PreguntaBancoDto Q02()
        {
            var p = Base("Q02", 1, 2, "Mira cómo se pica.");
            p.es_mensaje_riesgo = true;
            p.opciones.Add(new OpcionBancoDto
            {
                opcion_id = "Q02_R1", texto = "Aviso a un adulto.",
                tipo = "segura_optima", impacto_puntuacion = 2,
                siguiente_pregunta = "FIN_SEGURO", orden = 0,
            });
            p.opciones.Add(new OpcionBancoDto
            {
                opcion_id = "Q02_R2", texto = "Lo dejo pasar.",
                tipo = "insegura", impacto_puntuacion = -1,
                siguiente_pregunta = "FIN_INSEGURO", orden = 1,
            });
            return p;
        }

        /// <summary>Nodo de cierre: sin fase, que es la clave del bug.</summary>
        private static PreguntaBancoDto FinSeguro()
        {
            var p = Base("FIN_SEGURO", null, null, "[SISTEMA] Flamenco se desconecta.");
            p.categoria = "neutral";
            p.nivel_riesgo = 0;
            p.es_fin_de_npc = true;
            return p;
        }

        private static PreguntaBancoDto FinInseguro()
        {
            var p = Base("FIN_INSEGURO", null, null, "[SISTEMA] La exclusión siguió.");
            p.categoria = "neutral";
            p.nivel_riesgo = 0;
            p.es_fin_de_npc = true;
            return p;
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
