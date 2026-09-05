using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fishy.Detective
{
    /// <summary>
    /// Prueba fondos para el Modo Detective: toma al azar una imagen de una
    /// carpeta de Resources y deja cambiarla con una tecla, sin salir del juego.
    ///
    /// Está para responder "¿cuál queda mejor?" sin tener que parar, arrastrar
    /// otra imagen y volver a entrar cada vez. Al abrir el modo elige una al
    /// azar; con la tecla de <see cref="DetectiveUITheme.Fondo.TeclaSiguiente"/>
    /// salta a otra, y muestra el nombre en pantalla para poder anotar la que
    /// gustó.
    ///
    /// Las imágenes van en Assets/Resources/&lt;carpeta&gt;/ y tienen que estar
    /// importadas como Sprite (Texture Type: Sprite (2D and UI)), o
    /// Resources.LoadAll no las ve.
    ///
    /// Cuando el fondo esté elegido, esto se apaga poniendo
    /// DetectiveUITheme.Fondo.Rotar en false y dejando FijoPorNombre.
    /// </summary>
    [DisallowMultipleComponent]
    public class DetectiveFondoAleatorio : MonoBehaviour
    {
        /// <summary>Image sobre el que se pinta la ilustración.</summary>
        public Image destino;

        /// <summary>Dónde colgar el rótulo del nombre. Va aparte del fondo porque
        /// el fondo se dibuja debajo de las burbujas y el rótulo quedaría tapado
        /// justo cuando hace falta leerlo. Si queda vacío, se cuelga de este
        /// objeto.</summary>
        public Transform etiquetaPadre;

        private Sprite[] _fondos = new Sprite[0];
        private int _actual = -1;
        private TextMeshProUGUI _etiqueta;

        /// <summary>Nombre del fondo que se está viendo, o vacío si no hay.</summary>
        public string NombreActual =>
            _actual >= 0 && _actual < _fondos.Length ? _fondos[_actual].name : "";

        private void Awake()
        {
            if (destino == null) destino = GetComponent<Image>();

            _fondos = Resources.LoadAll<Sprite>(DetectiveUITheme.Fondo.Carpeta);

            if (_fondos == null || _fondos.Length == 0)
            {
                // Sin imágenes no es un error: el modo se ve igual que siempre,
                // con el color liso. Pero conviene decir dónde ponerlas.
                Debug.Log($"[Detective] No hay fondos en " +
                          $"'Assets/Resources/{DetectiveUITheme.Fondo.Carpeta}/'. " +
                          "Deja ahí las imágenes (importadas como Sprite) para probarlas.", this);
                if (destino != null) destino.enabled = false;
                return;
            }

            // Con la rotación apagada manda el fondo elegido; el rótulo es una
            // ayuda de prueba y no tiene por qué salir en el juego terminado.
            if (!DetectiveUITheme.Fondo.Rotar)
            {
                string fijo = DetectiveUITheme.Fondo.FijoPorNombre;

                if (string.IsNullOrEmpty(fijo))
                {
                    if (destino != null) destino.enabled = false;
                    return;
                }

                if (!AplicarPorNombre(fijo))
                {
                    Debug.LogWarning($"[Detective] El fondo fijo '{fijo}' no está en " +
                                     $"'Assets/Resources/{DetectiveUITheme.Fondo.Carpeta}/'. " +
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
            if (!DetectiveUITheme.Fondo.Rotar || _fondos.Length < 2) return;

            // Keyboard.current es null si no hay teclado (mando, móvil), y ahí
            // esto no debe reventar: es una herramienta de prueba, no del juego.
            Keyboard teclado = Keyboard.current;
            if (teclado == null) return;

            if (teclado[DetectiveUITheme.Fondo.TeclaSiguiente].wasPressedThisFrame)
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
                destino.enabled       = true;
                destino.sprite        = _fondos[indice];
                destino.color         = DetectiveUITheme.Fondo.Tinte;
                destino.type          = DetectiveUITheme.Fondo.Repetir
                                            ? Image.Type.Tiled
                                            : Image.Type.Simple;
                destino.preserveAspect = false;   // el fondo llena la ventana
                destino.raycastTarget  = false;   // los clics son para las burbujas
            }

            string texto = $"fondo {indice + 1}/{_fondos.Length}: {_fondos[indice].name}";
            if (_etiqueta != null) _etiqueta.text = texto;
            Debug.Log($"[Detective] {texto}", this);
        }

        /// <summary>
        /// Rótulo con el nombre del fondo. Sin esto se ve cuál gusta pero no cuál
        /// es, que es justo el dato que hace falta para dejarlo fijo después.
        /// </summary>
        private void CrearEtiqueta()
        {
            if (!DetectiveUITheme.Fondo.MostrarNombre) return;

            var go = new GameObject("NombreFondo", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(etiquetaPadre != null ? etiquetaPadre : transform, false);
            go.transform.SetAsLastSibling();   // por encima de las burbujas

            _etiqueta = go.GetComponent<TextMeshProUGUI>();
            _etiqueta.fontSize      = DetectiveUITheme.Fondo.TamanoNombre;
            _etiqueta.color         = DetectiveUITheme.Colores.TextoSuave;
            _etiqueta.alignment     = TextAlignmentOptions.BottomRight;
            _etiqueta.raycastTarget = false;

            var font = Resources.Load<TMP_FontAsset>(DetectiveUITheme.Fuentes.RutaCuerpo);
            if (font != null) _etiqueta.font = font;

            var rt = _etiqueta.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 6f);
            rt.offsetMax = new Vector2(-8f, -6f);
        }
    }
}
