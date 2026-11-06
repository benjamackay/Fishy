using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fishy.Album;
using Fishy.Net;

namespace Fishy.Detective
{
    public class DetectiveCaseResult
    {
        public int aciertos;
        public int totalRiesgo;
        public float porcentaje;
        public List<(DetectiveMessage mensaje, string explicacion)> noIdentificados;

        /// <summary>Aciertos que hay que <b>superar</b> para aprobar: con la mitad justa
        /// todavía se repite.</summary>
        public const float UmbralAprobacion = 0.5f;

        public bool DebeOfrecerRepetir => porcentaje <= UmbralAprobacion;
    }

    public class DetectiveCaseManager : MonoBehaviour
    {
        private DetectiveCase _caso;
        private HashSet<string> _marcados = new HashSet<string>();

        /// <summary>
        /// Si el último <see cref="CalcularResultado"/> superó el umbral de aprobación.
        /// Empieza en true a propósito: si se cierra el modo Detective sin haber
        /// confirmado ninguna respuesta —el jugador se arrepiente y sale— no hay
        /// resultado reprobado que justifique bloquear el reintento más adelante.
        /// </summary>
        public bool UltimoAprobado { get; private set; } = true;

        public void CargarCaso(DetectiveCase caso)
        {
            _caso = caso;
            _marcados.Clear();
        }

        public List<DetectiveMessage> GetMensajes() => _caso.mensajes;

        public bool EstaMarcado(string id) => _marcados.Contains(id);

        public void ToggleMarca(string id)
        {
            if (_marcados.Contains(id))
                _marcados.Remove(id);
            else
                _marcados.Add(id);

            Debug.Log($"[Detective] Mensaje {id} {(EstaMarcado(id) ? "marcado" : "desmarcado")}");
        }

        public DetectiveCaseResult CalcularResultado()
        {
            // Solo mensajes de riesgo NO ambiguos cuentan para el puntaje
            var riesgoReal = _caso.mensajes
                .Where(m => m.esRiesgo && !m.esAmbiguo)
                .ToList();

            int aciertos = riesgoReal.Count(m => _marcados.Contains(m.id));
            int total    = riesgoReal.Count;
            float porcentaje = total > 0 ? (float)aciertos / total : 1f;

            // Las explicaciones, por mensajeId. La lista puede venir en null o con
            // ids repetidos; ver DetectiveCase.ExplicacionesPorMensaje.
            var expDict = _caso.ExplicacionesPorMensaje();

            var noIdentificados = riesgoReal
                .Where(m => !_marcados.Contains(m.id))
                .Select(m => (
                    mensaje: m,
                    explicacion: expDict.TryGetValue(m.id, out var exp) ? exp : ""
                ))
                .ToList();

            Debug.Log($"[Detective] Resultado: {aciertos}/{total} ({porcentaje * 100:F0}%)");

            var resultado = new DetectiveCaseResult
            {
                aciertos        = aciertos,
                totalRiesgo     = total,
                porcentaje      = porcentaje,
                noIdentificados = noIdentificados
            };

            UltimoAprobado = !resultado.DebeOfrecerRepetir;

            // HDU-12 CA1 — Cada señal bien marcada queda en el álbum, con el nombre
            // del caso, su zona, su tipo de señal y la explicación.
            //
            // Va acá, y antes de la recompensa y del reporte, por dos razones. Una:
            // este es el ÚNICO instante en que están a la vez el caso completo —título,
            // zona, etiquetas, textos, explicaciones— y el conjunto marcado; el
            // servidor solo guarda ids, así que lo que no se anote ahora no se podrá
            // reconstruir después. Dos: se registra aunque no apruebe. Una señal bien
            // marcada es un acierto del niño/a aunque no alcance el umbral del pin, y
            // el álbum es donde se ve lo que fue aprendiendo, no solo lo que ganó.
            //
            // Y no en DetectiveLauncher al cerrar, porque el niño/a puede pulsar
            // "Repetir" sin salir: cada intento tiene que sumar lo suyo.
            AlbumDetectivePuente.RegistrarBienMarcadas(_caso, _marcados);

            OtorgarRecompensaSiCorresponde(porcentaje);
            OtorgarAlbumSiHayEvidencias();
            ReportarProgreso(resultado);
            return resultado;
        }

        /// <summary>
        /// HDU-11 — Entrega el pin del caso si el jugador superó el umbral de
        /// aciertos —igual que para aprobar, la mitad justa no alcanza—. La recompensa se busca primero en <see cref="_caso"/> (lo que
        /// haya traído el backend con el caso) y, si no vino nada ahí, se cae al
        /// catálogo local <see cref="CatalogoRecompensasDetective"/> — el mismo
        /// contenido, pero embebido en el juego. Cubre tres situaciones con el
        /// mismo código: backend todavía no manda el campo (hoy), backend caído
        /// (Detective siempre tiene respaldo local, ver DetectiveCaseLoader), y
        /// juego sin conexión.
        ///
        /// Guard con GetQuantity a propósito: InventoryManager.AddItem SUMA
        /// cantidad, no la fija (no es idempotente). Sin este guard, repetir un
        /// caso ya aprobado volvería a sumar el pin cada vez.
        /// </summary>
        private void OtorgarRecompensaSiCorresponde(float porcentaje)
        {
            var recompensa = ResolverRecompensa(_caso);
            if (recompensa == null || porcentaje <= recompensa.umbralAciertos) return;

            var item = CatalogoItems.Buscar(recompensa.itemId);
            if (item == null)
            {
                Debug.LogWarning($"[Detective] Recompensa '{recompensa.itemId}' del caso " +
                                  $"{_caso.caseId} no tiene ItemData en Resources/Items.");
                return;
            }

            bool yaLoTiene = InventoryManager.Instance.GetQuantity(item) > 0;
            if (recompensa.noDuplicaAlRepetir && yaLoTiene) return;

            InventoryManager.Instance.AddItem(item, 1);
            Debug.Log($"[Detective] Recompensa entregada: {recompensa.itemId} ({_caso.caseId})");

            DetectiveRewardEvents.RaiseOtorgada(new RecompensaOtorgada(recompensa.itemId, recompensa.nombre));
        }

        private static RecompensaCaso ResolverRecompensa(DetectiveCase caso)
        {
            if (caso.TieneRecompensa)
            {
                return new RecompensaCaso
                {
                    caseId = caso.caseId,
                    itemId = caso.recompensaItemId,
                    nombre = caso.recompensaNombre,
                    accesorioHdu06 = caso.recompensaAccesorioHdu06,
                    umbralAciertos = caso.recompensaUmbralAciertos,
                    noDuplicaAlRepetir = caso.recompensaNoDuplicaAlRepetir,
                };
            }
            return CatalogoRecompensasDetective.Buscar(caso.caseId);
        }

        /// <summary>
        /// HDU-12 — El álbum aparece en la mochila en cuanto hay algo que mirar
        /// dentro.
        ///
        /// Antes colgaba del primer pin, cuando el álbum no era más que la vitrina de
        /// los pines. Ya no sirve: una sola señal bien marcada deja evidencia y no da
        /// pin —el pin pide superar la mitad—, así que atarlo al pin dejaba al niño/a
        /// que acertó una de cuatro con evidencias guardadas y sin ninguna forma de
        /// abrir el álbum para verlas. El ítem es la única puerta de entrada.
        /// </summary>
        private void OtorgarAlbumSiHayEvidencias()
        {
            if (AlbumDeEvidencias.Cantidad == 0) return;

            var album = CatalogoItems.Buscar(AlbumEvidenciasUI.ItemIdAlbum);
            if (album == null)
            {
                Debug.LogWarning($"[Detective] No se encontró el ItemData del álbum " +
                                  $"('{AlbumEvidenciasUI.ItemIdAlbum}') en Resources/Items.");
                return;
            }

            if (InventoryManager.Instance.GetQuantity(album) == 0)
                InventoryManager.Instance.AddItem(album, 1);
        }

        /// <summary>Registro best-effort en el backend: si no hay sesión/partida
        /// activa, o falla la llamada, el resultado ya calculado en memoria sigue
        /// siendo válido para la UI — esto no bloquea nada.</summary>
        private void ReportarProgreso(DetectiveCaseResult resultado)
        {
            var api = ApiManager.Instance;
            if (api == null || api.IsLocalMode || !api.IsLoggedIn || api.PartidaId == null) return;

            // Los marcados se copian AHORA: el jugador puede reabrir el caso antes de
            // que la cola se vacíe, y entonces `_marcados` ya sería otra cosa. Esto es
            // un hecho que ocurrió, no un snapshot del estado actual.
            string casoId = _caso.caseId;
            var marcados = new List<string>(_marcados);
            int aciertos = resultado.aciertos;
            int total = resultado.totalRiesgo;
            float porcentaje = resultado.porcentaje;

            ColaDeCambios.EncolarAppend($"detective:{casoId}",
                (ok, error) =>
                {
                    var actual = ApiManager.Instance;
                    if (actual == null || actual.PartidaId == null) { error("No hay partida."); return; }

                    actual.RegistrarProgresoDetective(casoId, marcados, aciertos, total, porcentaje,
                        onSuccess: _ => ok(),
                        onError: e =>
                        {
                            Debug.LogWarning($"[Detective] No se pudo registrar el progreso: {e}");
                            error(e);
                        });
                },
                $"caso {casoId}");
        }
    }
}