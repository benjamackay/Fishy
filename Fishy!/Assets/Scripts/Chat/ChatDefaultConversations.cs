using System.Collections.Generic;
using UnityEngine;

namespace Fishy.Chat
{
    /// <summary>
    /// Conversaciones de ejemplo para el módulo de chat (HDU-8), listas para usar
    /// sin crear assets. Cada una incluye un mensaje neutro y uno con señal de
    /// riesgo, con 2-3 opciones que ramifican (aceptar / rechazar / bloquear).
    /// </summary>
    public static class ChatDefaultConversations
    {
        public enum Plantilla { ParqueDespuesDelColegio, FotosYSecreto }

        public static ChatConversation Create(Plantilla plantilla)
        {
            return plantilla == Plantilla.FotosYSecreto ? CreateFotos() : CreateParque();
        }

        /// <summary>
        /// Lista de conversaciones para la Zona Desconocidos.
        /// Usa el banco de preguntas oficial (banco_preguntas.json) si está disponible;
        /// de lo contrario cae en los ejemplos hardcodeados.
        /// </summary>
        public static List<ChatConversation> CreateZonaDesconocidos()
        {
            try
            {
                var banco = BancoPreguntasLoader.CreateHDU2Conversations();
                if (banco != null && banco.Count > 0)
                {
                    Debug.Log($"[ChatDefault] Usando banco de preguntas ({banco.Count} NPCs).");
                    return banco;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ChatDefault] Fallo al cargar banco: {e.Message}. Usando fallback.");
            }
            return new List<ChatConversation> { CreateParque(), CreateFotos() };
        }

        /// <summary>Conversaciones HDU-8 del banco (Grooming · Ciberacoso · Reto Viral).</summary>
        public static List<ChatConversation> CreateZonaChatSimulado()
        {
            try
            {
                var banco = BancoPreguntasLoader.CreateHDU8Conversations();
                if (banco != null && banco.Count > 0) return banco;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ChatDefault] Fallo al cargar HDU-8: {e.Message}.");
            }
            return new List<ChatConversation> { CreateParque(), CreateFotos() };
        }

        // Ejemplo del criterio: "Me gusta tu mochila" (neutro) + "¿Nos vemos en el parque?" (riesgo).
        public static ChatConversation CreateParque()
        {
            var c = ScriptableObject.CreateInstance<ChatConversation>();
            c.zoneId = "zona_desconocidos";
            c.contactName = "NuevoAmigo";
            c.categoriaRiesgo = "grooming";
            c.startNodeId = "n0";
            c.nodes = new List<ChatNode>
            {
                Msg("n0", ChatMessageKind.Neutral, "¡Hola! Me gusta tu mochila 🎒", next: "n1"),

                Risk("n1", "¿Podemos encontrarnos en el parque después del colegio?",
                    Opt("Aceptar e ir al parque", OptionSafety.Unsafe, "n_acepta"),
                    Opt("Decir que no, no me siento cómodo/a", OptionSafety.Safe, "n_rechaza"),
                    Opt("Bloquear al contacto", OptionSafety.Safe, "n_bloquea")),

                // Rama ACEPTAR: el NPC propone encuentro y pide más.
                Risk("n_acepta", "¡Genial! No le digas a nadie, es nuestro secreto 🤫. ¿Me das tu dirección?",
                    Opt("Darle mi dirección", OptionSafety.Unsafe, "n_capturado"),
                    Opt("Mejor no, le cuento a un adulto", OptionSafety.Safe, "n_adulto")),

                // Rama RECHAZAR: el NPC insiste.
                Risk("n_rechaza", "Dale, no seas así, solo será un ratito 😅",
                    Opt("Bloquear al contacto", OptionSafety.Safe, "n_bloquea"),
                    Opt("Igual le cuento a un adulto", OptionSafety.Safe, "n_adulto"),
                    Opt("Bueno, está bien", OptionSafety.Unsafe, "n_acepta")),

                // Cierres.
                System("n_bloquea", "Bloqueaste el contacto. El chat se cerró. 🛡️ ¡Buena decisión!"),
                System("n_adulto", "Le contaste a un adulto de confianza. 👨‍👩‍👧 ¡Eso te mantiene seguro/a!"),
                System("n_capturado",
                    "Compartiste datos privados con un desconocido. 🛑 Recuerda: nunca des tu dirección " +
                    "ni guardes secretos así. Cuéntaselo a un adulto de confianza."),
            };
            return c;
        }

        // Segunda conversación: pedir fotos + guardar secreto.
        public static ChatConversation CreateFotos()
        {
            var c = ScriptableObject.CreateInstance<ChatConversation>();
            c.zoneId = "zona_desconocidos";
            c.contactName = "Jugador_X";
            c.categoriaRiesgo = "grooming";
            c.startNodeId = "f0";
            c.nodes = new List<ChatNode>
            {
                Msg("f0", ChatMessageKind.Neutral, "¡Buena partida la de hoy! 🎮", next: "f1"),

                Risk("f1", "¿Me mandas una foto tuya? Y no le digas a nadie 🤳",
                    Opt("Mandar la foto", OptionSafety.Unsafe, "f_insiste"),
                    Opt("Decir que no", OptionSafety.Safe, "f_adulto"),
                    Opt("Bloquear al contacto", OptionSafety.Safe, "f_bloquea")),

                Risk("f_insiste", "Solo una, será nuestro secreto 🤫. ¿Dale?",
                    Opt("Bueno, se la mando", OptionSafety.Unsafe, "f_capturado"),
                    Opt("No. Bloquear al contacto", OptionSafety.Safe, "f_bloquea")),

                System("f_bloquea", "Bloqueaste el contacto. 🛡️ Nadie debería pedirte fotos ni secretos."),
                System("f_adulto", "Dijiste que no. 👍 Si alguien insiste, cuéntaselo a un adulto de confianza."),
                System("f_capturado",
                    "Enviaste una foto a un desconocido. 🛑 Las fotos pueden compartirse sin tu permiso. " +
                    "Cuéntale a un adulto de confianza lo que pasó."),
            };
            return c;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static ChatNode Msg(string id, ChatMessageKind kind, string text, string next)
            => new ChatNode { id = id, kind = kind, text = text, nextNodeId = next };

        private static ChatNode Risk(string id, string text, params ChatOption[] options)
            => new ChatNode { id = id, kind = ChatMessageKind.Risk, text = text, options = new List<ChatOption>(options) };

        private static ChatNode System(string id, string text)
            => new ChatNode { id = id, kind = ChatMessageKind.Neutral, text = text, isSystem = true, closesChat = true };

        private static ChatOption Opt(string text, OptionSafety safety, string next)
            => new ChatOption(text, safety, next);
    }
}
