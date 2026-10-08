using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Health playerHealth;

    [SerializeField] private Image totalHealth;

    [SerializeField] private Image currentHealth;

    [Header("Overheal (drawn over the health fill, gold, shimmering)")]
    [SerializeField] private Color overhealColor = new Color(1f, 0.82f, 0.25f, 1f);

    private Image overheal;
    private float shownOverheal;

    private void Start()
    {
        totalHealth.fillAmount = playerHealth.currentHealth / 100;
    }


    private void Update()
    {
        currentHealth.fillAmount = playerHealth.currentHealth / 100;
        UpdateOverheal();
    }

    // A copy of the fill on top of it: 0..100 extra HP = 0..100% of the bar in gold. Drains as the overheal decays.
    private void UpdateOverheal()
    {
        float max = Mathf.Max(1f, playerHealth.MaxOverheal);
        float target = Mathf.Clamp01(playerHealth.Overheal / max);
        if (overheal == null)
        {
            if (target <= 0f) return;
            RectTransform src = currentHealth.rectTransform;
            var go = new GameObject("Overheal Fill", typeof(RectTransform), typeof(Image));
            go.layer = currentHealth.gameObject.layer;
            RectTransform r = (RectTransform)go.transform;
            r.SetParent(src.parent, false);
            r.SetSiblingIndex(src.GetSiblingIndex() + 1);
            r.anchorMin = src.anchorMin; r.anchorMax = src.anchorMax; r.pivot = src.pivot;
            r.anchoredPosition = src.anchoredPosition; r.sizeDelta = src.sizeDelta; r.localScale = src.localScale;
            overheal = go.GetComponent<Image>();
            overheal.sprite = GoldCopy(currentHealth.sprite); // the fill art is green: tinting it gold gave a murky green
            overheal.type = Image.Type.Filled;
            overheal.fillMethod = currentHealth.fillMethod;
            overheal.fillOrigin = currentHealth.fillOrigin;
            overheal.preserveAspect = currentHealth.preserveAspect;
            overheal.raycastTarget = false;
        }
        // jumps up, drains smoothly
        shownOverheal = target > shownOverheal ? target : Mathf.MoveTowards(shownOverheal, target, Time.unscaledDeltaTime * 2f);
        overheal.fillAmount = shownOverheal;
        float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
        overheal.color = new Color(1f, 1f, Mathf.Lerp(0.85f, 1f, shimmer), shownOverheal > 0f ? 1f : 0f);
        overheal.enabled = shownOverheal > 0.001f;
    }

    // Same pixels as the health fill, recoloured to a gold ramp by brightness (keeps the art's shading and shape).
    // Read through a RenderTexture so the source doesn't need Read/Write enabled.
    private Sprite GoldCopy(Sprite source)
    {
        if (source == null) return null;
        Rect rect = source.textureRect;
        int w = Mathf.Max(1, (int)rect.width), h = Mathf.Max(1, (int)rect.height);
        Texture2D src = source.texture;
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = src.filterMode, wrapMode = TextureWrapMode.Clamp, name = "OverhealGold" };
        tex.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color dark = new Color(0.62f, 0.36f, 0.05f), mid = overhealColor, bright = new Color(1f, 0.97f, 0.7f);
        Color32[] px = tex.GetPixels32();
        float lo = 1f, hi = 0f;
        foreach (Color32 p in px) if (p.a > 0) { float l = (p.r * 0.3f + p.g * 0.59f + p.b * 0.11f) / 255f; lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); }
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a == 0) continue;
            float l = (px[i].r * 0.3f + px[i].g * 0.59f + px[i].b * 0.11f) / 255f;
            float k = hi > lo ? (l - lo) / (hi - lo) : 0.6f;
            Color c = k < 0.5f ? Color.Lerp(dark, mid, k * 2f) : Color.Lerp(mid, bright, (k - 0.5f) * 2f);
            px[i] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), px[i].a);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        Vector2 pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
        return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
    }
}
