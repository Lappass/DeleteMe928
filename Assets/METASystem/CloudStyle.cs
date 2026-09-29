using FloatingObjects;
using UnityEngine;

// One palette shared by the world, particles and HUD. Color always predicts effect.
public static class CloudStyle
{
    public static Color ColorFor(FloatEffect effect)
    {
        switch (effect)
        {
            case FloatEffect.VerticalLaunch: return new Color(1f, .69f, .64f);
            case FloatEffect.HorizontalLaunch: return new Color(.58f, .79f, 1f);
            case FloatEffect.LowGravity: return new Color(.78f, .67f, .97f);
            default: return new Color(.56f, .88f, .74f);
        }
    }

    public static string NameFor(FloatEffect effect)
    {
        switch (effect)
        {
            case FloatEffect.VerticalLaunch: return "PEACH / UPDRAFT";
            case FloatEffect.HorizontalLaunch: return "BLUE / SLINGSHOT";
            case FloatEffect.LowGravity: return "LILAC / FEATHER";
            default: return "MINT / LIFT";
        }
    }

    public static string DescriptionFor(FloatEffect effect)
    {
        switch (effect)
        {
            case FloatEffect.VerticalLaunch: return "Bounce high. Steer your exit.";
            case FloatEffect.HorizontalLaunch: return "Launch in your chosen direction.";
            case FloatEffect.LowGravity: return "Fall gently with lighter gravity.";
            default: return "Catch your fall, then float up.";
        }
    }

    public static string SymbolFor(FloatEffect effect)
    {
        switch (effect)
        {
            case FloatEffect.VerticalLaunch: return "UP";
            case FloatEffect.HorizontalLaunch: return ">>";
            case FloatEffect.LowGravity: return "~";
            default: return "+";
        }
    }
}
