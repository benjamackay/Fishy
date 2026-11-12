using System.Collections.Generic;
using Fishy.Mision;
using UnityEngine;

/// <summary>
/// Hace que un NPC entregue VARIAS misiones, una detrás de otra, y que diga algo
/// distinto en cada momento según cómo vayan.
///
/// El caso que lo pedía: Huemul entrega «Conoce el Bosque»; cuando está hecha, la
/// primera secundaria; cuando esa también, la tercera; y cuando no le queda nada,
/// se despide. Entremedias, si se le vuelve a hablar, recuerda lo que falta o comenta
/// la misión que el niño/a lleva entre manos.
///
/// <b>Qué lo diferencia de <see cref="MissionGiver"/>.</b> Aquél entrega UNA misión y
/// sigue siendo el camino correcto para la mayoría de los NPC —y el único que sabe de
/// chats y de casos detective, además de diálogos—. Este es para los NPC que llevan
/// una cadena. Son caminos paralelos y excluyentes: los dos entregarían, así que si
/// están en el mismo objeto se avisa y este se queda quieto. Es la misma convivencia
/// que <see cref="ConexionAutomaticaMisiones"/> y
/// <see cref="EntregarMisionDelCatalogo"/>: el caso simple por un lado y el que
/// necesita más por otro.
///
/// <b>Qué lo diferencia de <c>DialogoNeutroNPC.dialogosSiguientes</c>.</b> Esa lista
/// avanza al terminar cada charla, o sea que cuenta conversaciones. Con ella, hablarle
/// dos veces a Huemul antes de hacer nada ya lo deja en la conversación de la segunda
/// misión. Aquí el diálogo es una función del estado de las misiones, así que es
/// correcto en todo momento y al cargar la partida. También son excluyentes, y también
/// se avisa.
///
/// Hace pareja con <see cref="PresenciaSegunMision"/>: aquél decide si el NPC está, y
/// este qué dice cuando está. La decisión de qué toca decir vive en
/// <see cref="GuionDelNpc"/>, en el ensamblado <c>Fishy.Mision</c>, porque es la parte
/// que se prueba con NUnit.
/// </summary>
[DisallowMultipleComponent]
public class CadenaDeMisionesDelNpc : MonoBehaviour
{
    [Header("La cadena, en orden")]
    [Tooltip("Las misiones que este NPC entrega, en el orden en que las entrega. " +
             "Cada paso lleva el diálogo con el que la da y, opcionalmente, el que " +
             "dice mientras esa misión sigue en curso.")]
    public List<PasoDelGuion> pasos = new List<PasoDelGuion>();

    [Header("Cuando no tiene nada que dar")]
    [Tooltip("Diálogos según la misión que esté activa en ese momento, aunque no sea " +
             "de este NPC. Se lee de arriba abajo y gana la primera que encaje. " +
             "Sirve para que el NPC comente lo que el niño/a trae entre manos.")]
    public List<ReglaPorMisionActiva> reglasPorMisionActiva = new List<ReglaPorMisionActiva>();

    [Tooltip("Lo que dice cuando ya entregó toda su cadena. Vacío = se queda con el " +
             "último diálogo que dijo.")]
    public string dialogoAlFinal = "";

    [Tooltip("Escribir en consola cada cambio de diálogo y cada entrega.")]
    public bool verboseLogs;

    /// <summary>En qué situación está el NPC según la última revisión. Lo lee quien
    /// quiera reaccionar a ella —un gesto, un icono sobre su cabeza— sin tener que
    /// repetir la cuenta; <c>verboseLogs</c> la escribe en consola.</summary>
    public QueDiceElNpc QueEstaDiciendo { get; private set; }

    private DialogoNeutroNPC npc;
    private MissionManager _manager;

    /// <summary>El paso que se entregaría al cerrar el diálogo que el NPC tiene puesto
    /// ahora. -1 = el diálogo de ahora no entrega nada.</summary>
    private int _pasoPorEntregar = -1;

    private bool _desactivadoPorConflicto;

    private void Awake()
    {
        npc = GetComponent<DialogoNeutroNPC>();

        if (npc == null)
        {
            Debug.LogError($"[CadenaDeMisionesDelNpc] '{name}' no tiene un " +
                           "DialogoNeutroNPC al lado, que es a quien le cambia el " +
                           "diálogo. Este componente no va a hacer nada.", this);
            _desactivadoPorConflicto = true;
            return;
        }

        if (GetComponent<MissionGiver>() != null)
        {
            Debug.LogError($"[CadenaDeMisionesDelNpc] '{name}' tiene además un " +
                           "MissionGiver: los dos entregan misiones y se pisarían. " +
                           "Quita uno —si este NPC lleva una cadena, el MissionGiver " +
                           "sobra—. Por ahora manda el MissionGiver y la cadena se " +
                           "queda quieta.", this);
            _desactivadoPorConflicto = true;
            return;
        }

        if (npc.TieneVariosDialogos)
        {
            Debug.LogError($"[CadenaDeMisionesDelNpc] '{name}' tiene llena la lista " +
                           "'Dialogos Siguientes' del DialogoNeutroNPC, que avanza " +
                           "contando conversaciones, mientras esta cadena va por el " +
                           "estado de las misiones. Las dos se pelearían por el mismo " +
                           "diálogo. Vacía esa lista y pon sus textos como pasos de " +
                           "aquí. Por ahora la cadena se queda quieta.", this);
            _desactivadoPorConflicto = true;
            return;
        }

        AvisarDeLaConfiguracionQueNoSuena();
    }

    private void OnEnable()
    {
        if (_desactivadoPorConflicto) return;

        _manager = MissionManager.GetOrCreate();
        _manager.onPanelActualizado.AddListener(Revisar);
        npc.onDialogueEnded.AddListener(AlTerminarLaCharla);

        // El primer reparto es inmediato: el NPC tiene que tener el diálogo correcto
        // antes de que al niño/a le dé tiempo de llegar hasta él.
        Revisar();
    }

    private void OnDisable()
    {
        if (_manager != null) _manager.onPanelActualizado.RemoveListener(Revisar);
        if (npc != null) npc.onDialogueEnded.RemoveListener(AlTerminarLaCharla);
    }

    /// <summary>
    /// Avisa de las configuraciones con las que algo escrito no se va a oír nunca, que
    /// de otra forma solo se notan jugando y con suerte.
    ///
    /// Son dos: un id de misión que nadie conoce —típicamente uno viejo que ya no está
    /// en el catálogo—, que se queda en "sin entregar" para siempre y atasca la cadena
    /// ahí; y un NPC no repetible con textos de respaldo escritos, porque a la segunda
    /// vez que se le habla no reabre el panel y esos textos no se leen.
    /// </summary>
    private void AvisarDeLaConfiguracionQueNoSuena()
    {
        if (pasos == null || pasos.Count == 0)
        {
            Debug.LogWarning($"[CadenaDeMisionesDelNpc] '{name}' no tiene ningún paso: " +
                             "no va a entregar nada.", this);
            return;
        }

        for (int i = 0; i < pasos.Count; i++)
        {
            PasoDelGuion paso = pasos[i];
            string id = paso?.IdMirado;

            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[CadenaDeMisionesDelNpc] '{name}', paso {i + 1}: sin " +
                                 "misión. Se salta, así que ese diálogo no se dirá nunca.",
                                 this);
                continue;
            }

            if (paso.ficha == null && CatalogoMisiones.Buscar(id) == null &&
                CatalogoDesafios.Buscar(id) == null)
            {
                Debug.LogWarning($"[CadenaDeMisionesDelNpc] '{name}', paso {i + 1}: " +
                                 $"'{id}' no está en el catálogo de misiones ni hay ficha " +
                                 "con ese id. Nadie va a poder completarla, así que la " +
                                 "cadena se atasca en este paso. ¿Es un id viejo?", this);
            }

            if (string.IsNullOrWhiteSpace(paso.dialogoDeEntrega))
            {
                Debug.LogWarning($"[CadenaDeMisionesDelNpc] '{name}', paso {i + 1} ({id}): " +
                                 "sin 'Dialogo De Entrega'. La misión se entregaría sin " +
                                 "que el NPC diga nada.", this);
            }
        }

        bool hayRespaldo = !string.IsNullOrWhiteSpace(dialogoAlFinal) ||
                           (reglasPorMisionActiva != null && reglasPorMisionActiva.Count > 0);

        foreach (PasoDelGuion paso in pasos)
            if (paso != null && !string.IsNullOrWhiteSpace(paso.dialogoMientras)) hayRespaldo = true;

        if (hayRespaldo && !npc.repetible)
        {
            Debug.LogWarning($"[CadenaDeMisionesDelNpc] '{name}' tiene textos de respaldo " +
                             "—recordatorios, comentarios o despedida— pero su " +
                             "DialogoNeutroNPC no es 'Repetible', así que al volver a " +
                             "hablarle no se reabre el panel y no se leerán. Marca " +
                             "'Repetible'.", this);
        }
    }

    /// <summary>
    /// Mira cómo van las misiones y le pone al NPC el diálogo que toca.
    ///
    /// Se llama en cada refresco del panel de misiones, que es el mismo aviso que
    /// escucha <see cref="PresenciaSegunMision"/>: cubre tanto lo que pasa en vivo como
    /// la partida que se acaba de cargar, sin preguntar cada frame.
    /// </summary>
    public void Revisar()
    {
        if (_desactivadoPorConflicto || npc == null) return;

        // El grupo de métodos se asigna en dos pasos y no dentro de la llamada: un
        // `cond ? _manager.GetEstado : null` no tiene tipo común y no compila.
        System.Func<string, EstadoDesafio?> estadoDe = null;
        if (_manager != null) estadoDe = _manager.GetEstado;

        string misionActiva = _manager != null && _manager.Activa != null
            ? _manager.Activa.Id
            : null;

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(
            pasos, estadoDe, misionActiva, reglasPorMisionActiva, dialogoAlFinal);

        QueEstaDiciendo = dice.Que;

        // Solo se marca para entregar si el diálogo que se va a oír es el de entrega.
        // Sin esto, cerrar un comentario o un recordatorio entregaría la misión del
        // paso pendiente sin que el NPC la haya dado.
        _pasoPorEntregar = dice.Que == QueDiceElNpc.Entregar ? dice.Paso : -1;

        if (dice.Que == QueDiceElNpc.Nada) return;

        string antes = npc.DialogoActualId;
        npc.PonerDialogo(dice.DialogoId);

        if (verboseLogs && npc.DialogoActualId != antes)
        {
            Debug.Log($"[CadenaDeMisionesDelNpc] '{name}' pasa a {dice.Que}: " +
                      $"'{npc.DialogoActualId}'" +
                      (dice.Paso >= 0 ? $" (paso {dice.Paso + 1}/{pasos.Count})" : ""), this);
        }
    }

    /// <summary>
    /// Terminó una conversación. Si era la de entrega, se registra la misión y se
    /// vuelve a resolver, que es lo que deja al NPC listo para lo siguiente.
    /// </summary>
    private void AlTerminarLaCharla()
    {
        if (_desactivadoPorConflicto) return;

        int paso = _pasoPorEntregar;
        _pasoPorEntregar = -1;

        if (paso >= 0 && paso < pasos.Count && EsElDialogoQueSeAcabaDeOir(pasos[paso]))
            Entregar(pasos[paso]);

        // Siempre, no solo tras entregar: puede haberse completado algo durante la
        // charla —el propio objetivo de "hablar con" este NPC, por ejemplo— y entonces
        // lo que toca decir ya es otra cosa.
        Revisar();
    }

    /// <summary>
    /// Si lo que acaba de terminar es el diálogo de entrega de ese paso.
    ///
    /// Se compara con <c>UltimoDialogoTerminado</c> y no con el diálogo puesto, porque
    /// para cuando esto corre el NPC puede haber cambiado ya de diálogo; y porque es el
    /// mismo criterio que usa el objetivo de "hablar con" para no confundir dos
    /// conversaciones del mismo NPC.
    /// </summary>
    private bool EsElDialogoQueSeAcabaDeOir(PasoDelGuion paso)
    {
        if (paso == null || string.IsNullOrWhiteSpace(paso.dialogoDeEntrega)) return false;

        return string.Equals(npc.UltimoDialogoTerminado, paso.dialogoDeEntrega.Trim(),
            System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Registra la misión del paso y la pone a seguir.
    ///
    /// Es lo mismo que hace <see cref="MissionGiver.Entregar"/> en su primera entrega,
    /// repetido aquí en vez de compartido a propósito: aquél arrastra su propia
    /// configuración —entrega al volver, zona a desbloquear, resolución tardía del
    /// catálogo— y mezclar las dos cabezas en una sola sería más difícil de seguir que
    /// estas quince líneas. Si algún día hacen falta las dos cosas en el mismo NPC,
    /// este es el sitio por donde empezar a unirlas.
    /// </summary>
    private void Entregar(PasoDelGuion paso)
    {
        string id = paso.IdMirado;
        DesafioData ficha = paso.ficha != null ? paso.ficha : CatalogoMisiones.Ficha(id);

        if (ficha == null)
        {
            Debug.LogWarning($"[CadenaDeMisionesDelNpc] '{name}' iba a entregar '{id}' " +
                             "pero no hay ficha: ni arrastrada aquí ni fabricable desde " +
                             "el catálogo. La cadena se queda en este paso.", this);
            return;
        }

        MissionManager manager = MissionManager.GetOrCreate();
        DesafioRuntime runtime = manager.RegistrarDesafioDisponible(ficha);

        if (verboseLogs)
            Debug.Log($"[CadenaDeMisionesDelNpc] '{name}' entrega '{id}'.", this);

        // Una partida cargada puede traerla ya completa: ponerle objetivos otra vez le
        // borraría el avance.
        if (runtime != null && runtime.estado == EstadoDesafio.Completado) return;

        MissionTracker.GetOrCreate().Seguir(ficha, ObjetivosDe(paso, id));
    }

    /// <summary>
    /// Los objetivos de la misión: los del catálogo, que es donde vive el contenido.
    /// Una lista vacía deja la misión informativa, igual que en
    /// <see cref="MissionGiver"/>: habrá que completarla desde otro sitio.
    /// </summary>
    private static List<ObjetivoMision> ObjetivosDe(PasoDelGuion paso, string id)
    {
        var objetivos = new List<ObjetivoMision>();

        MisionRegistro registro = CatalogoMisiones.Buscar(id);
        if (registro == null) return objetivos;

        foreach (ObjetivoRegistro o in registro.ObjetivosEnOrden())
        {
            ObjetivoMision objetivo = ObjetivoMision.DesdeRegistro(o);
            if (objetivo != null) objetivos.Add(objetivo);
        }

        return objetivos;
    }
}
