using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneResetOnDeath : MonoBehaviour
{
    // This method is called when the script is enabled
    void OnEnable()
    {
        ResetScene();
    }

    void ResetScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }
}