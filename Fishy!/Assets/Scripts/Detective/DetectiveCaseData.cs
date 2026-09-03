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
        public string npcObservado1;
        public string npcObservado2;
        public string permisoPlayerText;
        public string permisoNpcNombre;
        public string permisoNpcResponse;
        public List<DetectiveMessage> mensajes;
        public List<ExplicacionEntry> explicacionGuiada;
    }
}