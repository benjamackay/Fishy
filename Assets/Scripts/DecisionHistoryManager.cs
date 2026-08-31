using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishy
{
    // Tarea 1 — Revisar flujo narrativo completo (HDU09 CA3, CA4)
    // Registra cada decisión del jugador durante la sesión y calcula el patrón por zona.
    public class DecisionHistoryManager : MonoBehaviour
    {
        public static DecisionHistoryManager Instance { get; private set; }

        public struct DecisionRecord
        {
            public string questionId;
            public string optionType; // "segura_optima" | "segura_basica" | "insegura"
            public string zone;
            public float  timestamp;
        }

        [Serializable]
        public class ZoneSummary
        {
            public string zone;
            public int    seguraOptimaCount;
            public int    seguraBasicaCount;
            public int    inseguraCount;
            public string pattern; // "excelente" | "bueno" | "necesita_refuerzo"
        }

        private readonly List<DecisionRecord> _history = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RecordDecision(string questionId, string optionType, string zone)
        {
            _history.Add(new DecisionRecord
            {
                questionId = questionId,
                optionType = optionType,
                zone       = zone,
                timestamp  = Time.realtimeSinceStartup
            });
        }

        // CA4 — NPC puede consultar si hubo decisión insegura previa en la zona
        public bool HasPriorInseguraInZone(string zone)
        {
            foreach (var r in _history)
                if (r.zone == zone && r.optionType == "insegura") return true;
            return false;
        }

        // CA3 — resumen de decisiones por zona, listo para enviar a HDU13
        public ZoneSummary GetZoneSummary(string zone)
        {
            var s = new ZoneSummary { zone = zone };
            foreach (var r in _history)
            {
                if (r.zone != zone) continue;
                switch (r.optionType)
                {
                    case "segura_optima": s.seguraOptimaCount++; break;
                    case "segura_basica": s.seguraBasicaCount++; break;
                    case "insegura":      s.inseguraCount++;     break;
                }
            }

            int   total = s.seguraOptimaCount + s.seguraBasicaCount + s.inseguraCount;
            float pct   = total == 0
                ? 0f
                : (float)(s.seguraOptimaCount * 2 + s.seguraBasicaCount) / (total * 2);

            s.pattern = pct >= 0.8f ? "excelente"
                      : pct >= 0.5f ? "bueno"
                      : "necesita_refuerzo";

            return s;
        }
    }
}
