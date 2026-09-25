using Fishy.UI;
using UnityEngine;

/// <summary>
/// El dibujo de la flecha del juego: <c>Assets/Resources/NpcMarker/flecha.png</c>. Es blanco y
/// apunta hacia la derecha, así que cada uso lo tiñe del color que le toca y lo gira con el
/// ángulo que necesite.
///
/// Lo usan la flecha que señala la zona de destino (<see cref="ZoneMarker"/>) y la que marca a
/// Otto en el mapa del celular (<see cref="MapPageUI"/>). Va en un solo sitio para que cambiar
/// el dibujo sea sustituir un PNG, no tocar código en varios lados.
///
/// Si el PNG falta se cae al triángulo dibujado por código, sin error: una flecha fea sigue
/// siendo una flecha que se entiende.
/// </summary>
public static class DibujoFlecha
{
    private const string Ruta = "NpcMarker/flecha";

    private static Sprite _sprite;

    /// <summary>El dibujo de la flecha, o el triángulo de respaldo si no está el PNG.</summary>
    public static Sprite Obtener()
    {
        // Se vuelve a pedir mientras esté vacío: sin recarga de dominio al entrar en Play, un
        // sprite guardado de la sesión anterior puede haber dejado de existir.
        if (_sprite == null) _sprite = Resources.Load<Sprite>(Ruta);
        return _sprite != null ? _sprite : FishyUIKit.SpriteTriangulo();
    }
}
