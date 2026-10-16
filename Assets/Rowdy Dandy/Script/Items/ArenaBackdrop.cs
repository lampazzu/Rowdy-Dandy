using System.Collections.Generic;
using UnityEngine;

// How the level's own hand-made parallax (the Parallax layers: A_Parallax, B_Parallax ... Fog_Parallax) looks while the
// camera is inside a runtime colosseum. One asset per colosseum, in Resources/ArenaBackdrops:
//   Gloomwood.asset    - THE GLOOMWOOD
//   PurpleReign.asset  - THE PURPLE REIGN
// Select one in the Project window and edit it in the Inspector: a tint for every layer, and per layer (matched by the
// layer object's name) its own tint, a different sprite, or hide it. Back outside the colosseum the layers return to
// exactly how they were. Changes show live in Play mode.
// Right-click the asset's header > "Fill Layers From Open Scene" lists every parallax layer of the open scene.
[CreateAssetMenu(menuName = "Rowdy Dandy/Arena Backdrop", fileName = "ArenaBackdrop")]
public class ArenaBackdrop : ScriptableObject
{
    [System.Serializable]
    public class Layer
    {
        [Tooltip("The parallax layer object's name (A_Parallax, Fog_Parallax...). Copies like 'F_Parallax (1)' count too.")]
        public string layerName;
        [Tooltip("Hide this layer inside the colosseum")]
        public bool hide;
        [Tooltip("Another sprite for this layer (empty = keep yours). Same width as the original keeps the loop seamless.")]
        public Sprite sprite;
        [Tooltip("On = this layer uses its own tint below instead of the one for every layer")]
        public bool overrideTint;
        public Color tint = Color.white;
    }

    [Tooltip("Tint for every parallax layer while the camera is in this colosseum (white = your art as it is)")]
    public Color tint = Color.white;
    [Tooltip("Per-layer changes (optional)")]
    public Layer[] layers = new Layer[0];

    public Layer Find(string name)
    {
        if (layers == null) return null;
        string key = Clean(name);
        foreach (Layer l in layers) if (l != null && Clean(l.layerName) == key) return l;
        return null;
    }

    // "F_Parallax (1)" and "'G_Parallax '" both match their plain name
    public static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        string s = name.Trim();
        int paren = s.LastIndexOf(" (", System.StringComparison.Ordinal);
        if (paren > 0 && s.EndsWith(")")) s = s.Substring(0, paren);
        return s.Trim();
    }

    public static ArenaBackdrop Load(string name) => Resources.Load<ArenaBackdrop>("ArenaBackdrops/" + name);

#if UNITY_EDITOR
    [ContextMenu("Fill Layers From Open Scene")]
    private void FillFromScene()
    {
        var names = new List<string>();
        foreach (Parallax p in Object.FindObjectsByType<Parallax>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = Clean(p.name);
            if (!names.Contains(n)) names.Add(n);
        }
        names.Sort();
        var list = new List<Layer>(layers ?? new Layer[0]);
        foreach (string n in names) if (Find(n) == null) list.Add(new Layer { layerName = n });
        layers = list.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log(name + ": " + names.Count + " parallax layers listed");
    }
#endif
}

// Swaps the parallax look per colosseum (GloomArena registers the areas). Each frame: which area is the camera in?
// On a change the layers get that area's look, or their own back.
public class ArenaBackdrops : MonoBehaviour
{
    private struct Area { public float minX, maxX; public ArenaBackdrop look; }
    private class Piece { public SpriteRenderer sr; public Sprite sprite; public Color color; public bool enabled; public string layer; }

    private static ArenaBackdrops instance;
    private readonly List<Area> areas = new List<Area>();
    private readonly List<Piece> pieces = new List<Piece>();
    private ArenaBackdrop applied;
    private bool collected;

    public static void Register(float minX, float maxX, ArenaBackdrop look)
    {
        if (look == null) return;
        if (instance == null) instance = new GameObject("Arena Backdrops").AddComponent<ArenaBackdrops>();
        instance.areas.Add(new Area { minX = minX, maxX = maxX, look = look });
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    // every sprite of every Parallax layer (the layer itself and its copies / children), as it was at the start
    private void Collect()
    {
        collected = true;
        foreach (Parallax p in FindObjectsByType<Parallax>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (SpriteRenderer sr in p.GetComponentsInChildren<SpriteRenderer>(true))
                if (!pieces.Exists(x => x.sr == sr))
                    pieces.Add(new Piece { sr = sr, sprite = sr.sprite, color = sr.color, enabled = sr.enabled, layer = p.name });
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (!collected) Collect();
        float x = cam.transform.position.x;
        ArenaBackdrop want = null;
        foreach (Area a in areas) if (x >= a.minX && x < a.maxX) { want = a.look; break; }
        if (want == applied && (want == null || !Application.isEditor)) return; // in the editor it re-applies every frame: Inspector edits show live
        applied = want;
        foreach (Piece p in pieces)
        {
            if (p.sr == null) continue;
            if (want == null) { p.sr.sprite = p.sprite; p.sr.color = p.color; p.sr.enabled = p.enabled; continue; }
            ArenaBackdrop.Layer l = want.Find(p.layer);
            Color tint = l != null && l.overrideTint ? l.tint : want.tint;
            p.sr.sprite = l != null && l.sprite != null ? l.sprite : p.sprite;
            p.sr.color = p.color * tint;
            p.sr.enabled = p.enabled && (l == null || !l.hide);
        }
    }
}
