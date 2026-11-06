using System;
using System.Collections.Generic;
using System.Linq;
using Fishy.Chat;
using Fishy.Net;
using UnityEngine;

/// <summary>
/// Cómo terminó cada chat: si el jugador acabó rechazando (su última elección fue
/// segura) o no. Es lo que lee <c>ConexionAutomaticaMisiones</c> para decidir una
/// bifurcación como "el segundo reto solo si Otto rechazó el primero"
/// (<c>MisionRegistro.desbloquea_si_acepta</c>).
///
/// <b>Por qué no se le pregunta al backend</b>, que ya calcula los rechazos
/// consecutivos (<c>VariablesJugador</c>): las decisiones del chat se guardan en la
/// cola y no llegan al servidor hasta el siguiente guardado —cambio de zona o
/// cierre—. Justo al terminar un reto el backend todavía no sabe qué se eligió, y
/// la bifurcación saldría siempre por el lado de "no rechazó". Aquí se anota en el
/// momento, con la misma regla que usa el backend.
///
/// Se guarda en PlayerPrefs bajo la partida, igual que <c>HistorialDeObjetivos</c>:
/// si el juego se cierra entre el chat y la entrega de la misión siguiente, al
/// volver la decisión sigue estando. En otro equipo no está, y para eso
/// <c>ConexionAutomaticaMisiones</c> tiene su propio plan de respaldo.
/// </summary>
public static class RechazosEnChats
{
    private const string Prefijo = "Fishy.Rechazos.Partida.";

    private static readonly Dictionary<string, bool> _porEscenario =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>De qué partida es lo que hay en memoria. Se compara en cada uso: así
    /// cambiar de perfil no hereda los retos del anterior y no hace falta que nadie
    /// avise.</summary>
    private static int? _partida;

    // Los estáticos sobreviven a darle Play de nuevo en el editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarEstadoEstatico()
    {
        _porEscenario.Clear();
        _partida = null;
    }

    /// <summary>
    /// Anota cómo terminó la conversación de estos escenarios. Si se cerró sin elegir
    /// nada (<paramref name="ultimaEleccion"/> null) no se anota: no se sabe, y es
    /// mejor que la bifurcación lo trate como desconocido que inventar un resultado.
    /// </summary>
    public static void Anotar(IEnumerable<string> escenarios, OptionSafety? ultimaEleccion)
    {
        if (escenarios == null || ultimaEleccion == null) return;

        AtarALaPartidaActual();
        bool rechazo = ultimaEleccion == OptionSafety.Safe;

        bool algo = false;
        foreach (string id in escenarios)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            _porEscenario[id.Trim()] = rechazo;
            algo = true;
        }

        if (algo) Guardar();
    }

    /// <summary>
    /// Si el jugador terminó rechazando en alguno de estos escenarios (lista separada
    /// por comas, como en los objetivos). Null = no se sabe: no se jugó, o se jugó en
    /// otro equipo.
    /// </summary>
    public static bool? Rechazo(string escenarioIds)
    {
        if (string.IsNullOrWhiteSpace(escenarioIds)) return null;

        AtarALaPartidaActual();
        foreach (string id in escenarioIds.Split(','))
            if (_porEscenario.TryGetValue(id.Trim(), out bool rechazo))
                return rechazo;

        return null;
    }

    private static void AtarALaPartidaActual()
    {
        int? actual = ApiManager.Instance != null ? ApiManager.Instance.PartidaId : null;
        if (actual == _partida) return;

        _porEscenario.Clear();
        _partida = actual;
        if (actual == null) return;

        string guardado = PlayerPrefs.GetString(Prefijo + actual, "");
        foreach (string par in guardado.Split(','))
        {
            int igual = par.IndexOf('=');
            if (igual <= 0) continue;
            _porEscenario[par.Substring(0, igual).Trim()] = par.Substring(igual + 1).Trim() == "1";
        }
    }

    private static void Guardar()
    {
        if (_partida == null) return;   // sin partida, solo en memoria
        PlayerPrefs.SetString(Prefijo + _partida,
            string.Join(",", _porEscenario.Select(p => $"{p.Key}={(p.Value ? 1 : 0)}")));
        PlayerPrefs.Save();
    }
}
