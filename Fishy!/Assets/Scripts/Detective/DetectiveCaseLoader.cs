using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fishy.Net;

namespace Fishy.Detective
{
    public static class DetectiveCaseLoader
    {
        /// <summary>
        /// Carga un caso: intenta el backend primero (ApiManager.ObtenerCasoDetective)
        /// y si no hay sesión, está en modo local, o el caso no existe ahí, cae al
        /// JSON empaquetado en Resources/&lt;resourcePath&gt;.json — el juego nunca se
        /// queda sin caso por falta de conexión.
        /// </summary>
        public static void LoadAsync(string casoId, string resourcePath, Action<DetectiveCase> onLoaded)
        {
            var api = ApiManager.Instance;
            if (api == null || api.IsLocalMode || !api.IsLoggedIn)
            {
                Entregar(LoadLocal(resourcePath), onLoaded);
                return;
            }

            api.ObtenerCasoDetective(casoId,
                onSuccess: dto => Entregar(FromDto(dto), onLoaded),
                onError: e =>
                {
                    Debug.LogWarning($"[Detective] No se pudo obtener '{casoId}' del backend " +
                                      $"({e}); usando respaldo local '{resourcePath}'.");
                    Entregar(LoadLocal(resourcePath), onLoaded);
                });
        }

        /// <summary>
        /// Entrega el caso a quien lo pidió, pero antes aprovecha para completar el
        /// álbum con él (HDU-12).
        ///
        /// <b>Por qué acá.</b> Una evidencia sembrada desde el servidor trae solo los
        /// ids del caso y del mensaje: el backend no guarda ni el texto ni la
        /// explicación. Cada vez que un caso se carga —por cualquier vía, backend o
        /// respaldo local— tenemos esos textos en la mano, así que rellenar con ellos
        /// lo que falte es gratis y no necesita ninguna petición de más.
        /// </summary>
        private static void Entregar(DetectiveCase caso, Action<DetectiveCase> onLoaded)
        {
            if (caso != null) AlbumDetectivePuente.ConciliarDesdeElCaso(caso);
            onLoaded?.Invoke(caso);
        }

        private static DetectiveCase FromDto(CasoDetectiveDto dto)
        {
            var mensajesDto = (dto.mensajes ?? new List<MensajeDetectiveDto>())
                .OrderBy(m => m.orden)
                .ToList();

            var mensajes = mensajesDto
                .Select(m => new DetectiveMessage
                {
                    id = m.mensaje_id,
                    autor = m.npc_sender,
                    texto = m.texto,
                    esRiesgo = m.es_senal_riesgo,
                    esAmbiguo = m.es_ambiguo,
                })
                .ToList();

            // El schema viejo (local) guarda explícitamente los dos NPC observados;
            // el del backend no, así que se derivan de quién manda cada mensaje.
            var npcs = mensajes.Select(m => m.autor).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();

            var caso = new DetectiveCase
            {
                caseId = dto.caso_id,

                // HDU-12 — título, zona y etiquetas: los metadatos que cada
                // evidencia del álbum hereda del caso.
                titulo = dto.titulo,
                zona = dto.zona,
                etiquetasMl = dto.etiquetas_ml ?? new List<string>(),

                npcObservado1 = npcs.Count > 0 ? npcs[0] : dto.permiso_npc_nombre,
                npcObservado2 = npcs.Count > 1 ? npcs[1] : "",
                permisoPlayerText = dto.permiso_player_text,
                permisoNpcNombre = dto.permiso_npc_nombre,
                permisoNpcResponse = dto.permiso_npc_response,
                mensajes = mensajes,
                explicacionGuiada = mensajesDto
                    .Where(m => !string.IsNullOrEmpty(m.explicacion))
                    .Select(m => new ExplicacionEntry { mensajeId = m.mensaje_id, explicacion = m.explicacion })
                    .ToList(),

                // HDU-11 — si el backend todavía no manda estos campos quedan
                // vacíos y DetectiveCaseManager cae al catálogo local.
                recompensaItemId = dto.recompensa_item_id,
                recompensaNombre = dto.recompensa_nombre,
                recompensaAccesorioHdu06 = dto.recompensa_accesorio_hdu06,
                recompensaUmbralAciertos = dto.recompensa_umbral_aciertos,
                recompensaNoDuplicaAlRepetir = dto.recompensa_no_duplica_al_repetir,
            };

            Debug.Log($"[Detective] Caso cargado (backend): {caso.caseId} ({caso.mensajes.Count} mensajes)");
            return caso;
        }

        /// <summary>
        /// El caso tal como viaja empaquetado en Resources. Público porque el álbum lo
        /// reusa para completar sin conexión las evidencias que el servidor le dio solo
        /// como ids (ver <c>AlbumDetectivePuente.ConciliarDesdeElRespaldoLocal</c>).
        ///
        /// <paramref name="avisarSiFalta"/> en false para ese uso: ahí, un caso sin
        /// respaldo local es un dato que no tenemos, no un error — distinto de cuando
        /// es el juego el que necesita el caso para abrirlo.
        /// </summary>
        public static DetectiveCase LoadLocal(string resourcePath, bool avisarSiFalta = true)
        {
            TextAsset json = Resources.Load<TextAsset>(resourcePath);
            if (json == null)
            {
                if (avisarSiFalta)
                    Debug.LogError($"[Detective] No se encontró el archivo en Resources: {resourcePath}");
                return null;
            }

            DetectiveCase caso = JsonUtility.FromJson<DetectiveCase>(json.text);

            if (caso == null)
                Debug.LogError($"[Detective] Error al parsear el JSON: {resourcePath}");
            else
                Debug.Log($"[Detective] Caso cargado (local): {caso.caseId} ({caso.mensajes?.Count} mensajes)");

            return caso;
        }
    }
}
