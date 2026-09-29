using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.UI;
using Fishy.Chat;
using Fishy.UI;
using Fishy.World;
public class MenuController : MonoBehaviour

{
    public GameObject menuCanvas;

    [Header("Animación")]
    [Tooltip("Segundos que tarda en subir.")]
    [Min(0.05f)] public float duracionAbrir = 0.5f;

    [Tooltip("Segundos que tarda en bajar. Más corto que subir: al cerrar se quiere volver al juego ya.")]
    [Min(0.05f)] public float duracionCerrar = 0.25f;

    [Tooltip("Tamaño con el que empieza al subir y con el que termina al bajar (1 = el tamaño final).")]
    [Range(0.2f, 1f)] public float escalaInicial = 0.55f;

    [Tooltip("Cuánto se pasa de largo antes de asentarse en su sitio. 0 = nada; " +
             "1.7 es el rebote clásico; más de 2.5 ya se ve exagerado.")]
    [Range(0f, 3f)] public float sobrepaso = 1.2f;

    [Header("Orden de dibujo")]
    [Tooltip("Qué tan por encima de todo se dibuja el panel. Tiene que ser mayor que los diálogos, " +
             "el chat, el Modo Detective (950) y los avisos de zona (1000); y menor que el álbum " +
             "de evidencias (1300), la cinemática de zona (8000), el zoom del celular (9000) y el " +
             "menú de pausa (9500). El fondo borroso queda un punto por debajo.")]
    public int ordenDeDibujo = 1200;

    [Header("Botón de cerrar (rojo, con una X blanca)")]
    [Tooltip("Diámetro del botón, en unidades del panel (1500 de ancho).")]
    [Min(20f)] public float tamanoBotonCerrar = 64f;

    [Tooltip("Distancia del botón a la esquina superior derecha del panel.")]
    public Vector2 margenBotonCerrar = new Vector2(24f, 24f);

    [Tooltip("Color del botón.")]
    public Color colorBotonCerrar = new Color32(0xE0, 0x3C, 0x3C, 255);

    RawImage backdrop;
    GameObject canvasPropio;
    OttoController otto;
    bool bloqueamosAOtto;
    Texture2D snapshot;
    Coroutine opening;      // preparando la apertura: esperando al final del frame para la captura
    Coroutine animacion;    // subiendo o bajando
    bool cerrando;

    // Dónde está el panel cuando está abierto. Se lee del propio objeto en Start, así que
    // si se mueve o se reescala en la escena, la animación termina donde lo dejaste.
    RectTransform panel;
    Vector2 posReposo;
    Vector3 escalaReposo;

    // Cuánto del camino hacia "abierto" lleva: 0 = abajo y pequeño, 1 = en su sitio.
    // Pasa de 1 al subir (por eso el rebote), y no se pierde si se cambia de idea a mitad.
    float progreso;

    void Start()
    {
        if (menuCanvas != null)
        {
            panel = menuCanvas.transform as RectTransform;
            posReposo = panel.anchoredPosition;
            escalaReposo = panel.localScale;

            LlevarAUnCanvasPropio();
            ConstruirBotonCerrar();
            menuCanvas.SetActive(false);
        }
    }

    void Update()
    {
        if (menuCanvas != null && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            Alternar();
    }

    /// <summary>
    /// Saca el panel del canvas principal y lo pone en uno propio, de nivel raíz, con un
    /// orden de dibujo alto. En el canvas principal también vive el panel de diálogo, y
    /// ahí manda el orden en la jerarquía (el que va después se dibuja encima), así que
    /// el menú quedaba detrás. En su propio canvas queda por encima del diálogo, del chat,
    /// del Modo Detective y de las flechas; y el fondo borroso, que se crea como hermano
    /// del panel, comparte ese canvas y queda justo detrás de él.
    ///
    /// <b>Tiene que ser raíz de verdad</b>, sin padre. Este componente vive en el propio
    /// canvas principal; colgar el canvas nuevo de <c>transform</c> lo volvía un canvas
    /// ANIDADO: sin orden propio (seguía dentro del 0 del principal) y con el tamaño por
    /// defecto de 100×100, en el que el fondo borroso se quedaba en un cuadradito.
    ///
    /// Se copia el escalado del canvas de origen para que el panel mida y se coloque igual.
    /// </summary>
    void LlevarAUnCanvasPropio()
    {
        var canvasGO = new GameObject("MenuCelularCanvas", typeof(Canvas), typeof(CanvasScaler),
                                      typeof(GraphicRaycaster));
        canvasPropio = canvasGO;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ordenDeDibujo;

        var escalador = canvasGO.GetComponent<CanvasScaler>();
        var deOrigen = panel.parent != null ? panel.parent.GetComponentInParent<CanvasScaler>() : null;
        if (deOrigen != null)
        {
            escalador.uiScaleMode = deOrigen.uiScaleMode;
            escalador.referenceResolution = deOrigen.referenceResolution;
            escalador.screenMatchMode = deOrigen.screenMatchMode;
            escalador.matchWidthOrHeight = deOrigen.matchWidthOrHeight;
        }
        else
        {
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920f, 1080f);
            escalador.matchWidthOrHeight = 0.5f;
        }

        // false: se conservan anclas, tamaño y posición locales; el panel está anclado al
        // centro, así que queda en el centro del canvas nuevo tal como estaba en el viejo.
        panel.SetParent(canvasGO.transform, false);
        panel.anchoredPosition = posReposo;
        panel.localScale = escalaReposo;
    }

    /// <summary>
    /// Botón redondo rojo con una X blanca en la esquina superior derecha del panel, para
    /// cerrarlo con el ratón. Es hijo del panel: sube, baja y crece con él. Se dibuja por
    /// código (círculo rojo, aro oscuro y dos trazos blancos en diagonal) y va al final de
    /// la lista de hijos, para quedar por encima de las páginas.
    /// </summary>
    void ConstruirBotonCerrar()
    {
        float d = tamanoBotonCerrar;
        Color marron = Paleta.MarronOscuro;

        // Aro oscuro: es el objeto del botón y también el que recibe el clic.
        var raiz = new GameObject("BotonCerrar", typeof(RectTransform), typeof(Image), typeof(Button));
        raiz.transform.SetParent(panel, false);
        var rt = (RectTransform)raiz.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(d, d);
        rt.anchoredPosition = new Vector2(-margenBotonCerrar.x, -margenBotonCerrar.y);

        var aro = raiz.GetComponent<Image>();
        aro.sprite = FishyUIKit.SpriteRedondeado(64, 32);   // radio = mitad del lado: círculo
        aro.color = marron;

        // Relleno rojo, un poco más chico que el aro.
        var relleno = Circulo("Relleno", raiz.transform, colorBotonCerrar, d - 8f);

        // La X: dos trazos que se cruzan en el centro.
        float largo = d * 0.46f, grosor = d * 0.11f;
        Trazo(relleno.transform, largo, grosor, 45f);
        Trazo(relleno.transform, largo, grosor, -45f);

        var boton = raiz.GetComponent<Button>();
        boton.targetGraphic = relleno;
        boton.navigation = new Navigation { mode = Navigation.Mode.None };
        var colores = boton.colors;
        colores.normalColor = Color.white;
        colores.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);   // se aclara al pasar el ratón
        colores.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);       // y se oscurece al pulsar
        colores.selectedColor = Color.white;
        boton.colors = colores;
        boton.onClick.AddListener(Close);

        raiz.transform.SetAsLastSibling();
    }

    static Image Circulo(string nombre, Transform padre, Color color, float diametro)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(diametro, diametro);
        rt.anchoredPosition = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = FishyUIKit.SpriteRedondeado(64, 32);
        img.color = color;
        return img;
    }

    static void Trazo(Transform padre, float largo, float grosor, float grados)
    {
        var go = new GameObject("Trazo", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(largo, grosor);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, grados);

        var img = go.GetComponent<Image>();
        FishyUIKit.FondoRedondeado(img, Color.white, 16, 8);   // extremos redondeados
        img.raycastTarget = false;   // el clic lo recibe el aro, no el trazo
    }

    /// <summary>El menú está abierto, o a punto de abrirse (preparando la captura del fondo).
    /// Lo mira el botón del celular para esconderse.</summary>
    public bool EstaAbierto => (menuCanvas != null && menuCanvas.activeSelf) || opening != null;

    /// <summary>
    /// El menú sólo se abre caminando por el mundo. Todo lo demás —diálogo con un NPC
    /// (Huemul, Puma...), chat, Modo Detective, cinemáticas— le quita el movimiento a Otto
    /// con <see cref="OttoController.DisableMovement"/>, así que "Otto puede moverse" es
    /// justo la señal de "no hay nada en medio". Se suma el chat, que también lo mira.
    ///
    /// Se ignora al abrir, no al cerrar: un menú ya abierto siempre se puede cerrar.
    /// </summary>
    public bool PuedeAbrir
    {
        get
        {
            if (otto == null) otto = FindAnyObjectByType<OttoController>();
            if (otto != null && !otto.movementEnabled) return false;

            var chat = ChatModuleController.Instance;
            return chat == null || !chat.IsActive;
        }
    }

    /// <summary>Abre o cierra el menú. Lo llaman la tecla Tab y el botón del celular.</summary>
    public void Alternar()
    {
        if (menuCanvas == null) return;

        if (opening != null) { StopCoroutine(opening); opening = null; return; }
        if (!menuCanvas.activeSelf)
        {
            if (!PuedeAbrir) return;
            opening = StartCoroutine(Open());
        }
        else if (cerrando) Animar(true);   // Tab mientras baja: vuelve a subir
        else Close();
    }

    IEnumerator Open()
    {
        yield return new WaitForEndOfFrame();
        if (backdrop == null)
        {
            var go = new GameObject("PhoneBackdrop", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(menuCanvas.transform.parent, false);
            go.transform.SetSiblingIndex(menuCanvas.transform.GetSiblingIndex());
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            backdrop = go.GetComponent<RawImage>();
            Color colorPanel = new Color32(82, 50, 36, 255); // #523224
            backdrop.color = Color.Lerp(Color.white, colorPanel, 0.4f);
        }
        if (snapshot != null) Destroy(snapshot);
        var capture = ScreenCapture.CaptureScreenshotAsTexture();
        int w = Mathf.Max(1, Screen.width / 8), h = Mathf.Max(1, Screen.height / 8);
        var reduced = RenderTexture.GetTemporary(w, h, 0);
        var previous = RenderTexture.active;
        Graphics.Blit(capture, reduced);
        RenderTexture.active = reduced;
        snapshot = new Texture2D(w, h, TextureFormat.RGB24, false);
        snapshot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(reduced);
        Destroy(capture);
        var pixels = snapshot.GetPixels();
        var blurred = new Color[pixels.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    sum += pixels[Mathf.Clamp(y + dy, 0, h - 1) * w + Mathf.Clamp(x + dx, 0, w - 1)];
                blurred[y * w + x] = sum / 9;
            }
            var swap = pixels; pixels = blurred; blurred = swap;
        }
        snapshot.SetPixels(pixels); snapshot.Apply(); snapshot.filterMode = FilterMode.Bilinear;
        backdrop.texture = snapshot;

        // El panel y el fondo se colocan en su pose de partida ANTES de encenderse, para
        // que no se vea ni un frame el panel entero en su sitio final.
        progreso = 0f;
        Aplicar(progreso);
        backdrop.gameObject.SetActive(true);
        menuCanvas.SetActive(true);
        opening = null;
        BloquearAOtto();
        Animar(true);
    }

    // Con el menú abierto el mundo queda tapado por una captura fija, así que Otto no debe
    // seguir caminando por debajo sin que se vea. Se le quita el movimiento al abrir y se
    // le devuelve al cerrar, y sólo si fuimos nosotros quienes se lo quitamos.
    void BloquearAOtto()
    {
        if (otto == null) otto = FindAnyObjectByType<OttoController>();
        if (otto == null || !otto.movementEnabled) return;

        otto.DisableMovement();
        bloqueamosAOtto = true;
    }

    void LiberarAOtto()
    {
        if (!bloqueamosAOtto) return;
        bloqueamosAOtto = false;
        if (otto != null) otto.EnableMovement();
    }

    public void Close()
    {
        // Sin panel abierto, o con este componente apagado (no puede correr una animación),
        // se cierra en seco.
        if (menuCanvas == null || !menuCanvas.activeSelf || !isActiveAndEnabled)
        {
            CerrarYa();
            return;
        }
        if (!cerrando) Animar(false);
    }

    // ── Animación ────────────────────────────────────────────────────────────

    /// <summary>Lleva el panel hacia abierto (true) o hacia cerrado (false), desde donde esté.</summary>
    void Animar(bool abrir)
    {
        if (animacion != null) StopCoroutine(animacion);
        cerrando = !abrir;
        animacion = StartCoroutine(Mover(abrir));
    }

    IEnumerator Mover(bool abrir)
    {
        float desde = progreso;
        float destino = abrir ? 1f : 0f;
        float duracion = abrir ? duracionAbrir : duracionCerrar;

        // Tiempo sin escalar: el panel tiene que abrirse también con el juego en pausa.
        for (float t = 0f; t < duracion; t += Time.unscaledDeltaTime)
        {
            float k = t / duracion;
            float curva = abrir ? SalidaConRebote(k) : EntradaSuave(k);
            progreso = Mathf.LerpUnclamped(desde, destino, curva);
            Aplicar(progreso);
            yield return null;
        }

        progreso = destino;
        Aplicar(progreso);
        animacion = null;

        if (!abrir) CerrarYa();
    }

    /// <summary>Coloca el panel y el fondo según cuánto camino lleva (0 = abajo, 1 = arriba).</summary>
    void Aplicar(float p)
    {
        if (panel != null)
        {
            // Empieza justo por debajo del borde de la pantalla y sube hasta su sitio. Con
            // p > 1 (el rebote) se pasa un poco de largo, y con la escala igual.
            float altoPantalla = ((RectTransform)panel.parent).rect.height;
            float altoPanel = panel.rect.height * escalaInicial;
            var abajo = new Vector2(posReposo.x, posReposo.y - (altoPantalla + altoPanel) * 0.5f);

            panel.anchoredPosition = Vector2.LerpUnclamped(abajo, posReposo, p);
            panel.localScale = Vector3.LerpUnclamped(escalaReposo * escalaInicial, escalaReposo, p);
        }

        if (backdrop != null)
        {
            Color c = backdrop.color;
            c.a = Mathf.Clamp01(p);   // el fondo borroso entra y sale con fundido
            backdrop.color = c;
        }
    }

    /// <summary>Sube rápido, se pasa de largo y se asienta. Es el "easeOutBack" de siempre.</summary>
    float SalidaConRebote(float k)
    {
        float c1 = sobrepaso;
        float c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    /// <summary>Arranca despacio y acelera: bajar tiene que sentirse como soltar algo.</summary>
    float EntradaSuave(float k) => k * k * k;

    void CerrarYa()
    {
        if (animacion != null) { StopCoroutine(animacion); animacion = null; }
        cerrando = false;
        progreso = 0f;

        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (backdrop != null) backdrop.gameObject.SetActive(false);
        LiberarAOtto();

        // Deja el panel en su sitio final, listo para lo que lo use fuera de la animación.
        if (panel != null)
        {
            panel.anchoredPosition = posReposo;
            panel.localScale = escalaReposo;
        }
    }

    void OnDisable()
    {
        if (opening != null) StopCoroutine(opening);
        opening = null;
        CerrarYa();
    }
    void OnDestroy()
    {
        if (canvasPropio != null) Destroy(canvasPropio);
        if (snapshot != null) Destroy(snapshot);
        if (backdrop != null) Destroy(backdrop.gameObject);
    }
}
