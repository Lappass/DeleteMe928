using UnityEngine;

public class GoalBanner : MonoBehaviour
{
    [SerializeField] private float minCenterY;
    private bool cleared;
    private GUIStyle style;

    void OnTriggerStay(Collider other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.transform.position.y >= minCenterY) cleared = true;
    }

    void OnGUI()
    {
        if (!cleared || Time.timeScale == 0) return;
        if (style == null)
            style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        const float width = 280f;
        GUI.Box(new Rect((Screen.width - width) * .5f, 112f, width, 40f), "Clear", style);
    }
}
