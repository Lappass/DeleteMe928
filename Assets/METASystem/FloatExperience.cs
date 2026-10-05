using System.Collections.Generic;
using FloatingObjects;
using UnityEngine;

// Effect identity belongs to the cloud; collection history belongs to the player.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class FloatExperience : MonoBehaviour
{
    [SerializeField] private int seed = 928;
    private FloatRandom random;
    private PlayerMovement player;
    private float nextContact;
    private readonly Queue<FloatEffect> history = new Queue<FloatEffect>();
    public FloatEffect[] RecentEffects => history.ToArray();
    public FloatEffect LastEffect { get; private set; }
    public float MessageRemaining { get; private set; }
    public int CloudsThisLife { get; private set; }

    void Awake()
    {
        random = new FloatRandom(seed);
        player = GetComponent<PlayerMovement>();
    }

    void Update() => MessageRemaining = Mathf.Max(0, MessageRemaining - Time.deltaTime);

    public bool TryApply(Float source)
    {
        if (source == null || !source.IsAvailable || Time.time < nextContact) return false;
        FloatArea area = source.Area;
        nextContact = Time.time + Mathf.Max(.3f, area.Effects.contactInterval);
        FloatEffectSettings settings = area.Effects;
        FloatEffect effect = source.Effect;
        switch (effect)
        {
            case FloatEffect.VerticalLaunch:
            case FloatEffect.HorizontalLaunch:
                bool vertical = effect == FloatEffect.VerticalLaunch;
                // No hidden directional roll: the chosen input (or facing) wins.
                Vector3 direction = player.ChosenLaunchDirection;
                float speed = Sample(vertical ? settings.verticalHorizontalSpeed : settings.horizontalSpeed);
                float up = Sample(vertical ? settings.verticalUpSpeed : settings.horizontalUpSpeed);
                player.ApplyLaunch(direction * speed + Vector3.up * up);
                break;
            case FloatEffect.LowGravity:
                player.ApplyGravityEffect(Sample(settings.lowGravityMultiplier), Sample(settings.lowGravityDuration));
                break;
            case FloatEffect.Buoyancy:
                player.ApplyGravityEffect(-Sample(settings.buoyancyAcceleration) / player.BaseGravity,
                    Sample(settings.buoyancyDuration));
                break;
        }
        player.RefillAirDash();
        history.Enqueue(effect);
        if (history.Count > 4) history.Dequeue();
        LastEffect = effect;
        CloudsThisLife++;
        MessageRemaining = 2;
        return true;
    }

    private float Sample(Vector2 range) => random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

    public void ClearFeedback()
    {
        nextContact = Time.time + .3f;
        MessageRemaining = 0;
        CloudsThisLife = 0;
    }
}
