using UnityEngine;

namespace Fishy.Chat
{
    /// <summary>Estado emocional de Otto al cerrar una sesión de chat (HDU-8).</summary>
    public enum OttoMood { Seguro, Preocupado }

    /// <summary>
    /// HDU-8 — Aplica el estado emocional de Otto (animación/sprite) al final de la
    /// sesión de chat. Colócalo en el GameObject de Otto. Si tiene un Animator con
    /// los parámetros indicados, dispara el trigger correspondiente; además puede
    /// intercambiar un sprite.
    ///
    /// Es opcional: si no está presente, el módulo de chat igual muestra el estado
    /// emocional en la UI (mensaje + emoji).
    /// </summary>
    public class OttoMoodController : MonoBehaviour
    {
        [Tooltip("Animator de Otto. Si está vacío se busca en los hijos.")]
        public Animator animator;
        [Tooltip("SpriteRenderer de Otto. Si está vacío se busca en los hijos.")]
        public SpriteRenderer spriteRenderer;

        [Header("Sprites opcionales por estado")]
        public Sprite seguroSprite;
        public Sprite preocupadoSprite;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Aplica el estado emocional. <paramref name="animatorTrigger"/> es el
        /// nombre del trigger del Animator (p.ej. "Seguro" / "Preocupado").
        /// </summary>
        public void SetMood(OttoMood mood, string animatorTrigger)
        {
            if (animator != null && !string.IsNullOrEmpty(animatorTrigger) &&
                HasTrigger(animatorTrigger))
            {
                animator.SetTrigger(animatorTrigger);
            }

            if (spriteRenderer != null)
            {
                var sprite = mood == OttoMood.Seguro ? seguroSprite : preocupadoSprite;
                if (sprite != null) spriteRenderer.sprite = sprite;
            }
        }

        private bool HasTrigger(string name)
        {
            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == name)
                    return true;
            return false;
        }
    }
}
