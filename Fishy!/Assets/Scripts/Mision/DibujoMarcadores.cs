using UnityEngine;

/// <summary>
/// Los dibujos de los marcadores del mapa del celular, en <c>Assets/Resources/NpcMarker/</c>:
/// <c>exclamacion.png</c> y <c>interrogacion.png</c> (los mismos que van sobre la cabeza de los
/// NPC) y <c>estrella.png</c> para los objetos por recoger.
///
/// Cada uno puede faltar. Los signos devuelven null y quien los pide se dibuja con texto; la
/// estrella cae a una hecha por código. Así el mapa funciona antes de que exista el arte.
/// </summary>
public static class DibujoMarcadores
{
    private const string Carpeta = "NpcMarker/";

    private static Sprite _exclamacion, _interrogacion, _estrella, _estrellaDeRespaldo;

    public static Sprite Exclamacion   => Cargar(ref _exclamacion, "exclamacion");
    public static Sprite Interrogacion => Cargar(ref _interrogacion, "interrogacion");

    /// <summary>La estrella de los objetos: el PNG si está, y si no una estrella de cuatro
    /// puntas dibujada por código (amarilla con contorno oscuro).</summary>
    public static Sprite Estrella
    {
        get
        {
            Sprite dibujo = Cargar(ref _estrella, "estrella");
            if (dibujo != null) return dibujo;

            if (_estrellaDeRespaldo == null) _estrellaDeRespaldo = CrearEstrella();
            return _estrellaDeRespaldo;
        }
    }

    // Se vuelve a pedir mientras esté vacío: sin recarga de dominio al entrar en Play, un
    // sprite guardado de la sesión anterior puede haber dejado de existir. Si el PNG no
    // existe, Resources.Load devuelve null sin más y es barato.
    private static Sprite Cargar(ref Sprite guardado, string nombre)
    {
        if (guardado == null) guardado = Resources.Load<Sprite>(Carpeta + nombre);
        return guardado;
    }

    /// <summary>
    /// Estrella de cuatro puntas (un destello). La forma es la ecuación |x|^0,5 + |y|^0,5 ≤ 1,
    /// la "astroide": cóncava, con cuatro puntas finas. Se pinta dos veces, una más grande y
    /// oscura y otra más chica y amarilla, con bordes suavizados para que no se vea con dientes.
    /// </summary>
    private static Sprite CrearEstrella()
    {
        const int lado = 96;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "EstrellaDeRespaldo",
        };

        Color relleno = new Color32(0xFF, 0xDE, 0x59, 255);
        Color contorno = new Color32(0x1B, 0x14, 0x12, 255);
        const float suavizado = 0.03f;

        var pixeles = new Color[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float nx = (x + 0.5f) / lado * 2f - 1f;
                float ny = (y + 0.5f) / lado * 2f - 1f;
                float d = Mathf.Sqrt(Mathf.Abs(nx)) + Mathf.Sqrt(Mathf.Abs(ny));   // 1 = el borde exterior

                float dentro = 1f - Mathf.SmoothStep(0f, 1f, (d - 0.98f) / suavizado * 0.5f + 0.5f);
                float interior = 1f - Mathf.SmoothStep(0f, 1f, (d - 0.72f) / suavizado * 0.5f + 0.5f);

                Color c = Color.Lerp(contorno, relleno, interior);
                c.a = Mathf.Clamp01(dentro);
                pixeles[y * lado + x] = c;
            }
        }

        tex.SetPixels(pixeles);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), lado);
    }
}
