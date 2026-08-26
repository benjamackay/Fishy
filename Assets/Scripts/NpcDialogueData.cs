using System.Collections.Generic;
using UnityEngine;

namespace Fishy
{
    // Tarea 1 — Definir flujo conversacional
    [CreateAssetMenu(menuName = "Fishy/Flujo de Dialogo NPC", fileName = "NuevoDialogoNPC")]
    public class NpcDialogueData : ScriptableObject
    {
        [Tooltip("Lineas del NPC en orden de aparicion.")]
        public List<string> lines = new();

        [Tooltip("Pista de mision mostrada al terminar el dialogo (CA3).")]
        public string missionHint;

        [Tooltip("ID de la mision que se registra como disponible al cerrar el dialogo (CA4).")]
        public string missionId;
    }
}
