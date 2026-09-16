using System.Collections.Generic;
using Fishy.Mision;
using UnityEngine;

/// <summary>
/// Entrega una misión del catálogo por su id, sin que haga falta un NPC que la dé.
///
/// Pensado para engancharse desde un <see cref="DisparadorDeMision"/> (por ejemplo
/// <see cref="AlCompletarMision"/>): "cuando termine ESTA misión, entrega
/// automáticamente AQUELLA otra". El método público no recibe parámetros a propósito
/// —así se puede enganchar directo desde cualquier UnityEvent del inspector—: el id
/// va configurado aquí, en este componente, no en el disparador que lo llama.
///
/// Hace lo mismo que <c>MisionInicial.Entregar()</c>, sin la espera a que la partida
/// esté lista: si esto se está disparando es porque ya se estaba jugando —se acaba de
/// completar otra misión—, así que el contexto de la partida ya existe.
/// </summary>
public class EntregarMisionDelCatalogo : MonoBehaviour
{
    [Tooltip("Id de la misión a entregar, tal como está en Resources/misiones.json o " +
             "en la base. Ej: MISION_NPC_04. Se ignora si se arrastra una ficha abajo.")]
    public string misionId = "";

    [Header("Sin catálogo (opcional)")]
    [Tooltip("Ficha hecha a mano (Assets → Create → Fishy → Mision → Nuevo Desafio). Si " +
             "está puesta manda sobre 'Mision Id': sirve para una misión que no está en " +
             "el catálogo.")]
    public DesafioData mision;

    [Tooltip("Objetivos puestos a mano. Si la lista tiene algo, mandan sobre los del " +
             "catálogo, igual que en MissionGiver.")]
    public List<ObjetivoMision> objetivos = new List<ObjetivoMision>();

    [Tooltip("Escribir en consola qué se entregó.")]
    public bool verboseLogs = true;

    /// <summary>Engánchalo en cualquier UnityEvent: el 'Al Completar' de un
    /// DisparadorDeMision, un botón, el final de una cinemática.</summary>
    public void Entregar()
    {
        // Lo puesto a mano manda sobre el catálogo, como en MissionGiver.
        DesafioData ficha = mision;
        string id = ficha != null ? ficha.desafioId : (misionId ?? "").Trim();

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"[{name}] EntregarMisionDelCatalogo sin 'Mision Id' ni ficha.", this);
            return;
        }

        if (ficha == null) ficha = CatalogoMisiones.Ficha(id);
        if (ficha == null)
        {
            Debug.LogWarning($"[{name}] '{id}' no está en el catálogo de misiones " +
                             "(ni en la base ni en Resources/misiones.json).", this);
            return;
        }

        MissionManager manager = MissionManager.GetOrCreate();

        // Idempotente, igual que MisionInicial: si la partida ya la traía registrada
        // —restaurada, o entregada por otro camino— no se vuelve a anunciar.
        if (manager.GetEstado(ficha.desafioId) == null)
        {
            manager.RegistrarDesafioDisponible(ficha);
            if (verboseLogs)
                Debug.Log($"[EntregarMisionDelCatalogo] '{ficha.titulo}' ({id}) entregada.", this);
        }

        if (manager.EstaCompletado(ficha.desafioId)) return;

        List<ObjetivoMision> lista = objetivos != null && objetivos.Count > 0
            ? objetivos
            : ObjetivosDesdeCatalogo(id);
        if (lista.Count > 0)
            MissionTracker.GetOrCreate().Seguir(ficha, lista);
    }

    private static List<ObjetivoMision> ObjetivosDesdeCatalogo(string misionId)
    {
        var lista = new List<ObjetivoMision>();

        MisionRegistro registro = CatalogoMisiones.Buscar(misionId);
        if (registro == null) return lista;

        foreach (ObjetivoRegistro o in registro.ObjetivosEnOrden())
        {
            ObjetivoMision objetivo = ObjetivoMision.DesdeRegistro(o);
            if (objetivo != null) lista.Add(objetivo);
        }
        return lista;
    }
}
