using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishy
{
    // Tarea 1 — Definir flujo de conversación (HDU10 CA1, CA2)
    // Tarea 3 — Validar coherencia (HDU10 CA3, CA4, CA5, CA6)
    public class DetectiveCaseManager : MonoBehaviour
    {
        [Serializable]
        public class DetectiveMessage
        {
            public string id;
            public string npcSender;
            public string texto;
            public bool   esSenalRiesgo;
            public bool   esAmbiguo; // CA5: no cuenta ni como acierto ni como error
        }

        [Serializable]
        public class DetectiveCase
        {
            public string titulo;
            public string zona;
            public string permissionPlayerText;  // CA1: lo que el jugador dice al pedir permiso
            public string permissionNpcNombre;   // CA1: nombre del NPC que autoriza
            public string permissionNpcResponse; // CA1: autorización explícita del NPC
            public List<DetectiveMessage> conversacion = new();
            public Dictionary<string, string> explicaciones = new(); // CA6: guía por id de mensaje
        }

        public class CaseResult
        {
            public int   totalSenales;    // señales de riesgo reales (excluye ambiguos)
            public int   aciertos;        // señales marcadas correctamente
            public int   noIdentificadas; // señales reales que el jugador no marcó
            public bool  belowThreshold;  // < 50% → CA6: ofrecer repetir o ver explicación
        }

        private DetectiveCase        _activeCase;
        private int                  _currentMsgIndex = -1;
        private readonly HashSet<string> _markedSuspicious = new();

        // CA1 — el jugador solicitó permiso y el NPC autorizó
        public bool IsPermissionGranted { get; private set; }

        // true una vez que AdvanceMessage consumió todos los mensajes
        public bool IsCaseComplete { get; private set; }

        public DetectiveMessage CurrentMessage =>
            _activeCase != null
            && _currentMsgIndex >= 0
            && _currentMsgIndex < _activeCase.conversacion.Count
                ? _activeCase.conversacion[_currentMsgIndex]
                : null;

        // CA1 — el jugador solicita permiso para revisar la conversación
        public event Action<string> OnPermissionRequested;

        // CA1 — el NPC otorga permiso y pide ayuda
        public event Action<string, string> OnPermissionGranted; // (npcNombre, npcResponse)

        // CA2 — siguiente mensaje de la conversación pregrabada (jugador no responde)
        public event Action<DetectiveMessage> OnMessageDisplayed;

        // CA3 — confirmación de que el jugador marcó un mensaje como sospechoso
        public event Action<string> OnMessageMarked; // messageId

        // CA4 — resultado final del caso
        public event Action<CaseResult> OnCaseEvaluated;

        // CA6 — resultado < 50%: habilitar botón de repetir o ver explicación
        public event Action OnRetryOrExplainEnabled;

        // ── Flujo principal ──────────────────────────────────────────────────

        // CA1: paso 1 — jugador inicia solicitud
        public void RequestPermission(DetectiveCase c)
        {
            _activeCase        = c;
            _currentMsgIndex   = -1;
            IsPermissionGranted = false;
            IsCaseComplete      = false;
            _markedSuspicious.Clear();

            OnPermissionRequested?.Invoke(c.permissionPlayerText);
        }

        // CA1: paso 2 — NPC otorga autorización explícita
        public void GrantPermission()
        {
            if (_activeCase == null || IsPermissionGranted) return;
            IsPermissionGranted = true;
            OnPermissionGranted?.Invoke(_activeCase.permissionNpcNombre, _activeCase.permissionNpcResponse);
        }

        // CA2 — avanza un mensaje en la conversación pregrabada
        public void AdvanceMessage()
        {
            if (!IsPermissionGranted || _activeCase == null || IsCaseComplete) return;

            _currentMsgIndex++;
            if (_currentMsgIndex < _activeCase.conversacion.Count)
                OnMessageDisplayed?.Invoke(_activeCase.conversacion[_currentMsgIndex]);
            else
                IsCaseComplete = true;
        }

        // CA3 — jugador marca el mensaje actual como sospechoso
        public void MarkCurrentSuspicious()
        {
            if (CurrentMessage == null) return;
            _markedSuspicious.Add(CurrentMessage.id);
            OnMessageMarked?.Invoke(CurrentMessage.id);
        }

        // CA4 + CA5 + CA6 — el jugador confirma sus marcas y el sistema evalúa
        public CaseResult EvaluateCase()
        {
            if (_activeCase == null) return null;

            int total    = 0;
            int aciertos = 0;

            foreach (var msg in _activeCase.conversacion)
            {
                if (msg.esAmbiguo) continue; // CA5: ambiguos fuera del cálculo

                if (msg.esSenalRiesgo)
                {
                    total++;
                    if (_markedSuspicious.Contains(msg.id)) aciertos++;
                }
            }

            var result = new CaseResult
            {
                totalSenales    = total,
                aciertos        = aciertos,
                noIdentificadas = total - aciertos,
                belowThreshold  = total > 0 && (float)aciertos / total < 0.5f
            };

            OnCaseEvaluated?.Invoke(result); // CA4

            if (result.belowThreshold)
                OnRetryOrExplainEnabled?.Invoke(); // CA6

            return result;
        }

        // CA6 — devuelve la explicación guiada de los mensajes no identificados
        public List<(string msgId, string texto, string explicacion)> GetMissedExplanations()
        {
            var list = new List<(string, string, string)>();
            if (_activeCase == null) return list;

            foreach (var msg in _activeCase.conversacion)
            {
                if (msg.esAmbiguo || !msg.esSenalRiesgo) continue;
                if (_markedSuspicious.Contains(msg.id))  continue;

                _activeCase.explicaciones.TryGetValue(msg.id, out var exp);
                list.Add((msg.id, msg.texto, exp ?? string.Empty));
            }
            return list;
        }
    }
}
