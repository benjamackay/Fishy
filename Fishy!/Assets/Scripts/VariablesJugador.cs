using System;
using System.Collections.Generic;
using Fishy.Net;
using UnityEngine;

namespace Fishy.World
{
    /// <summary>
    /// El punto único desde el que un NPC pregunta "¿qué ha hecho el jugador hasta
    /// ahora?" antes de decidir qué decir.
    ///
    /// Es deliberadamente genérico: guarda un diccionario de variables con nombre
    /// (hoy solo enteros) y sabe refrescarlas contra el backend. La primera variable
    /// real es <see cref="PresionSocialRetoViral"/> (HDU-04 CA4), pero el mecanismo
    /// no sabe nada de retos virales — cualquier variable nueva se agrega con su
    /// propia llamada dentro de <see cref="Refrescar"/> y queda disponible con
    /// <see cref="Get"/> para cualquier NPC, sin tocar nada más.
    ///
    /// Quien decide QUÉ hacer con el valor es cada NPC (ver
    /// <see cref="Fishy.Chat.VarianteSegunVariable"/>), no esta clase: esta solo
    /// sabe consultar y guardar. Sin ese reparto, cada variable nueva obligaría a
    /// tocar el código de todos los lanzadores que quisieran usarla.
    ///
    /// <b>Por qué se lee del backend y no se cuenta en Unity.</b> Un contador local
    /// no serviría: una temática puede completarse en varias sesiones (partida
    /// guardada y retomada), así que al volver a abrir el juego el contador
    /// arrancaría siempre en cero. El backend ya guarda cada decisión como un
    /// <c>Mensaje</c>; de ahí se puede derivar el valor sin duplicar el dato en
    /// ningún sitio.
    ///
    /// <b>Por qué el valor es una caché y no se pide en el momento.</b> Un NPC
    /// decide qué escenario mostrar de forma síncrona, en el mismo instante en que
    /// hay que abrir el chat — esperar una respuesta de red ahí dejaría a Otto
    /// parado con el control ya bloqueado. Por eso se refresca por adelantado (al
    /// cambiar de zona) y los NPCs solo leen lo último que se supo. Si todavía no se
    /// supo nada, <see cref="Get"/> devuelve el valor por defecto — 0, "sin
    /// presión" — que es la lectura conservadora.
    /// </summary>
    [DisallowMultipleComponent]
    public class VariablesJugador : MonoBehaviour
    {
        public static VariablesJugador Instance { get; private set; }

        /// <summary>Nivel de presión social del próximo NPC de retos virales
        /// (HDU-04 CA4). 0 = presión base, sube de a uno con cada racha de rechazos
        /// consecutivos. Ver <c>ApiManager.ObtenerPresionSocial</c> y el endpoint
        /// <c>/partidas/{id}/presion-social/</c> para cómo se calcula.</summary>
        public const string PresionSocialRetoViral = "presion_social_reto_viral";

        [Tooltip("Escribir en consola cada refresco y su resultado.")]
        public bool verboseLogs;

        private readonly Dictionary<string, int> valores = new Dictionary<string, int>();
        private bool refrescando;

        // ── Ciclo de vida ─────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear() => GetOrCreate();

        public static VariablesJugador GetOrCreate()
        {
            if (Instance != null) return Instance;

            var encontrado = FindAnyObjectByType<VariablesJugador>();
            if (encontrado != null) return encontrado;

            var go = new GameObject("VariablesJugador");
            return go.AddComponent<VariablesJugador>();
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

        // ── Lectura ──────────────────────────────────────────────────────────

        /// <summary>Último valor conocido de una variable, o <paramref name="porDefecto"/>
        /// si nunca se refrescó (sin sesión, sin partida, o todavía no le tocó).</summary>
        public int Get(string nombre, int porDefecto = 0) =>
            !string.IsNullOrEmpty(nombre) && valores.TryGetValue(nombre, out int v)
                ? v
                : porDefecto;

        // ── Refresco ─────────────────────────────────────────────────────────

        private void AlCambiarDeZona(string anterior, string nueva) => Refrescar();

        /// <summary>
        /// Pide al backend el valor actual de cada variable conocida.
        ///
        /// Es best-effort y nunca bloquea: sin sesión, sin partida, o si la llamada
        /// falla, se queda con lo último que sabía (o con el valor por defecto si
        /// nunca supo nada) y llama a <paramref name="onListo"/> igual. Un NPC nunca
        /// debería quedarse sin poder hablar porque esta consulta no llegó a tiempo.
        /// </summary>
        public void Refrescar(Action onListo = null)
        {
            if (refrescando) { onListo?.Invoke(); return; }

            var api = ApiManager.Instance;
            if (api == null || api.IsLocalMode || !api.IsLoggedIn || api.PartidaId == null)
            {
                onListo?.Invoke();
                return;
            }

            refrescando = true;
            api.ObtenerPresionSocial(
                onSuccess: dto =>
                {
                    refrescando = false;
                    if (dto != null)
                    {
                        valores[PresionSocialRetoViral] = dto.SocialPressureLevel;
                        if (verboseLogs)
                            Debug.Log($"[VariablesJugador] {PresionSocialRetoViral} = " +
                                      $"{dto.SocialPressureLevel} ({dto.rechazos_consecutivos} " +
                                      "rechazo(s) consecutivo(s)).", this);
                    }
                    onListo?.Invoke();
                },
                onError: e =>
                {
                    refrescando = false;
                    Debug.LogWarning($"[VariablesJugador] No se pudo refrescar la presión " +
                                     $"social: {e}. Se mantiene el último valor conocido.");
                    onListo?.Invoke();
                });
        }
    }
}
