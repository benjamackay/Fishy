using System;
using System.Collections.Generic;

namespace Fishy.Album
{
    /// <summary>
    /// Una evidencia del álbum: una señal de riesgo que el niño/a marcó bien en un
    /// caso del Modo Detective, con todo lo que hace falta para volver a contarla
    /// más tarde (HDU-12 CA1).
    ///
    /// <b>Por qué guarda tanto.</b> La tentación es guardar solo los dos ids
    /// —el caso y el mensaje— y resolver el resto a la hora de pintar. No se puede:
    /// el caso se carga del backend (o del respaldo de Resources) y puede no estar
    /// disponible cuando el niño/a abre el álbum. Así que la evidencia se guarda
    /// <i>completa</i>, como una foto del momento en que se ganó: eso es también lo
    /// que la hace legible sin conexión.
    ///
    /// <b>La identidad son dos campos.</b> <see cref="casoId"/> + <see cref="mensajeId"/>
    /// (ver <see cref="Clave"/>). Un mensaje solo puede ser evidencia una vez, y
    /// repetir el caso no duplica nada.
    /// </summary>
    [Serializable]
    public class EvidenciaAlbum
    {
        // ── Identidad ────────────────────────────────────────────────────────
        public string casoId;
        public string mensajeId;

        // ── Metadatos del caso (CA1) ─────────────────────────────────────────
        public string casoTitulo;
        public string casoZona;

        /// <summary>El "tipo de señal": las <c>etiquetas_ml</c> del caso
        /// (grooming, privacidad, ciberacoso…). Es lo que agrupa por temática en
        /// CA2. Viene del caso y no del mensaje porque los datos no traen un tipo
        /// por mensaje; ver el plan de HDU-12.</summary>
        public List<string> etiquetas = new List<string>();

        // ── La señal y su explicación (CA1, CA3) ─────────────────────────────
        public string autor;
        public string texto;
        public string explicacion;

        /// <summary>Cuándo se registró, en ticks UTC. Da un orden estable que no
        /// depende del orden en que se serializó la lista.</summary>
        public long registradaEn;

        /// <summary>
        /// Si la evidencia tiene el texto del mensaje, y no solo los ids.
        ///
        /// Una evidencia <b>incompleta</b> es un esqueleto: lo que queda cuando se
        /// sembró desde el servidor, que solo guarda <c>mensajes_marcados</c> (ids)
        /// y no los textos. Se completa en cuanto los datos del caso vuelven a
        /// estar a mano (ver <c>AlbumDetectivePuente</c>).
        /// </summary>
        public bool EstaCompleta => !string.IsNullOrWhiteSpace(texto);

        /// <summary>Qué título mostrar. Un esqueleto no sabe el nombre del caso
        /// todavía: antes de dejar un hueco en la pantalla, se enseña el id.</summary>
        public string TituloVisible => string.IsNullOrWhiteSpace(casoTitulo) ? casoId : casoTitulo;

        public string Clave => Clavear(casoId, mensajeId);

        public static string Clavear(string casoId, string mensajeId)
            => (casoId ?? "").Trim() + "|" + (mensajeId ?? "").Trim();

        /// <summary>Copia suelta. El álbum nunca se queda con la instancia que le
        /// pasaron: quien la construyó puede seguir modificándola después.</summary>
        public EvidenciaAlbum Copiar()
        {
            return new EvidenciaAlbum
            {
                casoId = casoId,
                mensajeId = mensajeId,
                casoTitulo = casoTitulo,
                casoZona = casoZona,
                etiquetas = etiquetas == null ? new List<string>() : new List<string>(etiquetas),
                autor = autor,
                texto = texto,
                explicacion = explicacion,
                registradaEn = registradaEn,
            };
        }
    }

    /// <summary>
    /// Envoltorio de lo que va a PlayerPrefs.
    ///
    /// <b>Por qué JSON y no una lista separada por comas</b> como
    /// <c>HistorialDeObjetivos</c>: allá cada dato es un id sin espacios, acá cada
    /// registro son nueve campos de texto libre —el mensaje de un caso trae comas,
    /// comillas y a veces saltos de línea—. Un formato separado por comas habría
    /// que escaparlo a mano, y ese escapado es justo el tipo de código que falla
    /// con el primer mensaje raro.
    ///
    /// <b>Tolerancia hacia adelante</b>, las tres reglas de <c>JsonUtility</c> que
    /// esto aprovecha a propósito:
    /// <list type="number">
    /// <item>Ignora las claves que no conoce, así que un archivo escrito por una
    /// versión futura con campos de más se lee sin romperse.</item>
    /// <item>Deja los campos ausentes en su valor por omisión —y una
    /// <c>List&lt;T&gt;</c> ausente queda en <c>null</c>, NO en lista vacía—, de
    /// ahí que al cargar haya que normalizar.</item>
    /// <item><see cref="version"/> se lee pero no se exige: si no se reconoce, se
    /// carga igual y se avisa. Descartar el archivo sería perder las evidencias de
    /// un niño/a, que es exactamente lo que esta clase existe para evitar.</item>
    /// </list>
    /// </summary>
    [Serializable]
    internal class AlbumGuardado
    {
        public int version = AlbumDeEvidencias.VersionFormato;
        public List<EvidenciaAlbum> evidencias = new List<EvidenciaAlbum>();
    }
}
