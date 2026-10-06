using UnityEngine;

public class FireflyHover : MonoBehaviour
{
    public float radius = 2f;      // How far it can drift from the starting point
    public float speed = 1f;       // Speed of the movement
    public float heightVariation = 0.5f; // How much it moves up/down
    private Vector3 startPos;
    private float offsetX, offsetY, offsetZ;

    void Start()
    {
        startPos = transform.position;
        offsetX = Random.Range(0f, 100f);
        offsetY = Random.Range(0f, 100f);
        offsetZ = Random.Range(0f, 100f);
    }

    void Update()
    {
        float x = Mathf.PerlinNoise(Time.time * speed + offsetX, 0) * 2 - 1;
        float y = Mathf.PerlinNoise(Time.time * speed + offsetY, 0) * 2 - 1;
        float z = Mathf.PerlinNoise(Time.time * speed + offsetZ, 0) * 2 - 1;

        Vector3 targetPos = startPos + new Vector3(x, y * heightVariation, z) * radius;
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * speed);
    }
}
