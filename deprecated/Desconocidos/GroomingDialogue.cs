using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;

namespace Fishy.Desconocidos
{
    /// <summary>Calidad de una respuesta del niño/a (se mapea a la del backend).</summary>
    public enum ResponseQuality { Buena, Neutral, Mala }

    /// <summary>
    /// Tipo de nodo de diálogo. Permite distinguir las fases del guion de
    /// manipulación progresiva (HDU-2).
    /// </summary>
    public enum DialogueNodeKind
    {
        /// <summary>Tema neutro (sirve para intercalar = "discreto").</summary>
        Neutral,
        /// <summary>Halago / el NPC comparte info falsa para "ganar confianza".</summary>
        Flattery,
        /// <summary>Pide datos personales (nombre, edad, colegio, dirección, rutina).</summary>
        RequestData,
        /// <summary>Propone secreto o encuentro a solas (escalada fuerte).</summary>
        SecretMeeting,
        /// <summary>Fin: el niño/a NO compartió datos. El NPC se aleja y se marca éxito.</summary>
        EndSuccess,
        /// <summary>Fin: el niño/a compartió hasta el final (cierre con mensaje educativo).</summary>
        EndCaptured
    }

    /// <summary>Una opción de respuesta que el niño/a puede elegir.</summary>
    [System.Serializable]
    public class DialogueChoice
    {
        [TextArea] public string text;
        public ResponseQuality quality = ResponseQuality.Neutral;
        [Tooltip("True si esta opción comparte datos personales del niño/a.")]
        public bool sharesPersonalData;
        [Tooltip("Id del nodo al que salta tras elegir esta opción.")]
        public string nextNodeId;

        public DialogueChoice() { }
        public DialogueChoice(string text, ResponseQuality quality, bool sharesPersonalData, string nextNodeId)
        {
            this.text = text;
            this.quality = quality;
            this.sharesPersonalData = sharesPersonalData;
            this.nextNodeId = nextNodeId;
        }

        public string QualityKey => GroomingDialogue.QualityToKey(quality);
    }

    /// <summary>Un nodo (línea del NPC + opciones del niño/a).</summary>
    [System.Serializable]
    public class DialogueNode
    {
        public string id;
        [TextArea] public string npcLine;
        public DialogueNodeKind kind = DialogueNodeKind.Neutral;
        [Tooltip("Sólo informativo: el NPC comparte info personal FALSA en esta línea (gana confianza).")]
        public bool npcSharesFakeInfo;
        public List<DialogueChoice> choices = new List<DialogueChoice>();

        public bool HasChoices => choices != null && choices.Count > 0;
        public bool IsEnd => kind == DialogueNodeKind.EndSuccess || kind == DialogueNodeKind.EndCaptured;

        /// <summary>Convierte las opciones a la estructura que espera el backend.</summary>
        public List<OpcionRespuesta> ToOpciones()
        {
            var list = new List<OpcionRespuesta>();
            if (choices == null) return list;
            for (int i = 0; i < choices.Count; i++)
                list.Add(new OpcionRespuesta(choices[i].text, i, choices[i].QualityKey));
            return list;
        }
    }

    /// <summary>
    /// HDU-2 — Guion de manipulación/grooming de un NPC de la temática "Desconocidos".
    ///
    /// Define la conversación como un grafo de nodos con fases que escalan según las
    /// decisiones del niño/a. Puede autorizarse como asset (Create → Fishy →
    /// Diálogo Desconocidos) o generarse por código (ver DesconocidosDefaultScripts).
    /// </summary>
    [CreateAssetMenu(menuName = "Fishy/Diálogo Desconocidos", fileName = "GroomingDialogue")]
    public class GroomingDialogue : ScriptableObject
    {
        [Header("NPC")]
        public string npcName = "Desconocido";
        public string area = "Bosque de los Desconocidos";
        [Tooltip("Categoría de riesgo para el backend (HDU-2).")]
        public string categoriaRiesgo = "grooming";

        [Header("Grafo de diálogo")]
        public string startNodeId = "inicio";
        public List<DialogueNode> nodes = new List<DialogueNode>();

        [Header("Cierre educativo (ruta de captura)")]
        [TextArea]
        public string debriefCaptura =
            "🛑 Recuerda: un desconocido NO debería pedirte tu nombre, edad, colegio o " +
            "dirección, ni proponerte guardar secretos. Si te pasa, cuéntaselo a un " +
            "adulto de confianza.";

        private Dictionary<string, DialogueNode> _index;

        public DialogueNode GetNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_index == null || _index.Count != nodes.Count)
            {
                _index = new Dictionary<string, DialogueNode>();
                foreach (var n in nodes)
                    if (n != null && !string.IsNullOrEmpty(n.id))
                        _index[n.id] = n;
            }
            return _index.TryGetValue(id, out var node) ? node : null;
        }

        public DialogueNode StartNode => GetNode(startNodeId);

        public static string QualityToKey(ResponseQuality q)
        {
            switch (q)
            {
                case ResponseQuality.Buena: return "buena";
                case ResponseQuality.Mala: return "mala";
                default: return "neutral";
            }
        }
    }
}
