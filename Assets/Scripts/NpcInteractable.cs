using System;
using UnityEngine;

namespace Fishy
{
    // Tarea 3 — Simular logica de NPC (CA3 y CA4 de HDU01)
    // Estado: Idle → Interactuando (linea por linea) → Completo
    public class NpcInteractable : MonoBehaviour
    {
        [SerializeField] private NpcDialogueData dialogueData;

        private int _currentLine;

        public bool IsInteracting { get; private set; }
        public bool IsComplete    { get; private set; }

        public event Action<string> OnLineDisplayed;
        public event Action         OnDialogueComplete;

        public void StartInteraction()
        {
            if (IsComplete || dialogueData == null || dialogueData.lines.Count == 0) return;

            IsInteracting = true;
            _currentLine  = 0;
            ShowCurrentLine();
        }

        public void AdvanceDialogue()
        {
            if (!IsInteracting) return;

            _currentLine++;
            if (_currentLine >= dialogueData.lines.Count)
                EndDialogue();
            else
                ShowCurrentLine();
        }

        private void ShowCurrentLine()
        {
            OnLineDisplayed?.Invoke(dialogueData.lines[_currentLine]);
        }

        private void EndDialogue()
        {
            IsInteracting = false;
            IsComplete    = true;

            // CA3 — muestra la pista de mision activa
            if (!string.IsNullOrEmpty(dialogueData.missionHint))
                OnLineDisplayed?.Invoke(dialogueData.missionHint);

            // CA4 — registra la mision como disponible en el panel
            if (!string.IsNullOrEmpty(dialogueData.missionId) && MissionManager.Instance != null)
                MissionManager.Instance.RegisterMissionAvailable(dialogueData.missionId);

            OnDialogueComplete?.Invoke();
        }
    }
}
