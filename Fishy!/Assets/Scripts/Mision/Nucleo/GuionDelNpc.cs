using System;
using System.Collections.Generic;

namespace Fishy.Mision
{
    /// <summary>
    /// Un eslabón del guion de un NPC: una misión que entrega y lo que dice alrededor
    /// de ella.
    /// </summary>
    [Serializable]
    public class PasoDelGuion
    {
        /// <summary>Ficha de la misión, si está cableada a mano en la escena.</summary>
        public DesafioData ficha;

        /// <summary>Id en el catálogo. Es el caso normal: la misión vive en los datos
        /// y aquí solo se la nombra.</summary>
        public string misionId = "";

        /// <summary>Lo que dice al entregarla. Al cerrar ESTE diálogo —y solo este— la
        /// misión se registra.</summary>
        public string dialogoDeEntrega = "";

        /// <summary>Lo que dice mientras esa misión está en curso: el recordatorio de
        /// lo que falta. Vacío = no tiene nada que añadir y se pasa al siguiente
        /// candidato (una regla por misión activa, o la despedida).</summary>
        public string dialogoMientras = "";

        /// <summary>
        /// Misiones que además tienen que estar completadas para que este paso se
        /// pueda entregar.
        ///
        /// La cadena ya exige que el paso anterior esté completo, así que esto es para
        /// las puertas que vienen de fuera: "Huemul no suelta la secundaria hasta que
        /// Coipo haya dado su testimonio". Mientras no se cumplan, el NPC NO salta al
        /// paso siguiente —entregaría la cadena en desorden—: se queda esperando y
        /// dice lo que diría si no tuviera nada que dar.
        /// </summary>
        public List<string> requiereCompletadas = new List<string>();

        /// <summary>
        /// El id que se mira: el de la ficha si se arrastró una, si no el escrito. Es
        /// el mismo criterio y el mismo nombre que en <c>PresenciaSegunMision</c>, a
        /// propósito: quien configure las dos cosas en el mismo NPC encuentra la misma
        /// regla.
        /// </summary>
        public string IdMirado =>
            ficha != null && !string.IsNullOrWhiteSpace(ficha.desafioId)
                ? ficha.desafioId.Trim()
                : (misionId ?? "").Trim();
    }

    /// <summary>Qué dice el NPC cuando no tiene nada que entregar y hay una misión
    /// concreta en curso en otra parte del mundo.</summary>
    [Serializable]
    public class ReglaPorMisionActiva
    {
        /// <summary>Id de la misión activa que dispara esta regla.</summary>
        public string misionId = "";

        /// <summary>Lo que dice mientras esa misión sea la activa.</summary>
        public string dialogo = "";
    }

    /// <summary>En qué situación está el NPC cuando se le habla.</summary>
    public enum QueDiceElNpc
    {
        /// <summary>No hay nada que decir: se deja el diálogo que tuviera.</summary>
        Nada,

        /// <summary>Tiene una misión nueva que dar.</summary>
        Entregar,

        /// <summary>Ya la dio y sigue en curso: recuerda lo que falta.</summary>
        Recordar,

        /// <summary>No tiene nada que dar, pero sí algo que decir sobre la misión que
        /// el niño/a lleva entre manos.</summary>
        Comentar,

        /// <summary>Se le acabó la cadena: ya entregó todo lo que tenía.</summary>
        Despedirse,
    }

    /// <summary>Lo que el NPC diría si se le hablara ahora mismo.</summary>
    public readonly struct LoQueDiceElNpc
    {
        public readonly QueDiceElNpc Que;

        /// <summary>Id del diálogo del banco que toca. Vacío solo con
        /// <see cref="QueDiceElNpc.Nada"/>.</summary>
        public readonly string DialogoId;

        /// <summary>Id de la misión que hay que registrar al cerrar ese diálogo. Solo
        /// viene con <see cref="QueDiceElNpc.Entregar"/>.</summary>
        public readonly string MisionAEntregar;

        /// <summary>En qué paso de la cadena va, o -1 si no hay ninguno pendiente.
        /// Es para el log y para que el componente sepa qué ficha usar.</summary>
        public readonly int Paso;

        public LoQueDiceElNpc(QueDiceElNpc que, string dialogoId, string misionAEntregar, int paso)
        {
            Que = que;
            DialogoId = dialogoId ?? "";
            MisionAEntregar = misionAEntregar ?? "";
            Paso = paso;
        }

        public static readonly LoQueDiceElNpc Nada =
            new LoQueDiceElNpc(QueDiceElNpc.Nada, "", "", -1);
    }

    /// <summary>
    /// Decide qué diálogo dice un NPC y qué misión entrega, a partir del estado de las
    /// misiones. Es la cabeza de <c>CadenaDeMisionesDelNpc</c>.
    ///
    /// <b>De estado, no de cuentas.</b> La forma anterior de encadenar conversaciones
    /// —<c>DialogoNeutroNPC.dialogosSiguientes</c>— avanza al terminar cada charla, así
    /// que cuenta veces que se habló. Eso se desincroniza del juego en cuanto el
    /// niño/a le habla dos veces seguidas al mismo NPC: Huemul pasaría a la
    /// conversación de la segunda misión sin que la primera esté hecha. Aquí el
    /// diálogo es una FUNCIÓN del estado de las misiones, así que es correcto en todo
    /// momento, sobrevive a cargar la partida y no se puede desfasar. Es la misma
    /// distinción que ya documenta <c>PresenciaSegunMision</c> frente a
    /// <c>DisparadorDeMision</c>: aquello es de flanco y esto es de estado.
    ///
    /// <b>Por qué vive aquí y no en el MonoBehaviour.</b> Porque esta es la parte que
    /// decide, y un .asmdef de pruebas no puede referenciar Assembly-CSharp. Así el
    /// orden de prioridades —lo de abajo— se comprueba con NUnit en vez de a mano con
    /// una partida delante.
    ///
    /// El orden importa y es este:
    /// <list type="number">
    /// <item>Si tiene una misión nueva que dar, la da. Tener algo nuevo que entregar
    /// gana a cualquier comentario: es lo que mueve la partida.</item>
    /// <item>Si no, y hay una regla para la misión que está activa ahora, la dice. Un
    /// NPC que comenta lo que el niño/a trae entre manos se siente vivo.</item>
    /// <item>Si no, el recordatorio de la misión suya que sigue en curso.</item>
    /// <item>Si no queda ningún paso pendiente, la despedida.</item>
    /// </list>
    /// </summary>
    public static class GuionDelNpc
    {
        /// <summary>
        /// Qué diría el NPC ahora mismo.
        ///
        /// <paramref name="estadoDe"/> es <c>MissionManager.GetEstado</c>: devuelve
        /// null para una misión que todavía no se entregó. Se recibe como función y no
        /// se pide el gestor a propósito, que es lo que deja probar esto sin escena.
        /// </summary>
        public static LoQueDiceElNpc Resolver(
            IReadOnlyList<PasoDelGuion> pasos,
            Func<string, EstadoDesafio?> estadoDe,
            string misionActivaId = null,
            IReadOnlyList<ReglaPorMisionActiva> reglasPorMisionActiva = null,
            string dialogoAlFinal = null)
        {
            if (estadoDe == null) return LoQueDiceElNpc.Nada;

            // El primer paso que no esté completado es el que manda. Los de más
            // adelante no se miran siquiera: la cadena se entrega en orden.
            string recordatorio = null;
            int pasoPendiente = -1;

            if (pasos != null)
            {
                for (int i = 0; i < pasos.Count; i++)
                {
                    PasoDelGuion paso = pasos[i];
                    string id = paso?.IdMirado;

                    // Un paso sin misión es un hueco de configuración, no el final de
                    // la cadena: se salta, y de avisar se encarga el componente.
                    if (string.IsNullOrEmpty(id)) continue;

                    if (estadoDe(id) == EstadoDesafio.Completado) continue;

                    pasoPendiente = i;

                    if (estadoDe(id) == null && PuertasAbiertas(paso, estadoDe))
                    {
                        return new LoQueDiceElNpc(QueDiceElNpc.Entregar,
                            paso.dialogoDeEntrega, id, i);
                    }

                    // Entregada y en curso, o todavía con puertas cerradas: en los dos
                    // casos lo que le queda es hablar.
                    recordatorio = paso.dialogoMientras;
                    break;
                }
            }

            string comentario = DialogoDeLaRegla(reglasPorMisionActiva, misionActivaId);
            if (!string.IsNullOrWhiteSpace(comentario))
                return new LoQueDiceElNpc(QueDiceElNpc.Comentar, comentario, "", pasoPendiente);

            if (!string.IsNullOrWhiteSpace(recordatorio))
                return new LoQueDiceElNpc(QueDiceElNpc.Recordar, recordatorio, "", pasoPendiente);

            // Nada pendiente y nada que comentar: se despide. Con la cadena a medias
            // —hay paso pendiente pero sin recordatorio escrito— no se despide, que
            // sonaría a que ya terminó algo que no ha terminado.
            if (pasoPendiente < 0 && !string.IsNullOrWhiteSpace(dialogoAlFinal))
                return new LoQueDiceElNpc(QueDiceElNpc.Despedirse, dialogoAlFinal, "", -1);

            return LoQueDiceElNpc.Nada;
        }

        /// <summary>
        /// Si las misiones que este paso exige por fuera de la cadena están todas
        /// completadas. Sin exigencias, abiertas.
        /// </summary>
        private static bool PuertasAbiertas(PasoDelGuion paso, Func<string, EstadoDesafio?> estadoDe)
        {
            if (paso.requiereCompletadas == null) return true;

            foreach (string requisito in paso.requiereCompletadas)
            {
                if (string.IsNullOrWhiteSpace(requisito)) continue;
                if (estadoDe(requisito.Trim()) != EstadoDesafio.Completado) return false;
            }

            return true;
        }

        /// <summary>
        /// El diálogo de la primera regla que hable de la misión activa.
        ///
        /// La primera y no la última: una lista ordenada deja poner lo específico
        /// arriba, que es como se lee de arriba abajo en el inspector.
        /// </summary>
        private static string DialogoDeLaRegla(
            IReadOnlyList<ReglaPorMisionActiva> reglas, string misionActivaId)
        {
            if (reglas == null || string.IsNullOrWhiteSpace(misionActivaId)) return null;

            string activa = misionActivaId.Trim();

            for (int i = 0; i < reglas.Count; i++)
            {
                ReglaPorMisionActiva regla = reglas[i];
                if (regla == null || string.IsNullOrWhiteSpace(regla.dialogo)) continue;

                if (string.Equals((regla.misionId ?? "").Trim(), activa, StringComparison.Ordinal))
                    return regla.dialogo;
            }

            return null;
        }
    }
}
