using UnityEngine;

namespace Fishy.Net
{
    /// <summary>
    /// Decide si tiene sentido avisar de que no hay <c>PartidaId</c>.
    ///
    /// Los cinco sincronizadores esperan a que haya partida antes de guardar nada, y
    /// avisan si tardan demasiado. El aviso existe para un caso concreto y real: darle
    /// Play directo a la escena de juego, donde nunca va a haber partida porque no se
    /// pasó por el login, y todo lo que haga el niño/a se pierde en silencio.
    ///
    /// <b>El problema era la condición.</b> Bastaba con que pasaran 8 segundos, así que
    /// el aviso salía SIEMPRE: hacer login, elegir perfil y elegir partida tarda más que
    /// eso, de modo que los cinco avisos aparecían en cada arranque normal diciéndole a
    /// quien acababa de pasar por el login que pasara por el login. Cinco falsas alarmas
    /// por partida enseñan a no mirar la consola, y entonces el aviso de verdad —cuando
    /// de verdad se está perdiendo el progreso— tampoco se mira.
    ///
    /// La condición correcta no es el tiempo, es <b>dónde estamos</b>: si Otto existe en
    /// la escena se está jugando, y no tener partida ahí sí es un problema. En los menús
    /// Otto no existe y esperar es lo normal.
    /// </summary>
    internal static class AvisoSinPartida
    {
        /// <summary>Segundos jugando sin partida antes de darlo por perdido.</summary>
        public const float Paciencia = 8f;

        /// <summary>
        /// True si hay que avisar de que no hay partida.
        ///
        /// <paramref name="sinPartidaDesde"/> es el <c>Time.realtimeSinceStartup</c> de
        /// la última vez que sí había. El tiempo se comprueba primero para no barrer la
        /// escena en cada tic mientras se navega por los menús.
        /// </summary>
        public static bool HayQueAvisar(float sinPartidaDesde)
            => Time.realtimeSinceStartup - sinPartidaDesde > Paciencia && SeEstaJugando();

        /// <summary>
        /// Otto solo existe en la escena de juego, así que encontrarlo es la señal de
        /// que ya no estamos en un menú. Es la misma forma de buscarlo que usa
        /// <see cref="PersonajeBackendSync"/>.
        /// </summary>
        private static bool SeEstaJugando()
            => Object.FindAnyObjectByType<Fishy.World.OttoController>() != null;
    }
}
