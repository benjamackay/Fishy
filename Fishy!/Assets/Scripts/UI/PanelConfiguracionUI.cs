using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// HDU-17 CA2 — La pantalla de «Configuración» del menú de pausa.
    ///
    /// Es una tarjeta que se monta dentro del canvas que le pasen y se enciende y se
    /// apaga; no trae canvas ni telón propios, porque sale <b>encima</b> del menú de
    /// pausa, que ya puso los suyos. La arma <see cref="MenuPausa"/>.
    ///
    /// <b>Cada control dice su estado en palabras</b> («Velocidad del texto: lenta»,
    /// «Pantalla completa: sí») y no solo con la posición de un tirador o el color de un
    /// recuadro. Para un niño/a de 9 años un interruptor encendido y uno apagado se
    /// parecen demasiado, y quien juegue sin distinguir bien los colores no tiene de
    /// dónde agarrarse. Lo que cambia se lee.
    ///
    /// Los valores los guarda <see cref="ConfiguracionDelJuego"/>, no esta clase:
    /// acá solo se pinta y se escucha.
    /// </summary>
    public class PanelConfiguracionUI
    {
        private const float AnchoTarjeta = 660f;
        private const float AltoTarjeta = 590f;

        private readonly GameObject _raiz;
        private readonly TextMeshProUGUI _etiquetaTexto;
        private readonly TextMeshProUGUI _etiquetaPantalla;

        public bool Abierto => _raiz != null && _raiz.activeSelf;

        public void Abrir()
        {
            // Se relee al abrir, no al construir: la configuración puede haber
            // cambiado desde otro sitio entre una apertura y la siguiente.
            PintarVelocidad(ConfiguracionDelJuego.VelocidadTexto);
            PintarPantalla(ConfiguracionDelJuego.PantallaCompleta);

            _raiz.SetActive(true);
        }

        public void Cerrar() => _raiz.SetActive(false);

        /// <summary>
        /// Monta la tarjeta dentro de <paramref name="padre"/>, apagada.
        /// <paramref name="alVolver"/> es lo que hace el botón «Volver»: devolver al
        /// menú de pausa. <paramref name="alControles"/> abre la tarjeta de controles.
        /// </summary>
        public PanelConfiguracionUI(Transform padre, Action alVolver, Action alControles)
        {
            var go = new GameObject("Configuracion",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            go.transform.SetParent(padre, false);
            _raiz = go;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(AnchoTarjeta, AltoTarjeta);

            FishyUIKit.FondoRedondeado(go.GetComponent<Image>(), Paleta.Marron);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(40, 40, 32, 32);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            FishyUIKit.Texto(go.transform, "Titulo", "Configuración",
                52f, Paleta.Crema, TextAlignmentOptions.Center);

            // ── Velocidad del texto ──
            _etiquetaTexto = FishyUIKit.Texto(go.transform, "EtiquetaTexto", "",
                32f, Paleta.Arena, TextAlignmentOptions.Left);
            FishyUIKit.Boton(go.transform, "Cambiar velocidad", Paleta.MarronSuave, 30f, 64f,
                () =>
                {
                    var nueva = ConfiguracionDelJuego.SiguienteVelocidad(
                        ConfiguracionDelJuego.VelocidadTexto);
                    ConfiguracionDelJuego.VelocidadTexto = nueva;
                    PintarVelocidad(nueva);
                });

            // ── Pantalla completa ──
            _etiquetaPantalla = FishyUIKit.Texto(go.transform, "EtiquetaPantalla", "",
                32f, Paleta.Arena, TextAlignmentOptions.Left);
            FishyUIKit.Boton(go.transform, "Cambiar pantalla", Paleta.MarronSuave, 30f, 64f,
                () =>
                {
                    bool nuevo = !ConfiguracionDelJuego.PantallaCompleta;
                    ConfiguracionDelJuego.PantallaCompleta = nuevo;
                    PintarPantalla(nuevo);
                });

            // ── Controles ──
            FishyUIKit.Boton(go.transform, "Controles", Paleta.Madera, 30f, 64f,
                () => alControles?.Invoke());

            FishyUIKit.Boton(go.transform, "Volver", Paleta.Verde, 34f, 76f,
                () => alVolver?.Invoke());

            go.SetActive(false);
        }

        private void PintarVelocidad(ConfiguracionDelJuego.VelocidadDelTexto velocidad)
        {
            string palabra;
            switch (velocidad)
            {
                case ConfiguracionDelJuego.VelocidadDelTexto.Lenta:  palabra = "lenta"; break;
                case ConfiguracionDelJuego.VelocidadDelTexto.Rapida: palabra = "rápida"; break;
                default:                                             palabra = "normal"; break;
            }
            _etiquetaTexto.text = $"Velocidad del texto: {palabra}";
        }

        private void PintarPantalla(bool completa)
            => _etiquetaPantalla.text = completa
                ? "Pantalla completa: sí"
                : "Pantalla completa: no (en ventana)";
    }
}
