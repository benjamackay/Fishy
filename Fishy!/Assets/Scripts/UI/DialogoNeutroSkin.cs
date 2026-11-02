using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Med = Fishy.UI.DialogoNeutroTheme.Medidas;

namespace Fishy.UI
{
    /// <summary>
    /// Le da al diálogo de los NPCs neutros el mismo aspecto que al chat de NPCs y
    /// al Modo Detective: fondo redondeado, paleta de Fishy y la fuente Mango en el
    /// nombre.
    ///
    /// IMPORTANTE — esto solo PINTA. No añade botones, ni opciones, ni cambia cómo
    /// se avanza el diálogo. El diálogo neutro es de una sola vía (el jugador lee y
    /// avanza) y no está preparado para elegir respuestas: meterle el sistema de
    /// opciones del chat rompería su lógica de <c>dialogueIndex</c> y su efecto de
    /// máquina de escribir. Si algún día un NPC neutro necesita opciones, lo suyo es
    /// pasarlo al módulo de chat, no ampliar esto.
    ///
    /// Los colores y medidas están en <see cref="DialogoNeutroTheme"/>; aquí solo
    /// está la lógica que los aplica.
    ///
    /// Trabaja solo sobre las referencias que el NPC ya tiene asignadas en el
    /// Inspector, y comprueba cada una: el panel de la escena no tiene retrato ni
    /// etiqueta de nombre, así que lo que falte simplemente se salta.
    ///
    /// <b>Además de pintar, recoloca.</b> Es lo único que hace fuera de los colores
    /// y, como el nombre dice que solo pinta, conviene saber por qué: la escena traía
    /// el cuerpo a 24 pt —17 px reales en el equipo escolar— en una caja de 100 px
    /// que se solapaba con la del nombre. Las dos cosas se arreglan donde se ven, que
    /// es en la geometría, y las medidas están en <see cref="DialogoNeutroTheme"/>.
    /// Se puede apagar con <c>Medidas.AjustarGeometria</c>.
    /// </summary>
    public static class DialogoNeutroSkin
    {
        /// <summary>Paneles ya pintados. El panel de diálogo lo comparten varios NPCs,
        /// así que sin esto se repintaría en cada conversación sin necesidad.</summary>
        private static readonly HashSet<GameObject> _yaPintados = new HashSet<GameObject>();

        /// <summary>Suelo al que puede encoger el nombre si la escena no trae uno puesto.
        /// Por debajo de esto deja de leerse, y vale más que asome a que no se entienda.</summary>
        private const float TamanoMinimoNombre = 18f;

        /// <summary>
        /// Aplica el aspecto de Fishy. Es idempotente: llamarlo en cada conversación
        /// no cuesta nada a partir de la segunda.
        /// </summary>
        public static void Aplicar(GameObject panel, TMP_Text nombre, TMP_Text texto,
            Image retrato = null)
        {
            if (panel == null) return;
            // Se guarda el objeto y no su id: GetInstanceID quedó obsoleto en Unity 6 y
            // su reemplazo, EntityId, tampoco se puede convertir a int sin otro aviso.
            if (!_yaPintados.Add(panel)) return;

            var fondo = panel.GetComponent<Image>();
            if (fondo != null)
                FishyUIKit.FondoRedondeado(fondo, DialogoNeutroTheme.Colores.Panel, radio: Med.RadioEsquina);

            if (nombre != null)
            {
                nombre.font  = FishyUIKit.Titulos;      // Mango, la fuente de la marca
                nombre.color = DialogoNeutroTheme.Colores.Nombre;
                nombre.fontStyle = FontStyles.Bold;
                if (DialogoNeutroTheme.Fuente.Nombre > 0f) nombre.fontSize = DialogoNeutroTheme.Fuente.Nombre;

                // El nombre tiene ahora su propia franja y ya no se solapa con el
                // cuerpo (lo coloca Recolocar). Lo que sigue haciendo falta es que no
                // se parta en dos líneas: la segunda caería fuera de su franja, que es
                // de una línea, y volvería a aterrizar sobre el diálogo. Un nombre
                // largo —"Foca de Weddell"— encoge hasta caber en vez de envolver.
                float tamanoDeDiseno = nombre.fontSize;   // antes de encender el ajuste
                nombre.textWrappingMode = TextWrappingModes.NoWrap;
                nombre.enableAutoSizing = true;
                nombre.fontSizeMax = tamanoDeDiseno;      // sólo encoger, nunca agrandar
                if (nombre.fontSizeMin <= 0f) nombre.fontSizeMin = TamanoMinimoNombre;
            }

            if (texto != null)
            {
                // El cuerpo va en la fuente legible, no en Mango: son párrafos que
                // se leen enteros, no un título.
                texto.font  = FishyUIKit.Cuerpo;
                texto.color = DialogoNeutroTheme.Colores.Texto;
                if (DialogoNeutroTheme.Fuente.Texto > 0f) texto.fontSize = DialogoNeutroTheme.Fuente.Texto;

                // Envolver sí; encoger no (ver la nota de Fuente en el tema: se
                // escribe letra a letra y el ajuste automático lo haría temblar).
                texto.textWrappingMode = TextWrappingModes.Normal;
                texto.enableAutoSizing = false;
            }

            if (Med.AjustarGeometria)
                Recolocar(panel, nombre, texto, retrato);

            if (retrato != null)
            {
                // Blanco = sin teñir, para no ensuciar el arte del retrato.
                retrato.color = Color.white;
            }
        }

        /// <summary>
        /// Pone el panel y sus tres cajas donde dicen las medidas del tema.
        ///
        /// El reparto es una columna: el retrato a la izquierda, y a su derecha el
        /// nombre arriba y el cuerpo debajo, ocupando todo lo que queda hasta el
        /// borde de abajo. Nada se solapa porque cada caja empieza donde termina la
        /// anterior, y no al revés que antes, que estaban puestas a ojo.
        ///
        /// El hueco del retrato se reserva aunque el retrato esté apagado. El panel
        /// lo comparten todos los NPCs y la pantalla de finales, y uno sin cara
        /// dejaría el texto 150 px más a la izquierda que el siguiente: el cuerpo
        /// cambiaría de sitio y de ancho a mitad de conversación.
        /// </summary>
        private static void Recolocar(GameObject panel, TMP_Text nombre, TMP_Text texto,
            Image retrato)
        {
            var rt = panel.transform as RectTransform;
            if (rt == null) return;

            // Abajo y al centro, como estaba. Lo que cambia es el tamaño.
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Med.TamanoPanel;
            rt.anchoredPosition = new Vector2(0f, Med.MargenInferior + Med.TamanoPanel.y / 2f);

            // Bordes interiores del panel, en coordenadas locales (el centro es 0,0).
            float izquierda = -Med.TamanoPanel.x / 2f + Med.Margen;
            float derecha   =  Med.TamanoPanel.x / 2f - Med.Margen;
            float arriba    =  Med.TamanoPanel.y / 2f - Med.Margen;
            float abajo     = -Med.TamanoPanel.y / 2f + Med.Margen;

            if (retrato != null)
            {
                var rtRetrato = retrato.transform as RectTransform;
                if (rtRetrato != null)
                {
                    Anclar(rtRetrato);
                    rtRetrato.sizeDelta = new Vector2(Med.LadoRetrato, Med.LadoRetrato);
                    rtRetrato.anchoredPosition = new Vector2(izquierda + Med.LadoRetrato / 2f, 0f);
                }
            }

            float xTexto = izquierda + (retrato != null ? Med.LadoRetrato + Med.HuecoRetrato : 0f);
            float ancho  = derecha - xTexto;
            float techo  = arriba;

            if (nombre != null)
            {
                Caja(nombre, xTexto, ancho, techo, Med.AltoNombre, TextAlignmentOptions.Left);
                techo -= Med.AltoNombre + Med.Separacion;
            }

            if (texto != null)
                Caja(texto, xTexto, ancho, techo, techo - abajo, TextAlignmentOptions.TopLeft);
        }

        /// <summary>Coloca una caja de texto por su esquina superior izquierda, que es
        /// como se piensa un párrafo, y no por su centro, que es como las guarda
        /// Unity.</summary>
        private static void Caja(TMP_Text texto, float x, float ancho, float techo, float alto,
            TextAlignmentOptions alineacion)
        {
            var rt = texto.transform as RectTransform;
            if (rt == null) return;

            Anclar(rt);
            rt.sizeDelta = new Vector2(ancho, alto);
            rt.anchoredPosition = new Vector2(x + ancho / 2f, techo - alto / 2f);
            texto.alignment = alineacion;
        }

        /// <summary>Ancla al centro del padre. Sin esto, una caja anclada a una esquina
        /// se movería sola al cambiar el tamaño del panel y todas las cuentas de
        /// arriba —que son relativas al centro— dejarían de valer.</summary>
        private static void Anclar(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>Olvida lo pintado. Solo hace falta si se recarga la escena y los
        /// paneles se vuelven a crear reutilizando IDs.</summary>
        public static void Reiniciar() => _yaPintados.Clear();
    }
}
