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
    private Mesh collisionMesh;
    private MeshRenderer cloudRenderer;
    private ParticleSystem wisps;
    private FloatRandom cosmeticRandom;
    private Vector3 anchor;
    private Quaternion rotation;
    private float phase;
    private float spin;
    private float consumedAt = -1;
    private Vector3 lastSpawn;
    private bool hasSpawned;
    public int Appearance { get; private set; }
    public FloatEffect Effect { get; private set; }
    public Color EffectColor => CloudStyle.ColorFor(Effect);
    public FloatArea Area => area;
    public float ReservationRadius { get; private set; }
    public Vector3 Anchor => anchor;
    public bool HasSpawnedBefore => hasSpawned;
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
        cloudRenderer = GetComponent<MeshRenderer>();
        var particles = new GameObject("Cloud wisps");
        particles.transform.SetParent(transform, false);
        wisps = particles.AddComponent<ParticleSystem>();
        wisps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void Spawn(FloatArea owner, Vector3 position, int appearance, float diameter, FloatRandom random, FloatEffect effect)
    {
        area = owner;
        Appearance = appearance;
        Effect = effect;
        ReservationRadius = diameter * .5f + owner.BobAmplitude + owner.Clearance;
        anchor = lastSpawn = position;
        hasSpawned = true;
        consumedAt = -1;
        phase = random.Range(0, Mathf.PI * 2);
        spin = random.Range(-7, 7);
        rotation = Quaternion.Euler(0, random.Range(0, 360), 0);
        transform.SetPositionAndRotation(position, rotation);
        // Mesh dimensions are world units even under a scaled region transform.
        Vector3 scale = transform.parent.lossyScale;
        transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
        restingScale = transform.localScale;
        trigger.sharedMesh = null;
        if (ownedMesh != null) Destroy(ownedMesh);
        if (collisionMesh != null) Destroy(collisionMesh);
        CloudMesh.Create(diameter, appearance % 3, random, out ownedMesh, out collisionMesh);
        GetComponent<MeshFilter>().sharedMesh = ownedMesh;
        trigger.sharedMesh = collisionMesh;
        trigger.enabled = true;
        cloudRenderer.enabled = true;
        cloudRenderer.sharedMaterial = owner.FloatMaterial;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", EffectColor);
        cloudRenderer.SetPropertyBlock(properties);
        cosmeticRandom = new FloatRandom(random.Index(int.MaxValue));
        gameObject.SetActive(true);
        body.position = position;
        body.rotation = rotation;
        ConfigureWisps(diameter);
    }

    private Vector3 restingScale = Vector3.one;

    void FixedUpdate()
    {
        if (area == null || consumedAt >= 0) return;
        body.MovePosition(anchor + Vector3.up * (Mathf.Sin(Time.time * area.BobSpeed + phase) * area.BobAmplitude));
        rotation *= Quaternion.Euler(0, spin * Time.fixedDeltaTime, 0);
        body.MoveRotation(rotation);
    }

    void Update()
    {
        if (consumedAt < 0) return;
        float progress = (Time.time - consumedAt) / .18f;
        transform.localScale = restingScale * Mathf.Max(.001f, 1 - progress);
        if (progress >= 1) cloudRenderer.enabled = false;
        if (Time.time - consumedAt >= .7f) gameObject.SetActive(false);
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
        if (!experience.TryApply(this)) return;
        consumedAt = Time.time;
        trigger.enabled = false;
        Burst();
        area.Consumed(this);
    }

    private void ConfigureWisps(float diameter)
    {
        wisps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = wisps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 1.3f;
        main.startSpeed = .08f;
        main.startSize = diameter * .10f;
        main.startColor = EffectColor;
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        var emission = wisps.emission;
        emission.enabled = true;
        emission.rateOverTime = 2;
        var shape = wisps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = diameter * .34f;
        var size = wisps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
        var renderer = wisps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        renderer.sharedMaterial = area.FloatMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        wisps.Play();
    }

    private void Burst()
    {
        var emission = wisps.emission;
        emission.enabled = false;
        for (int i = 0; i < 14; i++)
        {
            Vector3 velocity = new Vector3(cosmeticRandom.Range(-1, 1), cosmeticRandom.Range(.1f, 1), cosmeticRandom.Range(-1, 1));
            var puff = new ParticleSystem.EmitParams
            {
                position = transform.position,
                velocity = velocity * .85f,
                startLifetime = .6f,
                startSize = cosmeticRandom.Range(.10f, .23f),
                startColor = EffectColor
            };
            wisps.Emit(puff, 1);
        }
    }

    void OnDestroy()
    {
        if (ownedMesh != null) Destroy(ownedMesh);
        if (collisionMesh != null) Destroy(collisionMesh);
    }
}
