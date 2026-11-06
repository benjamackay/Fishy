using UnityEngine;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Recorre una lista de sprites sobre un <see cref="Image"/> de UI.
    ///
    /// Existe porque un clip de animación no sirve aquí: los .anim del proyecto
    /// —`walk.anim`, el que usa Otto en el mundo— atan `m_Sprite` a un
    /// SpriteRenderer, que es otro componente. Puesto sobre un Image no da
    /// ningún error: simplemente se queda quieto, que es peor.
    ///
    /// Usa tiempo sin escalar, así que sigue animando aunque algo haya dejado
    /// <c>Time.timeScale</c> en 0 (el menú de pausa lo hace). Una pantalla de
    /// carga congelada parecería colgada.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class AnimacionPorFrames : MonoBehaviour
    {
        [Tooltip("Los frames en el orden en que se ven. Se arrastran desde el sprite " +
                 "desplegado en el Project, no desde el PNG entero.")]
        public Sprite[] frames;

        [Tooltip("Fotogramas por segundo.")]
        public float fps = 8f;

        private Image imagen;
        private float reloj;
        private int actual = -1;

        private void Awake() => imagen = GetComponent<Image>();

        private void OnEnable()
        {
            // Se reinicia al encenderse: si no, al volver a mostrar la pantalla
            // arrancaría a mitad del ciclo y con el reloj de la vez anterior.
            reloj = 0f;
            actual = -1;
            Mostrar(0);
        }

        private void Update()
        {
            if (frames == null || frames.Length < 2 || fps <= 0f) return;

            float porFrame = 1f / fps;
            reloj += Time.unscaledDeltaTime;
            if (reloj < porFrame) return;

            // Se cuentan los frames que caben en el tiempo pasado, no se avanza uno
            // solo: cargar una escena da tirones de varias décimas, y descartando el
            // resto la animación se iría quedando atrás.
            int saltos = 0;
            while (reloj >= porFrame)
            {
                reloj -= porFrame;
                saltos++;
            }

            Mostrar((actual + saltos) % frames.Length);
        }

        private void Mostrar(int indice)
        {
            if (frames == null || frames.Length == 0 || indice == actual) return;

            actual = indice;
            if (frames[indice] != null) imagen.sprite = frames[indice];
        }
    }
}
