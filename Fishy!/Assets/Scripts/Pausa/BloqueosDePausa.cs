using Fishy.Detective;
using Fishy.Pausa;
using Fishy.World;
using UnityEngine;

namespace Fishy.UI
{
    /// <summary>
    /// HDU-17 CA1 — Los momentos en los que Esc no debe abrir el menú de pausa:
    /// cinemáticas y transiciones de carga.
    ///
    /// <b>Por qué está acá y no en el núcleo.</b> <see cref="EstadoDePausa"/> vive en
    /// el ensamblado Fishy.Pausa, que a propósito no conoce ni las zonas, ni el Modo
    /// Detective, ni las pantallas de carga — eso es lo que deja probarlo con NUnit
    /// en PlayMode. El núcleo sabe <i>preguntar</i> si algo veta la pausa; quién
    /// responde se decide del lado de Assembly-CSharp, que sí ve a todo el mundo.
    /// Es la misma frontera que <c>AlbumDetectivePuente</c> cruza para el álbum.
    ///
    /// <b>Por qué no basta con que la cinemática congele a Otto.</b> Pausar en mitad
    /// de un desbloqueo de zona pone el <c>timeScale</c> a 0 con la cámara a medio
    /// viaje: al reanudar la cámara retoma el paneo desde donde iba, pero el menú de
    /// pausa ya le devolvió el movimiento a Otto —la cinemática se lo había
    /// quitado—, así que el niño/a camina mientras la cámara se va sola a otro sitio.
    /// </summary>
    public static class BloqueosDePausa
    {
        public const string Cinematica = "una cinemática";
        public const string Celebracion = "la entrega de un pin";
        public const string Carga = "una pantalla de carga";
        public const string Salida = "la vuelta al menú";

        /// <summary>
        /// Se apuntan al arrancar, una vez por corrida. Los bloqueos son preguntas y
        /// no banderines, así que no hay nada que mantener después: cada uno consulta
        /// a quien de verdad sabe si ese momento está en curso.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Registrar()
        {
            EstadoDePausa.RegistrarBloqueo(Cinematica,
                () => ZoneUnlockCinematic.Instance != null &&
                      ZoneUnlockCinematic.Instance.IsPlaying);

            EstadoDePausa.RegistrarBloqueo(Celebracion, () => DetectiveRewardPopup.EnCurso);

            EstadoDePausa.RegistrarBloqueo(Carga, HayPantallaDeCarga);

            // Mientras se guarda y se vuelve al menú, Esc no puede volver a abrir la
            // pausa encima del cartel de «Guardando tu partida…».
            EstadoDePausa.RegistrarBloqueo(Salida, () => SecuenciaDeSalida.EnCurso);
        }

        /// <summary>
        /// Si hay una transición de carga a la vista.
        ///
        /// Se busca en la escena en vez de guardarse una referencia porque la
        /// <see cref="PantallaDeCarga"/> vive en la escena del menú y muere con ella:
        /// una referencia guardada sería un null de Unity la mayor parte de la
        /// partida. La búsqueda solo corre cuando alguien pulsa Esc, no cada frame.
        /// </summary>
        private static bool HayPantallaDeCarga()
        {
            var pantalla = Object.FindAnyObjectByType<PantallaDeCarga>(FindObjectsInactive.Exclude);
            if (pantalla != null && pantalla.Cargando) return true;

            var login = Object.FindAnyObjectByType<LoadingScreen>(FindObjectsInactive.Exclude);
            return login != null && login.Visible;
        }
    }
}
