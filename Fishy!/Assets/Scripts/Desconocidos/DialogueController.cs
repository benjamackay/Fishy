using System.Collections.Generic;
using UnityEngine;
using Fishy.World;
using Fishy.Net;

namespace Fishy.Desconocidos
{
    /// <summary>
    /// HDU-2 — Orquesta una conversación de grooming: recorre el grafo de nodos,
    /// presenta opciones, aplica los criterios de aceptación y registra (best-effort)
    /// en el backend.
    ///
    /// Reglas:
    ///  • Línea inicial = el NPC inicia la conversación para ganar confianza y
    ///    empieza, discretamente, a pedir datos.
    ///  • Si el niño/a elige NO compartir → se llega a un nodo EndSuccess: el NPC se
    ///    aleja y se marca el éxito.
    ///  • Si comparte → el NPC sigue amistoso y pide datos más específicos.
    ///  • Ruta de captura (EndCaptured) → se muestra un cierre educativo.
    /// </summary>
    public class DialogueController : MonoBehaviour
    {
        public static DialogueController Instance { get; private set; }

        public bool IsActive { get; private set; }

        private DialogueUI ui;
        private DesconocidosNPC currentNpc;
        private GroomingDialogue dialogue;
        private OttoController otto;
        private GroomingChatLogger logger;
        private bool firstLine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static DialogueController GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("DialogueController");
                Instance = go.AddComponent<DialogueController>();
            }
            return Instance;
        }

        public void StartConversation(DesconocidosNPC npc, GroomingDialogue dlg, OttoController ottoController)
        {
            if (IsActive || dlg == null) return;

            IsActive = true;
            currentNpc = npc;
            dialogue = dlg;
            otto = ottoController;

            // El movimiento se bloquea mientras dura el diálogo (modal).
            if (otto != null) otto.DisableMovement();

            ui = DialogueUI.GetOrCreate();
            logger = new GroomingChatLogger();
            // Se registra en el backend si el NPC lo pide explícitamente o si hay
            // una sesión activa (login + partida). Así las respuestas se guardan
            // automáticamente sin tener que marcar nada en el inspector.
            bool report = (npc != null && npc.reportToBackend) || AutoReportEnabled();
            if (report)
                logger.Begin(dlg.npcName, dlg.area, dlg.categoriaRiesgo);

            firstLine = true;
            EnterNode(dlg.startNodeId);
        }

        /// <summary>
        /// True si hay un ApiManager con sesión y partida activa: en ese caso
        /// guardamos las respuestas en el backend automáticamente.
        /// </summary>
        private static bool AutoReportEnabled()
        {
            var api = ApiManager.Instance;
            return api != null && api.IsLoggedIn && api.PartidaId.HasValue;
        }

        private void EnterNode(string nodeId)
        {
            var node = dialogue.GetNode(nodeId);
            if (node == null)
            {
                Debug.LogError($"[DialogueController] Nodo '{nodeId}' no existe en '{dialogue.npcName}'.");
                EndConversation(false);
                return;
            }

            ui.SetLine(dialogue.npcName, node.npcLine);

            // Registro en backend según el tipo de línea.
            if (firstLine)
            {
                logger.LogStart(node.npcLine);
                firstLine = false;
            }
            else if (node.HasChoices)
            {
                logger.LogRequest(node.npcLine, node.ToOpciones());
            }

            switch (node.kind)
            {
                case DialogueNodeKind.EndSuccess:
                    // Resultado estructurado en BD: el niño/a se mantuvo a salvo.
                    logger.LogOutcome(true);
                    logger.LogEnd(node.npcLine);
                    ui.SetContinue("Cerrar", () => EndConversation(true));
                    return;

                case DialogueNodeKind.EndCaptured:
                    // Resultado estructurado en BD: el niño/a fue manipulado (captura).
                    logger.LogOutcome(false);
                    // Cierre educativo antes de terminar.
                    ui.SetContinue("Continuar", () =>
                    {
                        ui.SetLine("Sistema", dialogue.debriefCaptura);
                        logger.LogEnd(node.npcLine);
                        ui.SetContinue("Cerrar", () => EndConversation(false));
                    });
                    return;
            }

            // Nodo normal con opciones.
            var texts = new List<string>(node.choices.Count);
            foreach (var c in node.choices) texts.Add(c.text);
            ui.SetChoices(texts, idx => OnChoice(node, idx));
        }

        private void OnChoice(DialogueNode node, int index)
        {
            if (index < 0 || index >= node.choices.Count) return;
            var choice = node.choices[index];
            logger.LogChoice(choice.text, choice.QualityKey);
            EnterNode(choice.nextNodeId);
        }

        private void EndConversation(bool refusedSuccessfully)
        {
            if (ui != null) ui.Hide();
            if (otto != null) otto.EnableMovement();

            IsActive = false;
            var npc = currentNpc;
            currentNpc = null;
            dialogue = null;
            logger = null;

            if (npc != null) npc.OnConversationFinished(refusedSuccessfully);
        }
    }
}
