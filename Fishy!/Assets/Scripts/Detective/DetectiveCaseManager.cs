using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

            return new DetectiveCaseResult
            {
                aciertos        = aciertos,
                totalRiesgo     = total,
                porcentaje      = porcentaje,
                noIdentificados = noIdentificados
            };
        }
    }
}