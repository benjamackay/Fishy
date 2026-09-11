using Fishy.Net;
using UnityEngine;

/// <summary>
/// Marca una temática como completada en el backend (HDU-4 CA5: "marca la temática
/// como completada"). Es lo que lee el reporte del adulto: el progreso de la partida
/// es un porcentaje suelto que no dice cuál se cerró.
///
/// Es el mismo registro que hace BosqueDesconocidosManager al cerrar el Bosque, pero
/// suelto, para engancharlo desde un UnityEvent: el 'Al Completar' de un
/// <see cref="AlCompletarMision"/>. Es best-effort: sin sesión o sin partida no hace
/// nada, y si falla sólo avisa en consola.
/// </summary>
public class RegistrarZonaCompletada : MonoBehaviour
{
    [Tooltip("Slug de la temática en el banco: desconocidos, ciberacoso o reto_viral. " +
             "No el id de la zona en la escena (zona_3).")]
    public string zonaBanco = "reto_viral";

    /// <summary>Sin parámetros para poder engancharlo desde cualquier UnityEvent.</summary>
    public void Registrar()
    {
        if (string.IsNullOrWhiteSpace(zonaBanco))
        {
            Debug.LogWarning($"[{name}] RegistrarZonaCompletada sin 'Zona Banco'.", this);
            return;
        }

        string zona = zonaBanco.Trim();
        ApiManager api = ApiManager.Instance;
        if (api == null || !api.IsLoggedIn || api.PartidaId == null)
        {
            Debug.Log($"[RegistrarZonaCompletada] Sin sesión o sin partida: '{zona}' no " +
                      "queda marcada como completada en la BD.", this);
            return;
        }

        api.RegistrarProgresoZona(zona, completada: true,
            onSuccess: _ => Debug.Log($"[RegistrarZonaCompletada] Zona '{zona}' marcada como completada en la BD.", this),
            onError:   e => Debug.LogWarning($"[RegistrarZonaCompletada] No se pudo marcar '{zona}' como completada: {e}", this));
    }
}
