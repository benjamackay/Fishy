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
    [Tooltip("Ficha del desafío. Su 'desafioId' debe ser único en todo el juego. " +
             "Si se deja vacía se usa 'Mision Id' y la misión se saca del catálogo.")]
    public DesafioData desafio;

    [Tooltip("Id de la misión en el catálogo (la base de datos, o el archivo de " +
             "respaldo Resources/misiones.json). Se usa cuando no hay ficha arrastrada " +
             "arriba. Ej: MISION_SEC_MASCOTA_COIPO.")]
    public string misionId = "";

    [Tooltip("Qué hay que hacer para completarla. Si se deja vacía se toman los " +
             "objetivos del catálogo; si el catálogo tampoco trae ninguno, la misión " +
             "queda sólo informativa y habrá que completarla desde otro script.")]
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

    public string mensajeDesbloqueo = "✨ ¡Nueva zona desbloqueada!";
    public string mensajeMisionPendiente = "Aún no has completado la misión.";

    private NPC npc;
    private bool entregada;
    private bool recompensaEntregada;

    private void Awake()
    {
        npc = GetComponent<NPC>();

        ResolverDesdeCatalogo();

        if (desafio == null)
            Debug.LogWarning($"[{name}] MissionGiver sin 'Desafio' ni 'Mision Id' válido: " +
                             "no va a entregar nada.", this);

        npc.onDialogueEnded.AddListener(Entregar);
    }

    /// <summary>
    /// Rellena la ficha y los objetivos desde el catálogo cuando no están puestos a
    /// mano. Es lo que permite que el contenido viva en la base de datos en vez de en
    /// el Inspector, con el archivo de respaldo detrás si no hay conexión.
    ///
    /// <b>Lo del Inspector manda.</b> Una ficha arrastrada no se reemplaza y una lista
    /// de objetivos con algo dentro no se toca: hay misiones cableadas a mano que
    /// apuntan a objetos concretos de la escena, y el catálogo no tiene por qué
    /// saberlo.
    ///
    /// Se mira también el catálogo que llegue más tarde: si al arrancar sólo estaba el
    /// archivo de respaldo y después responde la base, se vuelve a resolver. Por eso
    /// no basta con hacerlo una vez en Awake.
    /// </summary>
    private void ResolverDesdeCatalogo()
    {
        string id = !string.IsNullOrWhiteSpace(misionId)
            ? misionId.Trim()
            : (desafio != null ? desafio.desafioId : null);

        if (string.IsNullOrWhiteSpace(id)) return;

        MisionRegistro registro = CatalogoMisiones.Buscar(id);
        if (registro == null)
        {
            if (desafio == null)
                Debug.LogWarning($"[{name}] '{id}' no está en el catálogo de misiones " +
                                 "(ni en la base ni en Resources/misiones.json), y aquí no " +
                                 "hay ficha arrastrada. Esta misión no se va a poder entregar.",
                                 this);
            return;
        }

        if (desafio == null) desafio = CatalogoMisiones.Ficha(id);

        if (objetivos == null || objetivos.Count == 0)
        {
            objetivos = new List<ObjetivoMision>();
            foreach (ObjetivoRegistro o in registro.ObjetivosEnOrden())
            {
                ObjetivoMision objetivo = ObjetivoMision.DesdeRegistro(o);
                if (objetivo != null) objetivos.Add(objetivo);
            }

            if (objetivos.Count > 0)
                Debug.Log($"[{name}] {objetivos.Count} objetivo(s) tomados del catálogo " +
                          $"para '{id}' (origen: {CatalogoMisiones.DeDonde}).", this);
        }
    }

    private void OnEnable()  => CatalogoMisiones.OnCatalogoCambiado += AlCambiarElCatalogo;
    private void OnDisable() => CatalogoMisiones.OnCatalogoCambiado -= AlCambiarElCatalogo;

    /// <summary>
    /// Llegó el catálogo de la base y reemplazó al del archivo. Sólo se vuelve a
    /// resolver si esta misión todavía no se entregó: cambiarle los objetivos a una
    /// misión que el niño/a ya está haciendo le borraría el avance a media partida.
    /// </summary>
    private void AlCambiarElCatalogo()
    {
        if (entregada) return;
        ResolverDesdeCatalogo();
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
