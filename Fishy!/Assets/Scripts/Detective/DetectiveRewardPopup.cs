using UnityEngine;
using Fishy.World;

namespace Fishy.Detective
{
    /// <summary>
    /// HDU-11 — Presentación temporal y simple del pin obtenido: reusa el cartel
    /// de <see cref="ZonePopupUI"/> (mismo patrón que HDU-5). Es intencionalmente
    /// la pieza "fácil de reemplazar": no hay lógica de negocio aquí, solo
    /// mostrar un mensaje cuando llega <see cref="DetectiveRewardEvents"/>. Para
    /// una versión más elaborada, basta con cambiar <see cref="MostrarPopup"/> —o
    /// este archivo entero— sin tocar el resto del modo Detective.
    /// </summary>
    public class DetectiveRewardPopup : MonoBehaviour
    {
        private static DetectiveRewardPopup _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (_instance != null) return;
            var go = new GameObject("DetectiveRewardPopup");
            _instance = go.AddComponent<DetectiveRewardPopup>();
            DontDestroyOnLoad(go);
        }

        private void OnEnable()  => DetectiveRewardEvents.OnRecompensaOtorgada += MostrarPopup;
        private void OnDisable() => DetectiveRewardEvents.OnRecompensaOtorgada -= MostrarPopup;

        private void MostrarPopup(RecompensaOtorgada r)
        {
            ZonePopupUI.Show($"¡Nuevo pin! {r.nombre}");
        }
    }
}
