using UnityEngine;

namespace Fishy.World
{
    /// <summary>
    /// HDU-5 — Zona bloqueada / límite del área accesible.
    ///
    /// Coloca este componente en un GameObject con uno o más Collider2D SÓLIDOS
    /// (no triggers) que delimiten la zona oscurecida. Mientras esté bloqueada:
    ///  • El collider impide físicamente que Otto avance (se detiene en el límite).
    ///  • La zona se muestra oscurecida (overlay).
    ///  • Al chocar/empujar contra ella, aparece el mensaje emergente:
    ///    «Esta zona aún está cerrada. Completa más misiones para abrirla.»
    ///
    /// Al desbloquearla (<see cref="Unlock"/>) se desactiva el bloqueo y el overlay,
    /// permitiendo a Otto entrar (p.ej. tras completar misiones / progreso).
    /// </summary>
    public class BlockedZone : MonoBehaviour
    {
        [Header("Identidad")]
        [Tooltip("Nombre/clave de la zona (para desbloquearla desde el gestor de misiones).")]
        public string zoneId = "zona_bloqueada";

        [Header("Estado")]
        [Tooltip("Si está bloqueada, Otto no puede entrar y se muestra el mensaje.")]
        public bool isLocked = true;

        [Header("Mensaje emergente")]
        [TextArea]
        public string mensajeBloqueo = "Esta zona aún está cerrada. Completa más misiones para abrirla.";
        [Tooltip("Segundos mínimos entre dos apariciones del mensaje (evita spam al empujar).")]
        public float mensajeCooldown = 2.5f;

        [Header("Aspecto (oscurecido)")]
        [Tooltip("SpriteRenderer que oscurece la zona. Opcional.")]
        public SpriteRenderer overlay;
        [Range(0f, 1f)]
        [Tooltip("Opacidad del oscurecido cuando la zona está bloqueada.")]
        public float darkenAlpha = 0.6f;

        [Header("Detección de Otto")]
        [Tooltip("Tag del jugador (Otto). Debe coincidir con el tag del GameObject de Otto.")]
        public string ottoTag = "Player";

        private Collider2D[] colliders;
        private float lastShown = -999f;

        private void Awake()
        {
            colliders = GetComponentsInChildren<Collider2D>();
            ApplyState();
        }

        private void OnValidate()
        {
            // Refleja el estado en el editor para previsualizar el oscurecido.
            if (overlay != null)
                SetOverlayVisible(isLocked);
        }

        /// <summary>Aplica el estado actual (bloqueo + oscurecido) a colliders y overlay.</summary>
        public void ApplyState()
        {
            if (colliders != null)
            {
                foreach (var c in colliders)
                {
                    if (c == null) continue;
                    // Los colliders sólidos bloquean; al desbloquear se desactivan.
                    if (!c.isTrigger) c.enabled = isLocked;
                }
            }
            SetOverlayVisible(isLocked);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (overlay == null) return;
            var c = overlay.color;
            c.a = visible ? darkenAlpha : 0f;
            overlay.color = c;
            overlay.enabled = visible;
        }

        /// <summary>Desbloquea la zona: Otto ya puede entrar y desaparece el oscurecido.</summary>
        public void Unlock()
        {
            if (!isLocked) return;
            isLocked = false;
            ApplyState();
        }

        /// <summary>Vuelve a bloquear la zona (caso poco común, p.ej. reinicio).</summary>
        public void Lock()
        {
            if (isLocked) return;
            isLocked = true;
            ApplyState();
        }

        // ── Detección de intento de avance hacia la zona ───────────────────────
        private void OnCollisionEnter2D(Collision2D collision) => TryShowMessage(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => TryShowMessage(collision.collider);

        private void TryShowMessage(Collider2D other)
        {
            if (!isLocked) return;
            if (!other.CompareTag(ottoTag)) return;
            if (Time.unscaledTime - lastShown < mensajeCooldown) return;

            lastShown = Time.unscaledTime;
            ZonePopupUI.Show(mensajeBloqueo);
        }
    }
}
