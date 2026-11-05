using System.Collections.Generic;
using Fishy.Chat;
using Fishy.Detective;
using Fishy.Mision;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Parte el tutorial en misiones de un solo objetivo, encadenadas: terminar una
/// abre la siguiente.
///
/// <b>Por qué una misión por paso y no una con cuatro objetivos.</b> El cartel de la
/// esquina y el dibujo del teclado (<c>TecladoEnPantalla</c>) miran lo que la misión
/// activa tiene pendiente. Con los cuatro objetivos abiertos a la vez, el cartel los
/// enseña todos y el teclado destaca todas sus teclas desde el primer minuto: el
/// niño/a ve cuatro cosas por hacer y ninguna le dice cuál toca ahora. Una misión por
/// paso hace que en cada momento haya exactamente un objetivo pendiente, una línea en
/// el cartel y una tecla encendida.
///
/// La secuencia, que es la misma que va contando el guía (<see cref="TutorialPasos"/>):
///
///   hablar con Huemulcito (todavía sin misión: es quien la entrega)
///     → recoger la lupa        ← la entrega el MissionGiver del huemul, en la escena
///     → hablar con la flamenca
///     → abrir el celular con Tab
///     → resolver el caso de la foca
///   salir del tutorial con Espacio (fuera de misiones, lo lleva TutorialPasos)
///
/// <b>Las tres últimas se arman aquí, en código.</b> No están en el catálogo ni en la
/// base: son del tutorial y de nadie más, y meterlas en el catálogo obligaría al
/// backend a aprenderse dos categorías de objetivo que solo existen en esta escena
/// (ver la nota de <see cref="TipoObjetivo"/>). Por eso también los disparadores de
/// las dos que no saben cerrarse solas —la flamenca y el Tab— están aquí: esta pieza
/// tiene delante las referencias de la escena, y las cierra con
/// <c>MissionTracker.Cumplir</c>.
///
/// Se crea sola en la escena del tutorial y en ninguna otra: si no hay un
/// <see cref="TutorialPasos"/>, no pinta nada.
/// </summary>
[DisallowMultipleComponent]
public class MisionesDelTutorial : MonoBehaviour
{
    /// <summary>
    /// Ids de las tres misiones que se arman aquí. Llevan el prefijo del tutorial
    /// porque el <c>MissionManager</c> las guarda por id junto a las del juego, y un
    /// id repetido haría que una partida normal creyera tenerlas hechas.
    /// </summary>
    public const string IdFlamenca = "TUTORIAL_FLAMENCA";
    public const string IdPanel    = "TUTORIAL_PANEL";
    public const string IdCaso     = "TUTORIAL_CASO";

    /// <summary>Un eslabón: su ficha, su único objetivo y cómo se llama.</summary>
    private class Eslabon
    {
        public DesafioData Ficha;
        public ObjetivoMision Objetivo;
    }

    private readonly List<Eslabon> _cadena = new List<Eslabon>();
    private int _entregados;

    private TutorialPasos _tutorial;
    private ChatModuleLauncher _flamenca;
    private MenuController _panel;
    private string _idDeLaPrimera;

    // ── Solo en la escena del tutorial ───────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCrear()
    {
        // Quitar antes de poner: sin recarga de dominio al entrar en Play, la
        // suscripción de la corrida anterior sigue viva. Misma receta que
        // MissionUIController.
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        CrearSiHayTutorial();
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo) => CrearSiHayTutorial();

    private static void CrearSiHayTutorial()
    {
        if (FindAnyObjectByType<TutorialPasos>() == null) return;
        if (FindAnyObjectByType<MisionesDelTutorial>() != null) return;

        new GameObject("MisionesDelTutorial").AddComponent<MisionesDelTutorial>();
    }

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    private void Start()
    {
        _tutorial = FindAnyObjectByType<TutorialPasos>();
        if (_tutorial == null) { enabled = false; return; }

        // Las piezas se le piden al tutorial, que ya las tiene buscadas y es quien
        // manda en esta escena. Así no hay dos sitios buscando lo mismo.
        _flamenca = _tutorial.npcOpciones;
        _panel    = _tutorial.panelDeJugador;

        ArmarCadena();
        BuscarLaPrimera();

        MissionManager.GetOrCreate().onDesafioCompletado.AddListener(AlCompletarUna);

        if (_flamenca != null) _flamenca.OnSesionFinalizada += AlCerrarElChat;
    }

    private void OnDestroy()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.onDesafioCompletado.RemoveListener(AlCompletarUna);

        if (_flamenca != null) _flamenca.OnSesionFinalizada -= AlCerrarElChat;
    }

    /// <summary>
    /// El disparador del Tab, mirado y no escuchado: abrir el panel no avisa a nadie
    /// —<c>MenuController</c> no tiene evento—, así que se le pregunta mientras esa
    /// misión esté pendiente. Es una propiedad booleana y solo se consulta durante un
    /// paso del tutorial, así que sale gratis.
    /// </summary>
    private void Update()
    {
        if (_panel == null || !_panel.EstaAbierto) return;

        Cumplir(IdPanel, "el tutorial: abrió el panel del celular");
    }

    // ── La cadena ────────────────────────────────────────────────────────────

    private void ArmarCadena()
    {
        _cadena.Clear();

        Agregar(IdFlamenca, "Conoce a la flamenca", new ObjetivoMision
        {
            tipo = TipoObjetivo.ChatearCaraACara,
            descripcion = "Hablar con Rosita y elegir una respuesta",
        });

        Agregar(IdPanel, "Tu celular", new ObjetivoMision
        {
            tipo = TipoObjetivo.AbrirElPanel,
            // Sin descripción a propósito: la escribe el propio objetivo con la tecla
            // de verdad, así que si el celular cambia de tecla, el cartel lo dice.
        });

        Agregar(IdCaso, "El caso de la foca", new ObjetivoMision
        {
            tipo = TipoObjetivo.CompletarCasoDetective,
            detective = _tutorial.npcDetective,
            descripcion = "Revisar el caso con la foca",
        });
    }

    private void Agregar(string id, string titulo, ObjetivoMision objetivo)
    {
        // El caso detective sí sabe cerrarse solo, pero necesita a la foca delante.
        // Sin ella, el eslabón se cae de la cadena en vez de dejarla trabada.
        if (objetivo.tipo == TipoObjetivo.CompletarCasoDetective && objetivo.detective == null)
        {
            Debug.LogWarning($"[Tutorial] Sin NPC del Modo Detective en la escena: me salto " +
                             $"la misión '{id}'.", this);
            return;
        }

        var ficha = ScriptableObject.CreateInstance<DesafioData>();
        ficha.desafioId = id;
        ficha.titulo = titulo;

        // El orden es la mitad de la clave con la que se identifica un objetivo
        // dentro de su misión; con uno solo por misión, siempre es el primero.
        objetivo.ordenCatalogo = 1;

        _cadena.Add(new Eslabon { Ficha = ficha, Objetivo = objetivo });
    }

    /// <summary>
    /// Cuál es la misión que entrega el huemul, para saber tras cuál empieza la
    /// cadena. Se le pregunta al MissionGiver de la escena en vez de escribir el id
    /// aquí: ese id está puesto en una ficha del proyecto y las dos copias se
    /// desincronizarían el día que alguien la cambie.
    /// </summary>
    private void BuscarLaPrimera()
    {
        MissionGiver giver = FindAnyObjectByType<MissionGiver>(FindObjectsInactive.Include);
        _idDeLaPrimera = giver != null && giver.desafio != null ? giver.desafio.desafioId : null;

        if (string.IsNullOrWhiteSpace(_idDeLaPrimera))
            Debug.LogWarning("[Tutorial] No encuentro la misión que entrega el huemul, así que " +
                             "la cadena de misiones del tutorial no va a arrancar.", this);
    }

    private void AlCompletarUna(DesafioRuntime completada)
    {
        if (completada == null || completada.data == null) return;

        string esperada = _entregados == 0 ? _idDeLaPrimera : _cadena[_entregados - 1].Ficha.desafioId;
        if (completada.data.desafioId != esperada) return;

        EntregarLaSiguiente();
    }

    private void EntregarLaSiguiente()
    {
        if (_entregados >= _cadena.Count) return;

        Eslabon eslabon = _cadena[_entregados];
        _entregados++;

        // Mismo par de llamadas que hace el MissionGiver al entregar: la ficha queda
        // disponible y el rastreador se encarga de su objetivo.
        MissionManager.GetOrCreate().RegistrarDesafioDisponible(eslabon.Ficha);
        MissionTracker.GetOrCreate().Seguir(eslabon.Ficha,
            new List<ObjetivoMision> { eslabon.Objetivo });
    }

    // ── Los disparadores que hay que traer a mano ────────────────────────────

    private void AlCerrarElChat(float porcentajeSeguro)
        => Cumplir(IdFlamenca, "el tutorial: cerró el chat de la flamenca");

    /// <summary>Cierra ese objetivo si su misión es la que está en curso. Repetir no
    /// molesta: el rastreador ignora lo ya cumplido.</summary>
    private void Cumplir(string id, string porQue)
    {
        if (MissionTracker.Instance == null) return;
        MissionTracker.Instance.Cumplir(id, orden: 1, porQue);
    }
}
