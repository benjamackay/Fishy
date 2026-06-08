using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;

namespace Fishy.Chat
{
    /// <summary>Tipo de mensaje del NPC (informativo / para validación).</summary>
    public enum ChatMessageKind
    {
        /// <summary>No pide datos, no presiona, no implica encuentros fuera del juego.</summary>
        Neutral,
        /// <summary>Señal de riesgo implícita: pide datos, propone encuentro o presiona.</summary>
        Risk
    }

    /// <summary>
    /// Clasificación de seguridad de una opción de respuesta (HDU-8).
    ///  • Safe   = "Decir que no", "Bloquear", "Consultar a un adulto".
    ///  • Unsafe = "Dar datos", "Aceptar encuentro", "Guardar secreto".
    ///  • Neutral= respuesta inocua (no cuenta para el porcentaje).
    /// </summary>
    public enum OptionSafety { Neutral, Safe, Unsafe }

    /// <summary>Una opción de respuesta del niño/a (1 línea, lenguaje claro).</summary>
    [System.Serializable]
    public class ChatOption
    {
        [TextArea] public string text;
        public OptionSafety safety = OptionSafety.Neutral;
        [Tooltip("Nodo al que continúa la narrativa tras elegir esta opción.")]
        public string nextNodeId;

        public ChatOption() { }
        public ChatOption(string text, OptionSafety safety, string nextNodeId)
        {
            this.text = text;
            this.safety = safety;
            this.nextNodeId = nextNodeId;
        }

        /// <summary>True si cuenta para el porcentaje de seguras (Safe o Unsafe).</summary>
        public bool CountsForScore => safety == OptionSafety.Safe || safety == OptionSafety.Unsafe;

        /// <summary>Mapea la seguridad a la calidad del backend.</summary>
        public string QualityKey =>
            safety == OptionSafety.Safe ? "buena" :
            safety == OptionSafety.Unsafe ? "mala" : "neutral";
    }

    /// <summary>Un mensaje del chat y, opcionalmente, las opciones de respuesta.</summary>
    [System.Serializable]
    public class ChatNode
    {
        public string id;
        [TextArea] public string text;
        public ChatMessageKind kind = ChatMessageKind.Neutral;

        [Tooltip("Mensaje del sistema (cierre/seguridad), se muestra con otro estilo.")]
        public bool isSystem;
        [Tooltip("Si es true, este nodo finaliza la conversación.")]
        public bool closesChat;

        [Tooltip("Si no hay opciones, salta automáticamente a este nodo (para encadenar mensajes).")]
        public string nextNodeId;

        public List<ChatOption> options = new List<ChatOption>();

        public bool HasOptions => options != null && options.Count > 0;

        /// <summary>Convierte las opciones a la estructura que espera el backend.</summary>
        public List<OpcionRespuesta> ToOpciones()
        {
            var list = new List<OpcionRespuesta>();
            if (options == null) return list;
            for (int i = 0; i < options.Count; i++)
                list.Add(new OpcionRespuesta(options[i].text, i, options[i].QualityKey));
            return list;
        }
    }

    /// <summary>
    /// HDU-8 — Conversación de chat de una zona.
    ///
    /// Cada conversación contiene al menos un mensaje neutro y uno con señal de
    /// riesgo, y presenta 2-3 opciones por mensaje de riesgo, cada una hacia una
    /// rama distinta. No se etiqueta cuál mensaje es peligroso.
    /// </summary>
    [CreateAssetMenu(menuName = "Fishy/Conversación Chat", fileName = "ChatConversation")]
    public class ChatConversation : ScriptableObject
    {
        [Header("Identidad")]
        public string zoneId = "zona_desconocidos";
        public string contactName = "Desconocido";
        [Tooltip("Categoría de riesgo para el backend.")]
        public string categoriaRiesgo = "grooming";

        [Header("Grafo")]
        public string startNodeId = "inicio";
        public List<ChatNode> nodes = new List<ChatNode>();

        private Dictionary<string, ChatNode> _index;

        public ChatNode GetNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_index == null || _index.Count != nodes.Count)
            {
                _index = new Dictionary<string, ChatNode>();
                foreach (var n in nodes)
                    if (n != null && !string.IsNullOrEmpty(n.id))
                        _index[n.id] = n;
            }
            return _index.TryGetValue(id, out var node) ? node : null;
        }

        public ChatNode StartNode => GetNode(startNodeId);
    }
}
