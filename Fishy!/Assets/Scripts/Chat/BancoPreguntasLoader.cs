using System.Collections.Generic;
using UnityEngine;

namespace Fishy.Chat
{
    /// <summary>
    /// Carga banco_preguntas.json (Resources/banco_preguntas.json) y lo convierte
    /// en objetos <see cref="ChatConversation"/> listos para el chat del celular.
    ///
    /// Métodos principales:
    ///  • <see cref="CreateHDU2Conversations"/>  — NPC_01 (Alex) + NPC_02 (Valen)
    ///  • <see cref="CreateHDU8Conversations"/>  — 3 escenarios de chat simulado
    ///  • <see cref="CreatePhoneConversations"/> — HDU-2 (para Zona Desconocidos)
    /// </summary>
    public static class BancoPreguntasLoader
    {
        private const string ResourcePath = "banco_preguntas";

        private static BancoRaiz _cache;

        // ── Carga ──────────────────────────────────────────────────────────────
        public static BancoRaiz Load()
        {
            if (_cache != null) return _cache;

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError("[BancoPreguntasLoader] No se encontró Resources/banco_preguntas.json");
                _cache = new BancoRaiz();
                return _cache;
            }

            _cache = JsonUtility.FromJson<BancoRaiz>(asset.text);
            if (_cache == null || _cache.preguntas == null)
            {
                Debug.LogError("[BancoPreguntasLoader] Error al parsear banco_preguntas.json");
                _cache = new BancoRaiz();
            }
            else
            {
                Debug.Log($"[BancoPreguntasLoader] Cargadas {_cache.preguntas.Count} preguntas del banco.");
            }

            return _cache;
        }

        // ── HDU-2: Zona Desconocidos ───────────────────────────────────────────
        /// <summary>
        /// Crea una <see cref="ChatConversation"/> por cada NPC de HDU-2
        /// (Alex y Valen), lista para el módulo de chat diegético del celular.
        /// </summary>
        public static List<ChatConversation> CreateHDU2Conversations()
        {
            var banco   = Load();
            var result  = new List<ChatConversation>();
            var byNpc   = GroupByNpc(banco.preguntas);

            foreach (var kv in byNpc)
                result.Add(BuildConversationFromPreguntas(kv.Key, kv.Value, "desconocidos"));

            return result;
        }

        // ── HDU-8: Chat Simulado ───────────────────────────────────────────────
        /// <summary>
        /// Crea una <see cref="ChatConversation"/> por escenario de HDU-8
        /// (Grooming · Ciberacoso · Reto Viral).
        /// </summary>
        public static List<ChatConversation> CreateHDU8Conversations()
        {
            var banco    = Load();
            var result   = new List<ChatConversation>();
            var byEscena = GroupByEscenario(banco.preguntas);

            foreach (var kv in byEscena)
                result.Add(BuildHDU8Conversation(kv.Key, kv.Value));

            return result;
        }

        /// <summary>
        /// Conversaciones para el celular de Zona Desconocidos (HDU-2).
        /// Alias semántico de <see cref="CreateHDU2Conversations"/>.
        /// </summary>
        public static List<ChatConversation> CreatePhoneConversations()
            => CreateHDU2Conversations();

        /// <summary>
        /// Retorna la <see cref="ChatConversation"/> de un único NPC de HDU-2.
        /// Útil cuando cada zona del mapa activa solo una conversación (p.ej. "NPC_01" o "NPC_02").
        /// </summary>
        public static List<ChatConversation> CreateHDU2ConversationForNpc(string npcId)
        {
            var banco  = Load();
            var byNpc  = GroupByNpc(banco.preguntas);
            var result = new List<ChatConversation>();
            if (byNpc.TryGetValue(npcId, out var preguntas))
                result.Add(BuildConversationFromPreguntas(npcId, preguntas, "desconocidos"));
            else
                Debug.LogWarning($"[BancoPreguntasLoader] No se encontró el NPC '{npcId}' en HDU-2.");
            return result;
        }

        // ── Agrupación ─────────────────────────────────────────────────────────
        private static Dictionary<string, List<PreguntaBanco>> GroupByNpc(List<PreguntaBanco> all)
        {
            var dict = new Dictionary<string, List<PreguntaBanco>>();
            foreach (var p in all)
            {
                if (p.hdu != "HDU-2") continue;
                if (string.IsNullOrEmpty(p.npc_id)) continue;
                if (!dict.ContainsKey(p.npc_id)) dict[p.npc_id] = new List<PreguntaBanco>();
                dict[p.npc_id].Add(p);
            }
            return dict;
        }

        private static Dictionary<string, List<PreguntaBanco>> GroupByEscenario(List<PreguntaBanco> all)
        {
            var dict = new Dictionary<string, List<PreguntaBanco>>();
            foreach (var p in all)
            {
                if (p.hdu != "HDU-8") continue;
                if (string.IsNullOrEmpty(p.escenario_id)) continue;
                if (!dict.ContainsKey(p.escenario_id)) dict[p.escenario_id] = new List<PreguntaBanco>();
                dict[p.escenario_id].Add(p);
            }
            return dict;
        }

        // ── Construcción de ChatConversation para HDU-2 ───────────────────────
        private static ChatConversation BuildConversationFromPreguntas(
            string npcId, List<PreguntaBanco> preguntas, string zoneId)
        {
            var conv = ScriptableObject.CreateInstance<ChatConversation>();
            conv.zoneId         = zoneId;
            conv.categoriaRiesgo = "grooming";

            // Nombre del NPC (primer nodo con nombre definido).
            string npcNombre = "";
            foreach (var p in preguntas)
                if (!string.IsNullOrEmpty(p.npc_nombre)) { npcNombre = p.npc_nombre; break; }
            conv.contactName = npcNombre;

            // Acumular todos los nodos.
            var allNodes = new List<ChatNode>();

            // Nodo de apertura del celular (mensaje introductorio del sistema).
            var intro = new ChatNode
            {
                id         = $"{npcId}_INTRO",
                text       = $"📱 {npcNombre} te está escribiendo…",
                kind       = ChatMessageKind.Neutral,
                isSystem   = true,
                closesChat = false,
                nextNodeId = preguntas.Count > 0 ? preguntas[0].id : ""
            };
            allNodes.Add(intro);
            conv.startNodeId = intro.id;

            // Construir un nodo por pregunta.
            foreach (var p in preguntas)
            {
                bool isFin = p.es_fin_de_npc || p.es_fin_de_zona;
                bool isSystem = isFin || p.mensaje_npc.StartsWith("[SISTEMA]") || p.mensaje_npc.StartsWith("[ZONA");

                var node = new ChatNode
                {
                    id         = p.id,
                    text       = LimpiarTextoSistema(p.mensaje_npc),
                    kind       = p.es_mensaje_riesgo ? ChatMessageKind.Risk : ChatMessageKind.Neutral,
                    isSystem   = isSystem,
                    closesChat = isFin,
                    nextNodeId = p.narrativa_continuacion ?? ""
                };

                // Si hay opciones, añadirlas + nodos de consecuencia.
                if (p.opciones_respuesta != null && p.opciones_respuesta.Count > 0)
                {
                    foreach (var op in p.opciones_respuesta)
                    {
                        OptionSafety safety = TipoToSafety(op.tipo);

                        // Crear nodo de consecuencia (retroalimentación pedagógica).
                        string consecNodeId = $"CONS_{op.id}";
                        bool consecFin = string.IsNullOrEmpty(op.siguiente_pregunta);

                        var consecNode = new ChatNode
                        {
                            id         = consecNodeId,
                            text       = op.consecuencia_narrativa ?? "",
                            kind       = ChatMessageKind.Neutral,
                            isSystem   = true,
                            closesChat = consecFin,
                            nextNodeId = consecFin ? "" : op.siguiente_pregunta
                        };
                        allNodes.Add(consecNode);

                        // La opción apunta al nodo de consecuencia.
                        node.options.Add(new ChatOption(op.texto, safety, consecNodeId, op.id));
                    }
                }
                // Sin opciones y sin nextNodeId definido: fin automático.
                else if (!isFin && string.IsNullOrEmpty(node.nextNodeId))
                {
                    node.closesChat = true;
                }

                allNodes.Add(node);
            }

            conv.nodes = allNodes;
            return conv;
        }

        // ── Construcción de ChatConversation para HDU-8 ───────────────────────
        private static ChatConversation BuildHDU8Conversation(
            string escenarioId, List<PreguntaBanco> preguntas)
        {
            var conv = ScriptableObject.CreateInstance<ChatConversation>();
            conv.zoneId         = "chat_simulado";
            conv.categoriaRiesgo = EscenarioToCategoria(escenarioId);

            // Nombre del contacto: extraído del historial del primer mensaje.
            string contactName = DeducirContactName(preguntas);
            conv.contactName = contactName;

            var allNodes = new List<ChatNode>();
            string startNodeId = null;

            for (int i = 0; i < preguntas.Count; i++)
            {
                var p = preguntas[i];

                // Historial previo (solo en el primer mensaje del escenario).
                if (p.historial_previo != null && p.historial_previo.Count > 0)
                {
                    string prevNext = p.id;
                    // Crear la cadena de historial en orden inverso.
                    for (int h = p.historial_previo.Count - 1; h >= 0; h--)
                    {
                        var hist = p.historial_previo[h];
                        string histId = $"HIST_{escenarioId}_{h}";
                        var histNode = new ChatNode
                        {
                            id         = histId,
                            text       = hist.mensaje,
                            kind       = ChatMessageKind.Neutral,
                            isSystem   = false,
                            closesChat = false,
                            nextNodeId = prevNext
                        };
                        allNodes.Add(histNode);
                        prevNext = histId;
                    }

                    // El primer nodo de la cadena es el startNodeId.
                    if (startNodeId == null) startNodeId = prevNext;
                }
                else if (startNodeId == null)
                {
                    startNodeId = p.id;
                }

                // Nodo principal de la pregunta.
                bool isFin = p.es_fin_de_npc || p.es_fin_de_zona;
                var node = new ChatNode
                {
                    id         = p.id,
                    text       = p.mensaje_npc,
                    kind       = p.es_mensaje_riesgo ? ChatMessageKind.Risk : ChatMessageKind.Neutral,
                    isSystem   = false,
                    closesChat = isFin && (p.opciones_respuesta == null || p.opciones_respuesta.Count == 0),
                    nextNodeId = p.narrativa_continuacion ?? ""
                };

                if (p.opciones_respuesta != null && p.opciones_respuesta.Count > 0)
                {
                    foreach (var op in p.opciones_respuesta)
                    {
                        OptionSafety safety = TipoToSafety(op.tipo);

                        // Nodo de consecuencia.
                        string consecNodeId = $"CONS_{op.id}";
                        bool termina = string.IsNullOrEmpty(op.siguiente_pregunta) || isFin;

                        allNodes.Add(new ChatNode
                        {
                            id         = consecNodeId,
                            text       = op.consecuencia_narrativa ?? "",
                            kind       = ChatMessageKind.Neutral,
                            isSystem   = true,
                            closesChat = termina,
                            nextNodeId = termina ? "" : op.siguiente_pregunta
                        });

                        node.options.Add(new ChatOption(op.texto, safety, consecNodeId, op.id));
                    }
                }

                allNodes.Add(node);
            }

            conv.startNodeId = startNodeId ?? (allNodes.Count > 0 ? allNodes[0].id : "");
            conv.nodes = allNodes;
            return conv;
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static OptionSafety TipoToSafety(string tipo)
        {
            switch (tipo)
            {
                case "insegura":       return OptionSafety.Unsafe;
                case "segura_basica":
                case "segura_optima":  return OptionSafety.Safe;
                default:               return OptionSafety.Neutral;
            }
        }

        private static string EscenarioToCategoria(string escenarioId)
        {
            if (escenarioId.Contains("GROOMING"))    return "grooming";
            if (escenarioId.Contains("CIBERACOSO"))  return "ciberacoso";
            if (escenarioId.Contains("RETO"))        return "reto_viral";
            return "desconocidos";
        }

        private static string DeducirContactName(List<PreguntaBanco> preguntas)
        {
            foreach (var p in preguntas)
            {
                if (p.historial_previo == null) continue;
                foreach (var h in p.historial_previo)
                    if (!string.IsNullOrEmpty(h.npc_nombre)) return h.npc_nombre;
            }
            return "Desconocido";
        }

        /// <summary>Quita prefijos "[SISTEMA]" y "[ZONA COMPLETADA]" del texto.</summary>
        private static string LimpiarTextoSistema(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            raw = raw.Replace("[SISTEMA] ", "").Replace("[ZONA COMPLETADA] ", "");
            return raw;
        }
    }
}
