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
        private PolygonColliderVisual[] meshOverlays;
        private float lastShown = -999f;

        private void Awake()
        {
            colliders = GetComponentsInChildren<Collider2D>();
            meshOverlays = GetComponentsInChildren<PolygonColliderVisual>(true);
            ApplyState();
        }

        private void OnValidate()
        {
            // Refleja el estado en el editor para previsualizar el oscurecido.
            // Sólo el sprite: la malla se construye en runtime y crear su
            // material en modo edición dejaría instancias colgando.
            if (overlay != null)
                SetSpriteOverlayVisible(isLocked);
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
            SetOverlayAlpha(visible ? darkenAlpha : 0f);
        }

        private void SetSpriteOverlayVisible(bool visible)
        {
            if (overlay == null) return;
            var c = overlay.color;
            c.a = visible ? darkenAlpha : 0f;
            overlay.color = c;
            overlay.enabled = visible;
        }

        // ── Accesores para la cinemática de desbloqueo ─────────────────────────
        /// <summary>SpriteRenderer que oscurece la zona (puede ser null).</summary>
        public SpriteRenderer Overlay => overlay;

        /// <summary>Opacidad del oscurecido cuando está bloqueada.</summary>
        public float DarkenAlpha => darkenAlpha;

        /// <summary>Centro del área de la zona en el mundo (para enfocar la cámara).</summary>
        public Vector2 WorldCenter
        {
            get
            {
                if (overlay != null) return overlay.bounds.center;
                if (colliders == null) colliders = GetComponentsInChildren<Collider2D>();
                foreach (var c in colliders)
                    if (c != null) return c.bounds.center;
                return transform.position;
            }
        }

        /// <summary>Tamaño del área en el mundo (para encuadrar). Vector2.zero si no se puede medir.</summary>
        public Vector2 WorldSize
        {
            get
            {
                if (overlay != null) return overlay.bounds.size;
                if (colliders == null) colliders = GetComponentsInChildren<Collider2D>();
                foreach (var c in colliders)
                    if (c != null) return c.bounds.size;
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Opacidad actual del oscurecido, venga del sprite o de la malla.
        /// La cinemática la usa como punto de partida del fundido.
        /// </summary>
        public float CurrentDarkenAlpha =>
            overlay != null ? overlay.color.a : (isLocked ? darkenAlpha : 0f);

        /// <summary>Ajusta la opacidad del oscurecido (para animar el desbloqueo).</summary>
        public void SetOverlayAlpha(float a)
        {
            if (overlay != null)
            {
                var c = overlay.color;
                c.a = a;
                overlay.color = c;
                overlay.enabled = a > 0.001f;
            }

            if (meshOverlays == null) return;
            foreach (var m in meshOverlays)
            {
                if (m == null) continue;
                m.SetAlpha(a);
            }
        }

        /// <summary>
        /// (Editor) Escala y centra el overlay para cubrir exactamente el área del
        /// collider sólido. Útil tras cambiar el tamaño del BoxCollider2D:
        /// clic derecho en el componente → "Ajustar overlay al collider".
        /// </summary>
        [ContextMenu("Ajustar overlay al collider")]
        public void FitOverlayToCollider()
        {
            if (overlay == null || overlay.sprite == null) return;

            // Tamaño objetivo: preferir BoxCollider2D (válido también en edit mode).
            Vector2 target;
            Vector3 center;
            var box = GetComponentInChildren<BoxCollider2D>();
            if (box != null)
            {
                var ls = box.transform.lossyScale;
                target = new Vector2(box.size.x * Mathf.Abs(ls.x), box.size.y * Mathf.Abs(ls.y));
                center = box.transform.TransformPoint(box.offset);
            }
            else
            {
                var col = GetComponentInChildren<Collider2D>();
                if (col == null) return;
                target = col.bounds.size;
                center = col.bounds.center;
            }

            Vector2 spriteSize = overlay.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            overlay.transform.position = new Vector3(center.x, center.y, overlay.transform.position.z);
            overlay.transform.localScale = new Vector3(
                target.x / spriteSize.x, target.y / spriteSize.y, 1f);
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
