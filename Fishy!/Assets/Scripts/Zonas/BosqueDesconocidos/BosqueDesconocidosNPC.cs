using System.Collections;
using UnityEngine;
using Fishy.World;
using Fishy.Chat;

namespace Fishy.Zonas.BosqueDesconocidos
{
    /// <summary>
    /// HDU-2 — NPC de la temática "Bosque de los Desconocidos".
    ///
    /// El diálogo en sí (guion, UI, ramificación, registro en el backend) corre
    /// por el módulo de chat genérico (<see cref="ChatModuleLauncher"/>, HDU-8) —
    /// requiere uno en el mismo GameObject, ya configurado con su propio
    /// Collider2D/Is Trigger y sus conversaciones. Este componente sólo reacciona
    /// cuando ESA sesión termina: decide si cuenta como éxito, hace que el NPC
    /// "se aleje" si corresponde, y avisa al <see cref="BosqueDesconocidosManager"/>
    /// para que lleve la cuenta y desbloquee la siguiente zona.
    /// </summary>
    [RequireComponent(typeof(ChatModuleLauncher))]
    public class BosqueDesconocidosNPC : MonoBehaviour
    {
        [Header("Resultado")]
        [Tooltip("% mínimo de respuestas seguras en la sesión para contar como éxito.")]
        public float umbralExito = 70f;
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
        /// <summary>True si el % de respuestas seguras alcanzó el umbral (éxito).</summary>
        public bool WasSuccessful { get; private set; }

        public string NpcName => name;

        private ChatModuleLauncher launcher;

        private void Awake()
        {
            launcher = GetComponent<ChatModuleLauncher>();
            launcher.OnSesionFinalizada += HandleSesionFinalizada;
        }

        private void OnDestroy()
        {
            if (launcher != null)
                launcher.OnSesionFinalizada -= HandleSesionFinalizada;
        }

        private void HandleSesionFinalizada(float safePercent)
        {
            if (Finished && !allowReplay) return;

            bool exito = safePercent >= umbralExito;
            bool firstTime = !Finished;
            Finished = true;
            WasSuccessful = exito;

            if (exito)
            {
                if (showSuccessFeedback)
                    ZonePopupUI.Show(successMessage);
                StartCoroutine(LeaveRoutine());
            }

            if (firstTime)
            {
                if (BosqueDesconocidosManager.Instance != null)
                    BosqueDesconocidosManager.Instance.NotifyNpcFinished(this, exito);
                else
                    Debug.LogWarning($"[BosqueDesconocidosNPC] '{NpcName}' terminó, pero NO hay un " +
                                     "BosqueDesconocidosManager en la escena: nadie llevará la cuenta " +
                                     "ni desbloqueará la siguiente zona. Crea un GameObject vacío con " +
                                     "ese componente (o usa el menú Fishy → Configurar Zona Desconocidos).");
            }
        }

        private IEnumerator LeaveRoutine()
        {
            // El NPC se aleja (dirección opuesta a Otto si se encuentra, si no hacia arriba).
            Vector2 dir = Vector2.up;
            var ottoCtrl = FindAnyObjectByType<OttoController>();
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
