using UnityEngine;
using UnityEngine.UI;

// Small vector shapes keep the HUD crisp without texture assets or input blocking.
public class CloudHUDShape : MaskableGraphic
{
    public bool cloud;
    public float cornerRadius = 16;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (cloud)
        {
            Circle(mesh, new Vector2(rect.x + rect.width * .28f, rect.center.y - rect.height * .05f), rect.height * .28f);
            Circle(mesh, new Vector2(rect.center.x, rect.center.y + rect.height * .11f), rect.height * .39f);
            Circle(mesh, new Vector2(rect.x + rect.width * .74f, rect.center.y), rect.height * .31f);
            Circle(mesh, new Vector2(rect.center.x, rect.center.y - rect.height * .12f), rect.height * .30f);
            return;
        }
        float radius = Mathf.Min(cornerRadius, Mathf.Min(rect.width, rect.height) * .5f);
        mesh.AddVert(rect.center, color, Vector2.zero);
        Vector2[] corners = {
            new Vector2(rect.xMax - radius, rect.yMax - radius), new Vector2(rect.xMin + radius, rect.yMax - radius),
            new Vector2(rect.xMin + radius, rect.yMin + radius), new Vector2(rect.xMax - radius, rect.yMin + radius)
        };
        const int steps = 8;
        for (int corner = 0; corner < 4; corner++)
            for (int step = 0; step <= steps; step++)
            {
                float angle = (corner * 90f + step * 90f / steps) * Mathf.Deg2Rad;
                mesh.AddVert(corners[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            }
        int count = 4 * (steps + 1);
        for (int i = 1; i <= count; i++) mesh.AddTriangle(0, i, i == count ? 1 : i + 1);
    }

    private void Circle(VertexHelper mesh, Vector2 center, float radius)
    {
        int first = mesh.currentVertCount;
        mesh.AddVert(center, color, Vector2.zero);
        const int segments = 28;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % segments);
    }
}
