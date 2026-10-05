using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneDoor : MonoBehaviour
{
    [SerializeField] private string sceneName;
    private static bool loading;

    void OnEnable()
    {
        loading = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (loading || string.IsNullOrEmpty(sceneName)) return;
        if (other.GetComponent<PlayerMovement>() == null) return;
        loading = true;
        SceneManager.LoadScene(sceneName);
    }
}
