using System.Collections.Generic;
using UnityEngine;

/// <summary>Streams procedural sections beyond PlatformerLevel's authored opening.</summary>
public sealed class EndlessWorld : MonoBehaviour
{
    public static EndlessWorld Instance { get; private set; }
    [SerializeField] private Transform authoredPlatforms;
    [SerializeField] private Material platformMaterial;
    [SerializeField] private Material lavaMaterial;
    [SerializeField] private Material cloudMaterial;
    [SerializeField] private int seed = 928;
    [SerializeField, Min(16)] private float segmentLength = 32f;
    [SerializeField, Min(1)] private int segmentsAhead = 3;
    [SerializeField, Min(20)] private float floorWidth = 44f;
    [SerializeField, Min(1)] private float difficultyRampDistance = 1200f;
    [SerializeField, Range(0, 1)] private float maximumDifficulty = .8f;
    [SerializeField, Range(4, 10)] private float maximumLavaRadius = 8f;
    [Header("Safe landing cloud lift")]
    [SerializeField, Range(.2f, 2.8f)] private float landingAssistDelay = 1.35f;
    [SerializeField, Range(1f, 3f)] private float landingAssistWindow = 3f;
    [SerializeField, Min(8)] private float landingAssistCloudRange = 34f;
    [SerializeField] private Vector3 firstSegmentStart = new Vector3(18.65f, 6.4f, 51.65f);
    private readonly SortedDictionary<int, EndlessSegment> segments = new SortedDictionary<int, EndlessSegment>();
    private PlayerMovement player;
    private Vector3 checkpointPosition;
    private Quaternion checkpointRotation = Quaternion.identity;
    private int checkpointSegment = -1;
    private float originZ;
    private float farthest;
    private float nextBestSave;
    private bool landingAssistPending;
    private float landingAssistReadyAt;
    private float landingAssistExpiresAt;
    private string bestKey = "CloudHop.PlatformerEndlessBest";
    public float CurrentDistance => player == null ? 0 : Mathf.Max(0, player.transform.position.z - originZ);
    public float BestDistance => Mathf.Max(farthest, PlayerPrefs.GetFloat(bestKey, 0));
    public bool LandingAssistPending => landingAssistPending;
    public float LandingAssistCountdown => landingAssistPending ? Mathf.Max(0, landingAssistReadyAt - Time.time) : 0;

    private void Awake()
    {
        Instance = this;
        originZ = 0;
    }

    private void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        if (player == null || authoredPlatforms == null || platformMaterial == null || lavaMaterial == null)
        {
            Debug.LogError("EndlessWorld requires the player, authored platforms, and terrain materials.", this);
            enabled = false;
            return;
        }
        checkpointPosition = player.transform.position;
        // Existing authored route surfaces become checkpoints as the player uses them.
        foreach (Collider col in authoredPlatforms.GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger) continue;
            var marker = col.GetComponent<EndlessSafePlatform>();
            if (marker == null) marker = col.gameObject.AddComponent<EndlessSafePlatform>();
        }
        GameObject goal = GameObject.Find("Goal");
        if (goal != null && goal.GetComponent<Collider>() != null && goal.GetComponent<EndlessSafePlatform>() == null)
            goal.AddComponent<EndlessSafePlatform>();
        CreateSafeOpeningFloor();
        EnsureSegments();
        SetOldLavaInactive();
    }

    private void Update()
    {
        if (player == null) return;
        farthest = Mathf.Max(farthest, CurrentDistance);
        float saved = PlayerPrefs.GetFloat(bestKey, 0);
        if (farthest > saved + .5f)
        {
            PlayerPrefs.SetFloat(bestKey, farthest);
            if (Time.unscaledTime >= nextBestSave)
            {
                PlayerPrefs.Save();
                nextBestSave = Time.unscaledTime + 5f;
            }
        }
        EnsureSegments();
        RecycleBehind();
        UpdateLandingAssist();
    }

    private void EnsureSegments()
    {
        float z = player != null ? player.transform.position.z : firstSegmentStart.z;
        int desired = Mathf.Max(0, Mathf.FloorToInt((z - firstSegmentStart.z) / segmentLength));
        int last = desired + segmentsAhead - 1;
        int firstNeeded = Mathf.Max(0, desired - 1);
        for (int index = firstNeeded; index <= last; index++)
        {
            if (segments.ContainsKey(index)) continue;
            Vector3 start = new Vector3(firstSegmentStart.x, firstSegmentStart.y,
                firstSegmentStart.z + index * segmentLength);
            var go = new GameObject($"Endless Segment {index + 1}");
            go.transform.SetParent(transform, true);
            EndlessSegment segment = go.AddComponent<EndlessSegment>();
            segment.ConfigureHoleRadius(maximumLavaRadius);
            float difficulty = maximumDifficulty * Mathf.Clamp01(index * segmentLength / Mathf.Max(1, difficultyRampDistance));
            segment.Build(index, start.z, start.x, start.y, segmentLength, floorWidth, seed, difficulty,
                firstSegmentStart.x, platformMaterial, lavaMaterial, cloudMaterial);
            segments.Add(index, segment);
        }
    }

    private void RecycleBehind()
    {
        int current = Mathf.Max(0, Mathf.FloorToInt((player.transform.position.z - firstSegmentStart.z) / segmentLength));
        var remove = new List<int>();
        foreach (var pair in segments)
            if ((pair.Key < current - 1 || pair.Key > current + segmentsAhead - 1) && pair.Key != checkpointSegment)
                remove.Add(pair.Key);
        foreach (int key in remove)
        {
            EndlessSegment old = segments[key];
            segments.Remove(key);
            Destroy(old.gameObject);
        }
    }

    private void CreateSafeOpeningFloor()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Safe ground under opening route";
        floor.transform.SetParent(transform, true);
        floor.transform.position = new Vector3(10, -3.15f, 23.3f);
        floor.transform.localScale = new Vector3(48, .3f, 59f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = platformMaterial;
    }

    private void SetOldLavaInactive()
    {
        GameObject oldLava = GameObject.Find("Lava");
        if (oldLava != null) oldLava.SetActive(false);
    }

    public void RegisterSafeSurface(Collider surface)
    {
        if (surface == null || player == null || !player.IsGrounded) return;
        EndlessSafePlatform marker = surface.GetComponent<EndlessSafePlatform>();
        if (marker == null) return;
        checkpointPosition = marker.RespawnPosition;
        checkpointRotation = marker.RespawnRotation;
        EndlessSegment segment = marker.GetComponentInParent<EndlessSegment>();
        checkpointSegment = segment != null ? segment.SegmentIndex : -1;
    }

    public void Respawn(PlayerMovement target)
    {
        if (target == null) return;
        CancelLandingAssist();
        target.RespawnAtCheckpoint(checkpointPosition, checkpointRotation);
    }

    public void CancelLandingAssist() => landingAssistPending = false;

    public void NotifySafeLanding(Collider surface)
    {
        if (surface == null || surface.isTrigger || surface.GetComponentInParent<Lava>() != null ||
            player == null || !player.IsGrounded) return;
        landingAssistPending = true;
        landingAssistReadyAt = Time.time + Mathf.Min(landingAssistDelay, landingAssistWindow - .1f);
        landingAssistExpiresAt = Time.time + landingAssistWindow;
    }

    private void UpdateLandingAssist()
    {
        if (!landingAssistPending) return;
        if (!player.IsGrounded || Time.time > landingAssistExpiresAt)
        {
            landingAssistPending = false;
            return;
        }
        if (Time.time < landingAssistReadyAt || !TryFindCloudLaunch(out Vector3 launch)) return;
        landingAssistPending = false;
        player.ApplyLaunch(launch);
    }

    private bool TryFindCloudLaunch(out Vector3 launch)
    {
        launch = Vector3.zero;
        Vector3 origin = player.transform.position;
        Vector3 facing = player.ChosenLaunchDirection;
        float speedLimit = Mathf.Min(36f, player.ExternalSpeedLimit * .95f);
        float bestScore = float.PositiveInfinity;
        foreach (Float cloud in FindObjectsByType<Float>())
        {
            if (!cloud.IsAvailable) continue;
            Vector3 delta = cloud.Anchor - origin;
            Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
            float distance = horizontal.magnitude;
            if (distance > landingAssistCloudRange || delta.y < -2f || delta.y > 19f || delta.z < -8f) continue;
            if (Physics.Linecast(origin + Vector3.up, cloud.Anchor, ~0, QueryTriggerInteraction.Ignore)) continue;
            Vector3 direction = distance > .01f ? horizontal / distance : Vector3.zero;
            for (float flightTime = .65f; flightTime <= 1.46f; flightTime += .2f)
            {
                float effectTime = Mathf.Min(flightTime, player.GravityEffectRemaining);
                float gravity = player.BaseGravity;
                float fallDistance = gravity * player.GravityMultiplier *
                    (effectTime * flightTime - .5f * effectTime * effectTime) +
                    .5f * gravity * (flightTime - effectTime) * (flightTime - effectTime);
                float upwardSpeed = (delta.y + fallDistance) / flightTime;
                if (upwardSpeed < 4f) continue;
                float drag = player.ExternalHorizontalDeceleration;
                float horizontalSpeed = distance >= .5f * drag * flightTime * flightTime
                    ? distance / flightTime + .5f * drag * flightTime
                    : Mathf.Sqrt(2f * drag * distance);
                Vector3 candidate = direction * horizontalSpeed + Vector3.up * upwardSpeed;
                if (candidate.magnitude > speedLimit) continue;
                float score = distance + Mathf.Max(0, -delta.z) * 1.5f +
                    (1f - Vector3.Dot(direction, facing)) * 4f +
                    candidate.magnitude * .03f + Mathf.Abs(flightTime - 1.05f);
                if (score >= bestScore) continue;
                bestScore = score;
                launch = candidate;
            }
        }
        return !float.IsPositiveInfinity(bestScore);
    }

    private void OnDestroy()
    {
        if (farthest > 0) PlayerPrefs.Save();
        if (Instance == this) Instance = null;
    }
}
