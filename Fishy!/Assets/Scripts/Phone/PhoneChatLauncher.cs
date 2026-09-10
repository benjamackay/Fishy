using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        // ── Contenido ──────────────────────────────────────────────────────────
        [Header("Contenido del chat")]
        [Tooltip("escenario_id del banco, separadas por coma si son varias fases de una " +
                 "misma historia (se reproducen en ese orden). Ej: 'M4_FASE01,M4_FASE02'.\n\n" +
                 "Es la única forma de elegir contenido: identifica la conversación en sí, " +
                 "sin ambigüedad. Antes había un enum 'source' con cinco modos, pero las 22 " +
                 "instancias del juego usaban este, y los otros cuatro solo servían para " +
                 "elegir mal por npc_id (que se repite entre conversaciones distintas).\n\n" +
                 "El contenido sale del banco: de la base de datos si hay sesión, y si no de " +
                 "la copia de Resources.")]
        public string escenarioIds = "";

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
        [Tooltip("Si está activo, la secuencia diegética del celular (vibración + " +
                 "notificación + zoom). Si no, el chat se abre directo frente al NPC " +
                 "visible, sin celular ni zoom — útil para NPCs con sprite en el mapa.")]
        public bool modoTelefono = true;
        [Tooltip("Le quita el control a Otto mientras dura la conversación y se lo " +
                 "devuelve al cerrarse. Misma casilla y mismo significado que en el " +
                 "NPC neutro. Desmarcarlo solo tiene sentido sin el celular: con el " +
                 "zoom puesto, Otto caminaría fuera de plano.")]
        public bool bloquearMovimiento = true;
        [Tooltip("Permite volver a hablar con este NPC tantas veces como se quiera. " +
                 "Hay que alejarse y volver a acercarse: la conversación no se " +
                 "reabre sola al cerrarla, o el niño/a quedaría atrapado en ella.")]
        public bool repetible   = false;
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
        public UnityEvent onChatOpened = new UnityEvent();
        [Tooltip("Se dispara al cerrarse el chat. MissionTracker se engancha aquí para " +
                 "los objetivos de misión de tipo 'Chatear Por Telefono', igual que se " +
                 "engancha al onDialogueEnded de un NPC.")]
        public UnityEvent onChatClosed = new UnityEvent();

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
            if (!repetible && _triggered) return;
            if (_sequenceRunning) return;
            if (!HayContenido()) return;

            // Resolver Otto desde el collider si no está asignado.
            if (otto == null) otto = other.GetComponent<OttoController>();

            _triggered = true;
            StartCoroutine(PhoneSequence());
        }

        // ── Secuencia diegética ────────────────────────────────────────────────
        private IEnumerator PhoneSequence()
        {
            _sequenceRunning = true;

            if (modoTelefono)
                yield return PhoneSequenceConCelular();
            else
                yield return PhoneSequenceDirecta();

            onChatClosed?.Invoke();
            _sequenceRunning = false;
        }

        /// <summary>Secuencia diegética completa: celular vibra, notificación, zoom.</summary>
        private IEnumerator PhoneSequenceConCelular()
        {
            // 1. Encontrar / crear el OttoPhone.
            if (phone == null) phone = OttoPhone.FindOrCreateOnOtto(otto);

            // 2. Detener movimiento de Otto.
            if (bloquearMovimiento && otto != null) otto.DisableMovement();

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
            if (bloquearMovimiento && otto != null) otto.EnableMovement();

            // 12. Limpiar modo teléfono para usos futuros (sesiones normales de chat).
            ui.EnablePhoneMode(false);
        }

        /// <summary>Sin celular ni zoom: el chat se abre directo frente al NPC visible.</summary>
        private IEnumerator PhoneSequenceDirecta()
        {
            if (bloquearMovimiento && otto != null) otto.DisableMovement();

            onChatOpened?.Invoke();

            var convos = BuildConversationList();
            var controller = ChatModuleController.GetOrCreate();
            controller.OpenSession(convos, ottoMood, reportToBackend);

            yield return new WaitUntil(() => !controller.IsActive);

            if (bloquearMovimiento && otto != null) otto.EnableMovement();
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private IList<ChatConversation> BuildConversationList()
        {
            return BancoPreguntasLoader.CreateConversationsForEscenarioIds(EscenariosPedidos());
        }

        /// <summary>
        /// Comprueba que haya algo que reproducir ANTES de arrancar la secuencia.
        ///
        /// Se mira aquí y no dentro de la corrutina porque para cuando esta llega a
        /// construir las conversaciones ya le quitó el control a Otto, encendió el
        /// celular y fundió a negro: abortar ahí dejaría al niño/a mirando una pantalla
        /// vacía sin poder moverse.
        /// </summary>
        private bool HayContenido()
        {
            if (EscenariosPedidos().Count > 0) return true;

            Debug.LogWarning($"[{name}] No tiene 'Escenario Ids', así que no hay " +
                             "conversación que abrir. Pon al menos un escenario_id del banco.", this);
            return false;
        }

        /// <summary>Los escenario_id de <see cref="escenarioIds"/>, ya separados y limpios.</summary>
        private List<string> EscenariosPedidos()
        {
            if (string.IsNullOrWhiteSpace(escenarioIds)) return new List<string>();

            return escenarioIds.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        // ── Apertura manual (botón de UI) ──────────────────────────────────────
        /// <summary>Abre la secuencia telefónica desde un botón (sin trigger físico).</summary>
        public void OpenManual()
        {
            if (_sequenceRunning) return;
            // Mismo criterio que el trigger: sin "repetible", una sola vez.
            if (!repetible && _triggered) return;
            if (!HayContenido()) return;
            if (otto == null) otto = FindAnyObjectByType<OttoController>();
            _triggered = true;
            StartCoroutine(PhoneSequence());
        }
    }
}
