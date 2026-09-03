using System.Collections.Generic;
using Fishy.Mision;
using Fishy.World;
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

    [Header("Entrega al volver")]
    [Tooltip("Si está activo, al hablar nuevamente con este NPC después de completar " +
             "la misión se entrega la recompensa y se desbloquea la zona indicada.")]
    public bool requiereVolverParaEntregar;

    [Tooltip("Zona que se desbloquea al entregar la misión completada.")]
    public BlockedZone zonaADesbloquear;

    [Tooltip("Muestra el movimiento de cámara y el aviso antes de abrir la zona.")]
    public bool usarCinematicaDesbloqueo = true;

    public string mensajeDesbloqueo = "¡Zona 2 desbloqueada!";
    public string mensajeMisionPendiente = "Aún no has completado la misión.";

    private NPC npc;
    private bool entregada;
    private bool recompensaEntregada;

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

    /// <summary>
    /// En la primera conversación registra la misión. En las siguientes, si la
    /// misión está completa, permite entregarla y desbloquea su zona.
    /// </summary>
    public void Entregar()
    {
        if (desafio == null) return;

        MissionManager manager = MissionManager.GetOrCreate();

        if (!entregada)
        {
            DesafioRuntime runtime = manager.RegistrarDesafioDisponible(desafio);
            entregada = true;

            // También cubre una partida cargada donde la misión ya estaba completa.
            if (runtime != null && runtime.estado == EstadoDesafio.Completado)
            {
                IntentarEntregarCompletada(manager);
                return;
            }

            MissionTracker.GetOrCreate().Seguir(desafio, objetivos);
            return;
        }

        if (requiereVolverParaEntregar)
        {
            IntentarEntregarCompletada(manager);
            return;
        }

        if (!soloUnaVez)
        {
            manager.RegistrarDesafioDisponible(desafio);
            MissionTracker.GetOrCreate().Seguir(desafio, objetivos);
        }
    }

    private void IntentarEntregarCompletada(MissionManager manager)
    {
        if (!requiereVolverParaEntregar || recompensaEntregada) return;

        if (!manager.EstaCompletado(desafio.desafioId))
        {
            if (!string.IsNullOrWhiteSpace(mensajeMisionPendiente))
                ZonePopupUI.Show(mensajeMisionPendiente);
            return;
        }

        if (zonaADesbloquear == null)
        {
            Debug.LogWarning(
                $"[{name}] La misión está completa, pero falta asignar 'Zona A Desbloquear'.",
                this);
            return;
        }

        recompensaEntregada = true;

        if (!zonaADesbloquear.isLocked) return;

        if (usarCinematicaDesbloqueo)
            ZoneUnlockCinematic.GetOrCreate().Play(zonaADesbloquear, mensajeDesbloqueo);
        else
            zonaADesbloquear.Unlock();
    }
}
