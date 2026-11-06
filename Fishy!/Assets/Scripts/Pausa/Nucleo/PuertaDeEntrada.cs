using System.Collections.Generic;
using UnityEngine;

namespace Fishy.Pausa
{
    /// <summary>
    /// Algo que lee la entrada del jugador y que la pausa tiene que callar
    /// (HDU-17 CA3): el movimiento de Otto, los botones táctiles, lo que venga.
    ///
    /// <b>Por qué no alcanza con <c>Time.timeScale = 0</c>.</b> Congelar el tiempo
    /// para la física y las corrutinas escaladas, pero <c>Update</c> sigue
    /// corriendo y las acciones del nuevo Input System siguen entregando valores.
    /// Con solo el timeScale, el jugador que mantiene W durante la pausa suelta a
    /// Otto a mitad de camino en cuanto reanuda, porque el movimiento acumulado se
    /// aplica entero en el primer <c>FixedUpdate</c> que corre.
    /// </summary>
    public interface IEntradaPausable
    {
        /// <summary>Si ahora mismo está leyendo la entrada. Es lo que se recuerda al
        /// suspender, para no encender al reanudar lo que ya estaba apagado.</summary>
        bool EntradaActiva { get; }

        void SuspenderEntrada();
        void ReanudarEntrada();
    }

    /// <summary>
    /// Quién deja de escuchar al jugador mientras el juego está en pausa (HDU-17 CA3).
    ///
    /// El informe del estado del proyecto lo decía: no hay un gestor de entrada
    /// central, cada quien arma sus <c>InputAction</c> por su cuenta
    /// (<c>OttoController</c>) o lee <c>Keyboard.current</c> directo
    /// (<c>MenuPausa</c>). Esto no viene a centralizar esa lectura —sería reescribir
    /// medio juego—, sino a darle un interruptor común: cada quien se apunta y la
    /// pausa los calla a todos de una.
    ///
    /// <b>Al reanudar se restaura el estado anterior, no «encendido».</b> Es la regla
    /// que hace que esto no rompa nada: Otto ya puede venir bloqueado por un diálogo
    /// (<c>MenuController.BloquearAOtto</c>) o por una cinemática. Si la pausa lo
    /// devolviera siempre a «puede moverse», bastaría con pulsar Esc durante una
    /// conversación para poder caminar en mitad de ella.
    /// </summary>
    public static class PuertaDeEntrada
    {
        /// <summary>Lo apuntado, con lo que estaba haciendo cuando se suspendió. Se
        /// recorre en orden de registro, que es el único orden estable que hay.</summary>
        private static readonly List<IEntradaPausable> _apuntados = new List<IEntradaPausable>();

        /// <summary>Quiénes estaban activos justo antes de suspender. Vacío mientras
        /// no hay suspensión en curso.</summary>
        private static readonly HashSet<IEntradaPausable> _estabanActivos =
            new HashSet<IEntradaPausable>();

        /// <summary>Si la entrada está callada ahora mismo.</summary>
        public static bool Suspendida { get; private set; }

        /// <summary>Cuántos se apuntaron. Para diagnóstico y pruebas.</summary>
        public static int Apuntados => _apuntados.Count;

        /// <summary>
        /// El editor conserva los estáticos entre una corrida de Play y la siguiente,
        /// y los apuntados de la corrida anterior son objetos ya destruidos. Mismo
        /// motivo y misma receta que <c>AlbumDeEvidencias.LimpiarEstadoEstatico</c>.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void LimpiarEstadoEstatico()
        {
            _apuntados.Clear();
            _estabanActivos.Clear();
            Suspendida = false;
        }

        /// <summary>
        /// Apunta a alguien para que la pausa lo calle. Repetir no duplica.
        ///
        /// Quien se apunta con la pausa ya puesta se suspende en el acto: si no, un
        /// Otto que aparece en escena durante la pausa —al volver de una carga— se
        /// movería con el juego congelado.
        /// </summary>
        public static void Registrar(IEntradaPausable quien)
        {
            if (quien == null || _apuntados.Contains(quien)) return;

            _apuntados.Add(quien);
            Revisar(quien);
        }

        /// <summary>
        /// Vuelve a mirar a uno solo: si hay pausa y resulta que se encendió por su
        /// cuenta, lo calla y se acuerda de él para el momento de reanudar.
        ///
        /// Hace falta porque apuntarse y estar listo para leer la entrada son dos
        /// momentos distintos. Otto se apunta en su <c>OnEnable</c>, cuando todavía
        /// tiene el movimiento apagado, y lo enciende en el <c>Start</c> del frame
        /// siguiente. Sin esta segunda mirada, un Otto que aparece con el juego en
        /// pausa no queda anotado como «estaba activo», y al reanudar nadie lo
        /// enciende: el niño/a vuelve de la pausa y no se puede mover.
        /// </summary>
        public static void Revisar(IEntradaPausable quien)
        {
            if (quien == null || !Suspendida) return;
            if (!_apuntados.Contains(quien)) return;
            if (!quien.EntradaActiva) return;

            _estabanActivos.Add(quien);
            quien.SuspenderEntrada();
        }

        /// <summary>Lo saca de la lista. Lo llama el <c>OnDestroy</c> del adaptador: un
        /// apuntado destruido al que se le hable al reanudar tira una excepción y deja
        /// a los que vienen detrás sin reanudar.</summary>
        public static void Soltar(IEntradaPausable quien)
        {
            if (quien == null) return;

            _apuntados.Remove(quien);
            _estabanActivos.Remove(quien);
        }

        /// <summary>Calla a todos los apuntados y se acuerda de cuáles estaban
        /// hablando. Llamarla dos veces seguidas no hace nada la segunda: si no, la
        /// segunda pasada vería a todos ya callados y al reanudar no encendería a
        /// nadie.</summary>
        public static void Suspender()
        {
            if (Suspendida) return;
            Suspendida = true;

            _estabanActivos.Clear();

            foreach (IEntradaPausable quien in Vivos())
            {
                if (!quien.EntradaActiva) continue;

                _estabanActivos.Add(quien);
                quien.SuspenderEntrada();
            }
        }

        /// <summary>Devuelve la entrada a quienes la tenían, y solo a esos (ver la nota
        /// de la clase).</summary>
        public static void Reanudar()
        {
            if (!Suspendida) return;
            Suspendida = false;

            foreach (IEntradaPausable quien in Vivos())
                if (_estabanActivos.Contains(quien)) quien.ReanudarEntrada();

            _estabanActivos.Clear();
        }

        /// <summary>
        /// Los apuntados que todavía existen, en una copia.
        ///
        /// La copia es porque reanudar a uno puede hacer que otro se suelte de la
        /// lista, y modificarla mientras se recorre revienta. Lo de «todavía existen»
        /// es por el null de Unity: un MonoBehaviour destruido no es null para C#
        /// —la interfaz lo ve como una referencia buena— pero sí para
        /// <c>UnityEngine.Object</c>, y hablarle tira MissingReferenceException.
        /// </summary>
        private static List<IEntradaPausable> Vivos()
        {
            var vivos = new List<IEntradaPausable>(_apuntados.Count);

            for (int i = _apuntados.Count - 1; i >= 0; i--)
            {
                IEntradaPausable quien = _apuntados[i];
                if (quien is Object unityObj && unityObj == null)
                {
                    _apuntados.RemoveAt(i);
                    _estabanActivos.Remove(quien);
                    continue;
                }

                vivos.Add(quien);
            }

            vivos.Reverse();   // se recorrió al revés para poder quitar; se devuelve en orden
            return vivos;
        }
    }
}
