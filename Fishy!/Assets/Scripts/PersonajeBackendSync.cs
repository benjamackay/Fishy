using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishy.Net
{
    /// <summary>
    /// HDU-15 — deja a Otto donde estaba, y va guardando dónde está.
    ///
    /// Sin esto, `OttoController.Start()` lo pone en el <c>spawnPoint</c> de la
    /// escena y el niño/a reaparece en la entrada del mapa cada vez que vuelve.
    ///
    /// <b>Dos cuidados que no tienen las misiones ni el inventario:</b>
    ///
    ///   1. <b>La carrera del arranque.</b> Otto se coloca en su `Start()`, mucho
    ///      antes de que conteste el servidor. Así que no se puede "poner la
    ///      posición al cargar": hay que esperar la respuesta y recién ahí moverlo.
    ///      Y si para entonces el niño/a ya empezó a caminar, <b>no se le mueve</b>:
    ///      devolverlo de un salto a donde estaba ayer sería peor que no restaurar.
    ///      Ver <see cref="RadioParaConsiderarQueNoSeHaMovido"/>.
    ///
    ///   2. <b>La posición cambia todos los frames.</b> Guardar en cada uno sería
    ///      un PATCH por frame. Se revisa cada pocos segundos y solo se manda si de
    ///      verdad se movió, más un guardado al salir del juego.
    /// </summary>
    public class PersonajeBackendSync : MonoBehaviour
    {
        public static PersonajeBackendSync Instance { get; private set; }

        /// <summary>Segundos entre reintentos mientras se espera a que haya partida.</summary>
        private const float EsperaEntreIntentos = 0.5f;

        /// <summary>Cada cuánto se revisa si Otto se movió lo suficiente para guardar.</summary>
        private const float SegundosEntreGuardados = 5f;

        /// <summary>
        /// Cuánto tiene que haberse movido para que valga un PATCH. Sin esto, el
        /// temblor de un Rigidbody2D apoyado contra un collider mandaría peticiones
        /// para siempre con el niño/a quieto.
        /// </summary>
        private const float DistanciaMinimaParaGuardar = 0.5f;

        /// <summary>
        /// Si al llegar la respuesta Otto sigue a menos de esto del punto donde
        /// apareció, se asume que el niño/a todavía no toma el control y se le puede
        /// mover. Más lejos significa que ya está jugando: ahí se respeta.
        /// </summary>
        private const float RadioParaConsiderarQueNoSeHaMovido = 1.5f;

        private int? partidaAtendida;
        private Vector2 ultimaGuardada;
        private bool hayUltimaGuardada;
        private Coroutine guardadoPeriodico;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (Instance != null) return;
            var go = new GameObject("PersonajeBackendSync");
            go.AddComponent<PersonajeBackendSync>();
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

        private void OnEnable()  { StartCoroutine(EsperarPartida()); }

        private void OnApplicationQuit()
        {
            // Último guardado antes de cerrar. Es best-effort: si el proceso muere
            // antes de que salga la petición se pierde, pero como se guarda cada
            // pocos segundos lo que se pierde son metros, no la partida.
            GuardarSiSeMovio(forzar: true);
        }

        private void OnApplicationPause(bool pausado)
        {
            // En móvil, `OnApplicationQuit` muchas veces no llega: el sistema mata la
            // app pausada sin avisar. Esta es la única señal fiable de "se va".
            if (pausado) GuardarSiSeMovio(forzar: true);
        }

        // ── 1. Restaurar ─────────────────────────────────────────────────────

        private IEnumerator EsperarPartida()
        {
            var espera = new WaitForSeconds(EsperaEntreIntentos);

            while (true)
            {
                var api = ApiManager.Instance;

                if (api != null && api.PartidaId != null && partidaAtendida != api.PartidaId)
                {
                    partidaAtendida = api.PartidaId;
                    hayUltimaGuardada = false;
                    Restaurar();
                }

                yield return espera;
            }
        }

        private void Restaurar()
        {
            var api = ApiManager.Instance;
            if (api == null) return;

            var otto = BuscarOtto();
            Vector2 dondeAparecio = otto != null ? (Vector2)otto.transform.position : Vector2.zero;

            api.ObtenerPersonaje(
                onSuccess: dto => Aplicar(dto, dondeAparecio),
                onError: e =>
                {
                    Debug.LogWarning($"[PersonajeBackendSync] No se pudo saber dónde quedó Otto: {e}");
                    ArrancarGuardadoPeriodico();
                });
        }

        private void Aplicar(PersonajeDto dto, Vector2 dondeAparecio)
        {
            var otto = BuscarOtto();

            if (otto == null)
            {
                // Pasa en las escenas de menú, que no tienen a Otto. No es un error:
                // el bucle vuelve a intentar cuando cambie la partida, y el guardado
                // periódico se salta solo mientras no haya a quién mirar.
                ArrancarGuardadoPeriodico();
                return;
            }

            string escenaActual = SceneManager.GetActiveScene().name;

            if (dto == null || !dto.tiene_posicion)
            {
                Debug.Log("[PersonajeBackendSync] Sin posición guardada: Otto se queda en el spawnPoint.");
            }
            else if (!string.IsNullOrEmpty(dto.escena) && dto.escena != escenaActual)
            {
                // Restaurar unas coordenadas de otra escena dejaría a Otto dentro de
                // un cerro o fuera del mapa.
                Debug.LogWarning(
                    $"[PersonajeBackendSync] La posición guardada es de '{dto.escena}' y esta escena " +
                    $"es '{escenaActual}'. Se ignora y Otto se queda en el spawnPoint.");
            }
            else if (Vector2.Distance(otto.transform.position, dondeAparecio) > RadioParaConsiderarQueNoSeHaMovido)
            {
                // La respuesta llegó tarde y el niño/a ya está jugando. Moverlo ahora
                // sería un tirón hacia atrás en medio del juego.
                Debug.LogWarning(
                    "[PersonajeBackendSync] Otto ya se movió antes de que llegara la respuesta: " +
                    "no se restaura la posición para no dar un salto en medio del juego.");
            }
            else
            {
                var destino = new Vector3(dto.pos_x.Value, dto.pos_y.Value, otto.transform.position.z);
                otto.TeleportTo(destino);
                ultimaGuardada = destino;
                hayUltimaGuardada = true;
                Debug.Log($"[PersonajeBackendSync] Otto restaurado en ({destino.x:F1}, {destino.y:F1}).");
            }

            ArrancarGuardadoPeriodico();
        }

        // ── 2. Guardar ───────────────────────────────────────────────────────

        private void ArrancarGuardadoPeriodico()
        {
            if (guardadoPeriodico != null) StopCoroutine(guardadoPeriodico);
            guardadoPeriodico = StartCoroutine(GuardarCadaTanto());
        }

        private IEnumerator GuardarCadaTanto()
        {
            var espera = new WaitForSeconds(SegundosEntreGuardados);
            while (true)
            {
                yield return espera;
                GuardarSiSeMovio(forzar: false);
            }
        }

        private void GuardarSiSeMovio(bool forzar)
        {
            var api = ApiManager.Instance;
            if (api == null || api.PartidaId == null) return;

            var otto = BuscarOtto();
            if (otto == null) return;   // escena de menú: no hay nada que guardar

            Vector2 ahora = otto.transform.position;

            if (!forzar && hayUltimaGuardada &&
                Vector2.Distance(ahora, ultimaGuardada) < DistanciaMinimaParaGuardar)
                return;

            string escena = SceneManager.GetActiveScene().name;

            api.GuardarPersonaje(escena, ahora.x, ahora.y,
                onSuccess: _ =>
                {
                    ultimaGuardada = ahora;
                    hayUltimaGuardada = true;
                },
                onError: e => Debug.LogWarning($"[PersonajeBackendSync] No se pudo guardar dónde está Otto: {e}"));
        }

        // ── Auxiliares ───────────────────────────────────────────────────────

        /// <summary>
        /// El Otto de la escena, o null si no hay (los menús no tienen). Se busca
        /// cada vez en vez de guardarlo: este componente es DontDestroyOnLoad y el
        /// Otto de la escena anterior queda destruido al cambiar de escena.
        /// </summary>
        private static Fishy.World.OttoController BuscarOtto()
        {
            return FindAnyObjectByType<Fishy.World.OttoController>();
        }
    }
}
