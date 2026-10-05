using System;
using System.Collections.Generic;
using Fishy.Mision;
using Fishy.Net;
using Fishy.World;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

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
/// cuando el objetivo es un chat de celular.
///
/// <b>Y haberlo hecho ANTES de recibir la misión sí cuenta.</b> Lo lleva
/// <see cref="HistorialDeObjetivos"/>, al que <c>ObjetivoMision.Evaluar</c> pregunta, así
/// que una misión entregada después de la conversación nace ya cumplida. Antes había que
/// repetirla, y con un PhoneChatLauncher sin 'repetible' —que no se vuelve a abrir solo—
/// el objetivo quedaba directamente imposible.
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

    /// <summary>Sube cada vez que se reinicia el rastreador. Los listeners que quedaron
    /// enganchados a un evento de un seguimiento viejo lo comparan y se callan.</summary>
    private int _generacion;
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

    /// <summary>
    /// Un objetivo pasó a cumplido JUGANDO (no al restaurarlo del servidor). Lo escucha
    /// <c>ObjetivosBackendSync</c> para guardarlo. Estático: quien escucha no puede
    /// depender de que este rastreador ya exista.
    /// </summary>
    public static event Action<DesafioData, ObjetivoMision> OnObjetivoCumplido;

    /// <summary>
    /// Marca como cumplidos los objetivos que el servidor ya tenía guardados para esta
    /// partida. <paramref name="yaCumplido"/> recibe (misionId, ordenCatalogo).
    /// Se llama al bajar el avance y también al empezar a seguir una misión, porque
    /// cualquiera de los dos puede llegar primero.
    /// </summary>
    public void AplicarCumplidos(Func<string, int, bool> yaCumplido)
    {
        if (yaCumplido == null) return;
        bool cambio = false;
        foreach (Seguimiento s in seguimientos)
            cambio |= MarcarGuardados(s, yaCumplido);
        if (cambio) { OnProgresoCambiado?.Invoke(); RevisarTodo(); }
    }

    private static bool MarcarGuardados(Seguimiento s, Func<string, int, bool> yaCumplido)
    {
        if (s.desafio == null) return false;
        bool cambio = false;
        foreach (ObjetivoMision o in s.objetivos)
        {
            if (o == null || o.cumplido || o.ordenCatalogo <= 0) continue;
            if (!yaCumplido(s.desafio.desafioId, o.ordenCatalogo)) continue;
            if (!o.Cumplir($"servidor: guardado como cumplido (#{o.ordenCatalogo})")) continue;
            cambio = true;

            if (Instance != null && Instance.verboseLogs)
                Debug.Log($"[Misiones] {s.desafio.desafioId} #{o.ordenCatalogo} ← {o.PorQue}");
        }
        return cambio;
    }

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
        MissionManager.OnPartidaCambiada += Reiniciar;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    /// <summary>
    /// Escena nueva, NPCs nuevos. Los objetivos que se cumplen por evento estaban
    /// enganchados a los de la escena anterior, ya destruidos, y <see cref="_suscritos"/>
    /// impedía volver a engancharlos: al volver del menú a la partida, "hablar con" o
    /// "chatear" no se podían cumplir nunca más. Se olvida lo enganchado y se vuelve a
    /// resolver contra la escena que acaba de cargar; la generación nueva deja mudos los
    /// oyentes viejos, por si alguno sobrevivió al cambio.
    /// </summary>
    private void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (modo != LoadSceneMode.Single) return;

        _generacion++;
        _suscritos.Clear();
        RevisarTodo();
    }

    /// <summary>
    /// Olvida lo que se estaba siguiendo. Es DontDestroyOnLoad, así que sin esto el
    /// perfil siguiente en el mismo PC heredaría los objetivos cumplidos del anterior:
    /// <see cref="Seguir"/> corta si la misión ya está en la lista, y con ella se
    /// quedaba el "cumplido" de otro niño/a.
    /// </summary>
    public void Reiniciar()
    {
        _generacion++;
        seguimientos.Clear();
        _suscritos.Clear();
        OnProgresoCambiado?.Invoke();
    }

    private void OnDestroy()
    {
        if (suscritoAlInventario && InventoryManager.instance != null)
            InventoryManager.instance.OnInventoryChanged -= RevisarTodo;

        ZonaActual.OnZonaCambiada -= AlCambiarDeZona;
        MissionManager.OnPartidaCambiada -= Reiniciar;
        SceneManager.sceneLoaded -= AlCargarEscena;
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

        // Antes que nada: numerar los que vienen del Inspector. Tiene que ir ANTES de
        // MarcarGuardados, que salta los que no tienen número.
        NumerarLosDelInspector(seguimiento);

        // Lo que el servidor ya sabe de esta misión, antes de enganchar eventos: un
        // objetivo cumplido en una sesión anterior no tiene que volver a hacerse.
        MarcarGuardados(seguimiento, ObjetivosBackendSync.YaCumplido);

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

        // El inventario de lo que se está siguiendo, con el estado de salida de cada
        // objetivo. Es lo que permite ver de un vistazo si una misión nació medio hecha
        // y por qué, en vez de descubrirlo cuando ya se completó sola.
        if (verboseLogs)
        {
            Debug.Log($"[Misiones] Empiezo a seguir '{desafio.desafioId}' con " +
                      $"{seguimiento.objetivos.Count} objetivo(s):", this);
            foreach (ObjetivoMision o in seguimiento.objetivos)
            {
                if (o == null) continue;
                string estado = o.cumplido
                    ? $"YA CUMPLIDO ← {o.PorQue ?? "(sin motivo anotado)"}"
                    : "pendiente";
                Debug.Log($"[Misiones]    #{o.ordenCatalogo} {o.tipo} — {o.Describir()} — {estado}", this);
            }
        }

        // Quien entrega la misión la registra en el MissionManager antes de llamar
        // aquí, así que el cartel ya se pintó con ella pero sin objetivos. Sin este
        // aviso se quedaba así hasta el siguiente cambio de zona o de inventario.
        OnProgresoCambiado?.Invoke();
    }

    /// <summary>Objetivos de una misión, para pintarlos en el panel. Vacío si no se sigue.</summary>
    /// <summary>
    /// Les da número a los objetivos armados a mano en el Inspector.
    ///
    /// <b>Por qué.</b> <c>ordenCatalogo</c> es la mitad de la clave con la que el backend
    /// guarda el avance: <c>(mision_id, orden)</c>. Los que vienen del catálogo la traen;
    /// los armados en la escena llegan con 0, y eso los dejaba <b>fuera del guardado por
    /// dos puertas</b>: <c>ObjetivosBackendSync</c> no los sube (<c>ordenCatalogo &lt;= 0</c>)
    /// y <see cref="MarcarGuardados"/> no los restaura. Su avance se perdía entero en cada
    /// recarga, y como además no se restauraban, lo único que sobrevivía era el historial
    /// de lo ya hecho — que al no saber de misiones completaba de más.
    ///
    /// Es lo que le pasaba a «Las llaves del cofre»: sus cuatro objetivos estaban en el
    /// prefab de la Foca y ninguno llegó nunca a la base.
    ///
    /// <b>La posición no es tan buena como el orden del catálogo</b>, porque reordenar la
    /// lista en el Inspector mueve la clave y el avance guardado deja de corresponder. Es
    /// estable mientras nadie la toque, y la alternativa era no guardarlos nunca. Lo
    /// correcto de verdad es que la misión tenga sus objetivos en el catálogo.
    ///
    /// Solo numera si <b>ninguno</b> trae orden: una lista con números es una del
    /// catálogo, e inventarlos encima los pisaría.
    /// </summary>
    private void NumerarLosDelInspector(Seguimiento seguimiento)
    {
        List<ObjetivoMision> objetivos = seguimiento.objetivos;
        if (objetivos == null || objetivos.Count == 0) return;

        foreach (ObjetivoMision o in objetivos)
            if (o != null && o.ordenCatalogo > 0) return;

        for (int i = 0; i < objetivos.Count; i++)
            if (objetivos[i] != null) objetivos[i].ordenCatalogo = i + 1;

        if (verboseLogs)
            Debug.Log($"[Misiones] '{seguimiento.desafio.desafioId}': sus {objetivos.Count} " +
                      "objetivo(s) venían del Inspector sin número de catálogo. Se numeran por " +
                      "posición para que su avance se pueda guardar y restaurar. Lo correcto " +
                      "es ponerlos en el catálogo de misiones.", this);
    }

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
            int generacion = _generacion;
            DesafioData desafioDelObjetivo = seguimiento.desafio;
            evento.AddListener(() =>
            {
                if (generacion != _generacion) return;   // de una partida anterior
                if (capturado.cumplido) return;
                if (!capturado.AceptaElEvento()) return;   // otro diálogo del mismo NPC
                if (!capturado.Cumplir($"evento del mundo ({capturado.tipo})")) return;
                if (verboseLogs)
                    Debug.Log($"[Misiones] Objetivo cumplido ← {capturado.PorQue}: " +
                              $"{capturado.Describir()}  [{desafioDelObjetivo.desafioId} " +
                              $"#{capturado.ordenCatalogo}]", this);
                OnObjetivoCumplido?.Invoke(desafioDelObjetivo, capturado);
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
            if (!estabaCumplido && objetivo.cumplido)
            {
                avanzo = true;
                if (verboseLogs)
                    Debug.Log($"[Misiones] Objetivo cumplido ← {objetivo.PorQue}: " +
                              $"{objetivo.Describir()}  [{seguimiento.desafio.desafioId} " +
                              $"#{objetivo.ordenCatalogo}]", this);
                OnObjetivoCumplido?.Invoke(seguimiento.desafio, objetivo);
            }
        }

        // Los que se cumplen por evento ya avisaron desde su listener; esto cubre a
        // los que se resuelven consultando el mundo —recoger objetos, llegar a una
        // zona—, que si no avanzarían mudos.
        if (avanzo) OnProgresoCambiado?.Invoke();

        // Una misión sin objetivos es sólo informativa: se queda disponible hasta
        // que alguien la complete a mano con MissionManager.CompletarDesafio().
        if (!todos || seguimiento.objetivos.Count == 0) return;

        seguimiento.completado = true;

        if (verboseLogs)
        {
            Debug.Log($"[Misiones] Misión completada: {seguimiento.desafio.titulo} " +
                      $"({seguimiento.desafio.desafioId}). Sus {seguimiento.objetivos.Count} " +
                      "objetivo(s) salieron de:", this);
            foreach (ObjetivoMision o in seguimiento.objetivos)
                if (o != null)
                    Debug.Log($"[Misiones]    #{o.ordenCatalogo} ← {o.PorQue ?? "(sin motivo anotado)"}", this);
        }

        MissionManager.GetOrCreate().CompletarDesafio(seguimiento.desafio, "el rastreador: se cumplieron todos sus objetivos");
    }
}
