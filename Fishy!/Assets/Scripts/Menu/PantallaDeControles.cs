using Fishy.UI;
using UnityEngine;

/// <summary>
/// La pantalla de controles del menú: enseña qué tecla hace qué.
///
/// <b>Es una pantalla, no una lámina.</b> Se entra desde Opciones y se sale con el botón
/// Volver, igual que se entra y se sale de Opciones: encendiendo un contenedor y
/// apagando el otro. Antes salía como tarjeta encima de los ajustes y tapaba lo que
/// había debajo.
///
/// <b>Las teclas no están escritas aquí ni en la escena.</b> Las dibuja
/// <see cref="TecladoEnPantalla"/>, que se las pregunta a quien las escucha de verdad;
/// en el menú, donde no hay nadie escuchando todavía, caen a las de siempre (WASD y
/// flechas, Shift para correr, E para hablar). Una pantalla de controles que enseña
/// teclas equivocadas es peor que no tener pantalla de controles.
///
/// Se monta la primera vez que se entra y se queda; no se rehace en cada visita.
/// </summary>
[DisallowMultipleComponent]
public class PantallaDeControles : MonoBehaviour
{
    [Tooltip("Dónde se dibuja el teclado. Vacío = este mismo objeto, centrado. Para " +
             "ponerlo en otro sitio, crea un objeto vacío donde lo quieras y arrástralo " +
             "aquí: el teclado se centra en lo que se le diga.")]
    [SerializeField] private Transform donde;

    [Tooltip("Tamaño del teclado, en píxeles de la resolución de referencia: 1 son los " +
             "480 px de ancho con que está diseñado y 1.3 es como lo dibuja la pausa. La " +
             "escala del contenedor se descuenta sola, así que este número significa lo " +
             "mismo donde sea que lo cuelgues. Se aplica cada vez que se entra, así que " +
             "se puede buscar el tamaño en pleno Play.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] private float escala = 1f;

    [Tooltip("Mostrar el título «Controles» y el botón «Volver» que trae el teclado. " +
             "Apagado a propósito: esta pantalla ya pone su título y su propio botón " +
             "Volver, con el aspecto del resto del menú.")]
    [SerializeField] private bool tituloYVolverPropiosDelTeclado = false;

    [Tooltip("La pantalla a la que se vuelve (Opciones). Solo hace falta si se usa el " +
             "botón Volver del teclado o si se cablea un botón a Volver(); si el botón " +
             "de la escena ya hace los dos SetActive, esto puede quedar vacío.")]
    [SerializeField] private GameObject pantallaALaQueVuelve;

    private TecladoEnPantalla teclado;

    private void OnEnable()
    {
        Transform padre = donde != null ? donde : transform;

        if (teclado == null)
            teclado = TecladoEnPantalla.CrearEn(padre, Volver, tituloYVolverPropiosDelTeclado);

        teclado.Escalar(escala / EscalaHeredada(padre));
        teclado.Abrir();
    }

    /// <summary>
    /// Cuánto agranda o achica el contenedor a lo que tenga dentro, contando desde el
    /// Canvas hacia abajo. La pantalla de opciones de este menú está a 2.4 para que
    /// quepan los ajustes; sin descontarlo, el teclado saldría dos veces y media más
    /// grande de lo pedido y habría que compensarlo a ojo en el inspector.
    ///
    /// Se multiplican las escalas locales en vez de usar <c>lossyScale</c> a propósito:
    /// esa incluye la del propio Canvas, que la pone el CanvasScaler según la resolución
    /// —y en el primer frame puede no estar puesta todavía—, así que el tamaño cambiaría
    /// con el tamaño de la ventana en vez de quedarse fijo.
    /// </summary>
    private float EscalaHeredada(Transform padre)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform tope = canvas != null ? canvas.transform : null;

        float acumulada = 1f;
        for (Transform t = padre; t != null && t != tope; t = t.parent)
            acumulada *= t.localScale.x;

        return Mathf.Approximately(acumulada, 0f) ? 1f : acumulada;
    }

    /// <summary>
    /// Vuelve a Opciones. Sirve para cablear un botón propio de la escena (o lo llama el
    /// botón Volver del teclado, si se dejó puesto). Si no hay pantalla configurada no
    /// apaga esta: dejar al jugador en una pantalla vacía es peor que no hacer nada.
    /// </summary>
    public void Volver()
    {
        if (pantallaALaQueVuelve == null)
        {
            Debug.LogWarning("[Controles] No hay pantalla a la que volver configurada en " +
                             "el inspector: el botón no hace nada. Rellena 'Pantalla A La " +
                             "Que Vuelve' o cablea el botón con los dos SetActive.", this);
            return;
        }

        pantallaALaQueVuelve.SetActive(true);
        gameObject.SetActive(false);
    }
}
