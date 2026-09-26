using System;
using UnityEngine;

/// <summary>
/// Fondo del menu hecho de capas que parten mostrando el lado derecho, recorren hacia la
/// izquierda, rebotan al llegar al borde y vuelven, en un vaiven lento y continuo.
///
/// Va en el objeto que contiene las capas (Fondo), que debe ocupar toda la pantalla.
/// Cada capa es una Image mas ancha que la pantalla: el sobrante es lo que recorre.
/// Todas rebotan a la vez y con el mismo ritmo; lo que cambia es la PROPORCION del
/// recorrido que usa cada una. Las de atras usan poco (0.25) y las de adelante todo (1),
/// y esa diferencia es la sensacion de profundidad. Con proporcion 0 la capa queda quieta.
///
/// El recorrido se calcula en cada cuadro con el ancho real de la capa y el de la
/// pantalla, asi que sirve con cualquier resolucion o proporcion de pantalla sin tocar nada.
/// </summary>
public class FondoParallax : MonoBehaviour
{
    [Serializable]
    public class Capa
    {
        [Tooltip("La Image de la capa. Debe estar centrada horizontalmente (ancla y pivote en 0.5).")]
        public RectTransform rect;

        [Tooltip("Cuanto del recorrido posible usa esta capa. 0 = quieta, 1 = todo el sobrante.")]
        [Range(0f, 1f)] public float proporcion = 1f;
    }

    [Tooltip("De la mas lejana (arriba en la lista) a la mas cercana.")]
    [SerializeField] private Capa[] capas;

    [Tooltip("Segundos que tarda el recorrido de un borde al otro. Mas alto = mas lento.")]
    [Min(1f)] [SerializeField] private float segundosPorRecorrido = 60f;

    [Tooltip("Frena suavemente al llegar a cada borde en vez de cambiar de sentido de golpe.")]
    [SerializeField] private bool suavizarExtremos = true;

    private RectTransform area;
    private float tiempoInicio;

    private void Awake() => area = (RectTransform)transform;

    // Cada vez que el fondo se activa parte de cero: asi siempre arranca del mismo lado,
    // sin importar cuanto lleve abierto el juego.
    private void OnEnable() => tiempoInicio = Time.unscaledTime;

    private void Update()
    {
        if (capas == null || area == null) return;

        // Tiempo sin escala: si el menu se pausa o se cambia Time.timeScale, el fondo sigue.
        float t = Mathf.PingPong((Time.unscaledTime - tiempoInicio) / segundosPorRecorrido, 1f);
        if (suavizarExtremos) t = Mathf.SmoothStep(0f, 1f, t);

        float anchoVisible = area.rect.width;

        foreach (Capa capa in capas)
        {
            if (capa == null || capa.rect == null) continue;

            // Lo que la capa sobra de cada lado. Si aun no mide mas que la pantalla
            // (primer cuadro, antes del layout) no hay recorrido y se queda centrada.
            float sobrante = Mathf.Max(0f, (capa.rect.rect.width - anchoVisible) * 0.5f);
            float alcance = sobrante * capa.proporcion;

            // t = 0 muestra el lado derecho de la imagen; al crecer t la imagen se
            // desliza hacia la derecha y la vista recorre hacia la izquierda.
            Vector2 pos = capa.rect.anchoredPosition;
            pos.x = Mathf.Lerp(-alcance, alcance, t);
            capa.rect.anchoredPosition = pos;
        }
    }
}
