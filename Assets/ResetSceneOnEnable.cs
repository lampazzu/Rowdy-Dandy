using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetSceneOnEnable : MonoBehaviour
{
    private void OnEnable()
    {
        // Get the active scene
        Scene currentScene = SceneManager.GetActiveScene();

        // Reload the active scene
        SceneManager.LoadScene(currentScene.buildIndex);
    }
}