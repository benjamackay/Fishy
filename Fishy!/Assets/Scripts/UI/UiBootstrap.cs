using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Fishy.UI
{
    /// <summary>
    /// Utilidades comunes de UI. Asegura que exista un EventSystem con el módulo de
    /// entrada correcto (InputSystemUIInputModule con el nuevo Input System), para
    /// que botones y campos de texto respondan sin tener que montarlo a mano.
    /// </summary>
    public static class UiBootstrap
    {
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
