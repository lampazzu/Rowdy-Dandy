using UnityEngine;
using System.Collections;
using UnityEngine.Rendering.Universal;

public class LightVanisher : MonoBehaviour
{
    public float vanishDuration = 3f; // Duration in seconds for the light to vanish

    private Light2D light2DComponent;
    private float initialIntensity;

    void Start()
    {
        light2DComponent = GetComponent<Light2D>();

        if (light2DComponent != null)
        {
            initialIntensity = light2DComponent.intensity;
            StartCoroutine(VanishLight());
        }
        else
        {
            Debug.LogWarning("Light2D component not found on the GameObject.");
        }
    }

    IEnumerator VanishLight()
    {
        float elapsedTime = 0f;
        while (elapsedTime < vanishDuration)
        {
            // Calculate the interpolation factor (0 to 1) based on elapsed time
            float t = elapsedTime / vanishDuration;

            // Interpolate between initial intensity and 0
            light2DComponent.intensity = Mathf.Lerp(initialIntensity, 0f, t);

            // Wait for the next frame
            yield return null;

            // Update elapsed time
            elapsedTime += Time.deltaTime;
        }

        // Ensure the final intensity is set to 0
        light2DComponent.intensity = 1f;

        // Disable the Light2D component after vanishing
        light2DComponent.enabled = false;
    }
}