using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PolygonColliderVisual : MonoBehaviour
{
    public PolygonCollider2D sourceCollider;
    public Color color = new Color(0f, 0f, 0f, 0.5f);

    private MeshRenderer meshRenderer;
    private bool built;

    void Awake()
    {
        Build();
    }

    /// <summary>
    /// Genera la malla y el material una sola vez. Es idempotente para que
    /// BlockedZone pueda pedir el oscurecido antes de que corra este Awake:
    /// el orden entre Awakes de distintos componentes no está garantizado.
    /// </summary>
    void Build()
    {
        if (built) return;
        built = true;

        meshRenderer = GetComponent<MeshRenderer>();
        if (sourceCollider == null) return;

        Mesh mesh = new Mesh();

        Vector2[] points = sourceCollider.points;

        Vector3[] vertices = new Vector3[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            vertices[i] = new Vector3(points[i].x, points[i].y, 0);
        }

        Triangulator triangulator = new Triangulator(points);
        int[] triangles = triangulator.Triangulate();

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;

        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = color;

        meshRenderer.material = material;
    }

    /// <summary>
    /// Ajusta la opacidad del oscurecido conservando su color. 0 lo apaga por
    /// completo (además desactiva el renderer, para no dibujar de balde).
    /// </summary>
    public void SetAlpha(float a)
    {
        Build();
        if (meshRenderer == null) return;

        Color c = color;
        c.a = a;
        meshRenderer.material.color = c;
        meshRenderer.enabled = a > 0.001f;
    }
}
