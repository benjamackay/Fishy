using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Prueba fondos de pantalla: toma al azar una imagen de una carpeta de
    /// Resources y deja cambiarla con una tecla, sin salir del juego.
    ///
    /// Está para responder "¿cuál queda mejor?" sin tener que parar, arrastrar
    /// otra imagen y volver a entrar cada vez. Al abrir la pantalla elige una al
    /// azar; con <see cref="teclaSiguiente"/> salta a otra, y muestra el nombre
    /// para poder anotar la que gustó.
    ///
    /// Nació en el Modo Detective y lo usa también el chat por teléfono, así que
    /// no sabe nada de ninguno de los dos: quien lo crea le pasa los ajustes
    /// desde su propio fichero de tema y llama a <see cref="Iniciar"/>.
    ///
    /// Las imágenes van en Assets/Resources/&lt;carpeta&gt;/ y tienen que estar
    /// importadas como Sprite (Texture Type: Sprite (2D and UI)), o
    /// Resources.LoadAll no las ve.
    ///
    /// Cuando el fondo esté elegido, esto se apaga poniendo <see cref="rotar"/>
    /// en false y dejando <see cref="fijoPorNombre"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class FondoAleatorio : MonoBehaviour
    {
        /// <summary>Image sobre el que se pinta la ilustración. Si queda vacío se
        /// usa el de este mismo objeto.</summary>
        public Image destino;

        /// <summary>Dónde colgar el rótulo del nombre. Va aparte del fondo porque
        /// el fondo se dibuja debajo de las burbujas y el rótulo quedaría tapado
        /// justo cuando hace falta leerlo. Si queda vacío, se cuelga de este
        /// objeto.</summary>
        public Transform etiquetaPadre;

        // --- Ajustes. Los pone quien crea el componente, desde su tema. ---

        /// <summary>Carpeta dentro de Assets/Resources con las candidatas.</summary>
        public string carpeta = "";

        /// <summary>En true elige una al azar al abrir y la tecla salta a otra.
        /// En false manda <see cref="fijoPorNombre"/>.</summary>
        public bool rotar = true;

        /// <summary>Con <see cref="rotar"/> en false, el fondo que queda fijo.
        /// Vacío = ninguno, y se ve el color liso de siempre.</summary>
        public string fijoPorNombre = "";

        /// <summary>Tecla para saltar al siguiente fondo al azar.</summary>
        public Key teclaSiguiente = Key.F;

        /// <summary>Tinte de la ilustración. El alfa es lo importante: a 1 el
        /// dibujo tapa el color de fondo y compite con los mensajes.</summary>
        public Color tinte = Color.white;

        /// <summary>Repetir la imagen en mosaico en vez de estirarla.</summary>
        public bool repetir = false;

        /// <summary>Muestra el nombre del fondo en pantalla mientras se prueba.</summary>
        public bool mostrarNombre = true;

        public float tamanoNombre = 22f;

        /// <summary>Color del rótulo de prueba.</summary>
        public Color colorNombre = Color.white;

        /// <summary>Fuente del rótulo, dentro de Resources.</summary>
        public string rutaFuenteNombre = FishyUIKit.RutaCuerpo;

        /// <summary>Nombre del módulo para los mensajes de consola, para saber
        /// cuál de las dos pantallas está hablando.</summary>
        public string modulo = "Fondo";

        private Sprite[] _fondos = new Sprite[0];
        private int _actual = -1;
        private TextMeshProUGUI _etiqueta;
        private bool _iniciado;

        /// <summary>Nombre del fondo que se está viendo, o vacío si no hay.</summary>
        public string NombreActual =>
            _actual >= 0 && _actual < _fondos.Length ? _fondos[_actual].name : "";

        /// <summary>
        /// Carga la carpeta y pone el primer fondo. Va aparte de Awake a
        /// propósito: AddComponent dispara Awake en el acto, antes de que quien
        /// crea el componente haya podido asignarle un solo ajuste, así que en
        /// Awake todo esto leería los valores por defecto.
        /// </summary>
        public void Iniciar()
        {
            if (_iniciado) return;
            _iniciado = true;

            if (destino == null) destino = GetComponent<Image>();

            _fondos = string.IsNullOrEmpty(carpeta)
                          ? new Sprite[0]
                          : Resources.LoadAll<Sprite>(carpeta);

            if (_fondos == null || _fondos.Length == 0)
            {
                // Sin imágenes no es un error: la pantalla se ve igual que
                // siempre, con el color liso. Pero conviene decir dónde ponerlas.
                Debug.Log($"[{modulo}] No hay fondos en 'Assets/Resources/{carpeta}/'. " +
                          "Deja ahí las imágenes (importadas como Sprite) para probarlas.", this);
                if (destino != null) destino.enabled = false;
                return;
            }

            // Con la rotación apagada manda el fondo elegido; el rótulo es una
            // ayuda de prueba y no tiene por qué salir en el juego terminado.
            if (!rotar)
            {
                if (string.IsNullOrEmpty(fijoPorNombre))
                {
                    if (destino != null) destino.enabled = false;
                    return;
                }

                if (!AplicarPorNombre(fijoPorNombre))
                {
                    Debug.LogWarning($"[{modulo}] El fondo fijo '{fijoPorNombre}' no está en " +
                                     $"'Assets/Resources/{carpeta}/'. " +
                                     "Reviso el nombre; va uno al azar mientras tanto.", this);
                    Siguiente();
                }
                return;
            }

            CrearEtiqueta();
            Siguiente();
        }

        private void Update()
        {
            if (!_iniciado || !rotar || _fondos.Length < 2) return;

            // Keyboard.current es null si no hay teclado (mando, móvil), y ahí
            // esto no debe reventar: es una herramienta de prueba, no del juego.
            Keyboard teclado = Keyboard.current;
            if (teclado == null) return;

            if (teclado[teclaSiguiente].wasPressedThisFrame)
                Siguiente();
        }

        /// <summary>
        /// Salta a otro fondo al azar. Nunca repite el que ya se está viendo:
        /// si al probar sale dos veces el mismo parece que la tecla no funciona.
        /// </summary>
        public void Siguiente()
        {
            if (_fondos.Length == 0) return;

            int elegido = Random.Range(0, _fondos.Length);
            if (_fondos.Length > 1)
            {
                while (elegido == _actual) elegido = Random.Range(0, _fondos.Length);
            }

            Aplicar(elegido);
        }

        /// <summary>Pone un fondo concreto por su nombre de archivo.</summary>
        public bool AplicarPorNombre(string nombre)
        {
            for (int i = 0; i < _fondos.Length; i++)
            {
                if (_fondos[i].name != nombre) continue;
                Aplicar(i);
                return true;
            }
            return false;
        }

        private void Aplicar(int indice)
        {
            _actual = indice;

            if (destino != null)
            {
                destino.enabled        = true;
                destino.sprite         = _fondos[indice];
                destino.color          = tinte;
                destino.type           = repetir ? Image.Type.Tiled : Image.Type.Simple;
                destino.preserveAspect = false;   // el fondo llena la ventana
                destino.raycastTarget  = false;   // los clics son para las burbujas
            }

            string texto = $"fondo {indice + 1}/{_fondos.Length}: {_fondos[indice].name}";
            if (_etiqueta != null) _etiqueta.text = texto;
            Debug.Log($"[{modulo}] {texto}", this);
        }

        /// <summary>
        /// Rótulo con el nombre del fondo. Sin esto se ve cuál gusta pero no cuál
        /// es, que es justo el dato que hace falta para dejarlo fijo después.
        /// </summary>
        private void CrearEtiqueta()
        {
            if (!mostrarNombre) return;

            var go = new GameObject("NombreFondo", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(etiquetaPadre != null ? etiquetaPadre : transform, false);
            go.transform.SetAsLastSibling();   // por encima de las burbujas

            _etiqueta = go.GetComponent<TextMeshProUGUI>();
            _etiqueta.fontSize      = tamanoNombre;
            _etiqueta.color         = colorNombre;
            _etiqueta.alignment     = TextAlignmentOptions.BottomRight;
            _etiqueta.raycastTarget = false;

            var font = Resources.Load<TMP_FontAsset>(rutaFuenteNombre);
            if (font != null) _etiqueta.font = font;

            var rt = _etiqueta.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 6f);
            rt.offsetMax = new Vector2(-8f, -6f);
        }
    }
}
