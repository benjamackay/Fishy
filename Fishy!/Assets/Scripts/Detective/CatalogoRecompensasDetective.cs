using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishy.Detective
{
    /// <summary>Recompensa de un caso: qué ítem, a partir de qué umbral, y si se
    /// vuelve a entregar al repetir el caso.</summary>
    [Serializable]
    public class RecompensaCaso
    {
        public string caseId;
        public string itemId;
        public string nombre;
        public string accesorioHdu06;
        public float umbralAciertos;
        public bool noDuplicaAlRepetir;
    }

    [Serializable]
    internal class RecompensasWrapper
    {
        public List<RecompensaCaso> recompensas;
    }

    /// <summary>
    /// HDU-11 — Encuentra la recompensa de un caso por su <c>caseId</c>.
    ///
    /// Vive separada del contenido del caso (mensajes, permiso) a propósito: así
    /// la recompensa se resuelve igual sin importar si el caso vino del backend o
    /// del respaldo local en Resources, y agregar/editar un pin no obliga a tocar
    /// <see cref="DetectiveCase"/> ni los JSON de cada caso. Mismo patrón "catálogo
    /// por id + Resources" que ya usa <c>CatalogoItems</c>.
    /// </summary>
    public static class CatalogoRecompensasDetective
    {
        private const string RutaResource = "detective_recompensas";

        private static Dictionary<string, RecompensaCaso> _porCaseId;

        /// <summary>Recompensa del caso, o null si no tiene.</summary>
        public static RecompensaCaso Buscar(string caseId)
        {
            if (string.IsNullOrWhiteSpace(caseId)) return null;
            Asegurar();
            return _porCaseId.TryGetValue(caseId.Trim(), out var recompensa) ? recompensa : null;
        }

        private static void Asegurar()
        {
            if (_porCaseId != null) return;

            _porCaseId = new Dictionary<string, RecompensaCaso>();

            TextAsset json = Resources.Load<TextAsset>(RutaResource);
            if (json == null)
            {
                Debug.LogWarning($"[Detective] No se encontró Resources/{RutaResource}.json: " +
                                  "ningún caso entregará recompensa.");
                return;
            }

            var wrapper = JsonUtility.FromJson<RecompensasWrapper>(json.text);
            if (wrapper?.recompensas == null) return;

            foreach (var r in wrapper.recompensas)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.caseId)) continue;

                string id = r.caseId.Trim();
                if (_porCaseId.ContainsKey(id))
                {
                    Debug.LogError($"[Detective] Recompensa duplicada para '{id}' en " +
                                    $"{RutaResource}.json. Se queda la primera.");
                    continue;
                }
                _porCaseId[id] = r;
            }
        }
    }
}
