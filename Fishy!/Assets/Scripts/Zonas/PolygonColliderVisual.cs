using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PolygonColliderVisual : MonoBehaviour
{
    public PolygonCollider2D sourceCollider;
    public Color color = new Color(0f, 0f, 0f, 0.5f);

    void Start()
    {
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

        GetComponent<MeshRenderer>().material = material;
    }
}