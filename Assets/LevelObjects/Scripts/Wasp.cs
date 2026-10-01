using UnityEngine;

// Hovering drone. A red beam tracks the player, freezes, then a shot leaves along that beam.
public class Wasp : MonoBehaviour
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform[] wings;
    [SerializeField] private float orbitRadius = 4f;
    [SerializeField] private float orbitSpeed = 0.45f;
    [SerializeField] private float hoverHeight = 5.4f;
    [SerializeField] private float phase;
    [SerializeField] private float lockDuration = 0.9f;
    [SerializeField] private float commitDuration = 0.32f;
    [SerializeField] private float recoverDuration = 1.35f;
    [SerializeField] private float shotSpeed = 20f;
    [SerializeField] private float aimSmoothTime = 0.08f;

    private enum State { Idle, Locking, Committed, Recover }

    private State state;
    private float stateTime;
    private bool fired;
    private Vector3 anchor;
    private Vector3 aimPoint;
    private Vector3 aimVelocity;
    private Vector3 frozenAim;
    private float angle;
    private PlayerMovement player;
    private LineRenderer laser;
    private Quaternion[] wingRest;
    private Material laserMat;

    void Awake()
    {
        anchor = transform.position;
        anchor.y = 0f;
        angle = phase;
        if (muzzle == null) muzzle = transform;
        laser = gameObject.AddComponent<LineRenderer>();
        laser.positionCount = 2;
        laser.useWorldSpace = true;
        laser.numCapVertices = 4;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.receiveShadows = false;
        laser.enabled = false;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            laserMat = new Material(shader);
            laserMat.SetColor("_BaseColor", Color.white);
            laser.material = laserMat;
        }
        wingRest = new Quaternion[wings == null ? 0 : wings.Length];
        for (int i = 0; i < wingRest.Length; i++)
            if (wings[i] != null) wingRest[i] = wings[i].localRotation;
    }

    void Update()
    {
        if (player == null) player = FindAnyObjectByType<PlayerMovement>();
        Hover();
        Flutter();
        if (player == null)
        {
            laser.enabled = false;
            return;
        }
        FacePlayer();
        switch (state)
        {
            case State.Idle:
                laser.enabled = false;
                if (TrySee(out Vector3 seen)) BeginLock(seen);
                break;
            case State.Locking:
                stateTime += Time.deltaTime;
                if (!TrySee(out Vector3 tracked))
                {
                    state = State.Idle;
                    laser.enabled = false;
                    break;
                }
                aimPoint = Vector3.SmoothDamp(aimPoint, tracked, ref aimVelocity, aimSmoothTime);
                DrawLaser(aimPoint, Mathf.Clamp01(stateTime / lockDuration), false);
                if (stateTime >= lockDuration)
                {
                    frozenAim = aimPoint;
                    fired = false;
                    state = State.Committed;
                    stateTime = 0f;
                }
                break;
            case State.Committed:
                stateTime += Time.deltaTime;
                DrawLaser(frozenAim, 1f, true);
                if (!fired && stateTime >= commitDuration)
                {
                    Fire();
                    fired = true;
                }
                if (stateTime >= commitDuration + 0.12f)
                {
                    laser.enabled = false;
                    state = State.Recover;
                    stateTime = 0f;
                }
                break;
            case State.Recover:
                stateTime += Time.deltaTime;
                if (stateTime >= recoverDuration) state = State.Idle;
                break;
        }
    }

    private void BeginLock(Vector3 seen)
    {
        state = State.Locking;
        stateTime = 0f;
        aimPoint = seen;
        aimVelocity = Vector3.zero;
    }

    private void Hover()
    {
        angle += orbitSpeed * Time.deltaTime;
        Vector3 home = anchor + new Vector3(Mathf.Cos(angle) * orbitRadius, hoverHeight, Mathf.Sin(angle) * orbitRadius);
        home.y += Mathf.Sin(Time.time * 1.6f + phase) * 0.28f;
        transform.position = Vector3.Lerp(transform.position, home, 1f - Mathf.Exp(-3f * Time.deltaTime));
    }

    private void FacePlayer()
    {
        Vector3 flat = player.transform.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) return;
        Quaternion look = Quaternion.LookRotation(flat);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-6f * Time.deltaTime));
    }

    private void Flutter()
    {
        float flap = Mathf.Sin(Time.time * 6f + phase) * 4f;
        for (int i = 0; i < wingRest.Length; i++)
        {
            if (wings[i] == null) continue;
            float side = i % 2 == 0 ? 1f : -1f;
            wings[i].localRotation = wingRest[i] * Quaternion.Euler(0f, 0f, flap * side);
        }
    }

    private bool TrySee(out Vector3 point)
    {
        point = player.transform.position;
        Vector3 origin = muzzle.position;
        Vector3 delta = point - origin;
        float distance = delta.magnitude;
        if (distance < 0.2f) return true;
        if (!Physics.Raycast(origin, delta / distance, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            return true;
        return hit.collider.GetComponentInParent<PlayerMovement>() != null;
    }

    private void DrawLaser(Vector3 target, float charge, bool committed)
    {
        laser.enabled = true;
        laser.SetPosition(0, muzzle.position);
        laser.SetPosition(1, target);
        float width = committed ? 0.09f + Mathf.Sin(Time.time * 40f) * 0.02f : Mathf.Lerp(0.025f, 0.06f, charge);
        laser.startWidth = width;
        laser.endWidth = width * 0.7f;
        Color color = committed
            ? new Color(1f, 0.15f, 0.1f, 1f)
            : Color.Lerp(new Color(1f, 0.25f, 0.2f, 0.45f), new Color(1f, 0.02f, 0.02f, 1f), charge);
        laser.startColor = color;
        laser.endColor = color;
    }

    private void Fire()
    {
        Vector3 origin = muzzle.position;
        Vector3 direction = frozenAim - origin;
        if (direction.sqrMagnitude < 0.01f) direction = muzzle.forward;
        var shot = new GameObject("WaspShot");
        shot.transform.position = origin;
        shot.AddComponent<WaspShot>().Launch(direction.normalized * shotSpeed);
    }

    void OnDestroy()
    {
        if (laserMat != null) Destroy(laserMat);
    }
}
