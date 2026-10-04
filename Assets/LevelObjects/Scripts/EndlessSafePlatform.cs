using UnityEngine;

/// <summary>Marks a landing surface that can be used as an endless level checkpoint.</summary>
public sealed class EndlessSafePlatform : MonoBehaviour
{
    public Vector3 RespawnPosition
    {
        get
        {
            Collider surface = GetComponent<Collider>();
            float top = surface != null ? surface.bounds.max.y : transform.position.y;
            return new Vector3(transform.position.x, top + 1.05f, transform.position.z);
        }
    }
    public Quaternion RespawnRotation => Quaternion.identity;

}
