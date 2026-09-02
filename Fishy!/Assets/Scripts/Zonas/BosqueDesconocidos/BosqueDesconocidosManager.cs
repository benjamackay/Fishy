using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Fishy.World;
using Fishy.Net;

namespace Fishy.Zonas.BosqueDesconocidos
{
    /// <summary>
    /// HDU-2 — Gestor de la temática "Bosque de los Desconocidos".
    ///
    /// Lleva la cuenta de los NPCs de la zona. Cuando el niño/a finaliza la última
    /// interacción de la temática, la marca como completada y habilita el acceso a
    /// la siguiente temática del mapa (desbloqueando una <see cref="BlockedZone"/>
    /// vía <see cref="WorldZoneManager"/>).
    /// </summary>
    public class BosqueDesconocidosManager : MonoBehaviour
    {
        public static BosqueDesconocidosManager Instance { get; private set; }

        [Header("NPCs de la temática")]
        [Tooltip("Si se deja vacío, se recogen todos los BosqueDesconocidosNPC de la escena.")]
        public List<BosqueDesconocidosNPC> npcs = new List<BosqueDesconocidosNPC>();

        [Header("Al completar la temática")]
        [Tooltip("zoneId de la BlockedZone que da acceso a la siguiente temática.")]
        public string siguienteZonaId = "";
        [Tooltip("Progreso (0-100) a fijar en la partida del backend al completar. <0 = no actualizar.")]
        public float progresoAlCompletar = -1f;
        [Tooltip("Mostrar la cinemática (zoom a la zona bloqueada + animación de desbloqueo).")]
        public bool mostrarCinematicaDesbloqueo = true;
        [TextArea]
        [Tooltip("Texto del cartel que aparece durante la cinemática de desbloqueo.")]
        public string mensajeDesbloqueo = "✨ ¡Nueva zona desbloqueada!";

        [Tooltip("Evento disparado una sola vez cuando se completa la temática.")]
        public UnityEvent onTematicaCompletada;

        /// <summary>True cuando todas las interacciones de la temática han terminado.</summary>
        public bool Completed { get; private set; }

        private readonly HashSet<BosqueDesconocidosNPC> finished = new HashSet<BosqueDesconocidosNPC>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (npcs.Count == 0)
                npcs.AddRange(FindObjectsByType<BosqueDesconocidosNPC>());
        }

        /// <summary>Llamado por cada NPC cuando su interacción termina (cualquier rama).</summary>
        public void NotifyNpcFinished(BosqueDesconocidosNPC npc, bool success)
        {
            if (Completed) return;
            if (npc != null) finished.Add(npc);

            // Log de avance para depurar la detección (cuántos van / cuántos faltan).
            int done = 0;
            foreach (var n in npcs)
                if (n != null && n.Finished) done++;
            Debug.Log($"[BosqueDesconocidos] NPC '{(npc != null ? npc.NpcName : "?")}' terminado " +
                      $"(éxito={success}). Avance: {done}/{npcs.Count} NPCs.");

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

            // Resumen de los desenlaces (cada NPC es un gatillante de zona).
            int aSalvo = 0, capturas = 0;
            foreach (var npc in npcs)
            {
                if (npc == null) continue;
                if (npc.WasSuccessful) aSalvo++; else capturas++;
            }
            Debug.Log($"[BosqueDesconocidos] Temática completada (a salvo={aSalvo}, capturas={capturas}). " +
                      "Se habilita la siguiente zona.");

            // Habilita el acceso a la siguiente temática en el mapa (HDU-5).
            if (!string.IsNullOrEmpty(siguienteZonaId) && WorldZoneManager.Instance != null)
            {
                var zona = WorldZoneManager.Instance.GetZone(siguienteZonaId);
                if (zona != null && mostrarCinematicaDesbloqueo)
                    // Zoom a la zona oscurecida + animación de desbloqueo, luego vuelve a Otto.
                    ZoneUnlockCinematic.GetOrCreate().Play(zona, mensajeDesbloqueo);
                else
                    // Sin cinemática (o zona no encontrada): desbloqueo directo.
                    WorldZoneManager.Instance.UnlockZone(siguienteZonaId);
            }

            // Persiste el avance en el backend: deja en la BD el registro de que esta
            // temática-gatillante se completó (best-effort, no bloquea el juego).
            if (ApiManager.Instance != null && ApiManager.Instance.IsLoggedIn &&
                ApiManager.Instance.PartidaId != null)
            {
                if (progresoAlCompletar >= 0f)
                {
                    ApiManager.Instance.ActualizarPartida(progreso: progresoAlCompletar,
                        onSuccess: _ => Debug.Log($"[BosqueDesconocidos] Progreso {progresoAlCompletar} guardado en la partida."),
                        onError:   e => Debug.LogWarning($"[BosqueDesconocidos] No se pudo actualizar progreso: {e}"));
                }
                else
                {
                    Debug.LogWarning("[BosqueDesconocidos] 'progresoAlCompletar' no está configurado (<0): " +
                                     "el desbloqueo no quedará reflejado en el progreso de la partida. " +
                                     "Asigna un valor 0-100 por zona para registrarlo en la BD.");
                }
            }

            onTematicaCompletada?.Invoke();
        }
    }
}
