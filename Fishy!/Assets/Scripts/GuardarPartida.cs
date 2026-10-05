using System.Collections;
using Fishy.Net;
using UnityEngine;
using UnityEngine.Events;

namespace Fishy.World
{
    /// <summary>
    /// Guarda la partida cuando se lo pide la escena. Se engancha <see cref="Guardar"/>
    /// a cualquier evento —el <c>alCompletar</c> de un <c>AlCompletarMision</c>, un
    /// botón, un trigger— y avisa cuando terminó.
    ///
    /// <b>Para qué existe.</b> El juego guarda solo al cambiar de zona y al cerrar. El
    /// final se calcula con las decisiones guardadas en la nube, pero al terminar la
    /// última misión las de la Zona 3 todavía están en la cola local: preguntarle al
    /// servidor en ese momento dejaría fuera la zona entera. Este componente es el
    /// guardado de "aquí la historia necesita estar al día", sin agregar un momento de
    /// guardado fijo para todo el juego.
    ///
    /// No sube nada por su cuenta: pasa por <see cref="SaveManager"/>, con el motivo
    /// <see cref="SaveManager.Motivo.Disparador"/>, que es el único que sabe guardar
    /// (posición de Otto, zona, cola).
    ///
    /// <b>Los dos avisos importan.</b> Quien dependa de los datos del servidor —el
    /// cálculo del final— tiene que engancharse a <see cref="alGuardar"/>, no al mismo
    /// evento que llama a <see cref="Guardar"/>: ahí todavía no subió nada. Y si no se
    /// pudo subir todo, sale <see cref="alQuedarPendiente"/>: el servidor está
    /// incompleto y un final calculado con él estaría mal.
    /// </summary>
    public class GuardarPartida : MonoBehaviour
    {
        [Tooltip("Segundos máximos que se espera a que suba todo.")]
        [Min(1f)] public float tope = 10f;

        [Tooltip("Todo quedó en la nube. Aquí va lo que necesite los datos al día " +
                 "(p. ej. calcular el final).")]
        public UnityEvent alGuardar = new UnityEvent();

        [Tooltip("No se pudo subir todo: sin conexión, sin sesión, o se acabó el plazo. " +
                 "Lo que quedó sigue en la cola y subirá en el próximo guardado.")]
        public UnityEvent alQuedarPendiente = new UnityEvent();

        [Tooltip("Escribir en consola el resultado.")]
        public bool verboseLogs = true;

        /// <summary>Hay un guardado de este componente en curso.</summary>
        public bool Guardando { get; private set; }

        /// <summary>Guarda y avisa. Llamarlo de nuevo mientras guarda no hace nada.</summary>
        public void Guardar()
        {
            if (Guardando) return;
            StartCoroutine(GuardarYAvisar());
        }

        private IEnumerator GuardarYAvisar()
        {
            Guardando = true;

            // Un frame de espera: el mismo evento que nos llama suele tener otros
            // oyentes que encolan cambios —registrar la zona completada, el progreso de
            // la misión—. El vaciado toma una foto de la cola al empezar, así que lo que
            // entrara después se quedaría fuera de este guardado.
            yield return null;

            yield return SaveManager.GetOrCreate()
                .GuardarYEsperar(SaveManager.Motivo.Disparador, tope);

            Guardando = false;

            if (TodoSubio())
            {
                if (verboseLogs) Debug.Log($"[GuardarPartida] '{name}': todo guardado en la nube.", this);
                alGuardar?.Invoke();
            }
            else
            {
                Debug.LogWarning($"[GuardarPartida] '{name}': quedaron {ColaDeCambios.Pendientes} " +
                                 "cambio(s) sin subir. Lo que dependa de la nube no tiene los datos " +
                                 "al día.", this);
                alQuedarPendiente?.Invoke();
            }
        }

        /// <summary>
        /// Sesión con partida, cola vacía y nada que saliera sin respuesta. Lo último
        /// importa: un envío al que se le acabó el plazo ya no está en la cola, pero
        /// tampoco se sabe si llegó.
        /// </summary>
        private static bool TodoSubio()
        {
            ApiManager api = ApiManager.Instance;
            if (api == null || api.IsLocalMode || !api.IsLoggedIn || api.PartidaId == null)
                return false;

            ColaDeCambios cola = ColaDeCambios.Instance;
            return ColaDeCambios.Pendientes == 0 &&
                   (cola == null || cola.UltimoResultado.SinRespuesta == 0);
        }
    }
}
