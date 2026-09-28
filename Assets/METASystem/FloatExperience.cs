using FloatingObjects;
using UnityEngine;

// One instance per player: the bag and history persist when moving between areas or respawning.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class FloatExperience : MonoBehaviour
{
    [SerializeField] private int seed = 928;
    private FloatRandom random;
    private PlayerMovement player;
    private float nextContact;
    private string message;
    private float messageUntil;
    private GUIStyle labelStyle;
    private static readonly int[] AllDirections = { 0, 1, 2, 3, 4, 5, 6, 7 };
    public FloatEffect[] RecentEffects => random.RecentEffects;

    void Awake()
    {
        random = new FloatRandom(seed);
        player = GetComponent<PlayerMovement>();
    }

    public bool TryApply(FloatArea area)
    {
        if (Time.time < nextContact) return false;
        nextContact = Time.time + Mathf.Max(.3f, area.Effects.contactInterval);
        FloatEffectSettings settings = area.Effects;
        FloatEffect effect = random.NextEffect();
        switch (effect)
        {
            case FloatEffect.VerticalLaunch:
            case FloatEffect.HorizontalLaunch:
                bool vertical = effect == FloatEffect.VerticalLaunch;
                int[] allowed = area.AllowedDirections(transform.position) ?? AllDirections;
                int sector = random.NextDirection(allowed);
                float angle = (sector * 45f + random.Range(-15, 15)) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                float speed = Sample(vertical ? settings.verticalHorizontalSpeed : settings.horizontalSpeed);
                float up = Sample(vertical ? settings.verticalUpSpeed : settings.horizontalUpSpeed);
                player.ApplyLaunch(direction * speed + Vector3.up * up);
                message = vertical ? "UPWARD LAUNCH" : "SIDEWAYS LAUNCH";
                break;
            case FloatEffect.LowGravity:
                player.ApplyGravityEffect(Sample(settings.lowGravityMultiplier), Sample(settings.lowGravityDuration));
                message = "LOW GRAVITY";
                break;
            case FloatEffect.Buoyancy:
                player.ApplyGravityEffect(-Sample(settings.buoyancyAcceleration) / player.BaseGravity,
                    Sample(settings.buoyancyDuration));
                message = "FLOATING UP";
                break;
        }
        messageUntil = Time.time + 1.5f;
        return true;
    }

    private float Sample(Vector2 range) => random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

    public void ClearFeedback()
    {
        nextContact = Time.time + .3f;
        message = null;
        messageUntil = 0;
    }

    void OnGUI()
    {
        if (Time.timeScale == 0) return;
        if (labelStyle == null)
            labelStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        float width = Mathf.Min(320, Screen.width - 20);
        float x = (Screen.width - width) * .5f;
        if (Time.time < messageUntil)
            GUI.Box(new Rect(x, 35, width, 34), message, labelStyle);
        if (player.GravityEffectRemaining > 0)
        {
            string effectName = player.GravityMultiplier < 0 ? "FLOATING UP" : "LOW GRAVITY";
            GUI.Box(new Rect(x, 73, width, 30), $"{effectName}  {player.GravityEffectRemaining:0.0}s", labelStyle);
        }
    }
}
