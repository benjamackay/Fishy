using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishy
{
    // Maneja el panel de misiones activas (CA4, CA5, CA6 de HDU01)
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        private readonly HashSet<string> _available  = new();
        private readonly HashSet<string> _completed  = new();

        public event Action<string> OnMissionAvailable;
        public event Action<string> OnMissionCompleted;
        public event Action<string> OnMissionUnlockedDisplay;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // CA4 — registra la mision como disponible en el panel
        public void RegisterMissionAvailable(string missionId)
        {
            if (_available.Add(missionId))
                OnMissionAvailable?.Invoke(missionId);
        }

        // CA5 — registra la mision como completada en el panel
        public void RegisterMissionCompleted(string missionId)
        {
            _available.Remove(missionId);
            if (_completed.Add(missionId))
                OnMissionCompleted?.Invoke(missionId);
        }

        // CA6 — muestra que la mision ha sido desbloqueada (Otto se acerca a ella y presiona boton)
        public void DisplayMissionUnlocked(string missionId)
        {
            if (_available.Contains(missionId))
                OnMissionUnlockedDisplay?.Invoke(missionId);
        }

        public bool IsMissionAvailable(string missionId)  => _available.Contains(missionId);
        public bool IsMissionCompleted(string missionId)  => _completed.Contains(missionId);
    }
}
