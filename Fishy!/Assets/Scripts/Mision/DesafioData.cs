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
    }
}
