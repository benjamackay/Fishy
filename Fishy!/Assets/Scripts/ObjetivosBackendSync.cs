using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Mision;

namespace Fishy.Net
{
    /// <summary>
    /// Avance POR OBJETIVO dentro de una misión (B.3 de REQUISITOS_BD).
    ///
    /// Hasta ahora sólo se guardaba la misión completa: una misión de 4 objetivos con 3
    /// hechos volvía a 0 al cerrar el juego. Esto guarda cada objetivo cumplido y, al
    /// entrar a la partida, lo devuelve a <see cref="MissionTracker"/>.
    ///
    /// No se sincroniza <c>recoger_objeto</c>: el backend documenta que ese avance se
    /// recalcula de la mochila, que ya se guarda aparte. Guardarlo aquí sería una segunda
    /// fuente de verdad que podría contradecir a la primera.
    ///
    /// Sólo viajan los objetivos que vienen del catálogo (<c>ordenCatalogo</c> &gt; 0):
    /// la clave en el servidor es (mision_id, orden), y uno armado a mano no la tiene.
    ///
    /// Vive aparte de <see cref="MisionBackendSync"/> por el mismo motivo que aquél: la
    /// misión no puede llamar a ApiManager. Se crea solo; no hay nada que arrastrar.
    /// </summary>
    public class ObjetivosBackendSync : MonoBehaviour
    {
        private const float EsperaEntreIntentos = 1f;

        /// <summary>Lo que el servidor (o esta sesión) ya tiene como cumplido: misión → órdenes.</summary>
        private static readonly Dictionary<string, HashSet<int>> Cumplidos =
            new Dictionary<string, HashSet<int>>();

        /// <summary>Tras un fallo se espera esto antes de reintentar: un servidor sin el
        /// endpoint no debe recibir (ni loguear) una petición por segundo.</summary>
        private const float EsperaTrasFallo = 20f;

        /// <summary>De qué partida son los datos de <see cref="Cumplidos"/>. Sin esto, tras
        /// cambiar de perfil se verían los objetivos del anterior hasta el siguiente tic.</summary>
        private static int? _partidaDeLosDatos;

        private int? _partidaDescargada;
        private float _noAntesDe;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void LimpiarEstadoEstatico()
        {
            Cumplidos.Clear();
            _partidaDeLosDatos = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (FindAnyObjectByType<ObjetivosBackendSync>() != null) return;
            DontDestroyOnLoad(new GameObject("ObjetivosBackendSync").AddComponent<ObjetivosBackendSync>());
        }

        /// <summary>¿Este objetivo ya figura cumplido? Lo consulta el rastreador al empezar a seguir una misión.</summary>
        public static bool YaCumplido(string misionId, int orden)
            => !string.IsNullOrEmpty(misionId) &&
               _partidaDeLosDatos != null && _partidaDeLosDatos == ApiManager.Instance?.PartidaId &&
               Cumplidos.TryGetValue(misionId, out var ordenes) && ordenes.Contains(orden);

        /// <summary>Deja los datos en memoria como de esta partida, descartando los de otra.</summary>
        private static void AtarA(int partida)
        {
            if (_partidaDeLosDatos == partida) return;
            Cumplidos.Clear();
            _partidaDeLosDatos = partida;
        }

        private void OnEnable()  => MissionTracker.OnObjetivoCumplido += AlCumplirObjetivo;
        private void OnDisable() => MissionTracker.OnObjetivoCumplido -= AlCumplirObjetivo;

        private void Start() => StartCoroutine(EsperarPartidaYBajar());

        private IEnumerator EsperarPartidaYBajar()
        {
            var espera = new WaitForSeconds(EsperaEntreIntentos);
            while (true)
            {
                var api = ApiManager.Instance;
                if (api != null && api.PartidaId != null && api.IsLoggedIn && !api.IsLocalMode &&
                    _partidaDescargada != api.PartidaId && Time.realtimeSinceStartup >= _noAntesDe)
                {
                    int partida = api.PartidaId.Value;
                    _partidaDescargada = partida;
                    AtarA(partida);
                    api.ObtenerProgresoObjetivos(partida,
                        onSuccess: lista => Aplicar(partida, lista),
                        onError: e =>
                        {
                            Debug.LogWarning($"[ObjetivosBackendSync] No se pudo bajar el avance por objetivo: {e}");
                            if (_partidaDescargada == partida)
                            {
                                _partidaDescargada = null;
                                _noAntesDe = Time.realtimeSinceStartup + EsperaTrasFallo;
                            }
                        });
                }
                yield return espera;
            }
        }

        private void Aplicar(int partida, List<ObjetivoProgresoDto> lista)
        {
            if (ApiManager.Instance == null || ApiManager.Instance.PartidaId != partida || lista == null) return;
            AtarA(partida);

            foreach (var o in lista)
            {
                if (o == null || !o.cumplido || string.IsNullOrEmpty(o.mision_id)) continue;
                Agregar(o.mision_id, o.orden);
            }

            MissionTracker.Instance?.AplicarCumplidos(YaCumplido);
        }

        private static bool Agregar(string misionId, int orden)
        {
            if (!Cumplidos.TryGetValue(misionId, out var ordenes))
                Cumplidos[misionId] = ordenes = new HashSet<int>();
            return ordenes.Add(orden);
        }

        private void AlCumplirObjetivo(DesafioData desafio, ObjetivoMision objetivo)
        {
            if (desafio == null || objetivo == null || string.IsNullOrEmpty(desafio.desafioId)) return;
            if (objetivo.ordenCatalogo <= 0 || objetivo.tipo == TipoObjetivo.RecogerObjeto) return;

            var api = ApiManager.Instance;
            if (api == null || api.IsLocalMode || !api.IsLoggedIn || api.PartidaId == null) return;

            string misionId = desafio.desafioId;
            int orden = objetivo.ordenCatalogo;
            AtarA(api.PartidaId.Value);

            // Camino de ida: si ya figura cumplido no hay nada que mandar.
            if (!Agregar(misionId, orden)) return;

            ColaDeCambios.EncolarAppend($"objetivo:{misionId}:{orden}",
                (ok, error) =>
                {
                    var actual = ApiManager.Instance;
                    if (actual == null || actual.PartidaId == null) { error("No hay partida."); return; }

                    actual.RegistrarProgresoObjetivo(misionId, orden, true,
                        onSuccess: _ => ok(),
                        onError: e =>
                        {
                            Debug.LogWarning($"[ObjetivosBackendSync] No se pudo guardar {misionId} #{orden}: {e}");
                            error(e);
                        });
                },
                $"objetivo {misionId} #{orden}");
        }
    }
}
