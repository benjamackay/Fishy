using System.Collections.Generic;

namespace Fishy
{
    // Tarea 2 — Crear respuestas base de NPC
    public static class NpcBaseResponses
    {
        // NPC Guia — entrega pistas de mision al comienzo del mundo
        public static readonly List<string> NpcGuiaLineas = new()
        {
            "hola! soy la guia de este mundo, te puedo ayudar a encontrar lo que necesitas",
            "explora la zona y busca objetos brillantes, cada uno tiene una pista para tu mision",
            "si encuentras algo raro o que te hace sentir incomodo, siempre puedes pedirme ayuda",
        };
        public static readonly string NpcGuiaPista =
            "MISION ACTIVA: Encuentra los 3 objetos clave escondidos en la zona.";
        public static readonly string NpcGuiaMisionId = "MISION_EXPLORACION_01";

        // NPC Informante — desbloquea la mision de investigacion
        public static readonly List<string> NpcInformanteLineas = new()
        {
            "ey, te estaba esperando, tengo informacion importante",
            "en esta zona hay alguien que no es quien dice ser, ten cuidado con quien confias en internet",
        };
        public static readonly string NpcInformantePista =
            "MISION DESBLOQUEADA: Identifica quien es el desconocido en la zona.";
        public static readonly string NpcInformanteMisionId = "MISION_INVESTIGACION_01";
    }
}
