using System;
using Fishy.Net;
using UnityEngine;

namespace Fishy.World
{
    /// <summary>
    /// HDU-15 — El punto único desde el que se guarda la partida.
    ///
    /// <b>No sube nada por su cuenta.</b> Los cinco sincronizadores que ya existían
    /// —posición, inventario, objetos recogidos, NPCs y misiones— siguen siendo los
    /// que hablan con el backend. Lo que faltaba era un sitio del que se pueda decir
    /// "guarda ahora, por este motivo", y que se pueda probar.
    ///
    /// Hace falta porque cada sincronizador decidía su propio momento: unos por
    /// temporizador, otros al cambiar algo. Así no había forma de comprobar cuándo se
    /// guarda la partida, ni de forzarlo desde una prueba.
    ///
    /// <b>Se guarda en dos momentos, y solo en esos dos:</b> al cambiar de zona y al
    /// cerrar el juego. Hubo además un guardado periódico y otro al terminar cada
    /// interacción (chat, desbloqueo de zona); se quitaron a propósito. Los
    /// sincronizadores ya suben lo suyo en cuanto cambia, así que aquellos no tapaban
    /// un hueco de datos, y a cambio llenaban la consola y el tráfico de guardados que
    /// no guardaban nada. Si se vuelven a echar de menos, lo que falta es un motivo
    /// nuevo en <see cref="Motivo"/>, no reabrir los de antes.
    ///
    /// Se crea solo y sobrevive entre escenas.
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveManager : MonoBehaviour
    {
        /// <summary>Por qué se está guardando. Va en el log y en el evento, para que
        /// una prueba pueda afirmar "se guardó por esto y no de casualidad".</summary>
        public enum Motivo
        {
            CambioDeZona,
            CierreDeAplicacion,
            /// <summary>Alguien pidió guardar a mano: una prueba, o el menú de pausa
            /// antes de salir.</summary>
            Manual,
        }

        public static SaveManager Instance { get; private set; }

        [Header("Configuración")]
        [Tooltip("Segundos mínimos entre dos guardados seguidos. Caminar sobre el " +
                 "borde de dos zonas dispara cambios de zona en cadena; esto los " +
                 "agrupa. El cierre del juego se salta este freno.")]
        [Min(0f)]
        public float esperaMinima = 1f;

        [Tooltip("Escribir en consola cada guardado y su motivo.")]
        public bool verboseLogs = true;

        /// <summary>Se dispara después de PEDIR el guardado, no después de que el
        /// backend conteste: las peticiones quedan en vuelo. Trae el motivo.</summary>
        public static event Action<Motivo> OnGuardado;

        /// <summary>Último motivo por el que se guardó. Para diagnóstico y pruebas.</summary>
        public Motivo UltimoMotivo { get; private set; } = Motivo.Manual;

        /// <summary>Cuántas veces se ha guardado en esta sesión.</summary>
        public int Guardados { get; private set; }

        /// <summary>Zona en la que estaba Otto en el último guardado. Ver la nota de
        /// <see cref="GuardarZona"/> sobre por qué todavía no viaja al backend.</summary>
        public string ZonaGuardada { get; private set; } = "";

        private float _ultimoGuardado = float.NegativeInfinity;

        // ── Ciclo de vida ─────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear() => GetOrCreate();

        public static SaveManager GetOrCreate()
        {
            if (Instance != null) return Instance;

            var encontrado = FindAnyObjectByType<SaveManager>();
            if (encontrado != null) return encontrado;

            var go = new GameObject("SaveManager");
            return go.AddComponent<SaveManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            ZonaActual.OnZonaCambiada += AlCambiarDeZona;
        }

        private void OnDisable()
        {
            ZonaActual.OnZonaCambiada -= AlCambiarDeZona;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Los dos momentos ─────────────────────────────────────────────────

        private void AlCambiarDeZona(string anterior, string nueva)
        {
            Guardar(Motivo.CambioDeZona);
        }

        private void OnApplicationQuit() => Guardar(Motivo.CierreDeAplicacion);

        private void OnApplicationPause(bool pausado)
        {
            // En móvil `OnApplicationQuit` muchas veces no llega: el sistema mata la
            // app pausada sin avisar. Esta es la única señal fiable de "se va".
            if (pausado) Guardar(Motivo.CierreDeAplicacion);
        }

        // ── API pública ──────────────────────────────────────────────────────

        /// <summary>
        /// Guarda el estado de la partida.
        ///
        /// Pide a cada sincronizador que suba lo suyo ahora en vez de esperar su
        /// propio ritmo. Es idempotente y barato: si nada cambió, cada uno decide no
        /// mandar nada.
        /// </summary>
        public void Guardar(Motivo motivo)
        {
            // El cierre nunca se frena: es la última oportunidad que hay.
            bool urgente = motivo == Motivo.CierreDeAplicacion;
            if (!urgente && Time.unscaledTime - _ultimoGuardado < esperaMinima) return;

            var api = ApiManager.Instance;
            if (api == null || api.PartidaId == null)
            {
                // Sin partida no hay dónde guardar. No es un error: pasa en el menú y
                // en modo local.
                return;
            }

            _ultimoGuardado = Time.unscaledTime;
            UltimoMotivo = motivo;
            Guardados++;

            // Los dos momentos que quedan son de los que puede no haber otro después,
            // así que se fuerza siempre: que cada sincronizador mande lo suyo aunque
            // crea que no cambió nada. Cuando existía el guardado periódico, aquel NO
            // forzaba —era una red de seguridad, no una orden— y por eso hacía falta
            // distinguir. Ahora no.
            GuardarZona();
            PersonajeBackendSync.Instance?.GuardarAhora();
            InventarioBackendSync.Instance?.GuardarAhora();

            if (verboseLogs)
                Debug.Log($"[SaveManager] Guardado #{Guardados} por {motivo}" +
                          (string.IsNullOrEmpty(ZonaGuardada) ? "" : $" (zona: {ZonaGuardada})"), this);

            OnGuardado?.Invoke(motivo);
        }

        /// <summary>
        /// Anota en qué zona está Otto, y la deja lista para que viaje.
        ///
        /// No sube nada por su cuenta —como todo en esta clase—: quien la manda es
        /// <c>PersonajeBackendSync.GuardarPosicion()</c>, que la lee de
        /// <see cref="ZonaGuardada"/> y la mete en el mismo PATCH que la posición.
        /// Por eso esto corre justo antes de pedirle que suba, en <see cref="Guardar"/>.
        ///
        /// Va a la columna `zona_actual` de `PersonajeJugador` (migración 0012). Ojo:
        /// esa columna es la REGIÓN DEL MAPA (`zona_2`), y no tiene nada que ver con
        /// `ZonaProgreso.zona`, que es la temática del banco (`desconocidos`). Son dos
        /// vocabularios distintos y no hay tabla que los relacione.
        /// </summary>
        private void GuardarZona()
        {
            var zonas = ZonaActual.Instance;
            if (zonas == null) return;

            // Revisar en vez de leer Actual: entre tic y tic pueden pasar centésimas,
            // y guardar la zona de la que Otto acaba de salir sería justo el error.
            ZonaGuardada = zonas.Revisar();
        }
    }
}
