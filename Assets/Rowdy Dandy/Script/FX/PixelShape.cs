using UnityEngine;

// Pixel-exact shapes for effects (rings, light beams, cones, pools, glows) drawn by Resources/Shaders/PixelShape:
// the size you give is cut into the game's 64-per-unit pixels, so a big shockwave keeps a thin 3 px line instead of
// a small ring texture blown up into fat, stretched pixels. Set the size every frame you animate it.
//   SpriteRenderer r = PixelShape.Make("Ring", PixelShape.Kind.Ring, pos, order);
//   PixelShape.Size(r, 3f, 3f);                     // 3 x 3 units = 192 x 192 px
// Returns null when the shader isn't available (callers keep their old sprite then).
public static class PixelShape
{
    public enum Kind { Ring = 0, Beam = 1, Cone = 2, Pool = 3, Glow = 4, Shaft = 5 }

    private static Material material;
    private static bool tried;
    private static Sprite center, bottom, top;
    private static MaterialPropertyBlock block;
    private static readonly int PxId = Shader.PropertyToID("_Px"), KindId = Shader.PropertyToID("_Kind"), ThickId = Shader.PropertyToID("_Thick");

    public static bool Available => Material != null;

    private static Material Material
    {
        get
        {
            if (!tried)
            {
                tried = true;
                Shader s = Resources.Load<Shader>("Shaders/PixelShape");
                if (s != null && s.isSupported) material = new Material(s) { name = "PixelShape" };
            }
            return material;
        }
    }

    // 1 x 1 unit quad; pivot by shape: rings / pools / glows in the middle, beams stand on their bottom, cones hang from the top
    private static Sprite Quad(Kind k)
    {
        if (center == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "PixelShapeQuad" };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            center = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            bottom = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0f), 1f, 0, SpriteMeshType.FullRect);
            top = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 1f), 1f, 0, SpriteMeshType.FullRect);
        }
        return k == Kind.Beam || k == Kind.Shaft ? bottom : k == Kind.Cone ? top : center;
    }

    public static SpriteRenderer Make(string name, Kind kind, Vector3 at, int order, Transform parent = null, float thickPx = 3f)
    {
        if (Material == null) return null;
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Quad(kind);
        sr.sharedMaterial = Material;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = order;
        var tag = go.AddComponent<PixelShapeTag>();
        tag.kind = kind;
        tag.thick = thickPx;
        Size(sr, 0.25f, 0.25f);
        return sr;
    }

    // Size in world units (whole pixels). Works under a scaled / flipped parent.
    public static void Size(SpriteRenderer sr, float width, float height)
    {
        if (sr == null) return;
        int pw = Mathf.Max(1, Mathf.RoundToInt(width * 64f)), ph = Mathf.Max(1, Mathf.RoundToInt(height * 64f));
        Transform t = sr.transform;
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(pw / 64f / Mathf.Max(0.0001f, Mathf.Abs(parent.x)), ph / 64f / Mathf.Max(0.0001f, Mathf.Abs(parent.y)), 1f);
        PixelShapeTag tag = sr.GetComponent<PixelShapeTag>();
        if (block == null) block = new MaterialPropertyBlock();
        sr.GetPropertyBlock(block);
        block.SetVector(PxId, new Vector4(pw, ph, 0f, 0f));
        block.SetFloat(KindId, tag != null ? (float)tag.kind : 0f);
        block.SetFloat(ThickId, tag != null ? tag.thick : 3f);
        sr.SetPropertyBlock(block);
    }
}

// remembers what a PixelShape renderer draws
public class PixelShapeTag : MonoBehaviour
{
    public PixelShape.Kind kind;
    public float thick = 3f;
}
