using UnityEngine;
using System;

namespace Fishy.Chat
{
    /// <summary>Cómo se compara una <see cref="Fishy.World.VariablesJugador"/> contra un valor.</summary>
    public enum Comparador
    {
        MayorOIgual,
        Igual,
        MenorOIgual,
        Mayor,
        Menor,
    }

    /// <summary>
    /// Una alternativa de contenido para cuando una variable de
    /// <see cref="Fishy.World.VariablesJugador"/> cumple una condición.
    ///
    /// Es el mecanismo general para que un NPC cambie de qué habla según lo que hizo
    /// el jugador antes, sin que el propio banco de preguntas tenga que saber nada de
    /// condiciones: la variable se calcula aparte (hoy, derivándola del historial que
    /// ya se guarda en el backend) y aquí solo se decide qué escenario_id mostrar.
    ///
    /// El campo base del lanzador (p.ej. <c>escenarioIds</c> de
    /// <see cref="Fishy.Phone.PhoneChatLauncher"/>) sigue siendo el contenido por
    /// defecto: si la lista de variantes está vacía, o ninguna se cumple, se usa el
    /// campo base tal cual. Reemplaza por completo, no combina — un NPC dice una cosa
    /// u otra, no las dos.
    /// </summary>
    [Serializable]
    public class VarianteSegunVariable
    {
        [Tooltip("Nombre de la variable a consultar. Ej: 'presion_social_reto_viral'. " +
                 "Ver las constantes de VariablesJugador para los nombres disponibles.")]
        public string variable = "";

        public Comparador comparador = Comparador.MayorOIgual;

        [Tooltip("Valor contra el que se compara la variable.")]
        public int valor;

        [Tooltip("escenario_id del banco (o varios separados por coma) a usar si esta " +
                 "condición se cumple. Reemplaza por completo al campo base de arriba.")]
        public string escenarioIds = "";

        /// <summary>Se evalúan en orden y gana la primera que se cumpla — por eso el
        /// orden de la lista en el inspector importa: de más específica a más general.</summary>
        public bool Cumple(int valorActual)
        {
            switch (comparador)
            {
                case Comparador.MayorOIgual: return valorActual >= valor;
                case Comparador.Igual:       return valorActual == valor;
                case Comparador.MenorOIgual: return valorActual <= valor;
                case Comparador.Mayor:       return valorActual > valor;
                case Comparador.Menor:       return valorActual < valor;
                default:                     return false;
            }
        }
    }
}
