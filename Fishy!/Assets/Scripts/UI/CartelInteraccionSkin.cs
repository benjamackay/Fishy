using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Le pone fondo al cartel de «E para interactuar».
    ///
    /// El cartel está montado a mano en la escena —una manito, la letra encima y el
    /// texto al lado— y se dibuja directamente sobre el mapa, sin nada detrás. Con
    /// el texto en crema y un bosque claro debajo, hay sitios donde sencillamente no
    /// se lee; y resulta ser la única pista de teclas que el juego daba antes de
    /// que existiera el teclado de ayuda (<see cref="TecladoEnPantalla"/>).
    ///
    /// Igual que <see cref="DialogoNeutroSkin"/>: esto SOLO PINTA. No cambia cuándo
    /// aparece el cartel ni qué tecla dice —de eso sigue encargándose
    /// <c>InteractionDetector</c>, que escribe la letra leyéndola de su propio
    /// <c>interactKey</c>— y es idempotente, porque el cartel es uno solo y lo
    /// enciende y apaga el detector muchas veces por partida.
    ///
    /// El fondo se mide de lo que haya dentro en vez de ponerle un tamaño fijo: el
    /// cartel está colocado a ojo en la escena (la manito se sale de su propio
    /// rectángulo) y un tamaño escrito aquí se quedaría viejo en cuanto alguien
    /// moviera algo en el editor.
    /// </summary>
    public static class CartelInteraccionSkin
    {
        /// <summary>Nombre del objeto que se añade. Se busca por él para no pintar dos
        /// veces el mismo cartel aunque se reinicie la caché.</summary>
        public const string NombreFondo = "Fondo";

        private static readonly HashSet<GameObject> _yaPintados = new HashSet<GameObject>();

        public static void Aplicar(GameObject cartel)
        {
            if (cartel == null) return;
            if (!_yaPintados.Add(cartel)) return;

            var rt = cartel.transform as RectTransform;
            if (rt == null) return;

            // Si alguien ya le puso un fondo en la escena, no se le pone otro.
            if (rt.Find(NombreFondo) != null) return;

            // Lo que ocupan los hijos, medido en coordenadas del cartel. Si no hay
            // ninguno no hay nada que enmarcar.
            if (!Union(rt, out Bounds dentro)) return;

            float padding = CartelInteraccionTheme.Medidas.Padding;

            var go = new GameObject(NombreFondo, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rt, false);

            // Primero en la lista = detrás de todo lo demás, que es justo lo que es.
            go.transform.SetAsFirstSibling();

            var fondoRT = (RectTransform)go.transform;
            fondoRT.anchorMin = fondoRT.anchorMax = fondoRT.pivot = new Vector2(0.5f, 0.5f);
            fondoRT.sizeDelta = new Vector2(dentro.size.x + 2f * padding,
                                            dentro.size.y + 2f * padding);
            fondoRT.anchoredPosition = new Vector2(dentro.center.x, dentro.center.y);

            var img = go.GetComponent<Image>();
            FishyUIKit.FondoRedondeado(img, CartelInteraccionTheme.Colores.Panel,
                radio: CartelInteraccionTheme.Medidas.RadioEsquina);
            img.raycastTarget = false;   // es un cartel, no un botón
        }

        /// <summary>
        /// Lo que ocupan los hijos del cartel, en coordenadas del cartel.
        ///
        /// Se recorren uno a uno en vez de pedir las medidas del conjunto de una
        /// sola llamada, porque la versión de una llamada mete además el rectángulo
        /// del propio cartel — y ése está puesto a ojo en la escena, es más ancho que
        /// su contenido y está descentrado respecto a él. Con él dentro, el fondo
        /// salía medio metro más grande de lo necesario y se escapaba por el borde
        /// de la pantalla.
        /// </summary>
        private static bool Union(RectTransform cartel, out Bounds union)
        {
            union = default;
            bool hayAlguno = false;

            foreach (Transform hijo in cartel)
            {
                if (hijo.name == NombreFondo || !(hijo is RectTransform)) continue;

                Bounds suyo = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    cartel, hijo);

                if (!hayAlguno) { union = suyo; hayAlguno = true; }
                else union.Encapsulate(suyo);
            }

            return hayAlguno && union.size.x > 0f && union.size.y > 0f;
        }

        /// <summary>Olvida lo pintado. Igual que en <see cref="DialogoNeutroSkin"/>:
        /// solo hace falta si se recarga la escena.</summary>
        public static void Reiniciar() => _yaPintados.Clear();
    }
}
