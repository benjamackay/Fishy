using UnityEngine;

namespace Fishy.Mision
{
    /// <summary>
    /// HDU-1 — Ficha de datos de un desafío/misión desbloqueable por un objeto o NPC.
    ///
    /// Crear vía menú: Assets → Create → Fishy → Mision → Nuevo Desafio.
    /// El <see cref="desafioId"/> debe ser único en todo el juego (se usa para
    /// registrar/consultar el estado en <see cref="MissionManager"/> y para la
    /// persistencia local en PlayerPrefs).
    /// </summary>
    [CreateAssetMenu(fileName = "DesafioData", menuName = "Fishy/Mision/Nuevo Desafio")]
    public class DesafioData : ScriptableObject
    {
        [Tooltip("Identificador único del desafío. Ej: 'HDU1_PLAYA_LINTERNA'.")]
        public string desafioId;

        [Tooltip("Título corto mostrado en el panel de misión activa.")]
        public string titulo;

        [TextArea]
        [Tooltip("Descripción/pista del desafío (opcional, para tooltips o detalle).")]
        public string descripcion;

        [Tooltip("Ícono opcional para el panel de misión activa.")]
        public Sprite icono;

        [Header("HDU-16 — Guía hacia dónde ir")]
        [Tooltip("Id de la zona a la que hay que ir para avanzar esta misión: los mismos " +
                 "ids que usan ZonaMundo y BlockedZone (zona_1, zona_2, zona_3). " +
                 "Vacío = esta misión no señala ninguna zona y no dibuja indicador.")]
        public string zonaObjetivo;

        [Tooltip("Lugar en la historia. Menor va antes: la misión activa es la disponible " +
                 "con el orden más bajo. Deja huecos (10, 20, 30…) para poder intercalar " +
                 "una misión nueva sin renumerar las demás.")]
        public int orden = 100;
    }
}
