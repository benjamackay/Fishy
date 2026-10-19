using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Repite una accion mientras se mantiene pulsado un boton, acelerando poco a
    /// poco. Para listas largas donde ir de uno en uno a clics es inviable: el
    /// selector de edad del panel de perfil recorre 101 valores.
    ///
    /// Dispara al pulsar, no al soltar, asi que sustituye a <c>Button.onClick</c> en
    /// vez de sumarse a el. Si se dejan los dos puestos, un clic suelto cuenta dos
    /// veces.
    ///
    /// Lo normal es que lo agregue por codigo quien lo necesita
    /// (<c>AddComponent</c>), para no depender de que este puesto a mano en la
    /// escena; tambien funciona agregandolo en el Inspector.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class RepetidorDePulsacion : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        /// <summary>Se invoca una vez al pulsar y luego cada tanto mientras se sostiene.</summary>
        public event Action AlRepetir;

        /// <summary>Cuanto hay que sostener antes de que empiece a repetir. Por
        /// debajo de ~0.3 s un clic normal se convierte en dos sin querer.</summary>
        private const float EsperaInicial = 0.40f;

        private const float IntervaloInicial = 0.16f;

        /// <summary>Suelo del intervalo. Mas rapido que esto y pasarse del numero que
        /// se buscaba es casi inevitable.</summary>
        private const float IntervaloMinimo = 0.05f;

        /// <summary>Cuanto se acorta el intervalo en cada repeticion.</summary>
        private const float Aceleracion = 0.88f;

        private Selectable control;
        private bool sostenido;
        private float proximoDisparo;
        private float intervalo;

        private void Awake()
        {
            control = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData evento)
        {
            if (!Utilizable()) return;

            AlRepetir?.Invoke();

            sostenido = true;
            intervalo = IntervaloInicial;
            proximoDisparo = Time.unscaledTime + EsperaInicial;
        }

        public void OnPointerUp(PointerEventData evento) => Soltar();

        // Si el dedo o el raton se sale del boton, deja de repetir: sostener fuera
        // del control y que siga contando es justo lo que hace perder la cuenta.
        public void OnPointerExit(PointerEventData evento) => Soltar();

        private void OnDisable() => Soltar();

        private void Update()
        {
            if (!sostenido) return;

            // El boton se puede apagar a mitad de la pulsacion (por ejemplo cuando el
            // formulario se pone a esperar al servidor).
            if (!Utilizable()) { Soltar(); return; }

            if (Time.unscaledTime < proximoDisparo) return;

            AlRepetir?.Invoke();
            intervalo = Mathf.Max(IntervaloMinimo, intervalo * Aceleracion);
            proximoDisparo = Time.unscaledTime + intervalo;
        }

        private void Soltar()
        {
            sostenido = false;
        }

        /// <summary>Tiempo sin escalar a proposito: el menu puede estar con
        /// <c>Time.timeScale</c> en cero y las flechas tienen que seguir andando.</summary>
        private bool Utilizable()
        {
            return isActiveAndEnabled && (control == null || control.IsInteractable());
        }
    }
}
