using System.Collections;
using UnityEngine;
using Fishy.World;

namespace Fishy.Desconocidos
{
    /// <summary>
    /// HDU-2 — NPC de la temática "Desconocidos" (Bosque de los Desconocidos).
    ///
    /// Coloca este componente en el GameObject del NPC. Necesita un Collider2D
    /// marcado como <b>Is Trigger</b> que define el radio de cercanía: cuando Otto
    /// (tag Player) entra, el NPC inicia la conversación de manipulación.
    ///
    /// Resultado:
    ///  • Si el niño/a NO comparte datos → el NPC "se aleja" y se marca éxito.
    ///  • Si comparte hasta el final → se muestra el cierre educativo.
    /// En ambos casos la interacción cuenta como finalizada para completar la
    /// temática (ver <see cref="ZonaDesconocidosManager"/>).
    /// </summary>
    public class DesconocidosNPC : MonoBehaviour
    {
        public enum ScriptSource { Alex, Sam, Custom }

        [Header("Guion")]
        [Tooltip("Usa un guion incluido (Alex/Sam) o uno propio (Custom + customDialogue).")]
        public ScriptSource scriptSource = ScriptSource.Alex;
        [Tooltip("Sólo si scriptSource = Custom: asset GroomingDialogue propio.")]
        public GroomingDialogue customDialogue;

        [Header("Interacción")]
        [Tooltip("Tag del jugador (Otto).")]
        public string ottoTag = "Player";
        [Tooltip("Volver a conversar si el niño/a se acerca otra vez (la temática se completa igual al primer cierre).")]
        public bool allowReplay = false;

        [Header("Al alejarse (éxito del niño/a)")]
        [Tooltip("Distancia que recorre el NPC al alejarse.")]
        public float leaveDistance = 6f;
        [Tooltip("Velocidad a la que el NPC se aleja.")]
        public float leaveSpeed = 4f;
        [Tooltip("Desactivar el GameObject del NPC al terminar de alejarse.")]
        public bool disableAfterLeaving = true;
        [Tooltip("Mostrar mensaje de felicitación al niño/a cuando tiene éxito.")]
        public bool showSuccessFeedback = true;
        [TextArea]
        public string successMessage = "🦦 ¡Bien hecho! No compartiste tus datos con un desconocido.";

        /// <summary>True cuando la interacción con este NPC ya terminó (cualquier rama).</summary>
        public bool Finished { get; private set; }
        /// <summary>True si el niño/a evitó compartir datos (éxito).</summary>
        public bool WasSuccessful { get; private set; }

        private GroomingDialogue runtimeDialogue;

        private GroomingDialogue Dialogue
        {
            get
            {
                if (scriptSource == ScriptSource.Custom)
                    return customDialogue;
                if (runtimeDialogue == null)
                {
                    runtimeDialogue = DesconocidosDefaultScripts.Create(
                        scriptSource == ScriptSource.Sam
                            ? DesconocidosDefaultScripts.Personaje.Sam
                            : DesconocidosDefaultScripts.Personaje.Alex);
                }
                return runtimeDialogue;
            }
        }

        public string NpcName => Dialogue != null ? Dialogue.npcName : name;

        [Tooltip("Registrar la conversación en el backend (requiere login + partida activos).")]
        public bool reportToBackend = false;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Finished && !allowReplay) return;
            if (!other.CompareTag(ottoTag)) return;
            if (DialogueController.Instance != null && DialogueController.Instance.IsActive) return;

            var otto = other.GetComponentInParent<OttoController>();
            var dlg = Dialogue;
            if (dlg == null)
            {
                Debug.LogError($"[DesconocidosNPC] '{name}' no tiene guion asignado.");
                return;
            }

            DialogueController.GetOrCreate().StartConversation(this, dlg, otto);
        }

        /// <summary>Llamado por el DialogueController al cerrar la conversación.</summary>
        public void OnConversationFinished(bool refusedSuccessfully)
        {
            bool firstTime = !Finished;
            Finished = true;
            WasSuccessful = refusedSuccessfully;

            if (refusedSuccessfully)
            {
                if (showSuccessFeedback)
                    ZonePopupUI.Show(successMessage);
                StartCoroutine(LeaveRoutine());
            }

            if (firstTime)
            {
                if (ZonaDesconocidosManager.Instance != null)
                    ZonaDesconocidosManager.Instance.NotifyNpcFinished(this, refusedSuccessfully);
                else
                    Debug.LogWarning($"[DesconocidosNPC] '{NpcName}' terminó, pero NO hay un " +
                                     "ZonaDesconocidosManager en la escena: nadie llevará la cuenta " +
                                     "ni desbloqueará la siguiente zona. Crea un GameObject vacío con " +
                                     "ese componente (o usa el menú Fishy → Configurar Zona Desconocidos).");
            }
        }

        private IEnumerator LeaveRoutine()
        {
            // El NPC se aleja (dirección opuesta a Otto si se encuentra, si no hacia arriba).
            Vector2 dir = Vector2.up;
            var ottoCtrl = FindFirstObjectByType<OttoController>();
            if (ottoCtrl != null)
            {
                Vector2 away = (Vector2)(transform.position - ottoCtrl.transform.position);
                if (away.sqrMagnitude > 0.001f) dir = away.normalized;
            }

            Vector3 start = transform.position;
            Vector3 target = start + (Vector3)(dir * leaveDistance);
            float traveled = 0f;
            while (traveled < leaveDistance)
            {
                float step = leaveSpeed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, target, step);
                traveled += step;
                yield return null;
            }

            if (disableAfterLeaving)
                gameObject.SetActive(false);
        }
    }
}
