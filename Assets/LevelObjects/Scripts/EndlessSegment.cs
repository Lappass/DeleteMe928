using System.Collections.Generic;
using UnityEngine;

/// <summary>Procedural terrain and a moving, genuinely open lava vent for one streamed segment.</summary>
public sealed class EndlessSegment : MonoBehaviour
{
    private const float GridStep = .65f;
    private const int Sides = 5;
    private float startZ, length, centerX, floorY, floorWidth, phase;
    private float maximumHoleRadius = 8f;
    private Material groundMaterial, lavaMaterial;
    private Transform platformsRoot;
    private Mesh floorMesh;
    private MeshCollider floorCollider;
    private readonly List<Mesh> ownedMeshes = new List<Mesh>();
    private Transform lavaDisc;
    private CapsuleCollider lavaTrigger;
    private Light warningLight;
    private Transform warningRing;
    private Vector2 holeCenter;
    private float nextMeshUpdate;
    private int rows, columns;
    public int SegmentIndex { get; private set; }
    public float StartZ => startZ;
    public float EndZ => startZ + length;
    public float CenterX => centerX;
    public float EndX { get; private set; }
    public float EndY { get; private set; }
    public float SafeAreaRatio { get; private set; } = 1f;
    public Transform PlatformsRoot => platformsRoot;
    public FloatArea CloudArea { get; private set; }
    public float MaximumHoleRadius => maximumHoleRadius;

    public void ConfigureHoleRadius(float radius) => maximumHoleRadius = Mathf.Max(4f, radius);

    public void Build(int index, float z, float x, float y, float segmentLength, float width,
        int seed, float difficulty, float laneCenterX, Material platformMat, Material lavaMat, Material cloudMat)
    {
        SegmentIndex = index;
        startZ = z;
        centerX = x;
        length = segmentLength;
        floorWidth = width;
        // Leave at least 70% safe floor even with the grid's cell-size margin.
        maximumHoleRadius = Mathf.Min(maximumHoleRadius,
            Mathf.Sqrt(.3f * length * floorWidth / Mathf.PI) - GridStep);
        floorY = -3f;
        groundMaterial = platformMat;
        lavaMaterial = lavaMat;
        var rng = new System.Random(unchecked(seed * 397 ^ index * 7919));
        phase = (float)rng.NextDouble() * 30f;
        platformsRoot = new GameObject("Platforms").transform;
        platformsRoot.SetParent(transform, false);

        CreatePlatforms(rng, y, difficulty, laneCenterX, platformMat);
        CreateGround();
        CreateHole(rng);
        CreateCloudArea(seed, cloudMat);
    }

    private void CreatePlatforms(System.Random rng, float incomingY, float difficulty, float laneCenterX, Material material)
    {
        float x = centerX;
        float y = incomingY;
        const int paths = 8;
        float dz = length / paths;
        for (int i = 0; i < paths; i++)
        {
            // A gentle random walk keeps the route mostly forward and within jump range.
            // The last four steps ease back to a fixed seam anchor. Otherwise the
            // final snap can create an unreachable sideways or vertical jump.
            if (i == paths - 1)
            {
                // Land on a stable seam anchor so any recycled/recreated section
                // connects to the same next-section start.
                x = laneCenterX;
                y = incomingY;
            }
            else if (i >= paths - 4)
            {
                x = Mathf.MoveTowards(x, laneCenterX, 2.3f);
                y = Mathf.MoveTowards(y, incomingY, .55f + difficulty * .45f);
            }
            else
            {
                x = Mathf.Clamp(x + Range(rng, -2.3f, 2.3f), laneCenterX - 8f, laneCenterX + 8f);
                y = Mathf.Clamp(y + Range(rng, -0.55f - difficulty * .45f, .55f + difficulty * .45f), 2.3f, 10f);
            }
            float z = startZ + (i + 1) * dz;
            float radiusX = Range(rng, 1.45f, 2.25f);
            float radiusZ = Range(rng, 1.55f, 2.3f);
            float thickness = Range(rng, .38f, .72f);
            Quaternion rotation = Quaternion.Euler(0, Range(rng, 0, 360), 0);
            CreatePentagon($"Step {i + 1}", new Vector3(x, y, z), radiusX, radiusZ, thickness, rotation, rng, material);

            // Optional nearby step gives the player route choice and a recovery landing.
            if (i > 0 && (i % 2 == 0 || rng.NextDouble() < .35))
            {
                float sideX = Mathf.Clamp(x + (rng.Next(0, 2) == 0 ? -1 : 1) * Range(rng, 2.8f, 4f), laneCenterX - 11f, laneCenterX + 11f);
                float sideZ = Mathf.Clamp(z + Range(rng, -1.5f, 1.5f), startZ + .5f, startZ + length - .5f);
                float sideY = Mathf.Clamp(y + Range(rng, -.65f, .65f), 2.2f, 10f);
                CreatePentagon($"Choice {i + 1}", new Vector3(sideX, sideY, sideZ),
                    Range(rng, 1.35f, 2f), Range(rng, 1.4f, 2f), Range(rng, .35f, .62f),
                    Quaternion.Euler(0, Range(rng, 0, 360), 0), rng, material);
            }
        }
        EndX = laneCenterX;
        EndY = incomingY;
    }

    private void CreatePentagon(string name, Vector3 position, float rx, float rz, float thickness,
        Quaternion rotation, System.Random rng, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(EndlessSafePlatform));
        go.transform.SetParent(platformsRoot, true);
        go.transform.SetPositionAndRotation(position, rotation);
        Mesh mesh = BuildPentagonMesh(rx, rz, thickness, rng);
        ownedMeshes.Add(mesh);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        go.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static Mesh BuildPentagonMesh(float rx, float rz, float thickness, System.Random rng)
    {
        var vertices = new Vector3[12];
        float offset = Range(rng, 0, Mathf.PI * 2);
        for (int i = 0; i < Sides; i++)
        {
            float a = offset + i * Mathf.PI * 2f / Sides;
            float irregular = Range(rng, .82f, 1.12f);
            vertices[i] = new Vector3(Mathf.Cos(a) * rx * irregular, 0, Mathf.Sin(a) * rz * irregular);
            vertices[i + Sides] = new Vector3(vertices[i].x, -thickness, vertices[i].z);
        }
        vertices[10] = Vector3.zero;
        vertices[11] = new Vector3(0, -thickness, 0);
        var triangles = new List<int>(60);
        for (int i = 0; i < Sides; i++)
        {
            int next = (i + 1) % Sides;
            triangles.Add(10); triangles.Add(next); triangles.Add(i);
            triangles.Add(11); triangles.Add(i + Sides); triangles.Add(next + Sides);
            triangles.Add(i); triangles.Add(i + Sides); triangles.Add(next + Sides);
            triangles.Add(i); triangles.Add(next + Sides); triangles.Add(next);
        }
        var mesh = new Mesh { name = "Irregular Pentagon Step" };
        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreateGround()
    {
        var go = new GameObject("Safe ground with open lava vent", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(centerX, floorY, startZ + length * .5f);
        columns = Mathf.CeilToInt(floorWidth / GridStep);
        rows = Mathf.CeilToInt(length / GridStep);
        floorMesh = new Mesh { name = "Dynamic safe ground" };
        go.GetComponent<MeshFilter>().sharedMesh = floorMesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
        floorCollider = go.GetComponent<MeshCollider>();
        floorCollider.sharedMesh = floorMesh;
    }

    private void CreateHole(System.Random rng)
    {
        // holeCenter is in the floor mesh's centered local XZ coordinates.
        holeCenter = ChooseHoleCenter(rng);
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Lava glow beneath vent";
        disc.transform.SetParent(transform, false);
        disc.transform.position = new Vector3(centerX + holeCenter.x, floorY - 2.5f, startZ + length * .5f + holeCenter.y);
        disc.transform.localScale = new Vector3(5, .025f, 5);
        Object.Destroy(disc.GetComponent<Collider>());
        disc.GetComponent<MeshRenderer>().sharedMaterial = lavaMaterial;
        lavaDisc = disc.transform;

        var vent = new GameObject("Lava opening trigger", typeof(CapsuleCollider), typeof(Lava));
        vent.transform.SetParent(transform, false);
        vent.transform.position = new Vector3(centerX + holeCenter.x, floorY - 4.5f, startZ + length * .5f + holeCenter.y);
        lavaTrigger = vent.GetComponent<CapsuleCollider>();
        lavaTrigger.direction = 1;
        lavaTrigger.height = 6;
        lavaTrigger.radius = 2.5f;
        lavaTrigger.isTrigger = true;
        UpdateHoleMesh(2.5f);

        var rim = new GameObject("Warning glow", typeof(Light));
        rim.transform.SetParent(transform, false);
        rim.transform.position = new Vector3(centerX + holeCenter.x, floorY - .25f, startZ + length * .5f + holeCenter.y);
        warningLight = rim.GetComponent<Light>();
        warningLight.type = LightType.Point;
        warningLight.color = new Color(1f, .23f, .035f);
        warningLight.range = 5f;
        warningLight.intensity = 0;
        warningLight.shadows = LightShadows.None;

        var ring = new GameObject("Expanding vent warning ring", typeof(MeshFilter), typeof(MeshRenderer));
        ring.transform.SetParent(transform, false);
        warningRing = ring.transform;
        warningRing.position = new Vector3(centerX + holeCenter.x, floorY + .025f, startZ + length * .5f + holeCenter.y);
        warningRing.localScale = Vector3.one * 2.5f;
        Mesh warningMesh = CreateRingMesh();
        ownedMeshes.Add(warningMesh);
        warningRing.GetComponent<MeshFilter>().sharedMesh = warningMesh;
        warningRing.GetComponent<MeshRenderer>().sharedMaterial = lavaMaterial;
    }

    private void CreateCloudArea(int seed, Material cloudMaterial)
    {
        var go = new GameObject("Floating cloud area");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(centerX, 6f, startZ + length * .5f);
        CloudArea = go.AddComponent<FloatArea>();
        CloudArea.Configure(new Vector3(floorWidth - 4, 13, length - 2), platformsRoot, 4,
            unchecked(seed + SegmentIndex * 31337), cloudMaterial);
    }

    private void Update()
    {
        if (Time.time < nextMeshUpdate) return;
        nextMeshUpdate = Time.time + .35f;
        float cycle = Mathf.Repeat((Time.time + phase) / 23f, 1f);
        float radius = cycle < .44f ? Mathf.Lerp(1.4f, maximumHoleRadius, Mathf.SmoothStep(0, 1, cycle / .44f))
            : cycle < .88f ? Mathf.Lerp(maximumHoleRadius, 1.4f, Mathf.SmoothStep(0, 1, (cycle - .44f) / .44f)) : 1.4f;
        if (cycle < .035f)
        {
            int hash = SegmentIndex * 92821 + Mathf.FloorToInt((Time.time + phase) / 23f) * 137;
            var rng = new System.Random(hash);
            holeCenter = ChooseHoleCenter(rng);
        }
        UpdateHoleMesh(radius);
        float holeWorldZ = startZ + length * .5f + holeCenter.y;
        lavaDisc.position = new Vector3(centerX + holeCenter.x, floorY - 2.5f, holeWorldZ);
        lavaDisc.localScale = new Vector3(radius * 2, .025f, radius * 2);
        lavaTrigger.transform.position = new Vector3(centerX + holeCenter.x, floorY - 4.5f, holeWorldZ);
        lavaTrigger.radius = radius;
        lavaTrigger.height = Mathf.Max(6, radius * 2);
        if (warningLight != null)
        {
            warningLight.transform.position = new Vector3(centerX + holeCenter.x, floorY - .25f, holeWorldZ);
            warningLight.range = radius * 1.6f;
            warningLight.intensity = Mathf.Lerp(.2f, 3.2f, radius / maximumHoleRadius);
        }
        if (warningRing != null)
        {
            warningRing.position = new Vector3(centerX + holeCenter.x, floorY + .025f, holeWorldZ);
            warningRing.localScale = Vector3.one * radius;
        }
    }

    private void UpdateHoleMesh(float radius)
    {
        if (floorMesh == null) return;
        float widthStep = floorWidth / columns;
        float lengthStep = length / rows;
        float left = -floorWidth * .5f;
        float back = -length * .5f;
        var vertices = new List<Vector3>((columns + 1) * (rows + 1));
        var triangles = new List<int>(columns * rows * 6);
        int[,] ids = new int[columns + 1, rows + 1];
        for (int x = 0; x <= columns; x++)
            for (int z = 0; z <= rows; z++)
            {
                ids[x, z] = vertices.Count;
                vertices.Add(new Vector3(left + x * widthStep, 0, back + z * lengthStep));
            }
        int safeCells = 0, allCells = columns * rows;
        for (int x = 0; x < columns; x++)
            for (int z = 0; z < rows; z++)
            {
                float px = left + (x + .5f) * widthStep - holeCenter.x;
                float pz = back + (z + .5f) * lengthStep - holeCenter.y;
                if (px * px + pz * pz < radius * radius) continue;
                safeCells++;
                int a = ids[x, z], b = ids[x + 1, z], c = ids[x + 1, z + 1], d = ids[x, z + 1];
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
            }
        SafeAreaRatio = (float)safeCells / allCells;
        floorMesh.Clear();
        floorMesh.SetVertices(vertices);
        floorMesh.SetTriangles(triangles, 0);
        floorMesh.RecalculateNormals();
        floorMesh.RecalculateBounds();
        floorCollider.sharedMesh = null;
        floorCollider.sharedMesh = floorMesh;
    }

    private static float Range(System.Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();

    private Vector2 ChooseHoleCenter(System.Random rng)
    {
        // A margin at each edge leaves a walkable corridor and keeps neighboring
        // sections' vents from merging across their seams.
        float xTravel = Mathf.Min(floorWidth * .25f, Mathf.Max(0, floorWidth * .5f - maximumHoleRadius - 4f));
        float zTravel = Mathf.Max(0, length * .5f - maximumHoleRadius - 1.5f);
        return new Vector2(Range(rng, -xTravel, xTravel), Range(rng, -zTravel, zTravel));
    }

    private static Mesh CreateRingMesh()
    {
        const int count = 48;
        var vertices = new Vector3[count * 2];
        var indices = new int[count * 6];
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2 / count;
            float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(x * 1.16f, 0, z * 1.16f);
            vertices[i * 2 + 1] = new Vector3(x * .94f, 0, z * .94f);
            int next = (i + 1) % count;
            int t = i * 6;
            indices[t] = i * 2; indices[t + 1] = i * 2 + 1; indices[t + 2] = next * 2;
            indices[t + 3] = next * 2; indices[t + 4] = i * 2 + 1; indices[t + 5] = next * 2 + 1;
        }
        var mesh = new Mesh { name = "Vent warning ring" };
        mesh.vertices = vertices; mesh.triangles = indices; mesh.RecalculateNormals();
        return mesh;
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        if (floorMesh != null) Destroy(floorMesh);
    }
}
