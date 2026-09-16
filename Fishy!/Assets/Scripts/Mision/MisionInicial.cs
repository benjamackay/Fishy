using System.Collections;
using System.Collections.Generic;
using Fishy.Mision;
using Fishy.Net;
using UnityEngine;

/// <summary>
/// Entrega una misión sola, al empezar a jugar, sin que haga falta hablar con nadie.
///
/// <b>Por qué hace falta un componente propio.</b> Todas las misiones del juego las
/// entrega un <see cref="MissionGiver"/>, que cuelga del <c>onDialogueEnded</c> de un
/// NPC: la misión aparece cuando el niño/a termina de conversar. Eso no sirve para la
/// primera misión de la historia, que es justamente la que le dice a dónde ir y con
/// quién hablar. Si nadie la entrega, el cartel de misión activa arranca diciendo
/// "por ahora no hay nuevas misiones" y el niño/a no tiene de dónde agarrarse.
///
/// <b>No lleva cuenta de "primera vez" por su lado, y es a propósito.</b>
/// <see cref="MissionManager.RegistrarDesafioDisponible"/> ya respeta el estado que
/// traiga la partida: una misión ya completada se registra completada y no se
/// anuncia. Así que entregarla en cada arranque da el comportamiento correcto sin
/// guardar nada extra — y sin el riesgo clásico de una marca propia de "ya la di"
/// que se desincroniza del progreso real.
///
/// <b>Espera a que la partida esté lista antes de entregar.</b> Dos razones, y las dos
/// son errores que se ven:
///   · <c>ConfigurarPersistenciaParaPartida</c> <b>vacía</b> la lista de misiones al
///     atar el progreso a una partida. Entregar antes de eso significa entregar a la
///     basura.
///   · Hasta que no llega el progreso del servidor no se sabe si esta misión ya
///     estaba hecha. Entregarla antes la anunciaría como novedad a un niño/a que la
///     terminó la semana pasada.
///
/// Montaje: un GameObject vacío en la escena del mundo con este componente. Ver
/// README_CATALOGO_MISIONES.md.
/// </summary>
[DisallowMultipleComponent]
public class MisionInicial : MonoBehaviour
{
    [Header("Qué misión se entrega")]
    [Tooltip("Ficha de la misión. Si se deja vacía se usa 'Mision Id'.")]
    public DesafioData mision;

    [Tooltip("Id de la misión en el catálogo (la base de datos, o el archivo de " +
             "respaldo Resources/misiones.json). Si ESTO y la ficha están vacíos, se " +
             "entrega la primera del catálogo por orden de historia.")]
    public string misionId = "";

    [Header("Qué hay que hacer")]
    [Tooltip("Si se deja vacía se toman los objetivos del catálogo. Para la misión " +
             "inicial lo normal es un solo objetivo de tipo 'Hablar Con Npc'.")]
    public List<ObjetivoMision> objetivos = new List<ObjetivoMision>();

    [Header("Cuándo")]
    [Tooltip("Segundos como máximo esperando a que la partida esté atada y a que llegue " +
             "el progreso del servidor. Pasado el plazo se entrega igual: más vale una " +
             "misión anunciada de más que un niño/a mirando una pantalla sin nada que " +
             "hacer. 0 = esperar siempre (igual que BancoBackendSync).")]
    [Min(0f)]
    public float esperaMaxima = 15f;

    [Tooltip("Escribir en consola qué se entregó y cuándo.")]
    public bool verboseLogs = true;

    /// <summary>Ya se hizo el trabajo. No vuelve a entregar nada.</summary>
    public bool Entregada { get; private set; }

    private void Start() => StartCoroutine(EntregarCuandoSePueda());

    private IEnumerator EntregarCuandoSePueda()
    {
        float desde = Time.realtimeSinceStartup;
        bool SeAgotoElPlazo() => esperaMaxima > 0f &&
                                 Time.realtimeSinceStartup - desde >= esperaMaxima;

        // 0. ¿Hay flujo de partida en esta ejecución? ApiManager no se crea solo: lo
        //    trae la pantalla de acceso y viaja como DontDestroyOnLoad. Si no está, es
        //    que se le dio Play directo a una escena —lo normal en TestZone—, así que
        //    no hay ninguna partida que esperar y esperarla sería tener al niño/a (o a
        //    quien prueba) mirando un cartel vacío durante todo el plazo.
        //    Se le da un momento por si aparece en el mismo frame que esta escena.
        const float GraciaParaElApi = 1f;
        while (ApiManager.Instance == null &&
               Time.realtimeSinceStartup - desde < GraciaParaElApi)
            yield return null;

        if (ApiManager.Instance == null)
        {
            if (verboseLogs)
                Debug.Log("[MisionInicial] No hay ApiManager, así que no hay partida que " +
                          "esperar: entrego la misión ya. El progreso de esta sesión no se " +
                          "va a guardar, que es lo esperable al probar una escena sola.", this);
            Entregar();
            yield break;
        }

        // 1. Que el progreso esté atado a una partida. Antes de esto, registrar una
        //    misión es tirarla: atar el contexto vacía la lista.
        while (!PartidaAtada() && !SeAgotoElPlazo())
            yield return null;

        if (!PartidaAtada() && verboseLogs)
        {
            Debug.Log($"[MisionInicial] Se cumplió el plazo de {esperaMaxima:F0}s sin partida " +
                      "atada. Entrego igual, pero el progreso de esta sesión no se va a " +
                      "guardar. Si estás probando, entra por MenuUno para pasar por el login.", this);
        }

        // 2. Que haya llegado el progreso del servidor, si es que va a llegar. Así una
        //    misión ya terminada no se anuncia como nueva.
        while (HayServidor() && !MisionBackendSync.ProgresoDeMisionesAplicado && !SeAgotoElPlazo())
            yield return null;

        Entregar();
    }

    /// <summary>
    /// <see cref="MissionManager.persistirLocalmente"/> lo enciende
    /// <c>ConfigurarPersistenciaParaPartida</c>, así que sirve de señal pública de que
    /// el contexto de la partida ya está puesto.
    /// </summary>
    private static bool PartidaAtada() =>
        MissionManager.Instance != null && MissionManager.Instance.persistirLocalmente;

    private static bool HayServidor()
    {
        var api = ApiManager.Instance;
        return api != null && !api.IsLocalMode && api.IsLoggedIn;
    }

    /// <summary>
    /// Entrega la misión ya, sin esperar nada. Pública para poder dispararla desde otro
    /// sitio —el final de una cinemática de intro, por ejemplo— y desde el menú
    /// contextual del componente para probarla.
    /// </summary>
    [ContextMenu("Entregar ahora")]
    public void Entregar()
    {
        if (Entregada) return;

        DesafioData ficha = ResolverFicha();
        if (ficha == null || string.IsNullOrWhiteSpace(ficha.desafioId))
        {
            Debug.LogWarning(
                $"[{name}] MisionInicial no sabe qué misión entregar. Arrastra una ficha en " +
                "'Mision', escribe un 'Mision Id' que esté en el catálogo, o deja los dos " +
                "vacíos para que use la primera del catálogo (que ahora mismo está vacío).",
                this);
            return;
        }

        Entregada = true;

        MissionManager manager = MissionManager.GetOrCreate();
        string id = ficha.desafioId;

        // Si ya estaba registrada es porque la partida la restauró. Volver a
        // registrarla no haría daño —es idempotente— pero tampoco aporta, y dejarlo
        // explícito hace que el log diga la verdad de lo que pasó.
        EstadoDesafio? estado = manager.GetEstado(id);
        if (estado == null)
        {
            manager.RegistrarDesafioDisponible(ficha);
            if (verboseLogs)
                Debug.Log($"[MisionInicial] Misión inicial entregada: '{ficha.titulo}' ({id}).", this);
        }
        else if (verboseLogs)
        {
            Debug.Log($"[MisionInicial] '{id}' ya venía en la partida ({estado}). No se " +
                      "vuelve a anunciar.", this);
        }

        if (manager.EstaCompletado(id))
        {
            if (verboseLogs)
                Debug.Log($"[MisionInicial] '{id}' ya estaba completada; no hay objetivos " +
                          "que seguir.", this);
            return;
        }

        // Seguir los objetivos hace falta incluso cuando la misión ya estaba
        // registrada: al restaurar la partida, PrecargarConocidos repuebla el panel
        // pero nadie vuelve a engancharle los objetivos. Para las misiones normales eso
        // lo arregla el MissionGiver en la siguiente conversación; ésta no tiene NPC
        // que la entregue, así que le toca a este componente.
        List<ObjetivoMision> lista = ResolverObjetivos(id);
        if (lista.Count == 0)
        {
            if (verboseLogs)
                Debug.Log($"[MisionInicial] '{id}' no tiene objetivos, así que queda sólo " +
                          "informativa: habrá que completarla desde otro script.", this);
            return;
        }

        MissionTracker.GetOrCreate().Seguir(ficha, lista);
    }

    /// <summary>
    /// Qué ficha entregar. Manda la del Inspector; después el id escrito a mano; y si
    /// no hay ninguna de las dos, la primera del catálogo por orden de historia, que
    /// es lo que "la misión inicial" significa cuando el contenido vive en datos.
    /// </summary>
    private DesafioData ResolverFicha()
    {
        if (mision != null) return mision;

        string id = misionId;
        if (string.IsNullOrWhiteSpace(id))
        {
            List<MisionRegistro> enOrden = CatalogoMisiones.EnOrden();
            if (enOrden.Count == 0) return null;

            id = enOrden[0].mision_id;
            if (verboseLogs)
                Debug.Log($"[MisionInicial] Sin misión indicada: tomo la primera del " +
                          $"catálogo, '{id}' (origen: {CatalogoMisiones.DeDonde}).", this);
        }

        return CatalogoMisiones.Ficha(id);
    }

    /// <summary>
    /// Qué objetivos seguir. Los del Inspector si hay; si no, los del catálogo. Igual
    /// que en <see cref="MissionGiver"/>: lo puesto a mano nunca se pisa.
    /// </summary>
    private List<ObjetivoMision> ResolverObjetivos(string id)
    {
        if (objetivos != null && objetivos.Count > 0) return objetivos;

        var lista = new List<ObjetivoMision>();
        MisionRegistro registro = CatalogoMisiones.Buscar(id);
        if (registro == null) return lista;

        foreach (ObjetivoRegistro o in registro.ObjetivosEnOrden())
        {
            ObjetivoMision objetivo = ObjetivoMision.DesdeRegistro(o);
            if (objetivo != null) lista.Add(objetivo);
        }
        return lista;
    }
}
