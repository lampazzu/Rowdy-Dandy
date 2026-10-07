using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FireflyHover : MonoBehaviour
{
    public float radius = 2f;      // How far it can drift from the starting point
    public float speed = 1f;       // Speed of the movement
    public float heightVariation = 0.5f; // How much it moves up/down

    [Header("Scatter (when Rowdy / enemies come close)")]
    public bool scatter = true;
    public float scatterRadius = 1.6f;      // how close something must get to startle it
    public float scatterDistance = 2.2f;    // how far it flees
    public float settleTime = 2.5f;         // how long it takes to drift back
    public float glowBoost = 1.5f;          // extra light while startled

    private Vector3 startPos;
    private float offsetX, offsetY, offsetZ;
    private Vector3 scatterOffset;
    private float excitement;
    private Light2D glow;
    private float baseIntensity;

    void Start()
    {
        startPos = transform.position;
        offsetX = Random.Range(0f, 100f);
        offsetY = Random.Range(0f, 100f);
        offsetZ = Random.Range(0f, 100f);

        glow = GetComponent<Light2D>();
        if (glow != null) baseIntensity = glow.intensity;
    }

    void Update()
    {
        if (scatter) UpdateScatter();

        float x = Mathf.PerlinNoise(Time.time * speed + offsetX, 0) * 2 - 1;
        float y = Mathf.PerlinNoise(Time.time * speed + offsetY, 0) * 2 - 1;
        float z = Mathf.PerlinNoise(Time.time * speed + offsetZ, 0) * 2 - 1;

        Vector3 targetPos = startPos + new Vector3(x, y * heightVariation, z) * radius + scatterOffset;
        float followSpeed = speed * (1f + excitement * 4f); // darts away fast, drifts back slowly
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
    }

    private void UpdateScatter()
    {
        float dt = Time.deltaTime;
        var bodies = VegetationInteractor.All;
        for (int i = 0; i < bodies.Count; i++)
        {
            if (bodies[i].IsSlash) continue;
            Vector3 away = transform.position - bodies[i].BodyBounds.center;
            away.z = 0f;
            float distance = away.magnitude;
            if (distance >= scatterRadius) continue;

            // Flee away and a bit upward, harder the closer it gets
            Vector3 direction = distance > 0.01f ? away / distance : Random.insideUnitCircle.normalized;
            direction = (direction + Vector3.up * 0.5f).normalized;
            float closeness = 1f - distance / scatterRadius;
            scatterOffset += direction * scatterDistance * closeness * dt * 6f;
            excitement = 1f;
        }

        scatterOffset = Vector3.ClampMagnitude(scatterOffset, scatterDistance);
        scatterOffset = Vector3.Lerp(scatterOffset, Vector3.zero, dt / Mathf.Max(0.1f, settleTime));
        excitement = Mathf.Max(0f, excitement - dt / Mathf.Max(0.1f, settleTime));

        if (glow != null) glow.intensity = baseIntensity * (1f + excitement * glowBoost);
    }
}
