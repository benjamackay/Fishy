using System.Collections.Generic;
using Fishy.Mision;
using UnityEngine;

/// <summary>
/// Hace que un NPC entregue una misión al terminar de conversar con él.
///
/// Va en el MISMO GameObject que el <see cref="NPC"/>. Se engancha a su
/// <c>onDialogueEnded</c>, así que la misión aparece cuando el niño/a cerró el
/// diálogo, no al primer "hola": si se registrara en Interact() bastaría con
/// rozar al NPC para llenar el panel de misiones.
///
/// La ficha de la misión es un <see cref="DesafioData"/>
/// (Assets → Create → Fishy → Mision → Nuevo Desafio) y los objetivos se
/// configuran aquí en el inspector.
/// </summary>
[RequireComponent(typeof(NPC))]
public class MissionGiver : MonoBehaviour
{
    [Header("Misión que entrega")]
    [Tooltip("Ficha del desafío. Su 'desafioId' debe ser único en todo el juego.")]
    public DesafioData desafio;

    [Tooltip("Qué hay que hacer para completarla. Si se deja vacía, la misión queda " +
             "sólo informativa y habrá que completarla desde otro script.")]
    public List<ObjetivoMision> objetivos = new List<ObjetivoMision>();

    [Header("Comportamiento")]
    [Tooltip("Entregarla una sola vez. Si se desmarca, se vuelve a intentar cada vez " +
             "que termina una conversación (el MissionManager igual ignora duplicados).")]
    public bool soloUnaVez = true;

    private NPC npc;
    private bool entregada;

    private void Awake()
    {
        npc = GetComponent<NPC>();

        if (desafio == null)
            Debug.LogWarning($"[{name}] MissionGiver sin 'Desafio': no va a entregar nada.", this);

        npc.onDialogueEnded.AddListener(Entregar);
    }

    private void OnDestroy()
    {
        if (npc != null) npc.onDialogueEnded.RemoveListener(Entregar);
    }

    /// <summary>Registra la misión y empieza a seguir sus objetivos.</summary>
    public void Entregar()
    {
        if (desafio == null) return;
        if (soloUnaVez && entregada) return;

        entregada = true;
        MissionManager.GetOrCreate().RegistrarDesafioDisponible(desafio);
        MissionTracker.GetOrCreate().Seguir(desafio, objetivos);
    }
}
