using System.Collections.Generic;
using UnityEngine;

namespace Fishy.World
{
    /// <summary>
    /// HDU-5 — Gestor de zonas del mundo.
    ///
    /// Centraliza las <see cref="BlockedZone"/> de la escena y permite abrirlas
    /// cuando el niño/a avanza en la historia (misiones completadas / progreso de
    /// la partida). Pensado para conectarse con el progreso del backend
    /// (Fishy.Net.ApiManager), pero funciona también de forma local.
    ///
    /// Ejemplos de uso:
    ///     WorldZoneManager.Instance.UnlockZone("playa_norte");
    ///     WorldZoneManager.Instance.SetProgress(45f); // abre zonas con umbral <= 45
    /// </summary>
    public class WorldZoneManager : MonoBehaviour
    {
        public static WorldZoneManager Instance { get; private set; }

        [System.Serializable]
        public class ZoneRule
        {
            [Tooltip("Zona bloqueada de la escena.")]
            public BlockedZone zone;
            [Tooltip("Progreso de partida (0-100) necesario para abrirla automáticamente. <0 = sólo manual.")]
            public float progresoRequerido = -1f;
        }

        [Tooltip("Reglas de desbloqueo. Si está vacío se recogen todas las BlockedZone de la escena.")]
        public List<ZoneRule> zones = new List<ZoneRule>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (zones.Count == 0)
            {
                foreach (var z in FindObjectsByType<BlockedZone>(FindObjectsSortMode.None))
                    zones.Add(new ZoneRule { zone = z, progresoRequerido = -1f });
            }
        }

        /// <summary>Devuelve la <see cref="BlockedZone"/> con ese zoneId, o null si no existe.</summary>
        public BlockedZone GetZone(string zoneId)
        {
            foreach (var rule in zones)
                if (rule.zone != null && rule.zone.zoneId == zoneId)
                    return rule.zone;
            return null;
        }

        /// <summary>Desbloquea una zona concreta por su <see cref="BlockedZone.zoneId"/>.</summary>
        public bool UnlockZone(string zoneId)
        {
            foreach (var rule in zones)
            {
                if (rule.zone != null && rule.zone.zoneId == zoneId)
                {
                    rule.zone.Unlock();
                    return true;
                }
            }
            Debug.LogWarning($"[WorldZoneManager] No existe la zona '{zoneId}'.");
            return false;
        }

        /// <summary>
        /// Abre automáticamente todas las zonas cuyo umbral de progreso ya se alcanzó.
        /// Llamar al avanzar la partida (p.ej. tras ApiManager.ActualizarPartida).
        /// </summary>
        public void SetProgress(float progreso)
        {
            foreach (var rule in zones)
            {
                if (rule.zone == null) continue;
                if (rule.progresoRequerido >= 0f && progreso >= rule.progresoRequerido)
                    rule.zone.Unlock();
            }
        }
    }
}
