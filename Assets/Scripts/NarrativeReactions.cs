namespace Fishy
{
    // Tarea 2 — Ajustar diálogos según decisiones (HDU09 CA1, CA2, CA4)
    // Strings de reacción de Otto organizados por zona y tipo de decisión.
    internal static class NarrativeReactions
    {
        internal static string Positive(string optionType, string zone) =>
            optionType == "segura_optima" ? PositiveOptima(zone) : PositiveBasica(zone);

        internal static string PositiveOptima(string zone) => zone switch
        {
            "desconocidos" => "Otto anota algo en su cuaderno y asiente: \"¡Eso sí! Nueva pista desbloqueada.\"",
            "ciberacoso"   => "Otto levanta el sombrero: \"¡Buen ojo, detective! El misterio avanza.\"",
            "reto_viral"   => "Otto te guiña un ojo: \"Sabías que eso no olía bien. ¡Caso resuelto!\"",
            _              => "Otto cierra el cuaderno satisfecho. Nueva pista desbloqueada.",
        };

        internal static string PositiveBasica(string zone) => zone switch
        {
            "desconocidos" => "Otto asiente con la cabeza. La conversación sigue.",
            "ciberacoso"   => "Otto observa la pantalla y toma nota.",
            "reto_viral"   => "Otto sonríe tranquilo. Buen instinto.",
            _              => "Otto sigue de cerca. El misterio continúa.",
        };

        // CA4 — reacción positiva cuando el jugador corrige tras una insegura previa
        internal static string PositiveAfterRisk(string zone) => zone switch
        {
            "desconocidos" => "Otto sonríe de lado: \"¡Ahí está! Sabía que lo captarías.\"",
            "ciberacoso"   => "Otto señala el cuaderno: \"Bien. Todavía estamos a tiempo.\"",
            "reto_viral"   => "Otto suelta el aliento: \"¡Uf! Por poco. Pero lo lograste.\"",
            _              => "Otto asiente: \"Buen cambio. El misterio sigue adelante.\"",
        };

        // CA2 — consecuencia narrativa sin contenido atemorizante
        internal static string Consequence(string zone) => zone switch
        {
            "desconocidos" => "Otto frunce el ceño y escribe algo en su libreta. Esto no pinta bien...",
            "ciberacoso"   => "Otto mueve la cabeza y tapa parte de la pantalla con la mano.",
            "reto_viral"   => "Otto aparta la mirada un momento. La linterna parpadea.",
            _              => "Otto observa en silencio. Algo en esto se siente raro.",
        };
    }
}
