using System.Collections.Generic;
using FloatingObjects;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
[RequireComponent(typeof(Rigidbody))]
public class Float : MonoBehaviour
{
    private FloatArea area;
    private Rigidbody body;
    private MeshCollider trigger;
    private Mesh ownedMesh;
    private Vector3 anchor;
    private Quaternion rotation;
    private float phase;
    private float spin;
    private float consumedAt = -1;
    private Vector3 lastSpawn;
    private bool hasSpawned;
    public int Appearance { get; private set; }
    public float ReservationRadius { get; private set; }
    public Vector3 Anchor => anchor;
    public bool IsAvailable => gameObject.activeSelf && consumedAt < 0;
    public bool IsPreviousPosition(Vector3 position) => hasSpawned && Vector3.Distance(lastSpawn, position) < 1f;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        trigger = GetComponent<MeshCollider>();
        trigger.convex = true;
        trigger.isTrigger = true;
    }

    public void Spawn(FloatArea owner, Vector3 position, int appearance, float diameter, FloatRandom random)
    {
        area = owner;
        Appearance = appearance;
        ReservationRadius = diameter * .5f + owner.BobAmplitude + owner.Clearance;
        anchor = lastSpawn = position;
        hasSpawned = true;
        consumedAt = -1;
        phase = random.Range(0, Mathf.PI * 2);
        spin = random.Range(12, 28);
        rotation = Quaternion.Euler(random.Range(0, 360), random.Range(0, 360), random.Range(0, 360));
        transform.SetPositionAndRotation(position, rotation);
        // Mesh dimensions are world units even under a scaled region transform.
        Vector3 scale = transform.parent.lossyScale;
        transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
        restingScale = transform.localScale;
        trigger.sharedMesh = null;
        if (ownedMesh != null) Destroy(ownedMesh);
        ownedMesh = CreateMesh(diameter, appearance % 3, random);
        GetComponent<MeshFilter>().sharedMesh = ownedMesh;
        trigger.sharedMesh = ownedMesh;
        trigger.enabled = true;
        var renderer = GetComponent<MeshRenderer>();
        renderer.sharedMaterial = owner.FloatMaterial;
        var properties = new MaterialPropertyBlock();
        Color color = Color.HSVToRGB(random.Range(.43f, .65f), .35f, 1f);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
        gameObject.SetActive(true);
        body.position = position;
        body.rotation = rotation;
    }

    private Vector3 restingScale = Vector3.one;

    void FixedUpdate()
    {
        if (area == null || consumedAt >= 0) return;
        body.MovePosition(anchor + Vector3.up * (Mathf.Sin(Time.time * area.BobSpeed + phase) * area.BobAmplitude));
        rotation *= Quaternion.Euler(spin * .3f * Time.fixedDeltaTime, spin * Time.fixedDeltaTime, 0);
        body.MoveRotation(rotation);
    }

    void Update()
    {
        if (consumedAt < 0) return;
        float progress = (Time.time - consumedAt) / .18f;
        transform.localScale = restingScale * Mathf.Max(.001f, 1 - progress);
        if (progress >= 1) gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other) => TryConsume(other);
    void OnTriggerStay(Collider other) => TryConsume(other);

    private void TryConsume(Collider other)
    {
        if (!IsAvailable) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;
        FloatExperience experience = player.GetComponent<FloatExperience>();
        if (experience == null) experience = player.gameObject.AddComponent<FloatExperience>();
        if (!experience.TryApply(area)) return;
        consumedAt = Time.time;
        trigger.enabled = false;
        area.Consumed(this);
    }

    private static Mesh CreateMesh(float diameter, int shape, FloatRandom random)
    {
        var vertices = new List<Vector3> { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        var faces = new List<int> { 0,4,3, 0,3,5, 0,5,2, 0,2,4, 1,3,4, 1,5,3, 1,2,5, 1,4,2 };
        for (int subdivision = 0; subdivision < 2; subdivision++)
        {
            var midpoints = new Dictionary<long, int>();
            var refined = new List<int>();
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Midpoint(a, b, vertices, midpoints);
                int bc = Midpoint(b, c, vertices, midpoints);
                int ca = Midpoint(c, a, vertices, midpoints);
                refined.AddRange(new[] { a,ab,ca, ab,b,bc, ca,bc,c, ab,bc,ca });
            }
            faces = refined;
        }
        Vector3 stretch = shape == 1 ? new Vector3(1, .45f, .55f) :
            shape == 2 ? new Vector3(1, .35f, .85f) : new Vector3(1, .9f, .85f);
        float maxRadius = 0;
        for (int i = 0; i < vertices.Count; i++)
        {
            vertices[i] = Vector3.Scale(vertices[i], stretch) * random.Range(.82f, 1.15f);
            maxRadius = Mathf.Max(maxRadius, vertices[i].magnitude);
        }
        for (int i = 0; i < vertices.Count; i++) vertices[i] *= diameter * .5f / maxRadius;
        var mesh = new Mesh { name = "Procedural Float" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(faces, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
    {
        long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        if (cache.TryGetValue(key, out int index)) return index;
        index = vertices.Count;
        vertices.Add((vertices[a] + vertices[b]).normalized);
        cache.Add(key, index);
        return index;
    }

    void OnDestroy()
    {
        if (ownedMesh != null) Destroy(ownedMesh);
    }
}
