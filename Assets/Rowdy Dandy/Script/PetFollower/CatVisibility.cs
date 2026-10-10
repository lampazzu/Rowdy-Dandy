using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Makes it obvious what Rowdy's cats are doing:
//   - while a cat acts (attacking, executing, casting) it gets a bright outline in its colour, a ring when the power
//     goes off and a little sparkle trail
//   - a cat that's off screen while it acts shows up as a marker on the screen edge (its face + an arrow)
// The party leader keeps a faint gold outline all the time. Created automatically.
public class CatVisibility : MonoBehaviour
{
    private class Tracked
    {
        public SpriteOutline outline;
        public float lastPower = -10f;
        public float busy;          // 0..1 eased
        public float trailTimer;
        public RectTransform marker;
        public Image markerFace, markerArrow, markerPlate;
        public float markerAlpha;
    }

    private static CatVisibility instance;
    private readonly Dictionary<PetFollower, Tracked> tracked = new Dictionary<PetFollower, Tracked>();
    private readonly List<PetFollower> gone = new List<PetFollower>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("CatVisibility (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CatVisibility>();
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime, now = Time.time;
        Camera cam = Camera.main;

        gone.Clear();
        foreach (var pair in tracked) if (pair.Key == null) gone.Add(pair.Key);
        foreach (PetFollower p in gone) { Tracked t = tracked[p]; if (t.marker != null) Destroy(t.marker.gameObject); tracked.Remove(p); }

        foreach (PetFollower pet in PetFollower.Pets)
        {
            if (pet == null) continue;
            if (!tracked.TryGetValue(pet, out Tracked t))
            {
                t = new Tracked();
                tracked[pet] = t;
            }
            bool mine = pet.IsCollected;
            if (t.outline == null && mine && pet.TryGetComponent(out SpriteRenderer sr)) t.outline = SpriteOutline.Add(sr, Color.clear, 1, -1);

            bool busy = mine && pet.IsBusy && !PauseMenu.IsPaused;
            t.busy = Mathf.MoveTowards(t.busy, busy ? 1f : 0f, dt * (busy ? 10f : 3f));
            Color tint = Color.Lerp(pet.Tint, Color.white, 0.35f);

            // a power just went off: ring + flash
            if (mine && pet.LastPowerAt > t.lastPower + 0.01f)
            {
                t.lastPower = pet.LastPowerAt;
                PulseRing.Spawn(pet.transform.position, new Color(tint.r, tint.g, tint.b, 1f), 0.9f, 0.3f);
                FXParticle.Burst(pet.transform.position, tint, 8, 1f, 3f, 1f, 0.4f);
            }

            if (t.outline != null)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(now * 14f + pet.GetInstanceID());
                Color c = new Color(tint.r, tint.g, tint.b, t.busy * pulse);
                if (CatRoster.IsLeader(pet) && t.busy < 0.3f) c = new Color(1f, 0.85f, 0.35f, 0.35f + 0.15f * Mathf.Sin(now * 3f));
                if (!mine) c = Color.clear;
                t.outline.color = c;
            }

            if (t.busy > 0.5f)
            {
                t.trailTimer -= dt;
                if (t.trailTimer <= 0f)
                {
                    t.trailTimer = 0.05f;
                    FXParticle.Burst(pet.transform.position + (Vector3)(Random.insideUnitCircle * 0.12f), tint, 1, 0.1f, 0.4f, 0f, 0.35f);
                }
            }

            UpdateMarker(pet, t, cam, busy, tint);
        }
    }

    // A cat off screen while it acts: its face + an arrow on the nearest screen edge
    private void UpdateMarker(PetFollower pet, Tracked t, Camera cam, bool busy, Color tint)
    {
        bool offScreen = false;
        Vector3 vp = Vector3.zero;
        if (cam != null && pet.IsCollected)
        {
            vp = cam.WorldToViewportPoint(pet.transform.position);
            offScreen = vp.z > 0f && (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f);
        }
        bool show = offScreen && (busy || Time.time - pet.LastPowerAt < 1.5f) && !PauseMenu.IsPaused;
        t.markerAlpha = Mathf.MoveTowards(t.markerAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (t.markerAlpha <= 0f) { if (t.marker != null) t.marker.gameObject.SetActive(false); return; }

        if (t.marker == null) BuildMarker(pet, t);
        t.marker.gameObject.SetActive(true);

        // clamp to the edge, in canvas units
        RectTransform root = OverlayUI.Root;
        Vector2 size = root.rect.size;
        Vector2 clamped = new Vector2(Mathf.Clamp(vp.x, 0.04f, 0.96f), Mathf.Clamp(vp.y, 0.08f, 0.92f));
        Vector2 local = new Vector2((clamped.x - 0.5f) * size.x, (clamped.y - 0.5f) * size.y);
        float bob = Mathf.Sin(Time.unscaledTime * 8f) * 4f;
        Vector2 toward = new Vector2(vp.x - 0.5f, vp.y - 0.5f).normalized;
        t.marker.anchoredPosition = local - toward * bob;
        float angle = Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg;
        t.markerArrow.rectTransform.anchoredPosition = toward * 46f;
        t.markerArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 12f);
        t.markerPlate.color = new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, 0.85f * t.markerAlpha);
        t.markerFace.color = new Color(pet.FaceTint.r, pet.FaceTint.g, pet.FaceTint.b, t.markerAlpha);
        t.markerArrow.color = new Color(tint.r, tint.g, tint.b, t.markerAlpha * pulse);
        t.marker.localScale = Vector3.one * (0.9f + 0.1f * pulse);
    }

    private static void BuildMarker(PetFollower pet, Tracked t)
    {
        t.marker = OverlayUI.MakeRect("Cat Marker " + pet.CatName, OverlayUI.Root);
        t.marker.anchorMin = t.marker.anchorMax = t.marker.pivot = new Vector2(0.5f, 0.5f);
        t.marker.sizeDelta = new Vector2(72f, 72f);
        t.markerPlate = OverlayUI.MakeImage("Plate", t.marker, Color.white, BoonIcons.Medallion);
        Fill(t.markerPlate.rectTransform, 72f);
        t.markerFace = OverlayUI.MakeImage("Face", t.marker, Color.white, pet.Portrait);
        t.markerFace.preserveAspect = true;
        Fill(t.markerFace.rectTransform, 52f);
        t.markerArrow = OverlayUI.MakeImage("Arrow", t.marker, Color.white, Arrow());
        Fill(t.markerArrow.rectTransform, 30f);
    }

    private static void Fill(RectTransform r, float size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(size, size);
    }

    private static Sprite arrow;
    private static Sprite Arrow()
    {
        if (arrow != null) return arrow;
        arrow = BoonFX.FromRows("CatMarkerArrow", new[] {
            "...kk...",
            "..kwwk..",
            ".kwwwwk.",
            "kwwwwwwk",
            "kkkwwkkk",
            "..kwwk..",
            "..kwwk..",
            "..kkkk.." }, ch => ch == 'k' ? new Color32(27, 8, 32, 255) : new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f), 16f);
        return arrow;
    }
}
