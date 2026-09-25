using Fishy.World;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una ventana circular sobre el mapa del mundo, centrada en Otto: el minimapa del HUD y el mapa
/// grande que se abre con la tecla M son dos de estas, con distinto tamaño. Se apoyan en la misma
/// imagen, el mismo suelo y la misma conversión mundo → mapa.
///
/// La dibuja y la mueve <see cref="MinimapaUI"/>; esta clase sólo sabe montar el mapa dentro de
/// una ventana ya recortada en círculo y ponerlo en su sitio cada frame.
///
/// El mapa se desplaza para dejar a Otto en el centro y se detiene en sus bordes, para no enseñar
/// nunca lo que hay fuera de la imagen: se recorta con el cuadrado que envuelve al círculo, que
/// basta porque el círculo cabe dentro de él.
/// </summary>
public class VistaCircular : IVistaDeMapa
{
    private readonly RectTransform _ventana;
    private readonly Vector2 _tamanoVentana;
    private readonly SpriteRenderer _suelo;

    private RectTransform _mapa;
    private RectTransform _flecha;
    private MapaMarcadoresUI _marcadores;
    private Vector2 _tamanoMapa;

    public RectTransform Mapa => _mapa;

    /// <param name="ventana">El círculo que hace de máscara. Su tamaño se lee de sizeDelta, no de
    /// rect: puede estar dentro de un canvas todavía apagado, donde rect aún no vale nada.</param>
    public VistaCircular(RectTransform ventana, SpriteRenderer suelo)
    {
        _ventana = ventana;
        _tamanoVentana = ventana.sizeDelta;
        _suelo = suelo;
    }

    /// <summary>Monta el mapa (con la textura dada), los marcadores y la flecha de Otto.</summary>
    public void Construir(Texture textura, Sprite respaldo, float unidadesVisibles,
                          float factorMarcadores, bool estrellas, Vector2 tamanoFlecha, float bordeFlecha)
    {
        float pxPorUnidad = _tamanoVentana.x / unidadesVisibles;
        Bounds b = _suelo.bounds;
        _tamanoMapa = new Vector2(b.size.x, b.size.y) * pxPorUnidad;

        var mapaGO = new GameObject("Mapa", typeof(RectTransform));
        mapaGO.transform.SetParent(_ventana, false);
        _mapa = (RectTransform)mapaGO.transform;
        _mapa.anchorMin = _mapa.anchorMax = _mapa.pivot = new Vector2(0.5f, 0.5f);
        _mapa.sizeDelta = _tamanoMapa;

        if (textura != null)
        {
            var raw = mapaGO.AddComponent<RawImage>();
            raw.texture = textura;
            raw.raycastTarget = false;
        }
        else
        {
            var img = mapaGO.AddComponent<Image>();
            img.sprite = respaldo;
            img.raycastTarget = false;
        }

        // Marcadores antes que la flecha: quedan por debajo de ella.
        _marcadores = MapaMarcadoresUI.Crear(this, _mapa, factorMarcadores, estrellas);
        ConstruirFlecha(tamanoFlecha, bordeFlecha);
    }

    public Vector2 PosicionEnMapa(Vector2 mundo)
    {
        Bounds b = _suelo.bounds;
        float u = (mundo.x - b.min.x) / Mathf.Max(0.0001f, b.size.x);
        float v = (mundo.y - b.min.y) / Mathf.Max(0.0001f, b.size.y);
        return new Vector2((u - 0.5f) * _tamanoMapa.x, (v - 0.5f) * _tamanoMapa.y);
    }

    /// <summary>Pone la flecha donde está Otto, girada hacia donde mira, y desplaza el mapa.</summary>
    public void Actualizar(OttoController otto)
    {
        if (_mapa == null || otto == null) return;

        Vector2 enElMapa = PosicionEnMapa(otto.transform.position);
        _flecha.anchoredPosition = enElMapa;

        Vector2 f = otto.Facing;
        _flecha.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg);

        Vector2 sobra = (_tamanoMapa - _tamanoVentana) * 0.5f;
        Vector2 desplazamiento = -enElMapa;
        desplazamiento.x = sobra.x > 0f ? Mathf.Clamp(desplazamiento.x, -sobra.x, sobra.x) : 0f;
        desplazamiento.y = sobra.y > 0f ? Mathf.Clamp(desplazamiento.y, -sobra.y, sobra.y) : 0f;
        _mapa.anchoredPosition = desplazamiento;
    }

    /// <summary>Vuelve a colocar los marcadores. Sólo tiene sentido con la vista a la vista.</summary>
    public void RefrescarMarcadores()
    {
        if (_marcadores != null) _marcadores.Refrescar();
    }

    // ── Flecha de Otto ───────────────────────────────────────────────────────

    private void ConstruirFlecha(Vector2 tamano, float borde)
    {
        var go = new GameObject("FlechaOtto", typeof(RectTransform));
        go.transform.SetParent(_mapa, false);
        _flecha = (RectTransform)go.transform;
        _flecha.anchorMin = _flecha.anchorMax = _flecha.pivot = new Vector2(0.5f, 0.5f);
        _flecha.sizeDelta = Vector2.zero;

        // Igual que la del mapa del celular: blanca, con el mismo dibujo más grande y oscuro detrás.
        Triangulo("Borde", tamano + Vector2.one * (borde * 2f), new Color32(0x1B, 0x14, 0x12, 255));
        Triangulo("Relleno", tamano, Color.white);
    }

    private void Triangulo(string nombre, Vector2 tamano, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_flecha, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamano;

        var img = go.GetComponent<Image>();
        img.sprite = DibujoFlecha.Obtener();
        img.color = color;
        img.raycastTarget = false;
    }
}
