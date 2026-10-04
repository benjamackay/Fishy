using System;
using Fishy.Mision;
using UnityEngine;

/// <summary>
/// Pone y quita al NPC del mundo según en qué punto va una misión.
///
/// Se añade al propio NPC y basta con decirle qué misión mira y en qué estados tiene
/// que estar presente. Un NPC que solo aparece cuando ya se puede hablar con él:
/// <c>Visible En = Disponible</c>. Uno que se queda para siempre tras resolver algo:
/// <c>Disponible | Completada</c>.
///
/// <b>Por qué no apaga el GameObject.</b> Un objeto inactivo no recibe nada, ni
/// siquiera OnEnable, así que este componente no podría volver a encenderlo: estaría
/// dormido justo cuando le toca despertar. Por eso el NPC siempre está activo y lo
/// que se apaga es lo que lo hace existir para el jugador —lo que se dibuja, lo que
/// choca y lo que se puede interactuar—. Es también la diferencia con
/// <see cref="DisparadorDeMision"/>, que sirve para "cuando pase X, haz Y" pero no
/// para sostener un estado: aquella es de flanco y esto es de estado, tiene que ser
/// correcto en todo momento, incluido el arranque, y poder volver atrás.
///
/// Las <see cref="CondicionDeDesaparicion"/> que haya en el mismo objeto pueden
/// retrasar la retirada —ver <c>FueraDeCamara</c>—, nunca la aparición.
/// </summary>
[DisallowMultipleComponent]
public class PresenciaSegunMision : MonoBehaviour
{
    /// <summary>
    /// Los tres estados en los que puede estar una misión para quien la mira desde
    /// fuera. Son banderas porque lo normal es estar presente en más de uno.
    /// </summary>
    [Flags]
    public enum Estados
    {
        Nunca = 0,

        /// <summary>Todavía no se entregó: el panel no la conoce.</summary>
        SinEntregar = 1,

        /// <summary>Entregada y sin terminar.</summary>
        Disponible = 2,

        Completada = 4,
    }

    [Header("Misión que se mira")]
    [Tooltip("La ficha de la misión. Es la misma que lleva el MissionGiver del NPC " +
             "que la entrega.")]
    public DesafioData mision;

    [Tooltip("Id en el catálogo (Resources/misiones.json o la base). Se usa cuando " +
             "'Mision' se deja vacío: es la única forma de apuntar a una misión que " +
             "solo existe en los datos, que es el caso normal.")]
    public string misionId = "";

    [Header("Presencia")]
    [Tooltip("Estados de la misión en los que el NPC está en el mundo. En los demás " +
             "no se dibuja, no choca y no se puede interactuar con él.")]
    public Estados visibleEn = Estados.SinEntregar | Estados.Disponible;

    [Tooltip("Escribir en consola cada aparición y cada retirada.")]
    public bool verboseLogs;

    /// <summary>Si el NPC está ahora mismo en el mundo.</summary>
    public bool Presente { get; private set; } = true;

    /// <summary>Tiene que irse, pero alguna condición lo está reteniendo.</summary>
    public bool RetiradaPendiente { get; private set; }

    private MissionManager _manager;
    private CondicionDeDesaparicion[] _condiciones;

    private void OnEnable()
    {
        _condiciones = GetComponents<CondicionDeDesaparicion>();
        _manager = MissionManager.GetOrCreate();
        _manager.onPanelActualizado.AddListener(Revisar);

        // El catálogo puede llegar después de que cargue la escena, y hasta entonces
        // 'misionId' no resuelve a ninguna ficha. Sin esto, un NPC que mira una misión
        // del catálogo se quedaría con el estado de arranque toda la partida.
        CatalogoMisiones.OnCatalogoCambiado += Revisar;

        ResolverMision();

        // El primer reparto es inmediato y sin preguntarle a las condiciones: al
        // arrancar no hay nada que disimular —nadie vio aparecer al NPC— y esperar a
        // que la cámara mire a otro lado dejaría a la vista, durante los primeros
        // segundos, justo a quien no tenía que estar.
        Aplicar(DeberiaEstar(), inmediato: true);
    }

    private void OnDisable()
    {
        if (_manager != null) _manager.onPanelActualizado.RemoveListener(Revisar);
        CatalogoMisiones.OnCatalogoCambiado -= Revisar;
    }

    private void Update()
    {
        // Solo cuesta algo mientras hay una retirada esperando permiso.
        if (RetiradaPendiente) IntentarRetirar();
    }

    /// <summary>Rellena la ficha desde el catálogo si solo se dio el id. Lo puesto a
    /// mano manda, igual que en <see cref="DisparadorDeMision"/>.</summary>
    private void ResolverMision()
    {
        if (mision != null) return;
        if (string.IsNullOrWhiteSpace(misionId)) return;
        mision = CatalogoMisiones.Ficha(misionId.Trim());
    }

    private void Revisar()
    {
        ResolverMision();

        bool deberia = DeberiaEstar();
        if (deberia == Presente)
        {
            // Volvió a su sitio antes de que las condiciones dejaran retirarlo.
            RetiradaPendiente = false;
            return;
        }

        if (deberia)
        {
            Aplicar(true, inmediato: true);
            return;
        }

        RetiradaPendiente = true;
        IntentarRetirar();
    }

    /// <summary>En qué estado está la misión, traducido a bandera.</summary>
    private bool DeberiaEstar()
    {
        if (_manager == null || mision == null || string.IsNullOrEmpty(mision.desafioId))
        {
            // Sin saber qué mirar, se deja como está en la escena: equivocarse hacia
            // "no se ve" escondería contenido sin que nadie se entere.
            return Presente;
        }

        EstadoDesafio? estado = _manager.GetEstado(mision.desafioId);
        Estados ahora = estado == null              ? Estados.SinEntregar
                      : estado == EstadoDesafio.Completado ? Estados.Completada
                                                    : Estados.Disponible;

        return (visibleEn & ahora) != 0;
    }

    private void IntentarRetirar()
    {
        foreach (CondicionDeDesaparicion condicion in _condiciones)
        {
            if (condicion == null || !condicion.isActiveAndEnabled) continue;
            if (condicion.SePuedeDesaparecer()) continue;

            return;   // alguien dice que todavía no
        }

        RetiradaPendiente = false;
        Aplicar(false, inmediato: false);
    }

    /// <summary>
    /// Enciende o apaga lo que hace que el NPC exista para el jugador: lo que se
    /// dibuja, lo que choca y lo que responde a la tecla de interacción.
    ///
    /// Los componentes de este sistema se saltan a propósito: apagarse a sí mismo
    /// dejaría al NPC escondido para siempre, y apagar sus condiciones las dejaría
    /// sin voto en la siguiente vuelta.
    /// </summary>
    private void Aplicar(bool presente, bool inmediato)
    {
        Presente = presente;

        foreach (Renderer dibujo in GetComponentsInChildren<Renderer>(true))
            dibujo.enabled = presente;

        foreach (Collider2D choque in GetComponentsInChildren<Collider2D>(true))
            choque.enabled = presente;

        foreach (MonoBehaviour pieza in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (pieza == null || pieza == this) continue;
            if (pieza is CondicionDeDesaparicion) continue;
            if (pieza is IInteractable) pieza.enabled = presente;
        }

        if (verboseLogs)
            Debug.Log($"[PresenciaSegunMision] '{name}' " +
                      (presente ? "aparece" : "se retira") +
                      $" ({(inmediato ? "al instante" : "con permiso de las condiciones")}), " +
                      $"misión '{mision?.desafioId ?? misionId}'.", this);
    }
}
