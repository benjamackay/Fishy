using System;
using System.Collections.Generic;
using Fishy.Mision;
using Fishy.World;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Vigila los objetivos de las misiones entregadas y marca la misión como
/// completada en <see cref="MissionManager"/> cuando se cumplen todos.
///
/// Es singleton persistente y NO vive en el NPC que entregó la misión: si lo
/// hiciera, alejarse del NPC o que éste se desactive (como hace el del Bosque)
/// dejaría de seguir el progreso a media misión.
///
/// Los objetos se detectan escuchando <see cref="InventoryManager.OnInventoryChanged"/>;
/// las zonas, escuchando <see cref="ZonaActual.OnZonaCambiada"/>;
/// las conversaciones, suscribiéndose al evento que las cierra: el
/// <c>onDialogueEnded</c> del NPC, o el <c>onChatClosed</c> del PhoneChatLauncher
/// cuando el objetivo es un chat de celular. Ojo: haber hablado (o haber atendido
/// el chat) ANTES de recibir la misión no cuenta —no hay historial—, hay que
/// volver a hacerlo; y si el launcher no tiene 'repetible' activo y ya se disparó, no
/// se volverá a abrir solo.
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

    /// <summary>Objetivos cuyo evento ya está enganchado, para no engancharlo dos veces
    /// al reintentar. Por identidad de objeto, que es lo que los distingue.</summary>
    private readonly HashSet<ObjetivoMision> _suscritos = new HashSet<ObjetivoMision>();

    /// <summary>
    /// Algún objetivo pasó a cumplido. La misión puede seguir en curso: esto es
    /// "2/3 en vez de 1/3", no "terminada".
    ///
    /// Existe para el HUD de HDU-16, que muestra el resumen de objetivos de la misión
    /// activa y sin esto sólo se enteraría al completarse la misión entera — o sea,
    /// justo cuando el resumen deja de importar.
    ///
    /// También avisa al empezar a seguir una misión: sus objetivos pasan a existir y
    /// hay que pintarlos.
    /// </summary>
    public event Action OnProgresoCambiado;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // El cambio de zona es lo que cumple los objetivos de tipo LlegarAZona. Se
        // escucha aquí, una sola vez y para todos los seguimientos, por lo mismo que
        // el inventario: el evento es estático, así que no hace falta que ZonaActual
        // exista todavía cuando el rastreador arranca.
        ZonaActual.OnZonaCambiada += AlCambiarDeZona;
    }

    private void OnDestroy()
    {
        if (suscritoAlInventario && InventoryManager.instance != null)
            InventoryManager.instance.OnInventoryChanged -= RevisarTodo;

        ZonaActual.OnZonaCambiada -= AlCambiarDeZona;
    }

    private void AlCambiarDeZona(string anterior, string nueva) => RevisarTodo();

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

        SuscribirPendientes(seguimiento);

        // El inventario puede cambiar por cualquier vía, así que una sola suscripción
        // global y se revisan todos los seguimientos.
        if (!suscritoAlInventario)
        {
            InventoryManager.Instance.OnInventoryChanged += RevisarTodo;
            suscritoAlInventario = true;
        }

        // Puede que el objeto ya estuviera en la mochila antes de aceptar la misión.
        Revisar(seguimiento);

        // Quien entrega la misión la registra en el MissionManager antes de llamar
        // aquí, así que el cartel ya se pintó con ella pero sin objetivos. Sin este
        // aviso se quedaba así hasta el siguiente cambio de zona o de inventario.
        OnProgresoCambiado?.Invoke();
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

    /// <summary>
    /// Zona del primer objetivo "llegar a zona" que siga pendiente, o null si no hay
    /// ninguno.
    ///
    /// Es el respaldo del indicador de HDU-16: la zona de destino se declara en la
    /// ficha (<see cref="DesafioData.zonaObjetivo"/>), pero una misión cuyo objetivo
    /// literal es ir a un sitio ya lo dice ahí, y obligar a escribirlo dos veces es
    /// pedir que un día no coincidan.
    /// </summary>
    public string ZonaPendiente(string desafioId)
    {
        Seguimiento seguimiento = Buscar(desafioId);
        if (seguimiento == null) return null;

        foreach (ObjetivoMision objetivo in seguimiento.objetivos)
        {
            if (objetivo.tipo != TipoObjetivo.LlegarAZona || objetivo.cumplido) continue;
            if (string.IsNullOrWhiteSpace(objetivo.zonaDestino)) continue;
            return objetivo.zonaDestino.Trim();
        }
        return null;
    }

    /// <summary>
    /// Engancha el evento que cumple cada objetivo que todavía no esté enganchado.
    ///
    /// <b>Se reintenta, y por eso es un método aparte.</b> Un objetivo que llegó de la
    /// base apunta a un NPC por su id de diálogo, y ese NPC puede no existir todavía
    /// cuando la misión se entrega —está en una zona que aún no se abrió, o su objeto
    /// aparece más tarde—. Suscribirse una sola vez, al entregar la misión, dejaría ese
    /// objetivo muerto para el resto de la partida. Así que se vuelve a intentar en
    /// cada revisión, y los que se resuelven se enganchan entonces.
    ///
    /// Un objetivo ya enganchado no se vuelve a enganchar: <see cref="_suscritos"/> los
    /// recuerda, porque suscribirse dos veces al mismo UnityEvent lo dispararía dos
    /// veces y el progreso contaría mal.
    /// </summary>
    private void SuscribirPendientes(Seguimiento seguimiento)
    {
        foreach (ObjetivoMision objetivo in seguimiento.objetivos)
        {
            if (objetivo == null || objetivo.cumplido) continue;
            if (_suscritos.Contains(objetivo)) continue;

            // Sin referencias resueltas no hay evento al que engancharse. Se intentará
            // en la siguiente revisión.
            if (!objetivo.Resolver()) continue;

            // Cada tipo de objetivo dice cuál es el evento que lo cumple; los que se
            // resuelven consultando el mundo (recoger objetos, llegar a una zona)
            // devuelven null y no hay nada que enganchar.
            UnityEvent evento = objetivo.EventoQueLoCumple();
            if (evento == null)
            {
                _suscritos.Add(objetivo);   // resuelto y sin evento: no hay más que hacer
                continue;
            }

            ObjetivoMision capturado = objetivo;   // sin esto la lambda vería el último del bucle
            evento.AddListener(() =>
            {
                if (capturado.cumplido) return;
                capturado.cumplido = true;
                if (verboseLogs)
                    Debug.Log($"[Misiones] Objetivo cumplido: {capturado.Describir()}", this);
                OnProgresoCambiado?.Invoke();
                RevisarTodo();
            });
            _suscritos.Add(objetivo);
        }
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

        // Otra oportunidad para los objetivos cuyo NPC u objeto todavía no existía.
        SuscribirPendientes(seguimiento);

        bool todos = true, avanzo = false;
        foreach (ObjetivoMision objetivo in seguimiento.objetivos)
        {
            bool estabaCumplido = objetivo.cumplido;
            if (!objetivo.Evaluar()) todos = false;
            if (!estabaCumplido && objetivo.cumplido) avanzo = true;
        }

        // Los que se cumplen por evento ya avisaron desde su listener; esto cubre a
        // los que se resuelven consultando el mundo —recoger objetos, llegar a una
        // zona—, que si no avanzarían mudos.
        if (avanzo) OnProgresoCambiado?.Invoke();

        // Una misión sin objetivos es sólo informativa: se queda disponible hasta
        // que alguien la complete a mano con MissionManager.CompletarDesafio().
        if (!todos || seguimiento.objetivos.Count == 0) return;

        seguimiento.completado = true;
        MissionManager.GetOrCreate().CompletarDesafio(seguimiento.desafio);

        if (verboseLogs)
            Debug.Log($"[Misiones] Misión completada: {seguimiento.desafio.titulo}", this);
    }
}
