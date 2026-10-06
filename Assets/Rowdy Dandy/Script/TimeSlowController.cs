using UnityEngine;

public class TimeSlowController : MonoBehaviour
{
    [Range(0f, 1f)]
    public float timeSlowValue = 1f; // 1 = Normal Speed, 0.5 = Half Speed, 0 = Frozen

    private void Update()
    {
        Time.timeScale = timeSlowValue;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // Adjust physics update rate
    }
}
