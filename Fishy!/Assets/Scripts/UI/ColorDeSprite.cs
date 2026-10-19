using UnityEngine;

/// <summary>
/// Le da color a un SpriteRenderer desde el Inspector, sin tocar el sprite original
/// ni el shader. Va en el mismo GameObject que el sprite, igual que un Transform: se
/// pone una vez y se ajusta ahí mismo cuando haga falta.
///
/// No inventa nada nuevo — un SpriteRenderer ya multiplica sus píxeles por
/// <c>color</c> (blanco = el sprite tal cual). Esto solo le da un lugar cómodo para
/// configurarlo a mano y un método para cambiarlo desde otro script sin tener que ir
/// a buscar el SpriteRenderer cada vez.
///
/// <b>Se ve también en el editor, sin darle Play.</b> <c>OnValidate</c> se llama cada
/// vez que se toca un valor en el Inspector, así que arrastrar el color y ver el
/// resultado en la escena no necesita ejecutar el juego.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ColorDeSprite : MonoBehaviour
{
    [Tooltip("El SpriteRenderer a teñir. Si se deja vacío se busca en este mismo " +
             "GameObject.")]
    public SpriteRenderer sprite;

    [Tooltip("Color con el que se multiplica el sprite. Blanco = el sprite tal cual; " +
             "el canal alfa controla la transparencia igual que en cualquier " +
             "SpriteRenderer.")]
    public Color color = Color.white;

    private void Awake()
    {
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
    }

    private void OnEnable() => Aplicar();

    // Se llama cada vez que se toca un campo en el Inspector, incluso sin Play: es lo
    // que hace que el cambio de color se vea de inmediato mientras se ajusta.
    private void OnValidate()
    {
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        Aplicar();
    }

    /// <summary>Vuelve a pintar el sprite con el color actual. Público para poder
    /// llamarlo después de cambiar <see cref="color"/> a mano desde otro script.</summary>
    public void Aplicar()
    {
        if (sprite != null) sprite.color = color;
    }

    /// <summary>Cambia el color y lo aplica en el mismo paso.</summary>
    public void Establecer(Color nuevo)
    {
        color = nuevo;
        Aplicar();
    }
}
