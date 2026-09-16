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

    /// <summary>Avisa de cuál es la misión activa ahora. Puede llegar <c>null</c>:
    /// eso es "ya no queda ninguna", y es lo que dispara el mensaje de HDU-16 CA6.</summary>
    [Serializable] public class MisionActivaEvent : UnityEvent<DesafioRuntime> { }

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
        [Tooltip("Se dispara cuando cambia la misión activa, incluida la vez que se " +
                 "queda en ninguna. Es el enganche del HUD y del indicador de zona.")]
        public MisionActivaEvent onMisionActivaCambiada = new MisionActivaEvent();

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

        /// <summary>
        /// HDU-16 — La misión que el niño/a está haciendo ahora mismo, o <c>null</c> si
        /// no queda ninguna disponible.
        ///
        /// No se elige a mano: es siempre la disponible con el <see cref="DesafioData.orden"/>
        /// más bajo, así que al completar una, la siguiente entra sola (CA5) y cuando se
        /// acaban queda en null (CA6). Se recalcula ante cualquier cambio de estado.
        /// </summary>
        public DesafioRuntime Activa { get; private set; }

        /// <summary>Hay algo que hacer ahora mismo. En false toca el mensaje de "sin
        /// nuevas misiones".</summary>
        public bool HayMisionActiva => Activa != null;

        /// <summary>
        /// Zona que hay que señalar ahora mismo, o <c>null</c> si no hay ninguna que
        /// señalar —porque no hay misión activa, o porque la que hay no apunta a
        /// ninguna zona—.
        ///
        /// Es la decisión completa del indicador de HDU-16 CA2: <c>ZoneMarker</c> se
        /// enciende cuando esto tiene valor y se apaga cuando vuelve a null, que es lo
        /// que pasa al completar la misión (CA4). Vive aquí, y no en el marcador, para
        /// que se pueda probar sin escena ni cámara: el marcador de arriba no decide
        /// nada, sólo dibuja.
        /// </summary>
        public string ZonaObjetivoActiva
        {
            get
            {
                if (Activa == null || Activa.data == null) return null;
                string zona = Activa.data.zonaObjetivo;
                return string.IsNullOrWhiteSpace(zona) ? null : zona.Trim();
            }
        }

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
            RecalcularActiva();
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
        ///
        /// <paramref name="anunciar"/> en false registra sin disparar
        /// <see cref="onDesafioDisponible"/>. Lo usa la restauración al retomar la
        /// partida: ese evento abre el panel de misiones, y abrirlo solo al cargar
        /// —por misiones que el niño/a ya conocía— sería tratar lo viejo como novedad.
        /// El panel igual se refresca, porque <see cref="onPanelActualizado"/> sí se
        /// dispara siempre.
        /// </summary>
        public DesafioRuntime RegistrarDesafioDisponible(DesafioData data, bool anunciar = true)
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
            RecalcularActiva();

            if (!yaCompletado && anunciar)
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
        /// <summary>
        /// Repuebla el panel con las misiones que esta partida ya conocía, sacando cada
        /// ficha del <see cref="CatalogoDesafios"/>.
        ///
        /// Sin esto, el panel nacía vacío al retomar: el backend guardaba las misiones
        /// disponibles desde el primer día y hasta las mandaba, pero nadie las ponía de
        /// vuelta. Había que volver a interactuar con el objeto o NPC que las desbloquea
        /// para que reaparecieran.
        ///
        /// Se llama DESPUÉS de <see cref="PrecargarCompletados"/>, para que una misión ya
        /// terminada se registre como completada y no como disponible.
        /// </summary>
        public void PrecargarConocidos(IEnumerable<string> ids)
        {
            if (ids == null) return;

            int puestos = 0, sinFicha = 0;
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (desafios.ContainsKey(id)) continue;   // ya está en el panel

                var data = CatalogoDesafios.Buscar(id);
                if (data == null)
                {
                    // El id está en la base pero no hay DesafioData con ese id. Igual que
                    // con los ítems: se avisa, porque significa que Unity y el backend
                    // dejaron de hablar el mismo idioma y al niño/a le falta una misión
                    // del panel sin explicación.
                    Debug.LogWarning(
                        $"[MissionManager] '{id}' está guardado pero ninguna ficha tiene ese " +
                        "desafioId, así que no se puede mostrar en el panel.");
                    sinFicha++;
                    continue;
                }

                RegistrarDesafioDisponible(data, anunciar: false);
                puestos++;
            }

            Debug.Log($"[MissionManager] {puestos} misión(es) restauradas en el panel" +
                      (sinFicha > 0 ? $", {sinFicha} sin ficha." : "."));
        }

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

            if (cambio)
            {
                RecalcularActiva();
                onPanelActualizado?.Invoke();
            }
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

            // Antes de anunciar nada: quien escuche "completada" —el HUD, el indicador
            // de zona— tiene que poder preguntar ya cuál es la siguiente.
            RecalcularActiva();

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

        /// <summary>
        /// Lista para pintar el panel: disponibles primero y, dentro de cada grupo, en
        /// orden de historia (<see cref="DesafioData.orden"/>), con el título como
        /// desempate.
        ///
        /// Antes el desempate era lo único que había, y ordenaba alfabéticamente: la
        /// lista salía en un orden que no era el de nada. Con esto la primera fila
        /// disponible es siempre <see cref="Activa"/>, así que la página del Tab y el
        /// HUD cuentan lo mismo.
        /// </summary>
        public List<DesafioRuntime> GetListaOrdenada()
        {
            return desafios.Values
                .OrderBy(d => d.estado == EstadoDesafio.Completado ? 1 : 0)
                .ThenBy(d => d.data != null ? d.data.orden : int.MaxValue)
                .ThenBy(d => d.Titulo)
                .ToList();
        }

        /// <summary>
        /// Vuelve a elegir la misión activa y avisa si cambió (HDU-16 CA5 y CA6).
        ///
        /// Se llama después de tocar el estado y ANTES de <see cref="onPanelActualizado"/>,
        /// para que cualquier refresco de UI que dispare ese evento ya vea la misión
        /// activa nueva en vez de la vieja.
        /// </summary>
        private void RecalcularActiva()
        {
            DesafioRuntime siguiente = desafios.Values
                .Where(d => d.estado == EstadoDesafio.Disponible && d.data != null)
                .OrderBy(d => d.data.orden)
                .ThenBy(d => d.Titulo)
                .FirstOrDefault();

            if (ReferenceEquals(siguiente, Activa)) return;

            Activa = siguiente;

            Debug.Log(Activa != null
                ? $"[MissionManager] Misión activa: '{Activa.Titulo}'" +
                  (ZonaObjetivoActiva != null ? $" → {ZonaObjetivoActiva}." : " (sin zona).")
                : "[MissionManager] No queda ninguna misión disponible.");

            onMisionActivaCambiada?.Invoke(Activa);
        }

        /// <summary>Sólo para tests/depuración: limpia todo el estado en memoria (no borra PlayerPrefs).</summary>
        internal void ResetEnMemoria()
        {
            desafios.Clear();
            completadosRemotos.Clear();
            RecalcularActiva();
        }
    }
}
