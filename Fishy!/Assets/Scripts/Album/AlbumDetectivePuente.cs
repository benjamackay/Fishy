using System;
using System.Collections.Generic;
using System.Linq;
using Fishy.Album;
using UnityEngine;

namespace Fishy.Detective
{
    /// <summary>
    /// Traduce un caso del Modo Detective a evidencias del álbum (HDU-12).
    ///
    /// <b>Por qué existe este intermediario.</b> El núcleo del álbum
    /// (<see cref="AlbumDeEvidencias"/>) vive en su propio ensamblado, Fishy.Album, y
    /// a propósito no conoce nada del Modo Detective: ni <see cref="DetectiveCase"/>,
    /// ni la mochila, ni la UI. Eso es lo que permite que las pruebas NUnit en
    /// PlayMode lo vean —un asmdef de pruebas no puede referenciar Assembly-CSharp—.
    /// La frontera, entonces, es de datos planos, y todo lo que sabe convertir un caso
    /// en evidencias está acá, del lado de Assembly-CSharp, que sí ve las dos cosas.
    /// </summary>
    public static class AlbumDetectivePuente
    {
        /// <summary>
        /// CA1 — Las señales que el niño/a marcó bien, al álbum. Devuelve cuántas
        /// evidencias quedaron nuevas o más completas.
        /// </summary>
        public static int RegistrarBienMarcadas(DetectiveCase caso, IEnumerable<string> marcados)
        {
            if (caso == null || caso.mensajes == null || marcados == null) return 0;

            var marcadosSet = new HashSet<string>(marcados.Where(id => !string.IsNullOrWhiteSpace(id)));
            if (marcadosSet.Count == 0) return 0;

            Dictionary<string, string> explicaciones = caso.ExplicacionesPorMensaje();

            List<EvidenciaAlbum> evidencias = caso.mensajes
                .Where(m => EsSenalValida(m) && marcadosSet.Contains(m.id))
                .Select(m => Traducir(caso, m, explicaciones))
                .ToList();

            return AlbumDeEvidencias.RegistrarVarias(evidencias);
        }

        /// <summary>
        /// Completa con los datos de este caso las evidencias que solo tenían ids, y
        /// descarta los esqueletos que no correspondan a ninguna señal válida (ver
        /// <see cref="AlbumDeEvidencias.ConciliarConElCaso"/>).
        ///
        /// Se llama cada vez que un caso se carga, por cualquier vía: en ese momento
        /// los textos ya están en la mano y aprovecharlos no cuesta nada.
        /// </summary>
        public static int ConciliarDesdeElCaso(DetectiveCase caso)
        {
            if (caso == null || caso.mensajes == null || string.IsNullOrWhiteSpace(caso.caseId))
                return 0;

            Dictionary<string, string> explicaciones = caso.ExplicacionesPorMensaje();

            List<EvidenciaAlbum> senales = caso.mensajes
                .Where(EsSenalValida)
                .Select(m => Traducir(caso, m, explicaciones))
                .ToList();

            return AlbumDeEvidencias.ConciliarConElCaso(caso.caseId, senales);
        }

        /// <summary>
        /// Intenta completar todo lo que siga siendo un esqueleto, leyendo los casos
        /// del respaldo que viaja en el juego. Devuelve cuántas evidencias cambiaron.
        ///
        /// <b>Por qué no se le pregunta al servidor.</b> Lo que el servidor guarda de
        /// un caso jugado son los ids de los mensajes marcados y nada más: ni el texto
        /// del mensaje, ni su explicación, ni el nombre del caso. Para rellenar eso
        /// habría que pedirle los casos completos (<c>GET /casos-detective/</c>), y no
        /// hace falta: esos textos <b>ya vienen empaquetados</b> en Resources, los
        /// mismos que el juego usa cuando no hay conexión. Y el servidor de pruebas
        /// atiende una petición a la vez, a unos dos segundos cada una, con los
        /// <see cref="DetectiveLauncher"/> de la escena ya pidiendo el progreso: una
        /// petición más, para un dato que tenemos en disco, se nota.
        ///
        /// Si algún día el banco tiene casos sin respaldo local, ahí sí valdría la
        /// pena pedirlos — <see cref="ConciliarDesdeElCaso"/> recibe el caso de
        /// cualquier fuente.
        /// </summary>
        public static int ConciliarLoQueFalte()
        {
            int cambios = 0;
            foreach (string casoId in AlbumDeEvidencias.CasosConEsqueletos())
                cambios += ConciliarDesdeElRespaldoLocal(casoId);

            return cambios;
        }

        /// <summary>Completa las evidencias de un caso con su respaldo de Resources.
        /// Si ese caso no tiene respaldo, no hace nada y no se queja: es un dato que
        /// falta, no un error.</summary>
        public static int ConciliarDesdeElRespaldoLocal(string casoId)
        {
            if (string.IsNullOrWhiteSpace(casoId)) return 0;

            DetectiveCase caso = DetectiveCaseLoader.LoadLocal(RecursoLocalDe(casoId), avisarSiFalta: false);
            if (caso == null) return 0;

            return ConciliarDesdeElCaso(caso);
        }

        /// <summary>
        /// "DC_CASO_01" → "detective_caso_01", el nombre con el que ese caso está
        /// guardado en Resources.
        ///
        /// Es una convención de nombres y no una tabla, que es lo que la hace frágil:
        /// un caso nuevo que no la siga, simplemente no se podrá completar sin
        /// conexión (y se verá degradado, no roto). Si llega a pasar seguido, lo que
        /// corresponde es un <c>Resources/detective_casos_locales.json</c> con el mapa
        /// explícito, como hace <see cref="CatalogoRecompensasDetective"/>.
        /// </summary>
        public static string RecursoLocalDe(string casoId)
        {
            if (string.IsNullOrWhiteSpace(casoId)) return "";

            return casoId.Trim().ToLowerInvariant().Replace("dc_caso_", "detective_caso_");
        }

        /// <summary>
        /// Qué puede ser evidencia: una señal de riesgo que NO sea ambigua.
        ///
        /// El mensaje ambiguo queda fuera por el mismo motivo por el que no puntúa
        /// (ver la nota del banco y <c>DetectiveCaseManager.CalcularResultado</c>):
        /// marcarlo no es haber detectado una señal. Guardarlo en el álbum como un
        /// acierto le enseñaría al niño/a justo lo contrario de lo que le enseña el
        /// resultado del caso — que sospechar de todo no es lo mismo que darse cuenta.
        /// </summary>
        private static bool EsSenalValida(DetectiveMessage mensaje)
            => mensaje != null && !string.IsNullOrWhiteSpace(mensaje.id) &&
               mensaje.esRiesgo && !mensaje.esAmbiguo;

        private static EvidenciaAlbum Traducir(DetectiveCase caso, DetectiveMessage mensaje,
            Dictionary<string, string> explicaciones)
        {
            explicaciones.TryGetValue(mensaje.id, out string explicacion);

            return new EvidenciaAlbum
            {
                casoId = caso.caseId,
                mensajeId = mensaje.id,
                casoTitulo = caso.titulo,
                casoZona = caso.zona,
                etiquetas = caso.etiquetasMl == null
                    ? new List<string>()
                    : new List<string>(caso.etiquetasMl),
                autor = mensaje.autor,
                texto = mensaje.texto,
                explicacion = explicacion,
                registradaEn = DateTime.UtcNow.Ticks,
            };
        }
    }
}
