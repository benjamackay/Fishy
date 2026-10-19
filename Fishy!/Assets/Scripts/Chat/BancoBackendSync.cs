using System.Collections;
using Fishy.Net;
using UnityEngine;

namespace Fishy.Chat
{
    /// <summary>
    /// Baja el banco de preguntas desde la base al arrancar, para que los chats con
    /// NPCs sospechosos usen el contenido de la BD y no la copia empaquetada.
    ///
    /// <b>Por qué hace falta.</b> El chat leía únicamente
    /// <c>Resources/banco_preguntas.json</c>. Ese archivo es el mismo del que
    /// <c>manage.py cargar_banco</c> llena la tabla, así que el contenido coincidía…
    /// hasta que alguien corrige un texto en la base: eso no llegaba al juego hasta la
    /// siguiente compilación. Ahora sí.
    ///
    /// <b>Nunca bloquea.</b> El JSON de Resources se sigue usando mientras la descarga
    /// va en camino, y se queda si falla, si no hay sesión o si se juega en modo local.
    /// El chat no puede quedarse sin contenido por no haber red: cada conversación se
    /// arma en el momento de abrirla, así que la que se abra antes de que llegue la
    /// respuesta usa la copia local y las siguientes ya usan la base.
    ///
    /// Es contenido global, no progreso: no depende de la partida y se baja una sola
    /// vez por ejecución.
    /// </summary>
    [DisallowMultipleComponent]
    public class BancoBackendSync : MonoBehaviour
    {
        public static BancoBackendSync Instance { get; private set; }

        [Tooltip("Segundos entre intentos mientras se espera a que haya sesión iniciada.")]
        [Min(0.5f)]
        public float esperaEntreIntentos = 1f;

        [Tooltip("Cuántos segundos esperar a que aparezca la sesión antes de rendirse " +
                 "y quedarse con la copia de Resources. 0 = esperar siempre.")]
        [Min(0f)]
        public float tiempoMaximoDeEspera = 30f;

        [Tooltip("Escribir en consola el resultado de la descarga.")]
        public bool verboseLogs = true;

        /// <summary>True cuando el banco en memoria ya viene de la base.</summary>
        public bool DescargadoDeLaBase { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (Instance != null) return;
            var encontrado = FindAnyObjectByType<BancoBackendSync>();
            if (encontrado != null) return;

            new GameObject("BancoBackendSync").AddComponent<BancoBackendSync>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start() => StartCoroutine(Bajar());

        private IEnumerator Bajar()
        {
            float esperado = 0f;

            // La sesión se inicia en la pantalla de acceso, que puede ir por delante o
            // por detrás de esta escena según cómo se arranque el juego. Se espera en
            // vez de darlo por perdido en el primer frame.
            while (true)
            {
                var api = ApiManager.Instance;
                if (api != null && !api.IsLocalMode && api.IsLoggedIn) break;

                if (tiempoMaximoDeEspera > 0f && esperado >= tiempoMaximoDeEspera)
                {
                    if (verboseLogs)
                        Debug.Log("[BancoBackendSync] Sin sesión tras " +
                                  $"{tiempoMaximoDeEspera:F0}s: el chat usará la copia de Resources.");
                    yield break;
                }

                yield return new WaitForSecondsRealtime(esperaEntreIntentos);
                esperado += esperaEntreIntentos;
            }

            ApiManager.Instance.ObtenerPreguntasBanco(
                onSuccess: preguntas =>
                {
                    BancoPreguntasLoader.AplicarDesdeBackend(preguntas);
                    DescargadoDeLaBase = true;
                },
                onError: e => Debug.LogWarning(
                    $"[BancoBackendSync] No se pudo bajar el banco ({e}); " +
                    "el chat sigue con la copia de Resources."));
        }

        /// <summary>Vuelve a pedir el banco. Para el editor y las pruebas: en una
        /// partida normal se baja una vez al arrancar.</summary>
        public void Rebajar()
        {
            DescargadoDeLaBase = false;
            StopAllCoroutines();
            StartCoroutine(Bajar());
        }
    }
}
