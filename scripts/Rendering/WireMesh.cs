using Godot;

/// <summary>
/// Fabrique des maillages "fil-de-fer" (primitive Lines) réutilisés par les
/// MultiMesh. Un seul maillage partagé par type d'entité, instancié des
/// milliers de fois. Direction artistique : formes géométriques wireframe.
/// </summary>
public static class WireMesh
{
    /// <summary>Contour d'un polygone régulier à N côtés, rayon donné.</summary>
    public static ArrayMesh Polygon(int sides, float radius)
    {
        var verts = new Vector3[sides * 2];
        for (int s = 0; s < sides; s++)
        {
            float a0 = Mathf.Tau * s / sides - Mathf.Pi / 2f;
            float a1 = Mathf.Tau * (s + 1) / sides - Mathf.Pi / 2f;
            verts[s * 2] = new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f);
            verts[s * 2 + 1] = new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
        }
        return BuildLines(verts);
    }

    /// <summary>Petit segment horizontal (projectile), orienté ensuite par instance.</summary>
    public static ArrayMesh Segment(float half)
    {
        var verts = new Vector3[] { new(-half, 0f, 0f), new(half, 0f, 0f) };
        return BuildLines(verts);
    }

    private static ArrayMesh BuildLines(Vector3[] verts)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        return mesh;
    }
}
