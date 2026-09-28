using UnityEngine;

[System.Serializable]
public class FloatEffectSettings
{
    public Vector2 verticalHorizontalSpeed = new Vector2(2, 4);
    public Vector2 verticalUpSpeed = new Vector2(8, 11);
    public Vector2 horizontalSpeed = new Vector2(6, 9);
    public Vector2 horizontalUpSpeed = new Vector2(4, 6);
    public Vector2 lowGravityMultiplier = new Vector2(.25f, .55f);
    public Vector2 lowGravityDuration = new Vector2(3, 5);
    public Vector2 buoyancyAcceleration = new Vector2(2, 4);
    public Vector2 buoyancyDuration = new Vector2(2, 3);
    [Min(.3f)] public float contactInterval = .3f;
}
