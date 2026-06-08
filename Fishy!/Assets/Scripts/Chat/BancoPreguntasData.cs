using System.Collections.Generic;
using UnityEngine;

namespace Fishy.Chat
{
    // ─────────────────────────────────────────────────────────────────────────
    // Modelo de datos que refleja banco_preguntas.json (Luis González — MLOps)
    // Usado sólo para deserialización con JsonUtility; no instanciar a mano.
    // ─────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class BancoRaiz
    {
        public string version;
        public List<PreguntaBanco> preguntas = new List<PreguntaBanco>();
    }

    [System.Serializable]
    public class PreguntaBanco
    {
        public string id;
        public string hdu;               // "HDU-2" | "HDU-8"
        public string zona;              // "desconocidos" | "chat_simulado"
        public string npc_id;            // "NPC_01" | "NPC_02" | ""
        public string npc_nombre;        // "Alex" | "Valen" | ""
        public int    fase;              // 1–3 (null→0 por JsonUtility)
        public int    orden_en_fase;
        public string escenario_id;      // "CHAT_GROOMING_01" etc. (solo HDU-8)
        public string escenario_nombre;
        public List<HistorialPrevio> historial_previo = new List<HistorialPrevio>();
        public string categoria;         // "neutral" | "grooming_datos_personales" …
        public int    nivel_riesgo;      // 0–3
        public bool   es_mensaje_riesgo;
        public bool   es_fin_de_npc;
        public bool   es_fin_de_zona;
        public string mensaje_npc;
        public List<OpcionBanco> opciones_respuesta = new List<OpcionBanco>();
        public string narrativa_continuacion; // nextNodeId para avance automático
    }

    [System.Serializable]
    public class HistorialPrevio
    {
        public string remitente;   // "NPC" | "JUGADOR"
        public string npc_nombre;
        public string mensaje;
        public string categoria;
    }

    [System.Serializable]
    public class OpcionBanco
    {
        public string id;
        public string texto;
        public string tipo;                    // "insegura" | "segura_basica" | "segura_optima"
        public string consecuencia_narrativa;
        public int    impacto_puntuacion;      // -1 | 1 | 2
        public string siguiente_pregunta;      // puede ser null → fin de conversación
    }
}
