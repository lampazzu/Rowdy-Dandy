using UnityEngine;

// Fog that drifts slowly back and forth, a whole pixel at a time
public class FogDrift : MonoBehaviour
{
    private Vector3 home;
    private float phase;
    private void Start() { home = transform.position; phase = Random.Range(0f, 10f); }
    private void Update()
    {
        float x = home.x + Mathf.Sin(Time.time * 0.15f + phase) * 3f;
        transform.position = new Vector3(Mathf.Round(x * 64f) / 64f, home.y, 0f);
    }
}
