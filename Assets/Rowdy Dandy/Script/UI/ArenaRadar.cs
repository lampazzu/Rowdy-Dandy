using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// COLOSSEUM RADAR: during a trial every enemy that's off screen shows on the nearest screen edge - its portrait on a
// red medallion (gold for elites) with an arrow pointing at it, like the cats' markers (CatVisibility). Closer ones
// are bigger and brighter. Only while a colosseum trial runs. Created automatically.
public class ArenaRadar : MonoBehaviour
{
    private class Marker
    {
        public RectTransform root;
        public Image plate, face, arrow;
        public EnemyHealth target;
    }

    private const int MaxMarkers = 16;
    private static ArenaRadar instance;
    private readonly List<Marker> markers = new List<Marker>();
    private readonly Dictionary<EnemyHealth, (Sprite face, bool elite, Renderer body)> info = new Dictionary<EnemyHealth, (Sprite, bool, Renderer)>();
    private readonly List<EnemyHealth> drop = new List<EnemyHealth>();

    private static readonly Color Red = new Color(0.55f, 0.08f, 0.12f, 0.9f);
    private static readonly Color Gold = new Color(0.65f, 0.45f, 0.08f, 0.9f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("ArenaRadar (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ArenaRadar>();
    }

    private void LateUpdate()
    {
        FrontierArena arena = FrontierArena.Current;
        Camera cam = Camera.main;
        int used = 0;
        if (arena != null && cam != null && !PauseMenu.IsPaused && OverlayUI.Root != null)
        {
            Vector2 size = OverlayUI.Root.rect.size;
            float t = Time.unscaledTime;
            foreach (EnemyHealth e in arena.Alive)
            {
                if (used >= MaxMarkers) break;
                if (e == null || e.enemydead || !e.gameObject.activeInHierarchy) continue;
                var data = Info(e);
                Vector3 world = data.body != null ? data.body.bounds.center : e.transform.position;
                Vector3 vp = cam.WorldToViewportPoint(world);
                if (vp.z <= 0f || (vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)) continue; // on screen

                Marker m = Get(used++);
                m.target = e;
                m.root.gameObject.SetActive(true);
                Vector2 clamped = new Vector2(Mathf.Clamp(vp.x, 0.035f, 0.965f), Mathf.Clamp(vp.y, 0.07f, 0.93f));
                Vector2 toward = new Vector2(vp.x - 0.5f, vp.y - 0.5f).normalized;
                float bob = Mathf.Sin(t * 7f + e.GetInstanceID()) * 3f;
                m.root.anchoredPosition = new Vector2((clamped.x - 0.5f) * size.x, (clamped.y - 0.5f) * size.y) - toward * bob;
                // closer = bigger and brighter (how far past the screen edge it is)
                float past = Mathf.Max(Mathf.Max(-vp.x, vp.x - 1f), Mathf.Max(-vp.y, vp.y - 1f));
                float near = 1f - Mathf.Clamp01(past / 1.2f);
                m.root.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, near);
                float a = Mathf.Lerp(0.55f, 1f, near);
                Color plate = data.elite ? Gold : Red;
                m.plate.color = new Color(plate.r, plate.g, plate.b, plate.a * a);
                m.face.sprite = data.face;
                m.face.enabled = data.face != null;
                m.face.color = new Color(1f, 1f, 1f, a);
                m.arrow.rectTransform.anchoredPosition = toward * 34f;
                m.arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg - 90f);
                float pulse = 0.8f + 0.2f * Mathf.Sin(t * 10f);
                m.arrow.color = data.elite ? new Color(1f, 0.85f, 0.3f, a * pulse) : new Color(1f, 0.35f, 0.35f, a * pulse);
            }
        }
        for (int i = used; i < markers.Count; i++)
            if (markers[i].root != null && markers[i].root.gameObject.activeSelf) markers[i].root.gameObject.SetActive(false);

        // forget enemies that are gone
        if (Time.frameCount % 120 == 0)
        {
            drop.Clear();
            foreach (var kv in info) if (kv.Key == null) drop.Add(kv.Key);
            foreach (EnemyHealth e in drop) info.Remove(e);
        }
    }

    private (Sprite face, bool elite, Renderer body) Info(EnemyHealth e)
    {
        if (info.TryGetValue(e, out var d)) return d;
        EnemyCatalog.Entry kind = EnemyCatalog.Identify(e);
        d = (kind != null ? kind.Portrait : null, e.GetComponent<EliteEnemy>() != null, e.GetComponent<Renderer>());
        info[e] = d;
        return d;
    }

    private Marker Get(int i)
    {
        while (markers.Count <= i) markers.Add(new Marker());
        Marker m = markers[i];
        if (m.root != null) return m;
        m.root = OverlayUI.MakeRect("Enemy Marker", OverlayUI.Root);
        m.root.anchorMin = m.root.anchorMax = m.root.pivot = new Vector2(0.5f, 0.5f);
        m.root.sizeDelta = new Vector2(52f, 52f);
        m.plate = OverlayUI.MakeImage("Plate", m.root, Red, BoonIcons.Medallion);
        Fill(m.plate.rectTransform, 52f);
        m.face = OverlayUI.MakeImage("Face", m.root, Color.white);
        m.face.preserveAspect = true;
        Fill(m.face.rectTransform, 36f);
        m.arrow = OverlayUI.MakeImage("Arrow", m.root, Color.white, CatVisibility.Arrow());
        Fill(m.arrow.rectTransform, 22f);
        return m;
    }

    private static void Fill(RectTransform r, float size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(size, size);
    }
}
