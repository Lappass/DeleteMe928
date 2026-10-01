using UnityEngine;

public class WaspShot : MonoBehaviour
{
    private Vector3 velocity;
    private float dieAt;
    private static Material sharedMaterial;

    public void Launch(Vector3 shotVelocity)
    {
        velocity = shotVelocity;
        dieAt = Time.time + 3f;
    }

    void Awake()
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.transform.SetParent(transform, false);
        visual.transform.localScale = Vector3.one * 0.34f;
        Destroy(visual.GetComponent<Collider>());
        if (sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                sharedMaterial = new Material(shader);
                sharedMaterial.SetColor("_BaseColor", new Color(1f, 0.85f, 0.3f, 1f));
                sharedMaterial.EnableKeyword("_EMISSION");
                sharedMaterial.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.05f) * 2.5f);
            }
        }
        if (sharedMaterial != null) visual.GetComponent<Renderer>().sharedMaterial = sharedMaterial;

        SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.22f;
        Rigidbody body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    void Update()
    {
        float step = velocity.magnitude * Time.deltaTime;
        Vector3 direction = velocity.sqrMagnitude > 0.001f ? velocity.normalized : Vector3.forward;
        Vector3 next = transform.position + direction * step;
        if (Strike(next)) return;
        if (step > 0.001f && Physics.SphereCast(transform.position, 0.18f, direction, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
            {
                PlayerMovement player = hit.collider.GetComponentInParent<PlayerMovement>();
                if (player != null) player.ReturnToStart();
                Destroy(gameObject);
                return;
            }
        }
        transform.position = next;
        if (Time.time >= dieAt) Destroy(gameObject);
    }

    private bool Strike(Vector3 point)
    {
        Collider[] overlaps = Physics.OverlapSphere(point, 0.22f, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap.transform == transform || overlap.transform.IsChildOf(transform)) continue;
            PlayerMovement player = overlap.GetComponentInParent<PlayerMovement>();
            if (player != null) player.ReturnToStart();
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;
        player.ReturnToStart();
        Destroy(gameObject);
    }
}
