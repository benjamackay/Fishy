using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Fishy.World;
using Fishy.Net;

namespace Fishy.Desconocidos
{
    /// <summary>
    /// HDU-2 — Gestor de la temática "Desconocidos".
    ///
    /// Lleva la cuenta de los NPCs de la zona. Cuando el niño/a finaliza la última
    /// interacción de la temática, la marca como completada y habilita el acceso a
    /// la siguiente temática del mapa (desbloqueando una <see cref="BlockedZone"/>
    /// vía <see cref="WorldZoneManager"/>).
    /// </summary>
    public class ZonaDesconocidosManager : MonoBehaviour
    {
        public static ZonaDesconocidosManager Instance { get; private set; }

        [Header("NPCs de la temática")]
        [Tooltip("Si se deja vacío, se recogen todos los DesconocidosNPC de la escena.")]
        public List<DesconocidosNPC> npcs = new List<DesconocidosNPC>();

        [Header("Al completar la temática")]
        [Tooltip("zoneId de la BlockedZone que da acceso a la siguiente temática.")]
        public string siguienteZonaId = "";
        [Tooltip("Progreso (0-100) a fijar en la partida del backend al completar. <0 = no actualizar.")]
        public float progresoAlCompletar = -1f;

        [Tooltip("Evento disparado una sola vez cuando se completa la temática.")]
        public UnityEvent onTematicaCompletada;

        /// <summary>True cuando todas las interacciones de la temática han terminado.</summary>
        public bool Completed { get; private set; }

        private readonly HashSet<DesconocidosNPC> finished = new HashSet<DesconocidosNPC>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (npcs.Count == 0)
                npcs.AddRange(FindObjectsByType<DesconocidosNPC>(FindObjectsSortMode.None));
        }

        /// <summary>Llamado por cada NPC cuando su interacción termina (cualquier rama).</summary>
        public void NotifyNpcFinished(DesconocidosNPC npc, bool success)
        {
            if (Completed) return;
            if (npc != null) finished.Add(npc);

            if (AllFinished())
                CompleteTheme();
        }

        private bool AllFinished()
        {
            if (npcs == null || npcs.Count == 0) return false;
            foreach (var npc in npcs)
                if (npc != null && !npc.Finished) return false;
            return true;
        }

        private void CompleteTheme()
        {
            Completed = true;
            Debug.Log("[ZonaDesconocidos] Temática completada. Se habilita la siguiente zona.");

            // Habilita el acceso a la siguiente temática en el mapa (HDU-5).
            if (!string.IsNullOrEmpty(siguienteZonaId) && WorldZoneManager.Instance != null)
                WorldZoneManager.Instance.UnlockZone(siguienteZonaId);

            // Persiste el progreso en el backend (best-effort).
            if (progresoAlCompletar >= 0f && ApiManager.Instance != null &&
                ApiManager.Instance.IsLoggedIn && ApiManager.Instance.PartidaId != null)
            {
                ApiManager.Instance.ActualizarPartida(progreso: progresoAlCompletar,
                    onError: e => Debug.LogWarning($"[ZonaDesconocidos] No se pudo actualizar progreso: {e}"));
            }

            onTematicaCompletada?.Invoke();
        }
    }
}
