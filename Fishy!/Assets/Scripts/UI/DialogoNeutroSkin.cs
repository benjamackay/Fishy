using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    /// </summary>
    public static class DialogoNeutroSkin
    {
        /// <summary>Paneles ya pintados. El panel de diálogo lo comparten varios NPCs,
        /// así que sin esto se repintaría en cada conversación sin necesidad.</summary>
        private static readonly HashSet<int> _yaPintados = new HashSet<int>();

        /// <summary>
        /// Aplica el aspecto de Fishy. Es idempotente: llamarlo en cada conversación
        /// no cuesta nada a partir de la segunda.
        /// </summary>
        public static void Aplicar(GameObject panel, TMP_Text nombre, TMP_Text texto,
            Image retrato = null)
        {
            if (panel == null) return;
            if (!_yaPintados.Add(panel.GetInstanceID())) return;

            var fondo = panel.GetComponent<Image>();
            if (fondo != null)
                FishyUIKit.FondoRedondeado(fondo, DialogoNeutroTheme.Colores.Panel, radio: DialogoNeutroTheme.Medidas.RadioEsquina);

            if (nombre != null)
            {
                nombre.font  = FishyUIKit.Titulos;      // Mango, la fuente de la marca
                nombre.color = DialogoNeutroTheme.Colores.Nombre;
                nombre.fontStyle = FontStyles.Bold;
                if (DialogoNeutroTheme.Fuente.Nombre > 0f) nombre.fontSize = DialogoNeutroTheme.Fuente.Nombre;
            }

            if (texto != null)
            {
                // El cuerpo va en la fuente legible, no en Mango: son párrafos que
                // se leen enteros, no un título.
                texto.font  = FishyUIKit.Cuerpo;
                texto.color = DialogoNeutroTheme.Colores.Texto;
                if (DialogoNeutroTheme.Fuente.Texto > 0f) texto.fontSize = DialogoNeutroTheme.Fuente.Texto;
            }

            if (retrato != null)
            {
                // Blanco = sin teñir, para no ensuciar el arte del retrato.
                retrato.color = Color.white;
            }
        }

        /// <summary>Olvida lo pintado. Solo hace falta si se recarga la escena y los
        /// paneles se vuelven a crear reutilizando IDs.</summary>
        public static void Reiniciar() => _yaPintados.Clear();
    }
}
