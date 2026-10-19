using UnityEngine;
using UnityEngine.EventSystems;

namespace Fishy.World
{
    /// <summary>
    /// HDU-5 — Botón direccional en pantalla (para dispositivos táctiles).
    ///
    /// Coloca este componente en un Button/Image del Canvas y enlázalo con el
    /// <see cref="OttoController"/>. Mientras el dedo/puntero mantiene el botón,
    /// Otto se desplaza en esa dirección; al soltar, se detiene.
    ///
    /// Cubre la opción "teclas específicas en la pantalla del dispositivo".
    /// Para correr o saltar usa <see cref="action"/> = Run / Jump.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class OttoOnScreenButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum ButtonAction { Up, Down, Left, Right, Run, Jump }

        [Tooltip("Controlador de Otto. Si se deja vacío se busca en la escena.")]
        public OttoController otto;

        [Tooltip("Qué hace este botón.")]
        public ButtonAction action = ButtonAction.Up;

        private bool isPressed;

        private void Awake()
        {
            if (otto == null)
                otto = FindAnyObjectByType<OttoController>();
        }

        public void OnPointerDown(PointerEventData eventData) => SetPressed(true);
        public void OnPointerUp(PointerEventData eventData) => SetPressed(false);
        // Si el dedo se desliza fuera del botón, también se considera soltado.
        public void OnPointerExit(PointerEventData eventData) => SetPressed(false);

        private void OnDisable() => SetPressed(false);

        private void SetPressed(bool pressed)
        {
            if (otto == null) return;
            if (pressed == isPressed && action != ButtonAction.Jump) return;
            isPressed = pressed;

            switch (action)
            {
                case ButtonAction.Up: otto.SetVirtualUp(pressed); break;
                case ButtonAction.Down: otto.SetVirtualDown(pressed); break;
                case ButtonAction.Left: otto.SetVirtualLeft(pressed); break;
                case ButtonAction.Right: otto.SetVirtualRight(pressed); break;
                case ButtonAction.Run: otto.SetVirtualRun(pressed); break;
                case ButtonAction.Jump:
                    if (pressed) otto.TryJump();
                    break;
            }
        }
    }
}
