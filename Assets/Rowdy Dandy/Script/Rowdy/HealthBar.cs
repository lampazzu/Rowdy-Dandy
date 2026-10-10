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

    private Image overheal, overheal2; // gold = the first layer, overheal2 = Satisfied's second layer above it
    private float shownOverheal, shownOverheal2;
    private static readonly Color SecondLayer = new Color(1f, 0.45f, 0.85f); // hot pink-violet over the gold

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
        float max = Mathf.Max(1f, playerHealth.BaseOverheal);
        float target = Mathf.Clamp01(playerHealth.Overheal / max);
        float target2 = Mathf.Clamp01((playerHealth.Overheal - max) / max);
        if (overheal2 == null && target2 > 0f) overheal2 = MakeLayer("Overheal Fill 2", 2, RampCopy(currentHealth.sprite, new Color(0.45f, 0.08f, 0.4f), SecondLayer, new Color(1f, 0.9f, 1f)));
        if (overheal2 != null)
        {
            shownOverheal2 = target2 > shownOverheal2 ? target2 : Mathf.MoveTowards(shownOverheal2, target2, Time.unscaledDeltaTime * 2f);
            overheal2.fillAmount = shownOverheal2;
            float s2 = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 11f);
            overheal2.color = new Color(1f, Mathf.Lerp(0.85f, 1f, s2), 1f, 1f);
            overheal2.enabled = shownOverheal2 > 0.001f;
        }
        if (overheal == null)
        {
            if (target <= 0f) return;
            overheal = MakeLayer("Overheal Fill", 1, GoldCopy(currentHealth.sprite)); // the fill art is green: tinting it gold gave a murky green
        }
        // jumps up, drains smoothly
        shownOverheal = target > shownOverheal ? target : Mathf.MoveTowards(shownOverheal, target, Time.unscaledDeltaTime * 2f);
        overheal.fillAmount = shownOverheal;
        float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
        overheal.color = new Color(1f, 1f, Mathf.Lerp(0.85f, 1f, shimmer), shownOverheal > 0f ? 1f : 0f);
        overheal.enabled = shownOverheal > 0.001f;
    }

    // A copy of the health fill on top of it (layer 1 = gold, 2 = the second overheal layer, above the gold)
    private Image MakeLayer(string name, int above, Sprite sprite)
    {
        {
            RectTransform src = currentHealth.rectTransform;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = currentHealth.gameObject.layer;
            RectTransform r = (RectTransform)go.transform;
            r.SetParent(src.parent, false);
            r.SetSiblingIndex(src.GetSiblingIndex() + 1);
            if (above == 1 && overheal2 != null) overheal2.rectTransform.SetSiblingIndex(r.GetSiblingIndex() + 1); // the 2nd layer stays on top
            if (above == 2 && overheal != null) r.SetSiblingIndex(overheal.rectTransform.GetSiblingIndex() + 1);
            r.anchorMin = src.anchorMin; r.anchorMax = src.anchorMax; r.pivot = src.pivot;
            r.anchoredPosition = src.anchoredPosition; r.sizeDelta = src.sizeDelta; r.localScale = src.localScale;
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Filled;
            img.fillMethod = currentHealth.fillMethod;
            img.fillOrigin = currentHealth.fillOrigin;
            img.preserveAspect = currentHealth.preserveAspect;
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }
    }

    private Sprite GoldCopy(Sprite source) => RampCopy(source, new Color(0.62f, 0.36f, 0.05f), overhealColor, new Color(1f, 0.97f, 0.7f));

    // Same pixels as the health fill, recoloured to a gold ramp by brightness (keeps the art's shading and shape).
    // Read through a RenderTexture so the source doesn't need Read/Write enabled.
    private Sprite RampCopy(Sprite source, Color dark, Color mid, Color bright)
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
