using UnityEngine;

// White hit flash drawn over a sprite from code: a flat-white copy of the sprite on top of it for a few frames.
// Enemies normally flash white through their GetHit clip; hyper-armored ones (EnemyHealth.HyperArmor) don't play
// that clip (no flinch), so they flash through this instead and the hit still reads.
public class HitFlash : MonoBehaviour
{
    private SpriteRenderer source, overlay;
    private float until;
    private Color color = Color.white;

    public static void Flash(Component target, float seconds = 0.09f, Color? tint = null)
    {
        if (target == null) return;
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr == null) sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        HitFlash f = sr.GetComponent<HitFlash>();
        if (f == null)
        {
            f = sr.gameObject.AddComponent<HitFlash>();
            f.source = sr;
            var go = new GameObject("Hit Flash");
            go.transform.SetParent(sr.transform, false);
            f.overlay = go.AddComponent<SpriteRenderer>();
            if (CatFX.Silhouette != null) f.overlay.sharedMaterial = CatFX.Silhouette;
        }
        f.until = Time.time + seconds;
        f.color = tint ?? Color.white;
        f.LateUpdate();
    }

    private void LateUpdate()
    {
        if (overlay == null || source == null) return;
        bool on = Time.time < until && source.enabled && source.sprite != null;
        overlay.enabled = on;
        if (!on) return;
        overlay.sprite = source.sprite;
        overlay.flipX = source.flipX;
        overlay.flipY = source.flipY;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder + 1;
        overlay.color = color;
    }
}
