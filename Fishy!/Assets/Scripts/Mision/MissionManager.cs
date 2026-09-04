using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Fishy.Mision
{
    /// <summary>Estado de un desafío dentro del panel de misión activa.</summary>
    public enum EstadoDesafio { Disponible, Completado }

    /// <summary>Instancia en runtime de un desafío ligado a su ficha de datos.</summary>
    [Serializable]
    public class DesafioRuntime
    {
        public DesafioData data;
        public EstadoDesafio estado;

        public string Id => data != null ? data.desafioId : null;
        public string Titulo => data != null ? data.titulo : "(desafío desconocido)";
    }

    [Serializable] public class DesafioDisponibleEvent : UnityEvent<DesafioRuntime> { }
    [Serializable] public class DesafioCompletadoEvent : UnityEvent<DesafioRuntime> { }

    /// <summary>
    /// HDU-1 — Gestor central del panel de "misión activa".
    ///
    /// Cualquier <c>InteractableObject</c> o NPC que desbloquee un desafío llama a
    /// <see cref="RegistrarDesafioDisponible"/> al terminar su interacción (criterio 4).
    /// Cuando el niño/a termina ese desafío (p.ej. un minijuego asociado), quien lo
    /// controle llama a <see cref="CompletarDesafio"/> (criterio 5).
    ///
    /// Es un singleton persistente (DontDestroyOnLoad), igual que InventoryManager y
    /// ApiManager, para que el progreso de la sesión no se pierda al cambiar de escena.
    /// Sin backend conectado, el estado "completado" se guarda en PlayerPrefs como
    /// fallback local (mismo patrón que useLocalMode en ApiManager).
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("Eventos")]
        [Tooltip("Se dispara cuando un desafío nuevo queda disponible.")]
        public DesafioDisponibleEvent onDesafioDisponible = new DesafioDisponibleEvent();
        [Tooltip("Se dispara cuando un desafío se marca como completado.")]
        public DesafioCompletadoEvent onDesafioCompletado = new DesafioCompletadoEvent();
        [Tooltip("Se dispara cada vez que la lista cambia (para refrescar la UI).")]
        public UnityEvent onPanelActualizado = new UnityEvent();
        [Tooltip("Se dispara al registrar un desafío, esté disponible o ya completado. " +
                 "Es el enganche para sincronizar con el backend.")]
        public DesafioDisponibleEvent onDesafioRegistrado = new DesafioDisponibleEvent();

        [Header("Persistencia local (fallback sin backend)")]
        [Tooltip("Se activa al elegir una partida. El progreso se guarda separado para cada partida.")]
        public bool persistirLocalmente;

        private const string PrefsKeyPrefix = "Fishy.Desafio.Completado.";
        private string contextoPersistencia;

        private readonly Dictionary<string, DesafioRuntime> desafios = new Dictionary<string, DesafioRuntime>();

        /// <summary>Ids que el backend reporta como completados en esta partida.</summary>
        private readonly HashSet<string> completadosRemotos = new HashSet<string>();

        /// <summary>Todos los desafíos registrados en esta sesión (disponibles + completados).</summary>
        public IReadOnlyCollection<DesafioRuntime> Desafios => desafios.Values;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Devuelve la instancia activa, creándola si aún no existe en la escena.</summary>
        public static MissionManager GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("MissionManager");
                Instance = go.AddComponent<MissionManager>();
            }
            return Instance;
        }

        /// <summary>
        /// Asocia el progreso de misiones a una partida concreta. Antes se usaba
        /// una clave global, por lo que completar una misión en una prueba o en el
        /// perfil de otro jugador hacía que apareciera completada para todos.
        /// </summary>
        public void ConfigurarPersistenciaParaPartida(int partidaId)
        {
            if (partidaId <= 0)
            {
                Debug.LogWarning("[MissionManager] No se puede configurar una PartidaId inválida.");
                return;
            }

            string nuevoContexto = $"Partida.{partidaId}.";
            if (contextoPersistencia == nuevoContexto)
            {
                persistirLocalmente = true;
                return;
            }

            // Si se cambia de perfil/partida dentro de la misma ejecución, jamás
            // debe verse el estado que estaba cargado para la partida anterior.
            desafios.Clear();
            completadosRemotos.Clear();
            contextoPersistencia = nuevoContexto;
            persistirLocalmente = true;
            onPanelActualizado?.Invoke();

            Debug.Log($"[MissionManager] Progreso asociado a la partida {partidaId}.");
        }

        private bool PuedePersistir =>
            persistirLocalmente && !string.IsNullOrEmpty(contextoPersistencia);

        private string PrefsKey(string desafioId) =>
            PrefsKeyPrefix + contextoPersistencia + desafioId;

        /// <summary>
        /// Registra un desafío como disponible en el panel de misión activa (criterio 4).
        /// Llamar al finalizar la interacción con el objeto/NPC que lo desbloquea.
        /// Si ya estaba registrado (p.ej. se vuelve a interactuar), no hace nada nuevo.
        /// Si ya estaba completado en una sesión anterior (PlayerPrefs), conserva ese estado.
        /// </summary>
        public DesafioRuntime RegistrarDesafioDisponible(DesafioData data)
        {
            if (data == null || string.IsNullOrEmpty(data.desafioId))
            {
                Debug.LogError("[MissionManager] DesafioData nulo o sin 'desafioId' asignado.");
                return null;
            }

            if (desafios.TryGetValue(data.desafioId, out var existente))
                return existente;

            // El backend manda sobre PlayerPrefs: es el progreso del niño, no el de
            // este dispositivo. PlayerPrefs sigue valiendo cuando se juega sin sesión.
            bool yaCompletado =
                completadosRemotos.Contains(data.desafioId) ||
                (PuedePersistir && PlayerPrefs.GetInt(PrefsKey(data.desafioId), 0) == 1);

            var runtime = new DesafioRuntime
            {
                data = data,
                estado = yaCompletado ? EstadoDesafio.Completado : EstadoDesafio.Disponible
            };
            desafios[data.desafioId] = runtime;

            if (!yaCompletado)
                onDesafioDisponible?.Invoke(runtime);
            onDesafioRegistrado?.Invoke(runtime);
            onPanelActualizado?.Invoke();

            Debug.Log($"[MissionManager] Desafío '{data.titulo}' registrado ({runtime.estado}).");
            return runtime;
        }

        /// <summary>
        /// Aplica el progreso que vino del backend: los ids que ya estaban completados
        /// en esta partida, jugara donde jugara el niño/a.
        ///
        /// Se llama antes de que las fichas se registren (los objetos y NPCs de la
        /// escena lo hacen al interactuar), así que además de corregir lo que ya está
        /// en memoria se guarda la lista para que un desafío que se registre después
        /// nazca completado. Es lo que PlayerPrefs no puede dar: si el niño empezó en
        /// el PC de la feria y sigue en otro, PlayerPrefs viene vacío.
        /// </summary>
        public void PrecargarCompletados(IEnumerable<string> idsCompletados)
        {
            if (idsCompletados == null) return;

            bool cambio = false;
            foreach (var id in idsCompletados)
            {
                if (string.IsNullOrEmpty(id)) continue;
                completadosRemotos.Add(id);

                // Si la ficha ya estaba registrada en esta sesión, corregirla ahora.
                if (desafios.TryGetValue(id, out var runtime) &&
                    runtime.estado != EstadoDesafio.Completado)
                {
                    runtime.estado = EstadoDesafio.Completado;
                    cambio = true;
                }
            }

            if (cambio) onPanelActualizado?.Invoke();
            Debug.Log($"[MissionManager] {completadosRemotos.Count} misión(es) completadas según el backend.");
        }

        /// <summary>
        /// Marca un desafío como completado en el panel de misión activa (criterio 5).
        /// Llamar cuando el niño/a termina ese desafío. Es idempotente: completar dos
        /// veces el mismo desafío no produce errores ni eventos duplicados.
        /// </summary>
        public bool CompletarDesafio(string desafioId)
        {
            if (string.IsNullOrEmpty(desafioId)) return false;

            if (!desafios.TryGetValue(desafioId, out var runtime))
            {
                Debug.LogWarning($"[MissionManager] Se intentó completar '{desafioId}' pero no " +
                                  "estaba registrado como disponible. Regístralo primero con " +
                                  "RegistrarDesafioDisponible().");
                return false;
            }

            if (runtime.estado == EstadoDesafio.Completado) return true;

            runtime.estado = EstadoDesafio.Completado;

            if (PuedePersistir)
            {
                PlayerPrefs.SetInt(PrefsKey(desafioId), 1);
                PlayerPrefs.Save();
            }

            onDesafioCompletado?.Invoke(runtime);
            onPanelActualizado?.Invoke();

            Debug.Log($"[MissionManager] Desafío '{runtime.Titulo}' completado.");
            return true;
        }

        /// <summary>Overload de conveniencia: completar pasando la ficha en vez del id.</summary>
        public bool CompletarDesafio(DesafioData data) =>
            data != null && CompletarDesafio(data.desafioId);

        /// <summary>Estado actual de un desafío, o null si nunca fue registrado.</summary>
        public EstadoDesafio? GetEstado(string desafioId) =>
            desafios.TryGetValue(desafioId, out var r) ? r.estado : (EstadoDesafio?)null;

        public bool EstaDisponible(string desafioId) => GetEstado(desafioId) == EstadoDesafio.Disponible;
        public bool EstaCompletado(string desafioId) => GetEstado(desafioId) == EstadoDesafio.Completado;

        /// <summary>Lista para pintar el panel: disponibles primero, luego completados, alfabético.</summary>
        public List<DesafioRuntime> GetListaOrdenada()
        {
            return desafios.Values
                .OrderBy(d => d.estado == EstadoDesafio.Completado ? 1 : 0)
                .ThenBy(d => d.Titulo)
                .ToList();
        }

        /// <summary>Sólo para tests/depuración: limpia todo el estado en memoria (no borra PlayerPrefs).</summary>
        internal void ResetEnMemoria()
        {
            desafios.Clear();
            completadosRemotos.Clear();
        }
    }
}
