using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Mision;
using Fishy.World;

namespace Fishy.Net
{
    /// <summary>
    /// HDU-1 CA4/CA5, HDU-3 CA5, HDU-4 CA5 — puente entre el progreso en memoria
    /// (<see cref="MissionManager"/>, <see cref="BlockedZone"/>) y el backend.
    ///
    /// Hace dos cosas, en este orden:
    ///
    ///   1. <b>Al empezar la partida, baja lo que ya estaba hecho</b> y lo aplica:
    ///      las misiones completadas vuelven completadas y las zonas desbloqueadas
    ///      se abren solas. Es lo que PlayerPrefs no puede dar, porque vive en el
    ///      dispositivo: si el niño/a empezó en el PC de la feria y sigue en otro,
    ///      PlayerPrefs viene vacío y sin esto el mapa aparecería cerrado de nuevo.
    ///
    ///   2. <b>Después, sube cada cambio</b> — un desafío que queda disponible, uno
    ///      que se completa, una zona que se abre.
    ///
    /// <b>Por qué es un componente aparte y no código dentro de MissionManager:</b>
    /// <c>MissionManager</c> vive en el assembly <c>Fishy.Mision</c> (tiene su propio
    /// .asmdef) y <c>ApiManager</c> vive en Assembly-CSharp. Unity <b>no permite</b>
    /// que un assembly con .asmdef referencie a Assembly-CSharp, sólo al revés, así
    /// que la misión no puede llamar a la API directamente. Este puente vive del lado
    /// que sí ve a los dos y se engancha por eventos. De paso deja el panel de misión
    /// funcionando igual sin backend, que es el modo en que corren sus tests.
    ///
    /// Se crea solo al cargar la escena; no hay que arrastrar nada al inspector.
    /// </summary>
    public class MisionBackendSync : MonoBehaviour
    {
        public static MisionBackendSync Instance { get; private set; }

        /// <summary>Segundos entre reintentos mientras se espera a que haya partida.</summary>
        private const float EsperaEntreIntentos = 0.5f;

        /// <summary>Último estado conocido en el servidor, para no repetir POSTs iguales.</summary>
        private readonly Dictionary<string, bool> misionesEnServidor = new Dictionary<string, bool>();
        private readonly HashSet<string> zonasEnServidor = new HashSet<string>();

        private int? partidaSincronizada;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (Instance != null) return;
            var go = new GameObject("MisionBackendSync");
            go.AddComponent<MisionBackendSync>();
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

        private void OnEnable()
        {
            var misiones = MissionManager.GetOrCreate();
            misiones.onDesafioRegistrado.AddListener(AlRegistrarDesafio);
            misiones.onDesafioCompletado.AddListener(AlCompletarDesafio);
            BlockedZone.OnZonaDesbloqueada += AlDesbloquearZona;

            StartCoroutine(EsperarPartidaYBajarProgreso());
        }

        private void OnDisable()
        {
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.onDesafioRegistrado.RemoveListener(AlRegistrarDesafio);
                MissionManager.Instance.onDesafioCompletado.RemoveListener(AlCompletarDesafio);
            }
            BlockedZone.OnZonaDesbloqueada -= AlDesbloquearZona;
        }

        // ── 1. Bajar lo que ya estaba hecho ──────────────────────────────────

        /// <summary>
        /// Espera a que haya sesión y partida (el login y la elección de perfil ocurren
        /// en otra escena) y entonces baja el progreso. Si se cambia de partida dentro
        /// de la misma ejecución, vuelve a sincronizar con la nueva.
        /// </summary>
        private IEnumerator EsperarPartidaYBajarProgreso()
        {
            var espera = new WaitForSeconds(EsperaEntreIntentos);

            while (true)
            {
                var api = ApiManager.Instance;
                bool listo = api != null && !api.IsLocalMode && api.IsLoggedIn && api.PartidaId != null;

                if (listo && partidaSincronizada != api.PartidaId)
                {
                    partidaSincronizada = api.PartidaId;
                    misionesEnServidor.Clear();
                    zonasEnServidor.Clear();
                    BajarProgreso();
                }

                yield return espera;
            }
        }

        /// <summary>Pide misiones y zonas de la partida activa y las aplica al juego.</summary>
        public void BajarProgreso()
        {
            var api = ApiManager.Instance;
            if (api == null) return;

            api.ObtenerProgresoMisiones(
                onSuccess: AplicarMisiones,
                onError: e => Debug.LogWarning($"[MisionBackendSync] No se pudo bajar el progreso de misiones: {e}"));

            api.ObtenerProgresoZonas(
                onSuccess: AplicarZonas,
                onError: e => Debug.LogWarning($"[MisionBackendSync] No se pudo bajar el progreso de zonas: {e}"));
        }

        private void AplicarMisiones(List<MisionProgresoDto> progreso)
        {
            if (progreso == null) return;

            var completadas = new List<string>();
            foreach (var m in progreso)
            {
                if (m == null || string.IsNullOrEmpty(m.mision_id)) continue;
                misionesEnServidor[m.mision_id] = m.Completada;
                if (m.Completada) completadas.Add(m.mision_id);

                if (!m.en_catalogo)
                    Debug.LogWarning($"[MisionBackendSync] '{m.mision_id}' no está en el banco de " +
                                     "preguntas. El progreso se guarda igual, pero el id de Unity y " +
                                     "el del banco no están alineados.");
            }

            MissionManager.GetOrCreate().PrecargarCompletados(completadas);
        }

        private void AplicarZonas(List<ZonaProgresoDto> progreso)
        {
            if (progreso == null) return;

            foreach (var z in progreso)
            {
                if (z == null || string.IsNullOrEmpty(z.zona)) continue;
                zonasEnServidor.Add(z.zona);
                AbrirZonaEnEscena(z.zona);
            }
        }

        /// <summary>
        /// Abre la zona del mapa que corresponde a ese slug. Se busca por
        /// <see cref="BlockedZone.ZonaBackend"/> y no por <c>zoneId</c>, porque el id
        /// del collider en la escena y el slug del banco no tienen por qué coincidir.
        /// </summary>
        private void AbrirZonaEnEscena(string zona)
        {
            foreach (var bloqueada in FindObjectsByType<BlockedZone>())
            {
                if (bloqueada == null || bloqueada.ZonaBackend != zona) continue;
                if (!bloqueada.isLocked) continue;

                // Sin cinemática: esto es restaurar algo que el niño/a ya había abierto,
                // no un logro nuevo. Celebrarlo otra vez al entrar confundiría.
                bloqueada.Unlock();
                Debug.Log($"[MisionBackendSync] Zona '{zona}' restaurada como desbloqueada.");
            }
        }

        // ── 2. Subir cada cambio ─────────────────────────────────────────────

        private void AlRegistrarDesafio(DesafioRuntime runtime)
        {
            if (runtime == null || string.IsNullOrEmpty(runtime.Id)) return;
            Subir(runtime.Id, runtime.estado == EstadoDesafio.Completado);
        }

        private void AlCompletarDesafio(DesafioRuntime runtime)
        {
            if (runtime == null || string.IsNullOrEmpty(runtime.Id)) return;
            Subir(runtime.Id, true);
        }

        private void Subir(string misionId, bool completada)
        {
            var api = ApiManager.Instance;
            if (api == null || api.IsLocalMode || !api.IsLoggedIn || api.PartidaId == null) return;

            // No repetir lo que el servidor ya tiene. El endpoint es idempotente, así
            // que repetirlo no rompería nada; se evita sólo para no llenar el log ni
            // mandar una petición cada vez que el niño/a se acerca a un objeto.
            // Si allá ya figura completada no hay nada que mandar (completar es un
            // camino de ida); si figura disponible, sólo interesa el paso a completada.
            if (misionesEnServidor.TryGetValue(misionId, out bool completadaEnServidor) &&
                (completadaEnServidor || !completada))
                return;

            api.RegistrarProgresoMision(misionId, completada,
                onSuccess: dto =>
                {
                    misionesEnServidor[misionId] = dto != null && dto.Completada;
                    Debug.Log($"[MisionBackendSync] Misión '{misionId}' guardada como " +
                              $"{(completada ? "completada" : "disponible")}.");
                },
                onError: e => Debug.LogWarning($"[MisionBackendSync] No se pudo guardar '{misionId}': {e}"));
        }

        private void AlDesbloquearZona(BlockedZone zona)
        {
            if (zona == null) return;

            string slug = zona.ZonaBackend;
            if (string.IsNullOrEmpty(slug)) return;

            // Si vino del servidor, es una zona que ya estaba abierta: no hay que subirla.
            if (zonasEnServidor.Contains(slug)) return;

            var api = ApiManager.Instance;
            if (api == null || api.IsLocalMode || !api.IsLoggedIn || api.PartidaId == null) return;

            api.RegistrarProgresoZona(slug, completada: false,
                onSuccess: _ =>
                {
                    zonasEnServidor.Add(slug);
                    Debug.Log($"[MisionBackendSync] Zona '{slug}' guardada como desbloqueada.");
                },
                onError: e => Debug.LogWarning($"[MisionBackendSync] No se pudo guardar la zona '{slug}': {e}"));
        }
    }
}
