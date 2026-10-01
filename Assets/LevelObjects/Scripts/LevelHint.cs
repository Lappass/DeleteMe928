using UnityEngine;

public class LevelHint : MonoBehaviour
{
    [SerializeField] private string title;
    [TextArea(2, 4)]
    [SerializeField] private string tip;
    private GUIStyle style;

    void OnGUI()
    {
        if (Time.timeScale == 0) return;
        if (style == null)
            style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        float width = Mathf.Min(820f, Screen.width - 24f);
        float x = (Screen.width - width) * .5f;
        GUI.Box(new Rect(x, Screen.height - 108f, width, 92f), title + "\n" + tip, style);
    }
}
