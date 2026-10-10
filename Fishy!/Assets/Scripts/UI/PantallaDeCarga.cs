using System.Collections;
using Fishy.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishy.UI
{
    /// <summary>
    /// Entra al juego sin que se vea el arranque: carga la escena pero no la
    /// activa hasta que se sabe dónde quedó Otto.
    ///
    /// <b>Qué problema resuelve.</b> <c>OttoController.Start()</c> deja a Otto en
    /// el <c>spawnPoint</c>, y la posición guardada llega del servidor después.
    /// <see cref="PersonajeBackendSync"/> está hecho para ese caso —la pide ya en
    /// el menú y lo coloca en su primer frame—, pero si la respuesta se retrasa se
    /// ve a Otto en la entrada del mapa y luego teletransportarse. Con el backend
    /// en Railway, a un viaje de ida y vuelta por internet, dejó de ser raro.
    ///
    /// <b>Por qué esperar aquí y no tapar allá.</b> La primera versión de esto era
    /// una pantalla dentro de la escena de juego que se quitaba al colocarse Otto.
    /// Funcionaba, pero obligaba a ganarle en orden de dibujo a todo lo que asoma
    /// en los primeros frames —el cartel de misión (710), las flechas de zona
    /// (700), el panel del celular (1200)— y a mantener ese número por delante de
    /// cualquier Canvas que se añadiera después. Reteniendo la activación no hay
    /// nada que tapar: cuando la escena aparece, Otto ya está puesto y lo demás
    /// sale en su momento normal.
    ///
    /// <b>Montaje.</b> Va en un Canvas de la escena del menú, <b>apagado</b>: lo
    /// enciende <see cref="Cargar"/> al pulsar Jugar. Muere con el menú, en el
    /// mismo instante en que aparece el juego.
    /// </summary>
    public class PantallaDeCarga : MonoBehaviour
    {
        [Tooltip("Lo que se enciende al empezar a cargar. Si se deja vacío se usa este mismo objeto.")]
        public GameObject raiz;

        [Tooltip("Si en este tiempo no aparece ninguna partida se entra igual: se está " +
                 "probando el menú suelto y no hay posición que esperar.")]
        public float esperaSinPartida = 1f;

        [Tooltip("Tope absoluto. Pasado esto se entra aunque la posición no haya llegado, " +
                 "y se verá el salto. Quedarse en la pantalla sería peor.")]
        public float esperaMaxima = 5f;

        [Tooltip("Tiempo mínimo que se ve la pantalla, para que no dé un parpadeo cuando " +
                 "todo llega rapidísimo. 0 la quita en cuanto esté listo.")]
        public float tiempoMinimo = 0.5f;

        private bool cargando;

        /// <summary>
        /// Enciende la pantalla y entra a la escena cuando esté todo listo.
        ///
        /// Devuelve false si la escena no está en Build Settings, y en ese caso no
        /// enciende nada: quien llama se queda con el mando para avisar del error.
        /// </summary>
        public bool Cargar(string escena)
        {
            if (cargando) return true;

            if (!Application.CanStreamedLevelBeLoaded(escena))
            {
                Debug.LogError($"[PantallaDeCarga] La escena '{escena}' no está en Build Settings.", this);
                return false;
            }

            if (raiz == null) raiz = gameObject;
            raiz.SetActive(true);

            cargando = true;
            StartCoroutine(Rutina(escena));
            return true;
        }

        private IEnumerator Rutina(string escena)
        {
            float desde = Time.unscaledTime;

            AsyncOperation op = SceneManager.LoadSceneAsync(escena);
            op.allowSceneActivation = false;

            while (true)
            {
                float llevamos = Time.unscaledTime - desde;

                // 0.9 y no 1: con allowSceneActivation en false, Unity deja el
                // progreso clavado ahí y espera el permiso. Un bucle que aguarde a
                // que llegue a 1 no termina nunca.
                bool escenaLista = op.progress >= 0.9f;
                bool pasoElMinimo = llevamos >= tiempoMinimo;

                if (escenaLista && pasoElMinimo && (TodoListo(llevamos) || SeAcaboElTiempo(llevamos)))
                    break;

                yield return null;
            }

            op.allowSceneActivation = true;
        }

        /// <summary>
        /// Ya se puede entrar: se sabe dónde va Otto y qué zonas están abiertas, o
        /// no hay ninguna partida de la que esperar nada.
        ///
        /// Las dos cosas y no sólo la posición: son dos peticiones distintas, y
        /// entrar con una sola deja a Otto apareciendo dentro de una zona que
        /// todavía se cree cerrada. Su barrera aún tiene el collider puesto, así
        /// que lo empuja fuera y le saca el cartel; un instante después llega la
        /// respuesta, se abre, y queda como si nada hubiera pasado.
        /// </summary>
        private bool TodoListo(float llevamos)
        {
            ApiManager api = ApiManager.Instance;
            if (api == null || api.PartidaId == null)
            {
                // Darle Play directo al menú no crea partida. Se espera un momento por
                // si el login está a mitad, y si no aparece se entra: no hay nada que
                // restaurar y Otto se quedará en el spawnPoint, que es lo correcto.
                return llevamos >= esperaSinPartida;
            }

            PersonajeBackendSync personaje = PersonajeBackendSync.Instance;
            bool posicion = personaje != null && personaje.PosicionEnMano;

            return posicion && MisionBackendSync.ZonasEnMano;
        }

        private bool SeAcaboElTiempo(float llevamos)
        {
            if (llevamos < esperaMaxima) return false;

            Debug.LogWarning($"[PantallaDeCarga] Se agotó la espera ({esperaMaxima:0.#} s) sin que " +
                             "llegara la posición de Otto. Se entra igual y puede verse el salto.", this);
            return true;
        }
    }
}
