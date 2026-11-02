using System;
using System.Collections.Generic;

namespace Fishy.Detective
{
    [Serializable]
    public class DetectiveMessage
    {
        public string id;
        public string autor;
        public string texto;
        public bool esRiesgo;
        public bool esAmbiguo;
    }

    [Serializable]
    public class ExplicacionEntry
    {
        public string mensajeId;
        public string explicacion;
    }

    [Serializable]
    public class DetectiveCase
    {
        public string caseId;

        // HDU-12 — El backend siempre los sirvió (CasoDetectiveSerializer los lista)
        // y el loader los tiraba. Son los metadatos que cada evidencia del álbum
        // hereda de su caso: cómo se llama el caso, en qué zona pasó y el TIPO DE
        // SEÑAL, que son las etiquetas_ml del banco (grooming, ciberacoso…).
        //
        // El nombre del campo es "etiquetasMl" y no "etiquetas_ml" porque el
        // respaldo local lo lee JsonUtility.FromJson<DetectiveCase>, que empareja
        // por nombre exacto de campo y no traduce snake_case: con la clave en
        // snake_case la lista quedaría vacía EN SILENCIO, y justo por la ruta local,
        // que es por la que corren los arneses.
        public string titulo;
        public string zona;
        public List<string> etiquetasMl = new List<string>();

        public string npcObservado1;
        public string npcObservado2;
        public string permisoPlayerText;
        public string permisoNpcNombre;
        public string permisoNpcResponse;
        public List<DetectiveMessage> mensajes;
        public List<ExplicacionEntry> explicacionGuiada;

        // HDU-11 — Recompensa que trajo el backend con el caso (todavía no la
        // manda: ver REQUISITOS_BD.md, seccion C). Vacío a
        // propósito para los casos cargados del respaldo local en Resources, que
        // no traen estos campos: DetectiveCaseManager cae a
        // CatalogoRecompensasDetective cuando TieneRecompensa es false.
        public string recompensaItemId;
        public string recompensaNombre;
        public string recompensaAccesorioHdu06;
        public float recompensaUmbralAciertos;
        public bool recompensaNoDuplicaAlRepetir;

        public bool TieneRecompensa => !string.IsNullOrWhiteSpace(recompensaItemId);

        /// <summary>
        /// La explicación guiada, indexada por mensajeId.
        ///
        /// Vive acá y no en quien la usa porque la leen dos: el resultado del caso
        /// (para contar lo que no se identificó) y el álbum de evidencias (HDU-12).
        /// Y porque las dos trampas de este dato se tapan de una sola vez:
        ///
        /// 1. <see cref="explicacionGuiada"/> puede ser <b>null</b>. JsonUtility deja
        ///    en null las listas cuya clave no está en el JSON, y el respaldo local
        ///    del caso 03 no la traía: hacerle .ToDictionary() reventaba con un
        ///    NullReferenceException al pulsar "Confirmar", sin conexión.
        /// 2. Un mensajeId <b>repetido</b> en el JSON tiraba ArgumentException. Acá
        ///    gana el primero y se sigue adelante: una explicación duplicada es un
        ///    descuido de los datos, no algo que deba cortarle el caso a un niño/a.
        /// </summary>
        public Dictionary<string, string> ExplicacionesPorMensaje()
        {
            var porMensaje = new Dictionary<string, string>();
            if (explicacionGuiada == null) return porMensaje;

            foreach (ExplicacionEntry entrada in explicacionGuiada)
            {
                if (entrada == null || string.IsNullOrEmpty(entrada.mensajeId)) continue;
                if (porMensaje.ContainsKey(entrada.mensajeId)) continue;

                porMensaje[entrada.mensajeId] = entrada.explicacion;
            }

            return porMensaje;
        }
    }
}