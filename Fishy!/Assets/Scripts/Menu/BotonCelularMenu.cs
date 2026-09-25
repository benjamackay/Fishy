using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Botón del celular de Otto: abre el menú del Tab con un clic.
///
/// Se pone en un GameObject que ya tenga un <see cref="Button"/> (el dibujo, la posición
/// y el tamaño se acomodan en Unity). Este componente sólo le da comportamiento:
/// <list type="bullet">
///   <item>Al pasar el mouse crece un poco, y al mantenerlo pulsado se achica.</item>
///   <item>Al hacer clic se achica y se agranda rápido, y abre el menú.</item>
///   <item>Se esconde cuando el menú no se puede abrir (diálogos, chat, Modo Detective) y
///   con el juego detenido (pausa).</item>
/// </list>
///
/// El tamaño sale de dos capas que se multiplican: un resorte suave para el mouse
/// encima y pulsado, y una animación corta y fija para el clic. Van separadas para que
/// el clic se vea igual de rápido aunque el mouse siga encima.
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

    [Header("Texto de la tecla")]
    [Tooltip("Etiqueta bajo el ícono. Si se deja vacía se busca un texto TMP hijo de este botón.")]
    public TMP_Text etiqueta;

    [Tooltip("Lo que dice la etiqueta cuando hay un teclado conectado. Sin teclado " +
             "(pantalla táctil) la etiqueta se esconde: no hay tecla que apretar.")]
    public string textoConTeclado = "TAB";

    [Header("Al pasar el mouse y al pulsar")]
    [Tooltip("Escala con el mouse encima (1 = tamaño normal).")]
    [Range(1f, 1.5f)] public float escalaEncima = 1.1f;

    [Tooltip("Escala mientras se mantiene pulsado.")]
    [Range(0.5f, 1f)] public float escalaPulsado = 0.9f;

    [Header("Al hacer clic")]
    [Tooltip("Cuánto se achica primero (1 = nada, 0.8 = un 20 % más chico).")]
    [Range(0.4f, 1f)] public float escalaAchicado = 0.8f;

    [Tooltip("Cuánto se agranda después, antes de volver a su tamaño (1 = nada).")]
    [Range(1f, 1.5f)] public float escalaAgrandado = 1.15f;

    [Tooltip("Segundos que dura todo el movimiento de achicar y agrandar.")]
    [Min(0.05f)] public float duracionClic = 0.22f;

    [Header("Resorte (mouse encima y pulsado)")]
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
    private float _proximaRevisionEtiqueta;

    private float _escala = 1f, _velEscala;
    private float _tClic = -1f;   // segundos desde el clic; negativo = sin animación en curso

    private void Awake()
    {
        _boton = GetComponent<Button>();
        _grupo = GetComponent<CanvasGroup>();

        // El movimiento lo pone este componente: el tinte de color del Button le
        // estorbaría, y con la navegación por teclado Espacio y Enter lo apretarían.
        _boton.transition = Selectable.Transition.None;
        _boton.navigation = new Navigation { mode = Navigation.Mode.None };

        _boton.onClick.AddListener(AlHacerClic);

        if (etiqueta == null) etiqueta = GetComponentInChildren<TMP_Text>(true);
        // El clic lo recibe el botón entero, no el texto.
        if (etiqueta != null) etiqueta.raycastTarget = false;
        ActualizarEtiqueta();
    }

    private void OnDestroy()
    {
        if (_boton != null) _boton.onClick.RemoveListener(AlHacerClic);
    }

    private void OnDisable()
    {
        // Al reactivarlo no debe seguir a medio animar ni "pulsado" de antes.
        _encima = _pulsado = false;
        _escala = 1f; _velEscala = 0f;
        _tClic = -1f;
        transform.localScale = Vector3.one;
    }

    // ── Mouse ────────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData e) => _encima = true;
    public void OnPointerExit(PointerEventData e)  => _encima = false;
    public void OnPointerDown(PointerEventData e)  => _pulsado = true;
    public void OnPointerUp(PointerEventData e)    => _pulsado = false;

    private void AlHacerClic()
    {
        _tClic = 0f;   // arranca (o reinicia) el achicar y agrandar

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

        float clic = 1f;
        if (_tClic >= 0f)
        {
            _tClic += dt;
            float k = _tClic / duracionClic;
            if (k >= 1f) _tClic = -1f;
            else clic = FactorDeClic(k);
        }

        float escala = _escala * clic;
        transform.localScale = new Vector3(escala, escala, 1f);

        ActualizarVisibilidad(dt);

        // Un teclado se puede enchufar o quitar en pleno juego, pero no hace falta mirarlo
        // en cada frame.
        if (Time.unscaledTime >= _proximaRevisionEtiqueta)
        {
            _proximaRevisionEtiqueta = Time.unscaledTime + 0.5f;
            ActualizarEtiqueta();
        }
    }

    /// <summary>Con teclado la etiqueta dice qué tecla apretar; sin él no se muestra.</summary>
    private void ActualizarEtiqueta()
    {
        if (etiqueta == null) return;

        bool hayTeclado = Keyboard.current != null;
        if (etiqueta.gameObject.activeSelf != hayTeclado) etiqueta.gameObject.SetActive(hayTeclado);
        if (hayTeclado && etiqueta.text != textoConTeclado) etiqueta.text = textoConTeclado;
    }

    /// <summary>
    /// Multiplicador de tamaño durante el clic, con k de 0 a 1 a lo largo de
    /// <see cref="duracionClic"/>: baja a <see cref="escalaAchicado"/> en el primer 30 %,
    /// sube a <see cref="escalaAgrandado"/> hasta el 65 % y vuelve a 1. Cada tramo va
    /// suavizado para que no se note el cambio de dirección.
    /// </summary>
    private float FactorDeClic(float k)
    {
        if (k < 0.30f) return Mathf.Lerp(1f, escalaAchicado, Suave(k / 0.30f));
        if (k < 0.65f) return Mathf.Lerp(escalaAchicado, escalaAgrandado, Suave((k - 0.30f) / 0.35f));
        return Mathf.Lerp(escalaAgrandado, 1f, Suave((k - 0.65f) / 0.35f));
    }

    private static float Suave(float t) => t * t * (3f - 2f * t);

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

        // Se esconde mientras hay un diálogo, un chat o el Modo Detective: el menú no se
        // puede abrir ahí, y un botón que no responde es peor que ninguno.
        //
        // Con el menú ya abierto NO se esconde: el fondo borroso del panel lo tapa por
        // encima y bloquea sus clics, así que hacerlo desvanecerse en el momento del clic
        // sólo añadía un parpadeo. Ojo: al abrir, el menú le quita el movimiento a Otto y
        // por eso PuedeAbrir pasa a falso; sin esta excepción se desvanecería igual.
        bool menuAbierto = menu != null && menu.EstaAbierto;
        bool sePuedeAbrir = menu == null || menu.PuedeAbrir;
        bool visible = (menuAbierto || sePuedeAbrir) && Time.timeScale > 0f;

        float meta = visible ? 1f : 0f;
        _grupo.alpha = Mathf.MoveTowards(_grupo.alpha, meta, dt / duracionFundido);

        // Invisible no debe recibir clics: sería un botón fantasma en la esquina.
        _grupo.interactable = visible;
        _grupo.blocksRaycasts = visible;

        if (!visible) { _encima = _pulsado = false; }
    }
}
