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
                onLoaded?.Invoke(LoadLocal(resourcePath));
                return;
            }

            api.ObtenerCasoDetective(casoId,
                onSuccess: dto => onLoaded?.Invoke(FromDto(dto)),
                onError: e =>
                {
                    Debug.LogWarning($"[Detective] No se pudo obtener '{casoId}' del backend " +
                                      $"({e}); usando respaldo local '{resourcePath}'.");
                    onLoaded?.Invoke(LoadLocal(resourcePath));
                });
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
                npcObservado1 = npcs.Count > 0 ? npcs[0] : dto.permiso_npc_nombre,
                npcObservado2 = npcs.Count > 1 ? npcs[1] : "",
                mensajePermiso = dto.permiso_player_text,
                mensajes = mensajes,
                explicacionGuiada = mensajesDto
                    .Where(m => !string.IsNullOrEmpty(m.explicacion))
                    .Select(m => new ExplicacionEntry { mensajeId = m.mensaje_id, explicacion = m.explicacion })
                    .ToList(),
            };

            Debug.Log($"[Detective] Caso cargado (backend): {caso.caseId} ({caso.mensajes.Count} mensajes)");
            return caso;
        }

        private static DetectiveCase LoadLocal(string resourcePath)
        {
            TextAsset json = Resources.Load<TextAsset>(resourcePath);
            if (json == null)
            {
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
