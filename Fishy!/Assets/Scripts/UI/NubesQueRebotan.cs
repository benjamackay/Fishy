using UnityEngine;

namespace Fishy.UI
{
    /// <summary>
    /// Pasea una tira ancha de un lado a otro y la trae de vuelta, sin fin.
    ///
    /// La tira tiene que ser MÁS ANCHA que la pantalla: lo que se ve es una
    /// ventana recorriéndola. <see cref="amplitud"/> es cuánto se aparta del
    /// centro hacia cada lado, así que el máximo útil es
    /// <c>(ancho de la tira − 1920) / 2</c>; pasado eso asoma el borde vacío.
    /// Con la tira de nubes de 5760 ese tope son 1920.
    ///
    /// Se mueve con un seno y no a velocidad constante: así frena al acercarse a
    /// cada extremo y vuelve suave, en vez de pegar un tirón seco al cambiar de
    /// sentido. Es la diferencia entre algo que flota y algo que rebota.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class NubesQueRebotan : MonoBehaviour
    {
        [Tooltip("Cuánto se aparta del centro hacia cada lado, en unidades del Canvas.")]
        public float amplitud = 900f;

        [Tooltip("Segundos del viaje completo: ida a un extremo, al otro, y vuelta.")]
        public float duracion = 40f;

        private RectTransform rect;
        private float centro;
        private float reloj;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();

            // El centro del vaivén es donde la dejaste puesta en la escena. Así el
            // recorrido se reparte a los dos lados de tu encuadre y no hay que
            // repetir esa posición en un campo del inspector, donde se olvidaría
            // de actualizar en cuanto movieras la tira.
            centro = rect.anchoredPosition.x;
        }

        private void Update()
        {
            if (duracion <= 0f) return;

            // Repeat y no una suma sin fin: el reloj se queda siempre dentro de un
            // ciclo y no pierde precisión por mucho rato que esté la pantalla.
            reloj = Mathf.Repeat(reloj + Time.unscaledDeltaTime, duracion);

            Vector2 posicion = rect.anchoredPosition;
            posicion.x = centro + Mathf.Sin(reloj / duracion * Mathf.PI * 2f) * amplitud;
            rect.anchoredPosition = posicion;
        }
    }
}
