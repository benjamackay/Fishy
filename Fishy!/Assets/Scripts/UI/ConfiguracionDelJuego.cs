using UnityEngine;

namespace Fishy.UI
{
    /// <summary>
    /// HDU-17 CA2 — Lo que el niño/a puede ajustar desde el menú de pausa, y dónde
    /// queda guardado.
    ///
    /// <b>Son dos ajustes y no diez a propósito.</b> El criterio pide que la pausa
    /// ofrezca «Configuración»; lo que hay detrás tiene que ser algo que un niño/a de
    /// 9 a 13 entienda de una mirada y pueda deshacer. Un volumen y una pantalla
    /// completa se explican solos. Lo que no se pone: nada de idioma (el juego está
    /// solo en español), nada de calidad gráfica (elegir «bajo» y no saber por qué el
    /// juego se ve feo es una trampa), y nada que toque la partida.
    ///
    /// <b>Va en PlayerPrefs y no al backend.</b> Son preferencias del aparato, no de
    /// la cuenta: el volumen al que alguien juega en la sala de computación del
    /// colegio no tiene por qué seguirlo a su casa. Además así no se le manda nada al
    /// servidor sobre un menor que no haga falta mandar — Ley 19.628, principio de
    /// finalidad: solo se trata el dato que el servicio necesita.
    ///
    /// <b>Sin partida ni perfil en la clave</b>, al revés que el álbum: el volumen es
    /// del equipo y lo comparten los hermanos que juegan en él. Separarlo por partida
    /// obligaría a configurar el sonido de nuevo con cada perfil.
    /// </summary>
    public static class ConfiguracionDelJuego
    {
        public const string Prefijo = "Fishy.Config.";

        private const string ClaveVolumen = Prefijo + "Volumen";
        private const string ClavePantallaCompleta = Prefijo + "PantallaCompleta";

        /// <summary>Volumen general, de 0 a 1.</summary>
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

        public static bool PantallaCompleta
        {
            get => PlayerPrefs.GetInt(ClavePantallaCompleta, Screen.fullScreen ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ClavePantallaCompleta, value ? 1 : 0);
                PlayerPrefs.Save();
                Screen.fullScreen = value;
            }
        }

        /// <summary>
        /// Deja el juego como lo dejó la última vez. Corre al arrancar, antes de la
        /// primera escena: si esperara a que alguien abra la configuración, el primer
        /// sonido saldría al volumen de fábrica aunque estuviera guardado en silencio.
        ///
        /// La pantalla completa <b>no</b> se fuerza si nunca se tocó: cambiarle la
        /// ventana a alguien que no pidió nada es de mala educación, y en el editor
        /// además es molesto.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Aplicar()
        {
            AudioListener.volume = Volumen;

            if (PlayerPrefs.HasKey(ClavePantallaCompleta))
                Screen.fullScreen = PantallaCompleta;
        }
    }
}
