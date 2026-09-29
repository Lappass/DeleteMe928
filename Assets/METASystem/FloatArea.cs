using System.Collections.Generic;
using FloatingObjects;
using UnityEngine;

public class FloatArea : MonoBehaviour
{
    [Header("Region (local coordinates)")]
    [SerializeField] private Vector3 size = new Vector3(20, 10, 20);
    [SerializeField] private Transform platformRoot;
    [SerializeField, Min(1)] private int targetCount = 8;
    [SerializeField] private int seed = 928;
    [Header("Spawning")]
    [SerializeField, Min(.2f)] private float replenishDelay = 2;
    [SerializeField, Min(1)] private int attemptsPerSpawn = 30;
    [SerializeField] private Vector2 diameterRange = new Vector2(.9f, 2.1f);
    [SerializeField] private Vector2 heightAbovePlatform = new Vector2(.9f, 2.1f);
    [SerializeField, Min(0)] private float playerExclusionRadius = 2;
    [SerializeField, Min(0)] private float neighborDistance = 4;
    [SerializeField, Min(0)] private float clearance = .12f;
    [Header("Appearance")]
    [SerializeField] private Material floatMaterial;
    [SerializeField, Min(0)] private float bobAmplitude = .12f;
    [SerializeField, Min(0)] private float bobSpeed = 1.4f;
    [Header("Effects")]
    [SerializeField] private FloatEffectSettings effects = new FloatEffectSettings();
    private readonly List<Float> pool = new List<Float>();
    private readonly List<float> readyAt = new List<float>();
    private readonly List<Collider> surfaces = new List<Collider>();
    private FloatRandom random;
    private PlayerMovement[] players;
    private Material ownedMaterial;
    private float nextAttempt;
    private int slotCursor;
    public FloatEffectSettings Effects => effects;
    public float BobAmplitude => bobAmplitude;
    public float BobSpeed => bobSpeed;
    public float Clearance => clearance;
    public Material FloatMaterial => floatMaterial;
    public int ActiveCount
    {
        get { int count = 0; foreach (Float item in pool) if (item.IsAvailable) count++; return count; }
    }

    void Start()
    {
        random = new FloatRandom(seed);
        players = FindObjectsByType<PlayerMovement>();
        if (platformRoot == null)
        {
            Debug.LogError("FloatArea needs a platform root.", this);
            enabled = false;
            return;
        }
        foreach (Collider surface in platformRoot.GetComponentsInChildren<Collider>())
            if (!surface.isTrigger && WorldBounds.Intersects(surface.bounds)) surfaces.Add(surface);
        if (surfaces.Count == 0)
        {
            Debug.LogWarning("FloatArea has no platform surfaces in its bounds.", this);
            enabled = false;
            return;
        }
        if (floatMaterial == null)
        {
            Shader shader = Shader.Find("CloudHop/Soft Cloud");
            if (shader == null) { Debug.LogError("Assign a float material.", this); enabled = false; return; }
            ownedMaterial = new Material(shader) { name = "Float Material" };
            floatMaterial = ownedMaterial;
        }
        for (int i = 0; i < targetCount; i++)
        {
            var instance = new GameObject($"Float {i + 1}");
            instance.transform.SetParent(transform, true);
            Float item = instance.AddComponent<Float>();
            instance.SetActive(false);
            pool.Add(item);
            readyAt.Add(0);
        }
        Physics.SyncTransforms();
        for (int i = 0; i < pool.Count; i++) TrySpawn(i);
    }

    void Update()
    {
        if (Time.time < nextAttempt || pool.Count == 0) return;
        nextAttempt = Time.time + .25f;
        for (int i = 0; i < pool.Count; i++)
        {
            int slot = slotCursor;
            slotCursor = (slotCursor + 1) % pool.Count;
            if (!pool[slot].gameObject.activeSelf && Time.time >= readyAt[slot])
            {
                TrySpawn(slot);
                break;
            }
        }
    }

    private void TrySpawn(int slot)
    {
        var sizes = new int[3];
        var shapes = new int[3];
        var effectCounts = new int[4];
        foreach (Float item in pool)
            if (item.IsAvailable) { sizes[item.Appearance / 3]++; shapes[item.Appearance % 3]++; effectCounts[(int)item.Effect]++; }
        for (int attempt = 0; attempt < attemptsPerSpawn; attempt++)
        {
            int appearance = random.ChooseAppearance(sizes, shapes);
            FloatEffect effect = random.ChooseEffect(effectCounts);
            float step = (diameterRange.y - diameterRange.x) / 3;
            float diameter = random.Range(diameterRange.x + step * (appearance / 3), diameterRange.x + step * (appearance / 3 + 1));
            float radius = diameter * .5f + bobAmplitude + clearance;
            Collider surface = surfaces[random.Index(surfaces.Count)];
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy) continue;
            Bounds bounds = surface.bounds;
            Vector3 origin = new Vector3(random.Range(bounds.min.x, bounds.max.x), bounds.max.y + .1f,
                random.Range(bounds.min.z, bounds.max.z));
            if (!surface.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, bounds.size.y + .2f) || hit.normal.y < .8f) continue;
            float minimumHeight = Mathf.Max(heightAbovePlatform.x, radius + .02f);
            if (minimumHeight > heightAbovePlatform.y) continue;
            Vector3 position = hit.point + Vector3.up * random.Range(minimumHeight, heightAbovePlatform.y);
            if (!ContainsSphere(position, radius) || pool[slot].IsPreviousPosition(position)) continue;
            if (!SpaceAvailable(position, radius, appearance, effect)) continue;
            pool[slot].Spawn(this, position, appearance, diameter, random, effect);
            Physics.SyncTransforms();
            return;
        }
        readyAt[slot] = Time.time + .5f;
    }

    private bool SpaceAvailable(Vector3 position, float radius, int appearance, FloatEffect effect)
    {
        foreach (PlayerMovement player in players)
            if (player != null && Vector3.Distance(player.transform.position, position) < playerExclusionRadius + radius) return false;
        foreach (Float item in pool)
        {
            if (!item.gameObject.activeSelf) continue;
            float distance = Vector3.Distance(item.Anchor, position);
            if (distance < item.ReservationRadius + radius) return false;
            if (item.IsAvailable && item.Appearance == appearance && distance < neighborDistance) return false;
            if (item.IsAvailable && item.Effect == effect && distance < neighborDistance * .65f) return false;
        }
        // Other areas also reserve the full range of floating motion.
        foreach (FloatArea other in ActiveAreas)
        {
            if (other == this) continue;
            foreach (Float item in other.pool)
                if (item.gameObject.activeSelf && Vector3.Distance(item.Anchor, position) < item.ReservationRadius + radius) return false;
        }
        return !Physics.CheckSphere(position, radius, ~0, QueryTriggerInteraction.Ignore);
    }

    private static readonly HashSet<FloatArea> ActiveAreas = new HashSet<FloatArea>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAreas() => ActiveAreas.Clear();
    void OnEnable() => ActiveAreas.Add(this);
    void OnDisable() => ActiveAreas.Remove(this);

    public void Consumed(Float item)
    {
        int slot = pool.IndexOf(item);
        if (slot >= 0) readyAt[slot] = Time.time + Mathf.Max(.2f, replenishDelay);
    }

    private bool ContainsSphere(Vector3 position, float radius)
    {
        Vector3 local = transform.InverseTransformPoint(position);
        Vector3 scale = transform.lossyScale;
        return Mathf.Abs(local.x) + radius / Mathf.Max(.001f, Mathf.Abs(scale.x)) <= size.x * .5f &&
            Mathf.Abs(local.y) + radius / Mathf.Max(.001f, Mathf.Abs(scale.y)) <= size.y * .5f &&
            Mathf.Abs(local.z) + radius / Mathf.Max(.001f, Mathf.Abs(scale.z)) <= size.z * .5f;
    }

    private Bounds WorldBounds
    {
        get
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        bounds.Encapsulate(transform.TransformPoint(Vector3.Scale(size * .5f, new Vector3(x, y, z))));
            return bounds;
        }
    }

    void OnValidate()
    {
        targetCount = Mathf.Max(1, targetCount);
        attemptsPerSpawn = Mathf.Clamp(attemptsPerSpawn, 1, 30);
        size = new Vector3(Mathf.Max(1, size.x), Mathf.Max(1, size.y), Mathf.Max(1, size.z));
        diameterRange.x = Mathf.Max(.1f, diameterRange.x);
        diameterRange.y = Mathf.Max(diameterRange.x, diameterRange.y);
        heightAbovePlatform.y = Mathf.Max(heightAbovePlatform.x, heightAbovePlatform.y);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.2f, .9f, 1, .7f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, size);
    }

    void OnDestroy()
    {
        if (ownedMaterial != null) Destroy(ownedMaterial);
    }
}
