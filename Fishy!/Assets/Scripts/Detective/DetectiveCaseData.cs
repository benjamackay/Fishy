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

        // HDU-11 — Recompensa que trajo el backend con el caso (todavía no la
        // manda: ver Backend/PENDIENTE_HDU11_RECOMPENSAS_DETECTIVE.md). Vacío a
        // propósito para los casos cargados del respaldo local en Resources, que
        // no traen estos campos: DetectiveCaseManager cae a
        // CatalogoRecompensasDetective cuando TieneRecompensa es false.
        public string recompensaItemId;
        public string recompensaNombre;
        public string recompensaAccesorioHdu06;
        public float recompensaUmbralAciertos;
        public bool recompensaNoDuplicaAlRepetir;

        public bool TieneRecompensa => !string.IsNullOrWhiteSpace(recompensaItemId);
    }
}