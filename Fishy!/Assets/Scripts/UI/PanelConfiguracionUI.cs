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
    /// <b>Cada control dice su estado en palabras</b> («Volumen: 80%», «Pantalla
    /// completa: sí») y no solo con la posición de un tirador o el color de un
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
        private const float AltoTarjeta = 470f;

        private readonly GameObject _raiz;
        private readonly TextMeshProUGUI _etiquetaVolumen;
        private readonly TextMeshProUGUI _etiquetaPantalla;
        private readonly Slider _volumen;

        public bool Abierto => _raiz != null && _raiz.activeSelf;

        public void Abrir()
        {
            // Se relee al abrir, no al construir: la configuración puede haber
            // cambiado desde otro sitio entre una apertura y la siguiente.
            _volumen.SetValueWithoutNotify(ConfiguracionDelJuego.Volumen);
            PintarVolumen(ConfiguracionDelJuego.Volumen);
            PintarPantalla(ConfiguracionDelJuego.PantallaCompleta);

            _raiz.SetActive(true);
        }

        public void Cerrar() => _raiz.SetActive(false);

        /// <summary>
        /// Monta la tarjeta dentro de <paramref name="padre"/>, apagada.
        /// <paramref name="alVolver"/> es lo que hace el botón «Volver»: devolver al
        /// menú de pausa.
        /// </summary>
        public PanelConfiguracionUI(Transform padre, Action alVolver)
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

            // ── Volumen ──
            _etiquetaVolumen = FishyUIKit.Texto(go.transform, "EtiquetaVolumen", "",
                32f, Paleta.Arena, TextAlignmentOptions.Left);
            _volumen = ConstruirDeslizador(go.transform, ConfiguracionDelJuego.Volumen);
            _volumen.onValueChanged.AddListener(valor =>
            {
                ConfiguracionDelJuego.Volumen = valor;
                PintarVolumen(valor);
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

            FishyUIKit.Boton(go.transform, "Volver", Paleta.Verde, 34f, 76f,
                () => alVolver?.Invoke());

            go.SetActive(false);
        }

        private void PintarVolumen(float valor)
            => _etiquetaVolumen.text = $"Volumen: {Mathf.RoundToInt(valor * 100f)}%";

        private void PintarPantalla(bool completa)
            => _etiquetaPantalla.text = completa
                ? "Pantalla completa: sí"
                : "Pantalla completa: no (en ventana)";

        /// <summary>
        /// Un deslizador de volumen, armado a mano.
        ///
        /// Se construye acá y no en <see cref="FishyUIKit"/> porque es el único del
        /// juego: meterlo en el kit sería adivinar qué necesita el segundo. El
        /// tirador es grande a propósito —48 px de lado— porque esto se usa con el
        /// dedo tanto como con el mouse.
        /// </summary>
        private static Slider ConstruirDeslizador(Transform padre, float valorInicial)
        {
            const float Alto = 48f;
            const float AltoCarril = 16f;

            var go = new GameObject("Volumen",
                typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(padre, false);
            go.GetComponent<LayoutElement>().minHeight = Alto;

            var carril = new GameObject("Carril", typeof(RectTransform), typeof(Image));
            carril.transform.SetParent(go.transform, false);
            var carrilRT = (RectTransform)carril.transform;
            carrilRT.anchorMin = new Vector2(0f, 0.5f);
            carrilRT.anchorMax = new Vector2(1f, 0.5f);
            carrilRT.pivot = new Vector2(0.5f, 0.5f);
            carrilRT.sizeDelta = new Vector2(0f, AltoCarril);
            FishyUIKit.FondoRedondeado(carril.GetComponent<Image>(), Paleta.MarronOscuro, 64, 8);

            // El relleno va dentro de su propia área, como pide Slider: el componente
            // mueve el ancho de esta área y el relleno la sigue estirado.
            var areaRelleno = new GameObject("AreaRelleno", typeof(RectTransform));
            areaRelleno.transform.SetParent(go.transform, false);
            var areaRT = (RectTransform)areaRelleno.transform;
            areaRT.anchorMin = new Vector2(0f, 0.5f);
            areaRT.anchorMax = new Vector2(1f, 0.5f);
            areaRT.pivot = new Vector2(0.5f, 0.5f);
            areaRT.sizeDelta = new Vector2(0f, AltoCarril);

            var relleno = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
            relleno.transform.SetParent(areaRelleno.transform, false);
            var rellenoRT = (RectTransform)relleno.transform;
            FishyUIKit.Estirar(rellenoRT);
            FishyUIKit.FondoRedondeado(relleno.GetComponent<Image>(), Paleta.Verde, 64, 8);

            var areaTirador = new GameObject("AreaTirador", typeof(RectTransform));
            areaTirador.transform.SetParent(go.transform, false);
            FishyUIKit.Estirar((RectTransform)areaTirador.transform);

            var tirador = new GameObject("Tirador", typeof(RectTransform), typeof(Image));
            tirador.transform.SetParent(areaTirador.transform, false);
            var tiradorRT = (RectTransform)tirador.transform;
            tiradorRT.sizeDelta = new Vector2(Alto, Alto);
            FishyUIKit.FondoRedondeado(tirador.GetComponent<Image>(), Paleta.Crema, 64, 24);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = rellenoRT;
            slider.handleRect = tiradorRT;
            slider.targetGraphic = tirador.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(valorInicial);

            return slider;
        }
    }
}
