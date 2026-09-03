using System;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Mision;

namespace Fishy.Chat
{
    /// <summary>
    /// HDU-8 — Abre el módulo de chat de una zona.
    ///
    /// Úsalo de dos formas:
    ///  • Desde un Button de UI: enlaza <see cref="OpenChat"/> en su OnClick.
    ///  • Al acercarse: marca <see cref="openOnTriggerEnter"/> y pon un Collider2D
    ///    (Is Trigger); cuando Otto (tag Player) entra, se abre el chat.
    /// </summary>
    public class ChatModuleLauncher : MonoBehaviour
    {
        public enum Source { ZonaDesconocidosPorDefecto, ConversacionesAsignadas, BancoPorNpcId }

        [Header("Contenido")]
        public Source source = Source.ZonaDesconocidosPorDefecto;
        [Tooltip("Conversaciones a usar si source = ConversacionesAsignadas.")]
        public List<ChatConversation> conversaciones = new List<ChatConversation>();
        [Tooltip("npc_id del banco (banco_preguntas.json, HDU-2) a usar si source = BancoPorNpcId. Ej: \"NPC_01\".")]
        public string npcId = "";

        [Header("Otto")]
        [Tooltip("Controlador de estado emocional de Otto. Si está vacío se busca en la escena.")]
        public OttoMoodController ottoMood;

        [Header("Backend")]
        [Tooltip("Registrar la sesión en el backend (requiere login + partida).")]
        public bool reportToBackend = false;

        [Header("Misión")]
        [Tooltip("Desafío del panel de misión activa que esta conversación desbloquea/completa. Opcional.")]
        public DesafioData desafioAsociado;

        [Header("Apertura por cercanía (opcional)")]
        public bool openOnTriggerEnter = false;
        public string ottoTag = "Player";
        [Tooltip("Abrir una sola vez al acercarse.")]
        public bool openOnce = true;

        private bool alreadyOpened;

        /// <summary>
        /// Se dispara cuando ESTA sesión (la que abrió este launcher) se cierra,
        /// con el % de respuestas seguras acumulado. A diferencia de
        /// <see cref="ChatModuleController.OnSesionCerrada"/> (global, cualquier
        /// launcher), este evento sólo corresponde a la sesión que abrió este
        /// GameObject en particular.
        /// </summary>
        public event Action<float> OnSesionFinalizada;

        /// <summary>Abre el módulo de chat (enlazable a un Button.OnClick).</summary>
        public void OpenChat()
        {
            if (ChatModuleController.Instance != null && ChatModuleController.Instance.IsActive) return;

            List<ChatConversation> convos;
            if (source == Source.ConversacionesAsignadas && conversaciones.Count > 0)
                convos = conversaciones;
            else if (source == Source.BancoPorNpcId && !string.IsNullOrEmpty(npcId))
                convos = BancoPreguntasLoader.CreateHDU2ConversationForNpc(npcId);
            else
                convos = ChatDefaultConversations.CreateZonaDesconocidos();

            var controller = ChatModuleController.GetOrCreate();
            controller.OnSesionCerrada += HandleSesionCerrada;
            controller.OpenSession(convos, ottoMood, reportToBackend, desafioAsociado);
            alreadyOpened = true;
        }

        private void HandleSesionCerrada(float safePercent)
        {
            if (ChatModuleController.Instance != null)
                ChatModuleController.Instance.OnSesionCerrada -= HandleSesionCerrada;
            OnSesionFinalizada?.Invoke(safePercent);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!openOnTriggerEnter) return;
            if (openOnce && alreadyOpened) return;
            if (!other.CompareTag(ottoTag)) return;
            OpenChat();
        }
    }
}
