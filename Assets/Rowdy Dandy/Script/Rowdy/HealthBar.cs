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
            overheal.sprite = currentHealth.sprite;
            overheal.type = Image.Type.Filled;
            overheal.fillMethod = currentHealth.fillMethod;
            overheal.fillOrigin = currentHealth.fillOrigin;
            overheal.preserveAspect = currentHealth.preserveAspect;
            overheal.raycastTarget = false;
        }
        // jumps up, drains smoothly
        shownOverheal = target > shownOverheal ? target : Mathf.MoveTowards(shownOverheal, target, Time.unscaledDeltaTime * 2f);
        overheal.fillAmount = shownOverheal;
        float shimmer = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f);
        overheal.color = new Color(Mathf.Lerp(overhealColor.r, 1f, 1f - shimmer), Mathf.Lerp(overhealColor.g, 1f, 1f - shimmer), overhealColor.b, shownOverheal > 0f ? 0.95f : 0f);
        overheal.enabled = shownOverheal > 0.001f;
    }
}
