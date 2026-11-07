using UnityEngine;

namespace Fishy.UI
{
    /// <summary>
    /// HDU-17 CA2 — Lo que el niño/a puede ajustar desde el menú de pausa, y dónde
    /// queda guardado.
    ///
    /// <b>Son pocos ajustes y no diez a propósito.</b> El criterio pide que la pausa
    /// ofrezca «Configuración»; lo que hay detrás tiene que ser algo que un niño/a de
    /// 9 a 13 entienda de una mirada y pueda deshacer. La velocidad del texto, la
    /// pantalla completa y el volumen se explican solos. Lo que no se pone: nada de
    /// idioma (el juego está solo en español), nada de calidad gráfica (elegir «bajo»
    /// y no saber por qué el juego se ve feo es una trampa) y nada que toque la partida.
    ///
    /// <b>El volumen se guarda y se aplica aunque el juego todavía no suene.</b> A
    /// noviembre de 2026 no hay ni un archivo de audio en el proyecto, así que moverlo
    /// no se oye; está puesto porque es de los ajustes que se esperan en un menú y
    /// porque <c>AudioListener.volume</c> es global: el día que haya música y efectos,
    /// ya quedan atados sin tocar nada más.
    ///
    /// <b>Va en PlayerPrefs y no al backend.</b> Son preferencias del aparato, no de
    /// la cuenta: cómo se juega en la sala de computación del colegio no tiene por
    /// qué seguir a nadie a su casa. Además así no se le manda nada al servidor sobre
    /// un menor que no haga falta mandar — Ley 19.628, principio de finalidad: solo
    /// se trata el dato que el servicio necesita.
    ///
    /// <b>Sin partida ni perfil en la clave</b>, al revés que el álbum: la
    /// configuración es del equipo y la comparten los hermanos que juegan en él.
    /// </summary>
    public static class ConfiguracionDelJuego
    {
        public const string Prefijo = "Fishy.Config.";

        private const string ClavePantallaCompleta = Prefijo + "PantallaCompleta";
        private const string ClaveVelocidadTexto = Prefijo + "VelocidadTexto";
        private const string ClaveVolumen = Prefijo + "Volumen";

        /// <summary>Alto de la ventana al salir de pantalla completa, si el monitor da
        /// para tanto. 720 es lo mínimo en que el juego se sigue leyendo bien.</summary>
        private const int AltoVentana = 720;

        // ── Velocidad del texto ───────────────────────────────────────────────

        public enum VelocidadDelTexto { Lenta = 0, Normal = 1, Rapida = 2 }

        /// <summary>Con qué velocidad aparecen las letras en los diálogos. «Lenta» es
        /// para quien todavía lee despacio; «Rápida», para quien se aburre esperando.</summary>
        public static VelocidadDelTexto VelocidadTexto
        {
            get
            {
                int v = PlayerPrefs.GetInt(ClaveVelocidadTexto, (int)VelocidadDelTexto.Normal);
                return v >= 0 && v <= 2 ? (VelocidadDelTexto)v : VelocidadDelTexto.Normal;
            }
            set
            {
                PlayerPrefs.SetInt(ClaveVelocidadTexto, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Por cuánto multiplicar la espera entre letra y letra. Los
        /// diálogos guardan su propia espera; esto solo la estira o la acorta.</summary>
        public static float MultiplicadorEsperaTexto
        {
            get
            {
                switch (VelocidadTexto)
                {
                    case VelocidadDelTexto.Lenta:  return 2f;
                    case VelocidadDelTexto.Rapida: return 0.35f;
                    default:                       return 1f;
                }
            }
        }

        /// <summary>Lenta → Normal → Rápida → Lenta. Es lo que hace el botón.</summary>
        public static VelocidadDelTexto SiguienteVelocidad(VelocidadDelTexto actual)
            => (VelocidadDelTexto)(((int)actual + 1) % 3);

        // ── Volumen ───────────────────────────────────────────────────────────

        /// <summary>
        /// Volumen general, de 0 (apagado) a 1 (todo). Empieza en 1: nadie espera
        /// abrir un juego y encontrárselo mudo.
        ///
        /// Va a <c>AudioListener.volume</c>, que multiplica a todo lo que suene en la
        /// escena, así que no hace falta que cada sonido lo consulte. Se guarda en
        /// cuanto se cambia, igual que los demás ajustes: el deslizador del menú lo
        /// escribe mientras se arrastra, que son unas cuantas escrituras seguidas, y
        /// aun así es preferible a perder el ajuste si el juego se cierra de golpe.
        /// </summary>
        public static float Volumen
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(ClaveVolumen, 1f));
            set
            {
                float v = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(ClaveVolumen, v);
                PlayerPrefs.Save();
                AudioListener.volume = v;
            }
        }

        /// <summary>El volumen en porcentaje, que es como se escribe en la pantalla
        /// de opciones: «Volumen: 70%».</summary>
        public static int VolumenEnPorcentaje => Mathf.RoundToInt(Volumen * 100f);

        // ── Pantalla completa ─────────────────────────────────────────────────

        public static bool PantallaCompleta
        {
            get => PlayerPrefs.GetInt(ClavePantallaCompleta, Screen.fullScreen ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ClavePantallaCompleta, value ? 1 : 0);
                PlayerPrefs.Save();
                AplicarPantalla(value);
            }
        }

        /// <summary>
        /// Cambia el modo <b>y</b> el tamaño. Solo apagar <c>Screen.fullScreen</c> deja
        /// una ventana de 1920×1080 en un monitor de 1920×1080: la barra de título
        /// queda fuera de la pantalla y parece que no pasó nada. Por eso al salir de
        /// pantalla completa la ventana se achica a 1280×720 (o a lo que quepa), y al
        /// volver se usa la resolución del monitor.
        ///
        /// En el editor no hace nada visible: la ventana Game no cambia de modo. Hay
        /// que probarlo en el .exe.
        /// </summary>
        private static void AplicarPantalla(bool completa)
        {
            int anchoMonitor = Display.main.systemWidth;
            int altoMonitor = Display.main.systemHeight;

            if (completa)
            {
                Screen.SetResolution(anchoMonitor, altoMonitor, FullScreenMode.FullScreenWindow);
                return;
            }

            // 16:9 y con margen, para que la barra de título y la de tareas se vean.
            int alto = Mathf.Min(AltoVentana, Mathf.RoundToInt(altoMonitor * 0.8f));
            int ancho = Mathf.RoundToInt(alto * 16f / 9f);
            Screen.SetResolution(ancho, alto, FullScreenMode.Windowed);
        }

        /// <summary>
        /// Deja el juego como lo dejó la última vez. Corre al arrancar, antes de la
        /// primera escena.
        ///
        /// La pantalla completa <b>no</b> se fuerza si nunca se tocó: cambiarle la
        /// ventana a alguien que no pidió nada es de mala educación, y en el editor
        /// además es molesto. La velocidad del texto no necesita aplicarse: los
        /// diálogos la leen cada vez que escriben.
        ///
        /// El volumen sí se aplica también en el editor, al revés que la pantalla: no
        /// molesta a nadie y, si no, bajarlo desde el menú y darle a Play volvería a
        /// dejarlo arriba, que parece que el ajuste no se guardó.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Aplicar()
        {
            AudioListener.volume = Volumen;

            if (Application.isEditor) return;

            if (PlayerPrefs.HasKey(ClavePantallaCompleta))
                AplicarPantalla(PantallaCompleta);
        }
    }
}
