using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishy.Pausa
{
    /// <summary>
    /// HDU-17 — Si el juego está en pausa, y quién tiene derecho a ponerlo.
    ///
    /// <b>Por qué vive en su propio ensamblado.</b> Igual que <c>AlbumDeEvidencias</c>
    /// y por el mismo motivo: un assembly de pruebas con .asmdef no puede referenciar
    /// Assembly-CSharp, así que nada que viva ahí se puede probar con NUnit en
    /// PlayMode. Acá está la decisión —congelar, callar la entrada, dejar o no
    /// pausar—, que es lo que los criterios de HDU-17 piden comprobar;
    /// <c>Fishy.UI.MenuPausa</c> es la pantalla que la usa y no sabe nada de esto más
    /// allá de llamar a <see cref="Pausar"/> y <see cref="Reanudar"/>.
    ///
    /// <b>La pausa es una sola cosa, no dos.</b> Antes el menú ponía el
    /// <c>timeScale</c> a 0 por su cuenta y eso era toda la pausa. Con dos pantallas
    /// capaces de congelar —el menú y el cartel de «el guardado está tardando»— la
    /// segunda en abrir guardaba un <c>timeScale</c> que ya valía 0, y al cerrarse
    /// dejaba el juego congelado para siempre. Acá el estado es uno y los dos lo
    /// piden al mismo sitio.
    /// </summary>
    public static class EstadoDePausa
    {
        /// <summary>Si el juego está congelado ahora mismo.</summary>
        public static bool Pausado { get; private set; }

        /// <summary>Avisa cada vez que se entra o se sale de la pausa, para que lo
        /// que dependa de ella se entere sin preguntar cada frame.</summary>
        public static event Action<bool> AlCambiar;

        /// <summary>El <c>timeScale</c> de antes de pausar. No se da por hecho que sea
        /// 1: una cinemática puede estar corriendo a cámara lenta.</summary>
        private static float _timeScalePrevio = 1f;

        /// <summary>Quién veta la pausa y por qué (CA1). La clave es el nombre que
        /// sale en el log; el valor dice si ese veto está activo ahora mismo.</summary>
        private static readonly Dictionary<string, Func<bool>> _bloqueos =
            new Dictionary<string, Func<bool>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void LimpiarEstadoEstatico()
        {
            // Dejar `Pausado` en true entre corridas del editor significaría arrancar
            // la siguiente creyendo que hay un menú abierto que nadie puede cerrar.
            Pausado = false;
            _timeScalePrevio = 1f;
            _bloqueos.Clear();
            AlCambiar = null;
        }

        // ── Quién puede vetar la pausa (CA1) ──────────────────────────────────

        /// <summary>
        /// Apunta un motivo por el que, mientras dure, no se puede pausar: una
        /// cinemática, una pantalla de carga. Registrar dos veces el mismo nombre
        /// reemplaza al anterior.
        ///
        /// Es una pregunta y no un banderín que se enciende y se apaga porque los
        /// dueños de esos momentos ya saben decir si están corriendo
        /// (<c>ZoneUnlockCinematic.IsPlaying</c>), y un banderín que hay que acordarse
        /// de apagar se queda encendido el día que la cinemática termina por un camino
        /// que nadie previó — y entonces el juego no se puede pausar nunca más.
        /// </summary>
        public static void RegistrarBloqueo(string nombre, Func<bool> estaActivo)
        {
            if (string.IsNullOrWhiteSpace(nombre) || estaActivo == null) return;
            _bloqueos[nombre.Trim()] = estaActivo;
        }

        public static void SoltarBloqueo(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return;
            _bloqueos.Remove(nombre.Trim());
        }

        /// <summary>
        /// CA1 — Si se puede pausar ahora. <paramref name="motivo"/> queda con el
        /// nombre del primer veto activo, o vacío si no hay ninguno.
        ///
        /// Un veto que tira una excepción se trata como «no veta»: el dueño del
        /// momento se rompió, y eso no es razón para dejar al niño/a sin poder pausar.
        /// </summary>
        public static bool SePuedePausar(out string motivo)
        {
            foreach (KeyValuePair<string, Func<bool>> bloqueo in _bloqueos)
            {
                bool activo;
                try
                {
                    activo = bloqueo.Value();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Pausa] El bloqueo '{bloqueo.Key}' falló al " +
                                     $"responder y se ignora: {ex.Message}");
                    continue;
                }

                if (!activo) continue;

                motivo = bloqueo.Key;
                return false;
            }

            motivo = "";
            return true;
        }

        public static bool SePuedePausar() => SePuedePausar(out _);

        // ── Pausar y reanudar ─────────────────────────────────────────────────

        /// <summary>
        /// CA3 — Congela la acción y calla la entrada del jugador. Devuelve false si
        /// ya estaba pausado o si algo lo veta (CA1).
        /// </summary>
        public static bool Pausar()
        {
            if (Pausado) return false;

            if (!SePuedePausar(out string motivo))
            {
                Debug.Log($"[Pausa] No se pausa: {motivo} está en curso.");
                return false;
            }

            _timeScalePrevio = Time.timeScale;
            Time.timeScale = 0f;
            Pausado = true;

            PuertaDeEntrada.Suspender();

            AlCambiar?.Invoke(true);
            return true;
        }

        /// <summary>
        /// CA4 — Devuelve el juego a donde estaba: el tiempo vuelve a correr y la
        /// entrada vuelve a quien la tenía.
        ///
        /// Nada de esto toca el contenido de una sesión de chat o de un caso del Modo
        /// Detective, y ese es justamente el punto: esas pantallas guardan lo suyo en
        /// memoria y siguen montadas durante la pausa, así que al reanudar están como
        /// quedaron. Lo único que se congeló fue el reloj.
        /// </summary>
        public static bool Reanudar()
        {
            if (!Pausado) return false;

            Pausado = false;

            // A 1 si lo de antes era 0: si algo dejó el timeScale en 0 y la pausa lo
            // copió, restaurarlo tal cual dejaría el juego congelado sin menú que lo
            // deshaga, que es el peor final posible de esta función.
            Time.timeScale = _timeScalePrevio > 0f ? _timeScalePrevio : 1f;
            _timeScalePrevio = 1f;

            PuertaDeEntrada.Reanudar();

            AlCambiar?.Invoke(false);
            return true;
        }

        /// <summary>Pausa si estaba corriendo y reanuda si estaba pausado. Es lo que
        /// hace la tecla Esc.</summary>
        public static bool Alternar() => Pausado ? Reanudar() : Pausar();

        /// <summary>
        /// Suelta la pausa sin preguntar y deja el tiempo corriendo. Para el cambio de
        /// escena y para los arneses de prueba: cargar el menú con el
        /// <c>timeScale</c> en 0 deja la escena nueva congelada, y ahí ya no hay
        /// ningún menú de pausa que la descongele.
        /// </summary>
        public static void Restablecer()
        {
            Pausado = false;
            _timeScalePrevio = 1f;
            Time.timeScale = 1f;
            PuertaDeEntrada.Reanudar();
        }
    }
}
