using System.Collections.Generic;
using Fishy.Mision;
using UnityEngine;

/// <summary>
/// Vigila los objetivos de las misiones entregadas y marca la misión como
/// completada en <see cref="MissionManager"/> cuando se cumplen todos.
///
/// Es singleton persistente y NO vive en el NPC que entregó la misión: si lo
/// hiciera, alejarse del NPC o que éste se desactive (como hace el del Bosque)
/// dejaría de seguir el progreso a media misión.
///
/// Los objetos se detectan escuchando <see cref="InventoryManager.OnInventoryChanged"/>;
/// las conversaciones, suscribiéndose al <c>onDialogueEnded</c> de cada NPC
/// listado. Ojo: haber hablado con un NPC ANTES de recibir la misión no cuenta
/// (no hay historial), hay que volver a hablarle.
/// </summary>
public class MissionTracker : MonoBehaviour
{
    public static MissionTracker Instance { get; private set; }

    [Tooltip("Escribir en consola cada avance de objetivo.")]
    public bool verboseLogs = true;

    private class Seguimiento
    {
        public DesafioData desafio;
        public List<ObjetivoMision> objetivos;
        public bool completado;
    }

    private readonly List<Seguimiento> seguimientos = new List<Seguimiento>();
    private bool suscritoAlInventario;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (suscritoAlInventario && InventoryManager.instance != null)
            InventoryManager.instance.OnInventoryChanged -= RevisarTodo;
    }

    /// <summary>Devuelve la instancia activa, creándola si no existe.</summary>
    public static MissionTracker GetOrCreate()
    {
        if (Instance == null)
            Instance = new GameObject("MissionTracker").AddComponent<MissionTracker>();
        return Instance;
    }

    /// <summary>
    /// Empieza a seguir los objetivos de una misión recién entregada. Si la misión
    /// ya se estaba siguiendo no hace nada, así que volver a hablarle al NPC no
    /// reinicia el progreso.
    /// </summary>
    public void Seguir(DesafioData desafio, List<ObjetivoMision> objetivos)
    {
        if (desafio == null || string.IsNullOrEmpty(desafio.desafioId)) return;
        if (Buscar(desafio.desafioId) != null) return;

        var seguimiento = new Seguimiento
        {
            desafio = desafio,
            objetivos = objetivos ?? new List<ObjetivoMision>(),
        };
        seguimientos.Add(seguimiento);

        foreach (ObjetivoMision objetivo in seguimiento.objetivos)
        {
            if (objetivo.tipo != TipoObjetivo.HablarConNpc || objetivo.npc == null) continue;

            ObjetivoMision capturado = objetivo;   // sin esto la lambda vería el último del bucle
            objetivo.npc.onDialogueEnded.AddListener(() =>
            {
                if (capturado.cumplido) return;
                capturado.cumplido = true;
                if (verboseLogs)
                    Debug.Log($"[Misiones] Objetivo cumplido: {capturado.Describir()}", this);
                RevisarTodo();
            });
        }

        // El inventario puede cambiar por cualquier vía, así que una sola suscripción
        // global y se revisan todos los seguimientos.
        if (!suscritoAlInventario)
        {
            InventoryManager.Instance.OnInventoryChanged += RevisarTodo;
            suscritoAlInventario = true;
        }

        // Puede que el objeto ya estuviera en la mochila antes de aceptar la misión.
        Revisar(seguimiento);
    }

    /// <summary>Objetivos de una misión, para pintarlos en el panel. Vacío si no se sigue.</summary>
    public IReadOnlyList<ObjetivoMision> Objetivos(string desafioId)
    {
        Seguimiento seguimiento = Buscar(desafioId);
        return seguimiento != null ? seguimiento.objetivos : new List<ObjetivoMision>();
    }

    /// <summary>Progreso como "2/3", o null si esa misión no tiene objetivos seguidos.</summary>
    public string Progreso(string desafioId)
    {
        Seguimiento seguimiento = Buscar(desafioId);
        if (seguimiento == null || seguimiento.objetivos.Count == 0) return null;

        int hechos = 0;
        foreach (ObjetivoMision objetivo in seguimiento.objetivos)
            if (objetivo.cumplido) hechos++;

        return $"{hechos}/{seguimiento.objetivos.Count}";
    }

    private Seguimiento Buscar(string desafioId)
    {
        foreach (Seguimiento seguimiento in seguimientos)
            if (seguimiento.desafio != null && seguimiento.desafio.desafioId == desafioId)
                return seguimiento;
        return null;
    }

    private void RevisarTodo()
    {
        // Copia: completar una misión dispara eventos que podrían tocar la lista.
        var instantanea = new List<Seguimiento>(seguimientos);
        foreach (Seguimiento seguimiento in instantanea) Revisar(seguimiento);
    }

    private void Revisar(Seguimiento seguimiento)
    {
        if (seguimiento.completado) return;

        bool todos = true;
        foreach (ObjetivoMision objetivo in seguimiento.objetivos)
            if (!objetivo.Evaluar()) todos = false;

        // Una misión sin objetivos es sólo informativa: se queda disponible hasta
        // que alguien la complete a mano con MissionManager.CompletarDesafio().
        if (!todos || seguimiento.objetivos.Count == 0) return;

        seguimiento.completado = true;
        MissionManager.GetOrCreate().CompletarDesafio(seguimiento.desafio);

        if (verboseLogs)
            Debug.Log($"[Misiones] Misión completada: {seguimiento.desafio.titulo}", this);
    }
}
