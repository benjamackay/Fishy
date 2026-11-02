using System;
using System.Collections;
using UnityEngine;

namespace Fishy.Pausa
{
    /// <summary>
    /// Lo que hace falta para salir al menú principal, visto desde el núcleo: alguien
    /// que sepa guardar y alguien que sepa cargar la escena. Lo implementa
    /// <c>Fishy.UI.MenuPausa</c>, que es quien conoce al <c>SaveManager</c>, a la
    /// <c>ColaDeCambios</c> y al <c>SceneManager</c> — todos en Assembly-CSharp, que
    /// desde acá no se ve.
    /// </summary>
    public interface ISalidaAlMenu
    {
        /// <summary>Guarda la partida y espera a que termine. Es el guardado
        /// automático de CA5: el que corre sin que el niño/a tenga que pedirlo.</summary>
        IEnumerator GuardarTodo();

        /// <summary>Carga la escena del menú principal. No vuelve.</summary>
        void CargarMenu();
    }

    /// <summary>
    /// HDU-17 CA5 — Salir al menú principal guarda primero y carga después.
    ///
    /// <b>Por qué el orden es una clase y no tres líneas en el botón.</b> Porque el
    /// orden <i>es</i> el criterio de aceptación, y escrito dentro de
    /// <c>MenuPausa</c> —en Assembly-CSharp— no habría forma de afirmarlo desde una
    /// prueba: el .asmdef de pruebas no puede ver esa clase. Acá la secuencia se
    /// ejecuta contra <see cref="ISalidaAlMenu"/>, así que una prueba le pasa un
    /// doble que anota el orden en que le hablaron y comprueba que la carga llegó
    /// después del guardado, y no al revés ni en paralelo.
    ///
    /// Invertir ese orden no se ve al probarlo a mano —la partida casi siempre está
    /// al día y no se pierde nada visible—, y se nota una semana después, cuando a
    /// alguien se le borra la tarde de juego porque cerró desde el menú.
    /// </summary>
    public static class SecuenciaDeSalida
    {
        /// <summary>En qué va la salida. Se expone para que la pantalla pueda apagar
        /// sus botones y para diagnóstico.</summary>
        public enum Paso
        {
            Quieta,
            Guardando,
            Cargando,
            Terminada,
        }

        public static Paso EnQueVa { get; private set; } = Paso.Quieta;

        /// <summary>Si hay una salida en curso. Mientras lo esté, el menú no deja
        /// pulsar nada más.</summary>
        public static bool EnCurso => EnQueVa == Paso.Guardando || EnQueVa == Paso.Cargando;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void LimpiarEstadoEstatico() => EnQueVa = Paso.Quieta;

        /// <summary>
        /// CA5 — Guarda, descongela y recién entonces carga el menú.
        ///
        /// El <see cref="EstadoDePausa.Restablecer"/> del medio no es cosmético: se
        /// sale desde el menú de pausa, o sea con el <c>timeScale</c> en 0, y la
        /// escena del menú heredaría ese 0. Ahí ya no hay ningún menú de pausa que lo
        /// deshaga, así que el menú principal se quedaría con las animaciones muertas
        /// y sin forma de volver.
        ///
        /// Si el guardado revienta igual se carga el menú: dejar al niño/a encerrado
        /// en una pantalla de pausa que no responde es peor que volver al menú
        /// habiendo perdido el último tramo, y el fallo queda en el log.
        /// </summary>
        public static IEnumerator Ejecutar(ISalidaAlMenu destino)
        {
            if (destino == null)
            {
                Debug.LogError("[Pausa] Se pidió salir al menú sin decir a dónde.");
                yield break;
            }

            if (EnCurso) yield break;

            EnQueVa = Paso.Guardando;

            // El guardado se recorre a mano, paso a paso, en vez de con un
            // `yield return destino.GuardarTodo()`. Es más código, y es la única
            // forma de que un guardado que revienta no se lleve por delante el
            // cambio de escena: C# no deja poner un `yield return` dentro de un
            // try/catch, y `GuardarTodo()` es un iterador —su cuerpo no corre al
            // llamarlo, sino en cada MoveNext—, así que una excepción suya saldría
            // precisamente por donde no se la puede atrapar.
            IEnumerator guardado = Arrancar(destino);
            while (guardado != null)
            {
                object espera;
                try
                {
                    if (!guardado.MoveNext()) break;
                    espera = guardado.Current;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Pausa] El guardado de salida falló y se vuelve al " +
                                   $"menú igual: {ex}");
                    break;
                }

                yield return espera;
            }

            EstadoDePausa.Restablecer();

            EnQueVa = Paso.Cargando;
            try
            {
                destino.CargarMenu();
            }
            finally
            {
                EnQueVa = Paso.Terminada;
            }
        }

        /// <summary>Pide el iterador del guardado. Separado para poder atrapar lo que
        /// falle en la llamada misma —un <c>SaveManager</c> que ya no está, por
        /// ejemplo—, que es lo único que no revienta dentro del MoveNext.</summary>
        private static IEnumerator Arrancar(ISalidaAlMenu destino)
        {
            try
            {
                return destino.GuardarTodo();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Pausa] El guardado de salida falló al arrancar y se " +
                               $"vuelve al menú igual: {ex}");
                return null;
            }
        }
    }
}
