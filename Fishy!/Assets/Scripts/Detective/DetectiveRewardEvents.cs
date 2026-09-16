using System;

namespace Fishy.Detective
{
    /// <summary>Datos mínimos que la presentación necesita para anunciar un pin.
    /// No es <see cref="DetectiveCase"/> a propósito: quien escuche esto no
    /// debería depender del modelo de contenido del modo Detective.</summary>
    public readonly struct RecompensaOtorgada
    {
        public readonly string itemId;
        public readonly string nombre;

        public RecompensaOtorgada(string itemId, string nombre)
        {
            this.itemId = itemId;
            this.nombre = nombre;
        }
    }

    /// <summary>
    /// HDU-11 — Punto de desacople entre "se ganó un pin" (decisión de negocio,
    /// en <see cref="DetectiveCaseManager"/>) y "cómo se lo muestro al jugador"
    /// (presentación, en <see cref="DetectiveRewardPopup"/>). Quien reemplace el
    /// popup por una animación más elaborada solo toca el suscriptor, nunca esta
    /// clase ni DetectiveCaseManager.
    /// </summary>
    public static class DetectiveRewardEvents
    {
        public static event Action<RecompensaOtorgada> OnRecompensaOtorgada;

        public static void RaiseOtorgada(RecompensaOtorgada r) => OnRecompensaOtorgada?.Invoke(r);
    }
}
