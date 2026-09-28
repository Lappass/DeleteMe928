using UnityEngine;

public class Lava : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        //if the player touches the lava, send them back to where they started
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null) {
            player.ReturnToStart();
        }
    }
}
