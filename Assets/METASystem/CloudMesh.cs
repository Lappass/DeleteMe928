using System.Collections.Generic;
using FloatingObjects;
using UnityEngine;

public static class CloudMesh
{
    // Smooth overlapping lobes give a stable, fluffy silhouette from every angle.
    // The trigger uses only each lobe's six extremities: a cheap convex envelope.
    public static void Create(float diameter, int shape, FloatRandom random, out Mesh visual, out Mesh collision)
    {
        var centers = new List<Vector3> { new Vector3(0, -.06f, 0) };
        var radii = new List<Vector3> { new Vector3(.46f, .23f, .29f) };
        int lobes = 6 + random.Index(3);
        for (int i = 0; i < lobes; i++)
        {
            float angle = i * Mathf.PI * 2 / lobes;
            centers.Add(new Vector3(Mathf.Cos(angle) * .27f, random.Range(.0f, .13f), Mathf.Sin(angle) * .16f));
            float radius = random.Range(.20f, .29f);
            radii.Add(new Vector3(radius, radius * random.Range(.85f, 1.15f), radius * .95f));
        }
        centers.Add(new Vector3(-.025f, .18f, .01f));
        radii.Add(new Vector3(.29f, .29f, .27f));
        Vector3 stretch = shape == 1 ? new Vector3(1.2f, .9f, .8f) :
            shape == 2 ? new Vector3(1.1f, .65f, 1.05f) : Vector3.one;
        float bound = 0;
        for (int i = 0; i < centers.Count; i++)
        {
            centers[i] = Vector3.Scale(centers[i], stretch);
            radii[i] = Vector3.Scale(radii[i], stretch);
            // Conservative sphere encloses both the visual and the collision envelope.
            bound = Mathf.Max(bound, centers[i].magnitude + Mathf.Max(radii[i].x, Mathf.Max(radii[i].y, radii[i].z)));
        }
        float scale = diameter * .5f / bound;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        var hullVertices = new List<Vector3>();
        var hullTriangles = new List<int>();
        Vector3[] axes = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        int[] octahedron = { 0,4,3, 0,3,5, 0,5,2, 0,2,4, 1,3,4, 1,5,3, 1,2,5, 1,4,2 };
        for (int i = 0; i < centers.Count; i++)
        {
            AddLobe(centers[i] * scale, radii[i] * scale, vertices, normals, triangles);
            int offset = hullVertices.Count;
            foreach (Vector3 axis in axes) hullVertices.Add((centers[i] + Vector3.Scale(axis, radii[i])) * scale);
            foreach (int index in octahedron) hullTriangles.Add(offset + index);
        }
        visual = new Mesh { name = "Rounded cloud lobes" };
        visual.SetVertices(vertices);
        visual.SetNormals(normals);
        visual.SetTriangles(triangles, 0);
        var colors = new Color[vertices.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        visual.colors = colors;
        visual.RecalculateBounds();
        collision = new Mesh { name = "Cloud trigger envelope" };
        collision.SetVertices(hullVertices);
        collision.SetTriangles(hullTriangles, 0);
        collision.RecalculateBounds();
    }

    private static void AddLobe(Vector3 center, Vector3 radius, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
    {
        const int rings = 10, segments = 16;
        int offset = vertices.Count;
        for (int y = 0; y <= rings; y++)
        {
            float latitude = y * Mathf.PI / rings;
            for (int x = 0; x <= segments; x++)
            {
                float longitude = x * Mathf.PI * 2 / segments;
                Vector3 direction = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                vertices.Add(center + Vector3.Scale(direction, radius));
                normals.Add(new Vector3(direction.x / radius.x, direction.y / radius.y, direction.z / radius.z).normalized);
            }
        }
        for (int y = 0; y < rings; y++)
            for (int x = 0; x < segments; x++)
            {
                int a = offset + y * (segments + 1) + x;
                int b = a + segments + 1;
                if (y > 0) triangles.AddRange(new[] { a, a + 1, b });
                if (y < rings - 1) triangles.AddRange(new[] { a + 1, b + 1, b });
            }
    }
}
