using System;
using System.Collections;
using UnityEngine;

namespace Fishy
{
    // Tarea 1 — Revisar flujo narrativo completo (HDU09 CA1, CA2, CA4)
    // Tarea 2 — Ajustar diálogos según decisiones (despacha reacciones de Otto)
    // Punto de entrada: llamar HandleDecision() desde la UI tras cada elección del jugador.
    public class NarrativeController : MonoBehaviour
    {
        private const float ReactionDelay = 1f; // dentro del límite de 2 s (CA1)

        // CA1 — reacción positiva ante decisión segura
        public event Action<string> OnPositiveReaction;

        // CA2 — reacción de consecuencia ante decisión insegura
        public event Action<string> OnConsequenceReaction;

        // CA3 — resumen al finalizar zona (receptor lo envía a HDU13)
        public event Action<DecisionHistoryManager.ZoneSummary> OnZoneComplete;

        public void HandleDecision(string questionId, string optionType, string npcId, string zone)
        {
            DecisionHistoryManager.Instance?.RecordDecision(questionId, optionType, zone);

            bool hadPriorRisk = DecisionHistoryManager.Instance?.HasPriorInseguraInZone(zone) ?? false;

            if (optionType == "segura_optima" || optionType == "segura_basica")
                StartCoroutine(DispatchPositive(optionType, zone, hadPriorRisk));
            else
                StartCoroutine(DispatchConsequence(zone));
        }

        // CA3 — llamar al completar todas las preguntas de una zona
        public void FinalizeZone(string zone)
        {
            if (DecisionHistoryManager.Instance == null) return;
            OnZoneComplete?.Invoke(DecisionHistoryManager.Instance.GetZoneSummary(zone));
        }

        private IEnumerator DispatchPositive(string optionType, string zone, bool hadPriorRisk)
        {
            yield return new WaitForSeconds(ReactionDelay);
            string msg = hadPriorRisk
                ? NarrativeReactions.PositiveAfterRisk(zone)   // CA4 — mensaje diferente si hubo riesgo previo
                : NarrativeReactions.Positive(optionType, zone);
            OnPositiveReaction?.Invoke(msg);
        }

        private IEnumerator DispatchConsequence(string zone)
        {
            yield return new WaitForSeconds(ReactionDelay);
            OnConsequenceReaction?.Invoke(NarrativeReactions.Consequence(zone));
        }
    }
}
