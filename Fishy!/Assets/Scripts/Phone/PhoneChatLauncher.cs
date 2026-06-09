using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Fishy.Chat;
using Fishy.World;

namespace Fishy.Phone
{
    /// <summary>
    /// Zona de riesgo con celular diegético (HDU-2 / HDU-8 combinados).
    ///
    /// Flujo cuando Otto entra al trigger:
    ///  1. El celular de Otto vibra y su pantalla se enciende.
    ///  2. La cámara hace zoom hacia el celular.
    ///  3. Fade a negro → aparece la UI del chat encuadrada como pantalla de celular.
    ///  4. El jugador interactúa con el chat (grooming / prevención).
    ///  5. Al cerrar: fade → zoom de vuelta → Otto puede moverse de nuevo.
    ///
    /// Necesita un Collider2D marcado como "Is Trigger" en el mismo objeto.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PhoneChatLauncher : MonoBehaviour
    {
        public enum Source
        {
            /// <summary>NPC_01 (Alex) + NPC_02 (Valen) del banco de preguntas oficial.</summary>
            ZonaDesconocidos,
            /// <summary>Escenarios HDU-8 del banco (Grooming · Ciberacoso · Reto Viral).</summary>
            ZonaChatSimulado,
            /// <summary>Conversaciones asignadas manualmente en el Inspector.</summary>
            ConversacionesAsignadas,
            /// <summary>Un único NPC de HDU-2 identificado por <see cref="PhoneChatLauncher.npcId"/>.</summary>
            SoloNpc,
            /// <summary>Alias legacy — equivale a ZonaDesconocidos.</summary>
            ZonaDesconocidosPorDefecto = ZonaDesconocidos,
        }

        // ── Contenido ──────────────────────────────────────────────────────────
        [Header("Contenido del chat")]
        public Source source = Source.ZonaDesconocidos;
        [Tooltip("Conversaciones a usar cuando source = ConversacionesAsignadas.")]
        public List<ChatConversation> conversaciones = new List<ChatConversation>();
        [Tooltip("ID del NPC cuando source = SoloNpc. Ej: 'NPC_01' (Alex) o 'NPC_02' (Valen).")]
        public string npcId = "NPC_01";

        // ── Referencias opcionales ─────────────────────────────────────────────
        [Header("Referencias (se buscan si quedan vacías)")]
        [Tooltip("OttoController: se busca si no se asigna.")]
        public OttoController otto;
        [Tooltip("OttoPhone (hijo de Otto): se crea automáticamente si no existe.")]
        public OttoPhone phone;
        [Tooltip("Controlador de estado emocional de Otto.")]
        public OttoMoodController ottoMood;

        // ── Configuración ──────────────────────────────────────────────────────
        [Header("Comportamiento")]
        [Tooltip("Si está activo solo se dispara una vez.")]
        public bool openOnce    = true;
        public string ottoTag   = "Player";
        [Tooltip("Registrar la sesión en el backend (requiere ApiManager con sesión activa).")]
        public bool reportToBackend = false;

        [Header("Notificación antes del zoom")]
        [Tooltip("Texto que aparece como notificación sobre el celular.")]
        [TextArea(1, 2)]
        public string notificationText = "📱 Tienes un mensaje nuevo…";
        [Tooltip("Segundos que se ve la notificación antes de hacer zoom.")]
        [Range(0.5f, 4f)] public float previewDuration = 1.5f;

        [Header("Eventos")]
        public UnityEvent onChatOpened;
        public UnityEvent onChatClosed;

        // ── Estado interno ─────────────────────────────────────────────────────
        private bool               _triggered;
        private PhoneZoomController _zoom;
        private bool               _sequenceRunning;

        // ── Unity ──────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Asegurarse de que el collider es trigger.
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void Start()
        {
            // Obtener / crear el PhoneZoomController.
            _zoom = PhoneZoomController.Instance;
            if (_zoom == null)
            {
                var go = new GameObject("PhoneZoomController");
                _zoom = go.AddComponent<PhoneZoomController>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(ottoTag)) return;
            if (openOnce && _triggered) return;
            if (_sequenceRunning) return;

            // Resolver Otto desde el collider si no está asignado.
            if (otto == null) otto = other.GetComponent<OttoController>();

            _triggered = true;
            StartCoroutine(PhoneSequence());
        }

        // ── Secuencia diegética ────────────────────────────────────────────────
        private IEnumerator PhoneSequence()
        {
            _sequenceRunning = true;

            // 1. Encontrar / crear el OttoPhone.
            if (phone == null) phone = OttoPhone.FindOrCreateOnOtto(otto);

            // 2. Detener movimiento de Otto.
            if (otto != null) otto.DisableMovement();

            // 3. Mostrar notificación (popup flotante).
            if (!string.IsNullOrEmpty(notificationText))
                ZonePopupUI.Show(notificationText);

            // 4. Vibrar celular.
            bool vibraDone = false;
            if (phone != null) phone.Vibrate(() => vibraDone = true);
            else vibraDone = true;
            yield return new WaitUntil(() => vibraDone);

            // 5. Esperar un momento para que el jugador vea la notificación.
            yield return new WaitForSeconds(previewDuration);

            // 6. Zoom al celular + fade a negro.
            bool zoomDone = false;
            _zoom.ZoomToPhone(phone, () => zoomDone = true);
            yield return new WaitUntil(() => zoomDone);

            // 7. Abrir chat EN MODO TELÉFONO.
            onChatOpened?.Invoke();

            var convos = BuildConversationList();
            var controller = ChatModuleController.GetOrCreate();
            var ui         = ChatModuleUI.GetOrCreate();

            ui.EnablePhoneMode(true);
            controller.OpenSession(convos, ottoMood, reportToBackend);

            // 8. Esperar a que el chat termine.
            yield return new WaitUntil(() => !controller.IsActive);

            // 9. Apagar pantalla del celular.
            if (phone != null) phone.TurnScreenOff();

            // 10. Zoom de vuelta al mundo.
            bool zoomOutDone = false;
            _zoom.ZoomOut(() => zoomOutDone = true);
            yield return new WaitUntil(() => zoomOutDone);

            // 11. Restaurar movimiento.
            if (otto != null) otto.EnableMovement();

            // 12. Limpiar modo teléfono para usos futuros (sesiones normales de chat).
            ui.EnablePhoneMode(false);

            onChatClosed?.Invoke();
            _sequenceRunning = false;
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private IList<ChatConversation> BuildConversationList()
        {
            switch (source)
            {
                case Source.ConversacionesAsignadas when conversaciones != null && conversaciones.Count > 0:
                    return conversaciones;

                case Source.SoloNpc:
                    return BancoPreguntasLoader.CreateHDU2ConversationForNpc(npcId);

                case Source.ZonaChatSimulado:
                    return ChatDefaultConversations.CreateZonaChatSimulado();

                default: // ZonaDesconocidos / ZonaDesconocidosPorDefecto
                    return ChatDefaultConversations.CreateZonaDesconocidos();
            }
        }

        // ── Apertura manual (botón de UI) ──────────────────────────────────────
        /// <summary>Abre la secuencia telefónica desde un botón (sin trigger físico).</summary>
        public void OpenManual()
        {
            if (_sequenceRunning) return;
            if (otto == null) otto = FindFirstObjectByType<OttoController>();
            _triggered = true;
            StartCoroutine(PhoneSequence());
        }
    }
}
