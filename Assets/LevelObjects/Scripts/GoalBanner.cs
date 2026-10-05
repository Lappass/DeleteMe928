using UnityEngine;
using UnityEngine.SceneManagement;

public class GoalBanner : MonoBehaviour
{
    [SerializeField] private float minCenterY;
    [SerializeField] private string sceneName = "LevelHub";
    [SerializeField] private float returnDelay = 0.7f;
    private bool cleared;
    private bool returning;
    private float clearedAt;
    private GUIStyle style;

    void OnTriggerEnter(Collider other) => TryClear(other);

    void OnTriggerStay(Collider other) => TryClear(other);

    void TryClear(Collider other)
    {
        if (cleared) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || player.transform.position.y < minCenterY) return;
        cleared = true;
        clearedAt = Time.time;
    }

    void Update()
    {
        float delay = returnDelay > 0.01f ? returnDelay : 0.7f;
        if (!cleared || returning || Time.time < clearedAt + delay) return;
        returning = true;
        SceneManager.LoadScene(string.IsNullOrEmpty(sceneName) ? "LevelHub" : sceneName);
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
