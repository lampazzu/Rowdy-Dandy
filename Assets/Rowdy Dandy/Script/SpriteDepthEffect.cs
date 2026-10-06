using System.Collections.Generic;
using UnityEngine;

public class SpriteDepthEffect : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Only SpriteRenderers on this Sorting Layer will be affected.")]
    [SerializeField] private string targetSortingLayer = "Background";

    [Tooltip("Only sprites with Order in Layer equal to or below this value will be affected.")]
    [SerializeField] private int maximumOrderInLayer = 0;

    [Tooltip("If enabled, sprites below the target Sorting Layer will also be affected.")]
    [SerializeField] private bool affectLowerSortingLayers = false;

    [Header("Effects")]
    [SerializeField] private bool useDim = true;

    [Range(0f, 1f)]
    [SerializeField] private float dimAmount = 0.35f;

    [SerializeField] private bool useTint = true;

    [SerializeField] private Color tintColor = new Color(0.7f, 0.8f, 1f, 1f);

    [Range(0f, 1f)]
    [SerializeField] private float tintAmount = 0.25f;

    [SerializeField] private bool useOpacity = false;

    [Range(0f, 1f)]
    [SerializeField] private float opacity = 0.8f;

    [SerializeField] private bool useColorReduction = false;

    [Range(0f, 1f)]
    [SerializeField] private float colorReduction = 0.25f;

    [Header("Scene")]
    [Tooltip("Automatically finds sprites in the entire scene.")]
    [SerializeField] private bool searchEntireScene = true;

    [Tooltip("If disabled, the effect is only applied once.")]
    [SerializeField] private bool updateContinuously = true;

    [Tooltip("How often the scene is rescanned for new sprites.")]
    [SerializeField] private float updateInterval = 0.5f;

    private class SpriteData
    {
        public SpriteRenderer renderer;
        public Color originalColor;
    }

    private readonly List<SpriteData> affectedSprites =
        new List<SpriteData>();

    private float updateTimer;

    private void OnEnable()
    {
        RefreshSprites();
    }

    private void Update()
    {
        if (!updateContinuously)
            return;

        updateTimer += Time.deltaTime;

        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            RefreshSprites();
        }
    }

    private void OnDisable()
    {
        RestoreOriginals();
    }

    private void OnDestroy()
    {
        RestoreOriginals();
    }

    private void RefreshSprites()
    {
        RestoreOriginals();

        SpriteRenderer[] sprites;

        if (searchEntireScene)
        {
            sprites = FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        }
        else
        {
            sprites = GetComponentsInChildren<SpriteRenderer>(
                true
            );
        }

        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite == null)
                continue;

            if (!ShouldAffect(sprite))
                continue;

            SpriteData data = new SpriteData();

            data.renderer = sprite;
            data.originalColor = sprite.color;

            affectedSprites.Add(data);

            ApplyEffect(sprite, data.originalColor);
        }
    }

    private bool ShouldAffect(SpriteRenderer sprite)
    {
        int targetLayerID =
            SortingLayer.NameToID(targetSortingLayer);

        if (targetLayerID == 0)
        {
            Debug.LogWarning(
                "SpriteDepthEffect: Sorting Layer '" +
                targetSortingLayer +
                "' does not exist."
            );

            return false;
        }

        int targetLayerValue =
            SortingLayer.GetLayerValueFromID(
                targetLayerID
            );

        int spriteLayerValue =
            SortingLayer.GetLayerValueFromID(
                sprite.sortingLayerID
            );

        if (affectLowerSortingLayers)
        {
            if (spriteLayerValue > targetLayerValue)
                return false;
        }
        else
        {
            if (sprite.sortingLayerID != targetLayerID)
                return false;
        }

        if (sprite.sortingOrder > maximumOrderInLayer)
            return false;

        return true;
    }

    private void ApplyEffect(
        SpriteRenderer sprite,
        Color originalColor
    )
    {
        Color result = originalColor;

        // DIM
        if (useDim)
        {
            float multiplier =
                Mathf.Clamp01(1f - dimAmount);

            result.r *= multiplier;
            result.g *= multiplier;
            result.b *= multiplier;
        }

        // TINT
        if (useTint)
        {
            result.r = Mathf.Lerp(
                result.r,
                tintColor.r,
                tintAmount
            );

            result.g = Mathf.Lerp(
                result.g,
                tintColor.g,
                tintAmount
            );

            result.b = Mathf.Lerp(
                result.b,
                tintColor.b,
                tintAmount
            );
        }

        // COLOR REDUCTION
        if (useColorReduction)
        {
            float brightness =
                (result.r +
                 result.g +
                 result.b) / 3f;

            result.r = Mathf.Lerp(
                result.r,
                brightness,
                colorReduction
            );

            result.g = Mathf.Lerp(
                result.g,
                brightness,
                colorReduction
            );

            result.b = Mathf.Lerp(
                result.b,
                brightness,
                colorReduction
            );
        }

        // OPACITY
        if (useOpacity)
        {
            result.a *= opacity;
        }

        sprite.color = result;
    }

    private void RestoreOriginals()
    {
        foreach (SpriteData data in affectedSprites)
        {
            if (
                data != null &&
                data.renderer != null
            )
            {
                data.renderer.color =
                    data.originalColor;
            }
        }

        affectedSprites.Clear();
    }

    public void Refresh()
    {
        RefreshSprites();
    }
}