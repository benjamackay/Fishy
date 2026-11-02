namespace Fishy.Album
{
    /// <summary>
    /// HDU-12 CA4 — Qué le falta al álbum, cuando le falta algo.
    ///
    /// <b>Por qué no es un solo "está vacío".</b> El álbum tiene dos secciones que se
    /// llenan por caminos distintos: los pines se ganan superando la mitad de las
    /// señales de un caso, y las evidencias se ganan marcando bien <i>una</i> señal.
    /// Por eso se puede tener evidencias sin ningún pin —acertar una de cuatro— y se
    /// puede tener pines sin ninguna evidencia —haber cerrado casos antes de que
    /// existiera el registro de señales—. Decirle «tu álbum está vacío» a quien tiene
    /// tres pines a la vista es mentirle, y decirle «resuelve un caso» a quien ya
    /// resolvió tres no le explica nada de lo que tiene que hacer.
    /// </summary>
    public enum EstadoVacio
    {
        /// <summary>Hay pines y hay evidencias: no falta ningún mensaje.</summary>
        Completo,

        /// <summary>Ni pines ni evidencias. El álbum está recién estrenado y lo que
        /// hay que decir es cómo se llena.</summary>
        AlbumEntero,

        /// <summary>Hay evidencias pero ningún pin. Falta solo el mosaico de arriba y
        /// hay que explicar cómo se gana un pin.</summary>
        FaltanPines,

        /// <summary>Hay pines pero ninguna evidencia. Las de entonces no se pueden
        /// reconstruir —el servidor solo guardó los ids del último intento—, así que
        /// lo que se dice es que las próximas sí van a aparecer.</summary>
        FaltanEvidencias,
    }

    /// <summary>
    /// HDU-12 CA4 — Decide cuál de los tres mensajes de álbum vacío toca.
    ///
    /// <b>Por qué la decisión vive acá y el texto no.</b> Este ensamblado, Fishy.Album,
    /// es el que las pruebas NUnit en PlayMode pueden ver: un .asmdef de pruebas no
    /// puede referenciar Assembly-CSharp, que es donde está la pantalla. Así que la
    /// regla —qué estado es cuál— se decide y se comprueba acá, y la redacción se
    /// queda del lado de la UI (<c>AlbumUITheme.Textos</c>), que es donde se puede
    /// cambiar una palabra sin tocar una prueba.
    /// </summary>
    public static class EstadoVacioAlbum
    {
        /// <summary>
        /// <paramref name="pines"/> son los pines de recompensa que hay en la mochila
        /// y <paramref name="grupos"/> los grupos de evidencias que devolvió
        /// <see cref="AlbumDeEvidencias.PorCaso"/> o
        /// <see cref="AlbumDeEvidencias.PorTematica"/>. Una cuenta negativa se trata
        /// como cero: no hay forma de tener menos que nada.
        /// </summary>
        public static EstadoVacio Decidir(int pines, int grupos)
        {
            bool hayPines = pines > 0;
            bool hayEvidencias = grupos > 0;

            if (!hayPines && !hayEvidencias) return EstadoVacio.AlbumEntero;
            if (!hayPines) return EstadoVacio.FaltanPines;
            if (!hayEvidencias) return EstadoVacio.FaltanEvidencias;

            return EstadoVacio.Completo;
        }

        /// <summary>
        /// Si hay que dibujar las dos secciones con sus títulos, o un único mensaje
        /// centrado.
        ///
        /// Con el álbum entero vacío no se pintan: «Tus pines» y «Evidencias» sobre
        /// dos huecos convierten una pantalla que todavía no tiene nada en una que
        /// parece rota.
        /// </summary>
        public static bool MuestraSecciones(EstadoVacio estado)
            => estado != EstadoVacio.AlbumEntero;
    }
}
