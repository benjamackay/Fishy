using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Botón del celular de Otto: abre el menú del Tab con un clic.
///
/// Se pone en un GameObject que ya tenga un <see cref="Button"/> (el dibujo, la posición
/// y el tamaño se acomodan en Unity). Este componente sólo le da comportamiento:
/// <list type="bullet">
///   <item>Al pasar el mouse crece un poco, y al mantenerlo pulsado se achica.</item>
///   <item>Al hacer clic rebota y se sacude, como un celular que vibra, y abre el menú.</item>
///   <item>Se esconde con el menú abierto y con el juego detenido (pausa, diálogos).</item>
/// </list>
///
/// El movimiento es un resorte y no una curva fija: el clic le da un empujón y la
/// escala oscila hasta asentarse, así que si se hace clic varias veces seguidas los
/// rebotes se suman en vez de reiniciarse de golpe.
///
/// Usa tiempo sin escalar, para que se anime igual con el juego en pausa.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CanvasGroup))]
public class BotonCelularMenu : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Conexión")]
    [Tooltip("El controlador del menú. Si se deja vacío se busca solo en la escena.")]
    public MenuController menu;

    [Header("Al pasar el mouse y al pulsar")]
    [Tooltip("Escala con el mouse encima (1 = tamaño normal).")]
    [Range(1f, 1.5f)] public float escalaEncima = 1.1f;

    [Tooltip("Escala mientras se mantiene pulsado.")]
    [Range(0.5f, 1f)] public float escalaPulsado = 0.9f;

    [Header("Al hacer clic")]
    [Tooltip("Cuánto rebota al hacer clic. 0 = nada; más de 12 ya se ve exagerado.")]
    [Range(0f, 20f)] public float fuerzaRebote = 7f;

    [Tooltip("Cuánto se sacude de lado al hacer clic, en grados por segundo de empujón. 0 = no se sacude.")]
    [Range(0f, 1500f)] public float fuerzaSacudida = 500f;

    [Header("Resorte")]
    [Tooltip("Qué tan rápido vuelve a su sitio. Más alto = más nervioso.")]
    [Min(1f)] public float rigidez = 220f;

    [Tooltip("Cuánto tarda en calmarse. Más alto = menos rebotes.")]
    [Min(0f)] public float amortiguacion = 11f;

    [Header("Visibilidad")]
    [Tooltip("Segundos que tarda en desvanecerse o aparecer.")]
    [Min(0.01f)] public float duracionFundido = 0.15f;

    private Button _boton;
    private CanvasGroup _grupo;

    private bool _encima;
    private bool _pulsado;

    private float _escala = 1f, _velEscala;
    private float _angulo, _velAngulo;

    private void Awake()
    {
        _boton = GetComponent<Button>();
        _grupo = GetComponent<CanvasGroup>();

        // El movimiento lo pone este componente: el tinte de color del Button le
        // estorbaría, y con la navegación por teclado Espacio y Enter lo apretarían.
        _boton.transition = Selectable.Transition.None;
        _boton.navigation = new Navigation { mode = Navigation.Mode.None };

        _boton.onClick.AddListener(AlHacerClic);
    }

    private void OnDestroy()
    {
        if (_boton != null) _boton.onClick.RemoveListener(AlHacerClic);
    }

    private void OnDisable()
    {
        // Al reactivarlo no debe seguir a medio rebotar ni "pulsado" de antes.
        _encima = _pulsado = false;
        _escala = 1f; _velEscala = 0f;
        _angulo = 0f; _velAngulo = 0f;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
    }

    // ── Mouse ────────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData e) => _encima = true;
    public void OnPointerExit(PointerEventData e)  => _encima = false;
    public void OnPointerDown(PointerEventData e)  => _pulsado = true;
    public void OnPointerUp(PointerEventData e)    => _pulsado = false;

    private void AlHacerClic()
    {
        // Un empujón hacia arriba en la escala y otro de lado en el ángulo. El signo
        // de la sacudida se alterna para que no se vea siempre hacia el mismo lado.
        _velEscala += fuerzaRebote;
        _velAngulo += fuerzaSacudida * (Random.value < 0.5f ? -1f : 1f);

        if (menu == null) menu = FindAnyObjectByType<MenuController>();
        if (menu != null) menu.Alternar();
        else Debug.LogWarning("[BotonCelularMenu] No hay ningún MenuController en la escena.", this);
    }

    // ── Movimiento ───────────────────────────────────────────────────────────

    private void Update()
    {
        // Con un dt grande (un tirón de framerate) el resorte se dispararía.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        float objetivo = _pulsado ? escalaPulsado : (_encima ? escalaEncima : 1f);
        Resorte(ref _escala, ref _velEscala, objetivo, dt);
        Resorte(ref _angulo, ref _velAngulo, 0f, dt);

        transform.localScale = new Vector3(_escala, _escala, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, _angulo);

        ActualizarVisibilidad(dt);
    }

    private void Resorte(ref float valor, ref float velocidad, float objetivo, float dt)
    {
        velocidad += (objetivo - valor) * rigidez * dt;
        velocidad *= Mathf.Exp(-amortiguacion * dt);
        valor += velocidad * dt;
    }

    // ── Visibilidad ──────────────────────────────────────────────────────────

    /// <summary>
    /// Visible sólo cuando tiene sentido usarlo: con el menú cerrado y el juego
    /// corriendo. Se esconde con el CanvasGroup y no con SetActive, porque un objeto
    /// apagado dejaría de ejecutar este Update y ya no podría volver a aparecer.
    /// </summary>
    private void ActualizarVisibilidad(float dt)
    {
        if (menu == null) menu = FindAnyObjectByType<MenuController>();

        bool menuAbierto = menu != null && menu.EstaAbierto;

        // También se esconde mientras hay un diálogo, un chat o el Modo Detective: el menú
        // no se puede abrir ahí, y un botón que no responde es peor que ninguno.
        bool sePuedeAbrir = menu == null || menu.PuedeAbrir;
        bool visible = !menuAbierto && sePuedeAbrir && Time.timeScale > 0f;

        float meta = visible ? 1f : 0f;
        _grupo.alpha = Mathf.MoveTowards(_grupo.alpha, meta, dt / duracionFundido);

        // Invisible no debe recibir clics: sería un botón fantasma en la esquina.
        _grupo.interactable = visible;
        _grupo.blocksRaycasts = visible;

        if (!visible) { _encima = _pulsado = false; }
    }
}
