using System.Collections.Generic;

namespace Fishy.Album
{
    /// <summary>
    /// Un montón de evidencias con un rótulo: el resultado de agrupar (HDU-12 CA2).
    /// Lo devuelve <see cref="AlbumDeEvidencias.PorCaso"/> o
    /// <see cref="AlbumDeEvidencias.PorTematica"/>.
    ///
    /// <b>El núcleo agrupa, la UI nombra.</b> Agrupando por temática,
    /// <see cref="titulo"/> viene con la etiqueta cruda del banco
    /// ("retos_virales"), no con el nombre bonito: traducirla necesita el
    /// diccionario de textos, que vive del lado de la UI (Assembly-CSharp) y que
    /// este ensamblado no puede ver. Agrupando por caso, en cambio, el título es ya
    /// el del caso, que sí viaja dentro de cada evidencia.
    /// </summary>
    public class GrupoAlbum
    {
        /// <summary>Con qué se agrupó: el casoId, o la etiqueta.</summary>
        public string clave;

        /// <summary>Rótulo del grupo. Por caso, el título del caso; por temática,
        /// la etiqueta cruda (ver la nota de la clase).</summary>
        public string titulo;

        /// <summary>Segunda línea: la zona del caso y cuántas evidencias hay.
        /// Vacío si no hay nada que decir.</summary>
        public string subtitulo;

        public List<EvidenciaAlbum> evidencias = new List<EvidenciaAlbum>();

        public int Cantidad => evidencias == null ? 0 : evidencias.Count;
    }
}
