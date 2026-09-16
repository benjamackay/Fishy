using Fishy.Mision;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base de los disparadores de misión: componentes que esperan a que UNA misión
/// concreta llegue a cierto estado y disparan lo que se les haya enganchado en el
/// inspector.
///
/// Es la pieza que permite que una misión sea el disparador de cualquier cosa —abrir
/// una zona, encender un NPC, mostrar un cartel, arrancar una cinemática— sin que la
/// misión sepa nada de ello. Se pone un GameObject por cada cosa que reaccione, y
/// cada uno espera lo suyo.
///
/// La parte delicada vive aquí y no en cada disparador porque es la misma en todos y
/// es fácil de romper: hay que atender <b>dos caminos</b> y disparar una sola vez
/// entre los dos.
///
/// <b>Por qué dos caminos.</b> <see cref="MissionManager"/> no anuncia lo que
/// restaura: <c>PrecargarCompletados</c> marca las misiones en silencio y
/// <c>PrecargarConocidos</c> registra con <c>anunciar: false</c>, justamente para no
/// tratar lo viejo como novedad. Un disparador que solo escuchara el evento "en vivo"
/// no se enteraría jamás de lo que pasó en sesiones anteriores, y al cargar la partida
/// el mundo aparecería sin los efectos ya ganados: la puerta que se abrió la semana
/// pasada, cerrada otra vez. Lo que sí se dispara siempre es
/// <c>onPanelActualizado</c>, así que ese es el aviso al que engancharse para el
/// camino de la restauración.
///
/// <b>Y por qué dos eventos.</b> No es lo mismo que algo ocurra ahora —hay que
/// celebrarlo: cinemática, cartel, sonido— que cargar una partida donde ya había
/// ocurrido, donde solo hay que dejar el mundo como quedó, en silencio. Reproducir la
/// cinemática de apertura de zona cada vez que el niño/a abre el juego sería
/// felicitarle por algo de la semana pasada. Es la misma distinción que ya hacía
/// <c>BosqueDesconocidosNPC.RestaurarComoTerminado</c>.
/// </summary>
public abstract class DisparadorDeMision : MonoBehaviour
{
    [Tooltip("La misión que se está esperando. Es la misma ficha que lleva el " +
             "MissionGiver del NPC que la entrega.")]
    public DesafioData mision;

    [Tooltip("Id de la misión en el catálogo (Resources/misiones.json o la base). Se " +
             "usa cuando 'Mision' se deja vacío — es la única forma de apuntar a una " +
             "misión que solo existe en el catálogo, sin ficha en el proyecto, que es " +
             "el caso normal cuando el contenido viene de datos.")]
    public string misionId = "";

    [Tooltip("Escribir en consola cada disparo.")]
    public bool verboseLogs;

    /// <summary>Se dispara una sola vez. Volver a completar la misma misión —o
    /// recargar el panel— no repite el efecto.</summary>
    public bool YaDisparado { get; private set; }

    /// <summary>Lo que pasa cuando ocurre ahora, con el niño/a delante.</summary>
    protected abstract UnityEvent EnVivo { get; }

    /// <summary>Lo que pasa cuando ya había ocurrido al cargar la partida.</summary>
    protected abstract UnityEvent AlRestaurar { get; }

    /// <summary>Consulta directa del estado, para el camino de la restauración.</summary>
    protected abstract bool YaSeCumple(MissionManager manager, string misionId);

    /// <summary>Engancha el evento "en vivo" que le corresponda a este disparador.</summary>
    protected abstract void Suscribir(MissionManager manager);
    protected abstract void Desuscribir(MissionManager manager);

    /// <summary>Cómo se llama lo que se espera, para el log. Ej: "completada".</summary>
    protected abstract string Momento { get; }

    private MissionManager manager;

    private void OnEnable()
    {
        ResolverMision();

        if (mision == null || string.IsNullOrEmpty(mision.desafioId))
        {
            Debug.LogWarning($"[{name}] {GetType().Name} sin misión asignada (ni " +
                             "'Mision' ni un 'Mision Id' que esté en el catálogo): " +
                             "no va a disparar nunca.", this);
            return;
        }

        manager = MissionManager.GetOrCreate();
        Suscribir(manager);
        manager.onPanelActualizado.AddListener(RevisarSiYaEstaba);

        // Comprobación de entrada, por si la partida ya venía cargada antes de que
        // este objeto existiera y no queda ningún refresco por delante.
        RevisarSiYaEstaba();
    }

    private void OnDisable()
    {
        if (manager == null) return;
        Desuscribir(manager);
        manager.onPanelActualizado.RemoveListener(RevisarSiYaEstaba);
    }

    /// <summary>
    /// Rellena <see cref="mision"/> desde el catálogo cuando no se arrastró ninguna
    /// ficha. Lo puesto a mano manda: si ya hay una ficha, esto no la toca.
    /// </summary>
    private void ResolverMision()
    {
        if (mision != null) return;
        if (string.IsNullOrWhiteSpace(misionId)) return;
        mision = Fishy.Mision.CatalogoMisiones.Ficha(misionId.Trim());
    }

    /// <summary>¿El aviso que acaba de llegar es de la misión que espero?</summary>
    protected bool EsMiMision(DesafioRuntime runtime) =>
        runtime != null && mision != null && runtime.Id == mision.desafioId;

    /// <summary>La llaman los disparadores desde su evento "en vivo".</summary>
    protected void DispararEnVivo(DesafioRuntime runtime)
    {
        if (YaDisparado || !EsMiMision(runtime)) return;
        Disparar(EnVivo, $"{Momento} ahora");
    }

    /// <summary>
    /// El camino de la partida cargada. Se apoya en que <see cref="MissionManager"/>
    /// dispara siempre su evento concreto ANTES que <c>onPanelActualizado</c>: si
    /// acaba de ocurrir de verdad, <see cref="DispararEnVivo"/> ya marcó el disparo
    /// y esto no vuelve a entrar.
    /// </summary>
    private void RevisarSiYaEstaba()
    {
        if (YaDisparado || manager == null) return;
        if (!YaSeCumple(manager, mision.desafioId)) return;

        Disparar(AlRestaurar, $"ya estaba {Momento}");
    }

    private void Disparar(UnityEvent evento, string motivo)
    {
        YaDisparado = true;

        if (verboseLogs)
            Debug.Log($"[{GetType().Name}] '{mision.desafioId}' → {motivo}, " +
                      $"disparando '{name}'.", this);

        evento?.Invoke();
    }

    /// <summary>
    /// Rearma el disparador. Solo para pruebas y para el editor: en una partida
    /// normal cada cosa pasa una vez.
    /// </summary>
    public void Rearmar() => YaDisparado = false;
}
