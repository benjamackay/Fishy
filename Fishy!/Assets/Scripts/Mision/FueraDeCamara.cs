using UnityEngine;

/// <summary>
/// Espera a que el NPC salga de la pantalla antes de dejar que lo retiren.
///
/// Sin esto, terminar una misión hace desaparecer a alguien delante de los ojos del
/// niño/a, que es justo el tipo de cosa que rompe la ilusión de que el mundo existe
/// por su cuenta. Con esto el NPC sigue ahí hasta que la cámara mira a otro lado, y
/// cuando se vuelve ya no está: igual que si se hubiera ido.
/// </summary>
public class FueraDeCamara : CondicionDeDesaparicion
{
    [Tooltip("Cámara desde la que se mira. Vacío = la principal (MainCamera).")]
    public Camera camara;

    [Tooltip("Margen alrededor de la pantalla, en fracción de pantalla. Con 0 se " +
             "retira en cuanto el borde del sprite cruza el borde justo, y eso se " +
             "alcanza a ver de reojo; un poco de margen lo deja para más adentro.")]
    [Range(0f, 0.5f)] public float margen = 0.05f;

    public override string Motivo => "el jugador todavía lo ve";

    /// <summary>
    /// Se mide proyectando el sprite a coordenadas de pantalla en vez de usar
    /// <c>Renderer.isVisible</c>, que sería más corto pero miente al probar: cuenta
    /// como cámara la vista de escena del editor, así que el NPC se consideraría
    /// visible aunque en el juego no lo esté, y la desaparición no llegaría nunca
    /// mientras se depura.
    /// </summary>
    public override bool SePuedeDesaparecer()
    {
        Camera cam = camara != null ? camara : Camera.main;
        if (cam == null) return true;   // no hay quien mire: que se retire

        Renderer dibujo = GetComponentInChildren<Renderer>(true);
        Bounds caja = dibujo != null
            ? dibujo.bounds
            : new Bounds(transform.position, Vector3.zero);

        Vector3 a = cam.WorldToViewportPoint(caja.min);
        Vector3 b = cam.WorldToViewportPoint(caja.max);

        // Min/Max y no a/b directamente: según cómo esté puesta la cámara, la esquina
        // "mínima" del mundo puede caer a la derecha o arriba en pantalla.
        float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
        float y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);

        bool seVe = x1 >= -margen && x0 <= 1f + margen &&
                    y1 >= -margen && y0 <= 1f + margen;

        return !seVe;
    }
}
