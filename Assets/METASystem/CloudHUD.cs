using FloatingObjects;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement), typeof(FloatExperience))]
public class CloudHUD : MonoBehaviour
{
    private static readonly Color Ink = new Color(.16f, .22f, .29f);
    private static readonly Color Muted = new Color(.38f, .43f, .48f);
    private static readonly Color Paper = new Color(1f, .985f, .96f, .96f);
    private static readonly Color Alert = new Color(.70f, .25f, .20f);
    private PlayerMovement player;
    private FloatExperience experience;
    private CharacterController controller;
    private Font font;
    private Canvas canvas;
    private Text speed, vertical, clearance, surface, timer, effectName, effectTime, dash, hint, controlsText, toast;
    private CloudHUDShape effectBar;
    private RectTransform effectFill;
    private float nextRefresh;
    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    public float GroundDistance { get; private set; } = float.PositiveInfinity;
    public bool SurfaceIsHazard { get; private set; }
    public Canvas HudCanvas => canvas;

    void Start()
    {
        player = GetComponent<PlayerMovement>();
        experience = GetComponent<FloatExperience>();
        controller = GetComponent<CharacterController>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build();
        Refresh();
    }

    private void Build()
    {
        var root = new GameObject("Cloud HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;

        RectTransform legend = Panel("Cloud guide", new Vector2(0, 1), new Vector2(24, -24), new Vector2(310, 376));
        Label(legend, "Guide heading", "THE CLOUD GUIDE", 20, 16, 270, 24, 18, true);
        Label(legend, "Guide caption", "Choose a color. Know your next move.", 20, 43, 280, 24, 14);
        for (int i = 0; i < 4; i++)
        {
            FloatEffect effect = (FloatEffect)i;
            float y = 82 + i * 69;
            var icon = Shape(legend, "Cloud icon " + effect, 16, y + 3, 66, 46, CloudStyle.ColorFor(effect));
            icon.cloud = true;
            Label(legend, "Cloud symbol " + effect, CloudStyle.SymbolFor(effect), 16, y + 7, 66, 36, 13, true, TextAnchor.MiddleCenter);
            Label(legend, "Cloud name " + effect, CloudStyle.NameFor(effect), 90, y, 204, 23, 15, true);
            Label(legend, "Cloud description " + effect, CloudStyle.DescriptionFor(effect), 90, y + 24, 197, 36, 13);
        }

        RectTransform telemetry = Panel("Flight instruments", new Vector2(1, 1), new Vector2(-24, -24), new Vector2(264, 300));
        Label(telemetry, "Telemetry heading", "FLIGHT INSTRUMENTS", 20, 16, 230, 24, 17, true);
        speed = Label(telemetry, "Speed", "0.0", 20, 48, 150, 58, 44, true);
        Label(telemetry, "Speed unit", "m/s", 172, 70, 58, 28, 18);
        Label(telemetry, "Speed caption", "HORIZONTAL SPEED", 22, 108, 220, 20, 12);
        vertical = Label(telemetry, "Vertical speed", "VERTICAL  +0.0 m/s", 22, 132, 225, 25, 15);
        Shape(telemetry, "Divider", 20, 166, 224, 2, new Color(.83f, .85f, .85f));
        clearance = Label(telemetry, "Ground distance", "--", 20, 177, 225, 46, 32, true);
        Label(telemetry, "Distance caption", "BELOW YOUR FEET", 22, 222, 225, 20, 12);
        surface = Label(telemetry, "Surface below", "SCANNING...", 22, 253, 225, 28, 15, true);

        RectTransform title = Panel("Session", new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(340, 83));
        Label(title, "Title", "CLOUD HOP", 0, 9, 340, 32, 24, true, TextAnchor.MiddleCenter);
        timer = Label(title, "Survival time", "AIRTIME  00:00", 10, 45, 320, 24, 14, false, TextAnchor.MiddleCenter);

        RectTransform help = Panel("Controls", new Vector2(0, 0), new Vector2(24, 24), new Vector2(370, 132));
        Label(help, "Controls heading", "YOU SET THE DIRECTION", 20, 14, 335, 25, 17, true);
        controlsText = Label(help, "Control hints", "", 20, 45, 335, 76, 14);

        RectTransform status = Panel("Flight status", new Vector2(1, 0), new Vector2(-24, 24), new Vector2(310, 156));
        dash = Label(status, "Dash status", "DASH READY", 20, 12, 275, 24, 16, true);
        effectName = Label(status, "Active effect", "FREE FLIGHT", 20, 47, 275, 23, 16, true);
        effectTime = Label(status, "Effect duration", "Touch a cloud to refill your dash.", 20, 77, 275, 28, 13);
        Shape(status, "Effect track", 20, 119, 270, 7, new Color(.87f, .88f, .87f));
        effectBar = Shape(status, "Effect progress", 20, 119, 270, 7, CloudStyle.ColorFor(FloatEffect.Buoyancy));
        effectFill = effectBar.rectTransform;

        RectTransform guidance = Panel("Guidance", new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(450, 82));
        toast = Label(guidance, "Collection feedback", "Every cloud restores one air dash.", 16, 10, 418, 25, 16, true, TextAnchor.MiddleCenter);
        hint = Label(guidance, "Recovery hint", "Aim your move before touching a cloud.", 16, 42, 418, 23, 14, false, TextAnchor.MiddleCenter);
        RectTransform reticle = Rect(root.transform, "Aim", new Vector2(.5f, .5f), Vector2.zero, new Vector2(5, 5));
        var dot = reticle.gameObject.AddComponent<CloudHUDShape>();
        dot.color = new Color(1, 1, 1, .85f);
        dot.raycastTarget = false;
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .08f;
        Refresh();
    }

    public void Refresh()
    {
        if (player == null || canvas == null) return;
        ProbeGround();
        Vector3 velocity = player.ActualVelocity;
        speed.text = new Vector2(velocity.x, velocity.z).magnitude.ToString("0.0");
        vertical.text = $"VERTICAL  {velocity.y:+0.0;-0.0;0.0} m/s";
        clearance.text = float.IsPositiveInfinity(GroundDistance) ? "--" : $"{GroundDistance:0.0} m";
        surface.text = float.IsPositiveInfinity(GroundDistance) ? "NO SURFACE IN RANGE" : SurfaceIsHazard ? "LAVA BELOW" : "SOLID GROUND";
        surface.color = SurfaceIsHazard ? Alert : Muted;
        timer.text = $"ALIVE {Clock(player.LifeTime)}   BEST {Clock(player.BestLifeTime)}   CLOUDS {experience.CloudsThisLife}";
        dash.text = player.DashReady ? "DASH READY  /  1" : "DASH SPENT  /  0";
        dash.color = player.DashReady ? new Color(.15f, .43f, .32f) : Muted;
        bool pad = GetComponent<PlayerInput>()?.currentControlScheme == "Gamepad";
        controlsText.text = pad ? "Left stick  Steer    A  Jump / hold to glide\nL3  Dash    Y  Air brake\nNo stick input? Launch follows your view." :
            "WASD  Steer    SPACE  Jump / hold to glide\nSHIFT  Dash    E  Air brake\nNo move input? Launch follows your view.";
        float remaining = player.GravityEffectRemaining;
        if (remaining > 0)
        {
            FloatEffect effect = player.GravityMultiplier < 0 ? FloatEffect.Buoyancy : FloatEffect.LowGravity;
            effectName.text = CloudStyle.NameFor(effect);
            effectTime.text = $"{remaining:0.0}s remaining  /  steer while airborne";
            effectBar.color = CloudStyle.ColorFor(effect);
            effectFill.sizeDelta = new Vector2(270 * Mathf.Clamp01(remaining / Mathf.Max(.01f, player.GravityEffectDuration)), 7);
        }
        else
        {
            effectName.text = player.IsBraking ? "AIR BRAKE" : player.IsGliding ? "GLIDING" : player.IsWallRunning ? "WALL RUN" : player.IsGrounded ? "GROUNDED" : "FREE FLIGHT";
            effectTime.text = player.DashReady ? "Touch a cloud. Choose your next move." : "Land or touch a cloud to refill dash.";
            effectFill.sizeDelta = new Vector2(0, 7);
        }
        toast.text = experience.MessageRemaining > 0 ? CloudStyle.NameFor(experience.LastEffect) + "  /  DASH REFILLED" : "Every cloud restores one air dash.";
        bool danger = SurfaceIsHazard && GroundDistance < 6 && velocity.y < -.5f;
        hint.text = danger ? (pad ? "LAVA BELOW  /  Hold A to glide. L3 to dash." : "LAVA BELOW  /  Hold SPACE to glide. SHIFT to dash.") :
            player.IsGliding ? "Gliding. Steer toward a platform or cloud." : "Aim your move before touching a cloud.";
        hint.color = danger ? Alert : Muted;
    }

    private void ProbeGround()
    {
        GroundDistance = float.PositiveInfinity;
        SurfaceIsHazard = false;
        Vector3 feet = controller.bounds.center;
        feet.y = controller.bounds.min.y;
        int count = Physics.RaycastNonAlloc(feet + Vector3.up * .1f, Vector3.down, groundHits, 200, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i].collider;
            if (hit.transform.IsChildOf(transform) || groundHits[i].normal.y < .3f) continue;
            bool hazard = hit.GetComponentInParent<Lava>() != null;
            if (hit.isTrigger && !hazard) continue;
            float distance = Mathf.Max(0, groundHits[i].distance - .1f);
            if (distance >= GroundDistance) continue;
            GroundDistance = distance;
            SurfaceIsHazard = hazard;
        }
    }

    private static string Clock(float seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";

    private RectTransform Panel(string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform panel = Rect(canvas.transform, name, anchor, position, size);
        var shape = panel.gameObject.AddComponent<CloudHUDShape>();
        shape.color = Paper;
        shape.raycastTarget = false;
        return panel;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static CloudHUDShape Shape(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        RectTransform rect = Rect(parent, name, new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
        var shape = rect.gameObject.AddComponent<CloudHUDShape>();
        shape.color = color;
        shape.raycastTarget = false;
        return shape;
    }

    private Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size, bool bold = false, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        RectTransform rect = Rect(parent, name, new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.text = value;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.alignment = alignment;
        label.color = bold ? Ink : Muted;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }
}
