using System.Collections.Generic;
using UnityEngine;

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
        public enum Source { ZonaDesconocidosPorDefecto, ConversacionesAsignadas }

        [Header("Contenido")]
        public Source source = Source.ZonaDesconocidosPorDefecto;
        [Tooltip("Conversaciones a usar si source = ConversacionesAsignadas.")]
        public List<ChatConversation> conversaciones = new List<ChatConversation>();

        [Header("Otto")]
        [Tooltip("Controlador de estado emocional de Otto. Si está vacío se busca en la escena.")]
        public OttoMoodController ottoMood;

        [Header("Backend")]
        [Tooltip("Registrar la sesión en el backend (requiere login + partida).")]
        public bool reportToBackend = false;

        [Header("Apertura por cercanía (opcional)")]
        public bool openOnTriggerEnter = false;
        public string ottoTag = "Player";
        [Tooltip("Abrir una sola vez al acercarse.")]
        public bool openOnce = true;

        private bool alreadyOpened;

        /// <summary>Abre el módulo de chat (enlazable a un Button.OnClick).</summary>
        public void OpenChat()
        {
            if (ChatModuleController.Instance != null && ChatModuleController.Instance.IsActive) return;

            var convos = source == Source.ConversacionesAsignadas && conversaciones.Count > 0
                ? conversaciones
                : ChatDefaultConversations.CreateZonaDesconocidos();

            ChatModuleController.GetOrCreate().OpenSession(convos, ottoMood, reportToBackend);
            alreadyOpened = true;
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
