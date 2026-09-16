using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fishy.Net;

namespace Fishy.Detective
{
    public class DetectiveCaseResult
    {
        public int aciertos;
        public int totalRiesgo;
        public float porcentaje;
        public List<(DetectiveMessage mensaje, string explicacion)> noIdentificados;
        public bool DebeOfrecerRepetir => porcentaje < 0.5f;
    }

    public class DetectiveCaseManager : MonoBehaviour
    {
        private DetectiveCase _caso;
        private HashSet<string> _marcados = new HashSet<string>();

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

            // Construye diccionario de explicaciones para lookup rápido
            var expDict = _caso.explicacionGuiada
                .ToDictionary(e => e.mensajeId, e => e.explicacion);

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

            OtorgarRecompensaSiCorresponde(porcentaje);
            ReportarProgreso(resultado);
            return resultado;
        }

        /// <summary>
        /// HDU-11 — Entrega el pin del caso si el jugador llegó al umbral de
        /// aciertos. La recompensa se busca primero en <see cref="_caso"/> (lo que
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
            if (recompensa == null || porcentaje < recompensa.umbralAciertos) return;

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
            OtorgarAlbumSiEsPrimerPin();
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

        /// <summary>Adelanto mínimo de HDU-12: el álbum aparece en la mochila junto
        /// con el primer pin. La versión completa (agrupar por caso, cada señal
        /// individual) queda para esa historia.</summary>
        private void OtorgarAlbumSiEsPrimerPin()
        {
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