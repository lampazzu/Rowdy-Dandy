using System.Collections.Generic;
using UnityEngine;

// Water surface that reacts to bodies. The top edge is a row of nodes running a 1D wave equation, so a hit
// sends waves travelling out to both sides (water) instead of one point bouncing on a spring (rubber).
// Gentle idle waves roll on top. All distances are world units, so the object's scale doesn't change the feel.
// Pair with the Custom/Water2D_Advanced shader: it turns the surface into a pixel-stepped edge.
// Runs in edit mode too, only to build a still preview of the mesh so the water shader shows in the Scene view
// (the preview mesh is never saved; the waves only move in Play mode).
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
public class DynamicWater2D : MonoBehaviour
{
    [Header("Mesh Setup")]
    [Tooltip("Surface nodes per world unit. 8 = one node every 8 pixels at 64 PPU.")]
    [SerializeField] private float surfaceNodesPerUnit = 8f;
    [SerializeField] private float pixelsPerUnit = 64f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int orderInLayer = 0;

    [Header("Wave Physics (per physics step)")]
    [Tooltip("How fast waves travel along the surface.")]
    [Range(0.01f, 0.45f)] public float waveSpread = 0.22f;
    [Tooltip("How strongly the surface is pulled back flat. Low = heavy water, high = jelly.")]
    [Range(0f, 0.05f)] public float flattenStrength = 0.006f;
    [Tooltip("How fast waves die out.")]
    [Range(0f, 0.2f)] public float waveDamping = 0.02f;
    [Range(1, 4)] [SerializeField] private int substeps = 2;

    [Header("Wave Limits")]
    [SerializeField] private float maxWaveHeight = 0.6f;
    [SerializeField] private float maxNodeVelocity = 0.25f;

    [Header("Idle Waves")]
    [SerializeField] private float idleWaveHeight = 0.025f;
    [SerializeField] private float idleWaveLength = 3.5f;
    [SerializeField] private float idleWaveSpeed = 1.1f;

    [Header("Bodies On / In The Water")]
    [Tooltip("How far above the surface a body still counts as touching it (surfing on the tiles).")]
    [SerializeField] private float contactMargin = 0.25f;
    [Tooltip("Push from landing, per unit of falling speed.")]
    [SerializeField] private float landingForcePerSpeed = 0.014f;
    [Tooltip("Constant press under a body standing on the water (its weight).")]
    [SerializeField] private float weightForce = 0.0015f;
    [Tooltip("Press under a moving body, per unit of horizontal speed. This is the surf wake.")]
    [SerializeField] private float surfForcePerSpeed = 0.0022f;
    [Tooltip("Water heaped up in front of a moving body (bow wave), relative to the surf press.")]
    [Range(0f, 1.5f)] [SerializeField] private float bowWave = 0.6f;

    private static readonly List<DynamicWater2D> active = new List<DynamicWater2D>();

    private float[] heights;    // world-unit offset of each node from the rest surface
    private float[] velocities; // world units per physics step
    private float[] neighborSum;
    private int nodeCount;

    private MeshFilter meshFilter;
    private BoxCollider2D boxCollider;
    private Mesh waterMesh;
    private Vector3[] vertices;
    private Vector2[] uvs;
    private Vector2[] surfaceData;

    private readonly HashSet<Rigidbody2D> touching = new HashSet<Rigidbody2D>();
    private readonly HashSet<Rigidbody2D> touchingNow = new HashSet<Rigidbody2D>();
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private ContactFilter2D solidFilter;

    // =========================================================================
    // LOOKUP (used by WaterSplashFX / drowning)
    // =========================================================================

    // The water mesh whose surface is at or above this point (within 'margin' above the surface)
    public static DynamicWater2D FindAt(Vector2 point, float margin = 0.3f)
    {
        foreach (DynamicWater2D water in active)
        {
            if (water.Contains(point, margin)) return water;
        }
        return null;
    }

    public bool Contains(Vector2 point, float margin = 0f)
    {
        Bounds b = boxCollider.bounds;
        return point.x >= b.min.x && point.x <= b.max.x && point.y >= b.min.y && point.y <= b.max.y + margin;
    }

    // World Y of the moving surface at this X
    public float SurfaceY(float worldX)
    {
        if (!EnsureSetup()) return boxCollider.bounds.max.y;
        float f = NodeFloat(worldX);
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, nodeCount - 2);
        float h = Mathf.Lerp(heights[i], heights[i + 1], f - i);
        return boxCollider.bounds.max.y + h;
    }

    // =========================================================================

    private void Awake()
    {
        if (!Application.isPlaying) return; // edit mode: OnEnable builds the preview
        meshFilter = GetComponent<MeshFilter>();
        boxCollider = GetComponent<BoxCollider2D>();

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = orderInLayer;

        solidFilter = new ContactFilter2D();
        solidFilter.NoFilter();
        solidFilter.useTriggers = false;

        SetupWater();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) { BuildPreview(); return; }
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (!Application.isPlaying && waterMesh != null)
        {
            if (meshFilter != null && meshFilter.sharedMesh == waterMesh) meshFilter.sharedMesh = null;
            DestroyImmediate(waterMesh);
            waterMesh = null;
        }
    }

    // ---------------------------------------------------------------- edit mode preview
    private Vector3 previewKey;

    private void BuildPreview()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        meshFilter = GetComponent<MeshFilter>();
        if (boxCollider == null || meshFilter == null) return;
        if (waterMesh != null) DestroyImmediate(waterMesh);
        // same sorting as in Play (Awake sets it), so the preview stacks like the real thing
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null && (mr.sortingLayerName != sortingLayerName || mr.sortingOrder != orderInLayer)) { mr.sortingLayerName = sortingLayerName; mr.sortingOrder = orderInLayer; }
        SetupWater();
        previewKey = PreviewKey();
    }

    private Vector3 PreviewKey() => new Vector3(boxCollider.size.x * 1000f + boxCollider.offset.x, boxCollider.size.y * 1000f + boxCollider.offset.y, transform.lossyScale.x * 1000f + transform.lossyScale.y);

    public void SetupWater()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        float worldWidth = boxCollider.size.x * Mathf.Abs(transform.lossyScale.x);
        nodeCount = Mathf.Clamp(Mathf.CeilToInt(worldWidth * surfaceNodesPerUnit) + 1, 10, 2000);

        heights = new float[nodeCount];
        velocities = new float[nodeCount];
        neighborSum = new float[nodeCount];

        BuildMesh();
    }

    private void BuildMesh()
    {
        waterMesh = new Mesh { name = "Dynamic Water Mesh" };
        waterMesh.MarkDynamic();

        vertices = new Vector3[nodeCount * 2];
        uvs = new Vector2[nodeCount * 2];
        surfaceData = new Vector2[nodeCount * 2];
        var triangles = new int[(nodeCount - 1) * 6];

        UpdateMeshGeometry();

        int t = 0;
        for (int i = 0; i < nodeCount - 1; i++)
        {
            int topL = i * 2, botL = topL + 1, topR = topL + 2, botR = topL + 3;
            triangles[t++] = topL; triangles[t++] = topR; triangles[t++] = botL;
            triangles[t++] = botL; triangles[t++] = topR; triangles[t++] = botR;
        }
        waterMesh.triangles = triangles;

        if (Application.isPlaying) meshFilter.mesh = waterMesh;
        else
        {
            waterMesh.hideFlags = HideFlags.DontSave; // a preview: never written into the scene
            meshFilter.sharedMesh = waterMesh;
        }
    }

    private void UpdateMeshGeometry()
    {
        float leftX = boxCollider.offset.x - boxCollider.size.x / 2f;
        float rightX = boxCollider.offset.x + boxCollider.size.x / 2f;
        float topY = boxCollider.offset.y + boxCollider.size.y / 2f;
        float bottomY = boxCollider.offset.y - boxCollider.size.y / 2f;

        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        float time = Time.time * idleWaveSpeed;
        float k = idleWaveLength > 0.01f ? Mathf.PI * 2f / idleWaveLength : 0f;
        // The shader cuts the pixel-stepped edge itself, so the triangles reach a couple of pixels higher
        float pad = 2f / Mathf.Max(1f, pixelsPerUnit);

        for (int i = 0; i < nodeCount; i++)
        {
            float u = (float)i / (nodeCount - 1);
            float x = Mathf.Lerp(leftX, rightX, u);
            float worldX = transform.TransformPoint(new Vector3(x, 0f, 0f)).x;

            float idle = idleWaveHeight * (Mathf.Sin(worldX * k + time) + 0.5f * Mathf.Sin(worldX * k * 2.3f - time * 1.4f)) / 1.5f;
            float surfaceLocal = topY + (heights[i] + idle) / scaleY;

            vertices[i * 2] = new Vector3(x, surfaceLocal + pad / scaleY, 0f);
            vertices[i * 2 + 1] = new Vector3(x, bottomY, 0f);
            uvs[i * 2] = new Vector2(u, 1f);
            uvs[i * 2 + 1] = new Vector2(u, 0f);
            // y = how far this column is above/below rest (world units), for crest foam and trough shading
            surfaceData[i * 2] = new Vector2(surfaceLocal, heights[i] + idle);
            surfaceData[i * 2 + 1] = new Vector2(surfaceLocal, heights[i] + idle);
        }

        if (waterMesh != null)
        {
            waterMesh.vertices = vertices;
            waterMesh.uv = uvs;
            waterMesh.uv2 = surfaceData;
            waterMesh.RecalculateBounds();
        }
    }

    // Rebuilds the water if its runtime data was lost (e.g. scripts recompiled during Play mode)
    private bool EnsureSetup()
    {
        if (heights == null || vertices == null || heights.Length != nodeCount)
        {
            if (GetComponent<BoxCollider2D>() == null) return false;
            SetupWater();
        }
        return true;
    }

    private void FixedUpdate()
    {
        if (!EnsureSetup()) return;

        PushFromBodies();

        for (int s = 0; s < substeps; s++)
        {
            // Wave equation: each node is pulled toward its neighbours' average, so bumps travel outward
            for (int i = 0; i < nodeCount; i++)
            {
                float left = i > 0 ? heights[i - 1] : heights[i];
                float right = i < nodeCount - 1 ? heights[i + 1] : heights[i];
                neighborSum[i] = left + right - 2f * heights[i];
            }

            // Substeps only add stability; the wave speed stays the same
            float dt = 1f / substeps;
            for (int i = 0; i < nodeCount; i++)
            {
                float accel = waveSpread * neighborSum[i] - flattenStrength * heights[i] - waveDamping * velocities[i];
                velocities[i] = Mathf.Clamp(velocities[i] + accel * dt, -maxNodeVelocity, maxNodeVelocity);
                heights[i] += velocities[i] * dt;

                // Safety limit only; splashes are sized to stay well below it
                if (Mathf.Abs(heights[i]) > maxWaveHeight)
                {
                    heights[i] = Mathf.Sign(heights[i]) * maxWaveHeight;
                    velocities[i] *= 0.5f;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            // edit mode: rebuild the preview when the box is resized / moved
            if (boxCollider != null && waterMesh != null && PreviewKey() != previewKey) BuildPreview();
            return;
        }
        if (!EnsureSetup()) return;
        UpdateMeshGeometry();
    }

    private float NodeFloat(float worldX)
    {
        float localX = transform.InverseTransformPoint(new Vector3(worldX, 0f, 0f)).x;
        float leftX = boxCollider.offset.x - boxCollider.size.x / 2f;
        float u = Mathf.Clamp01((localX - leftX) / boxCollider.size.x);
        return u * (nodeCount - 1);
    }

    // Scene view: the water mesh only exists in Play mode, so the water's box is drawn here (see-through fill,
    // outer border, a brighter surface line on top). Editor only; selected water gets a stronger outline.
    private void OnDrawGizmos() => DrawOutline(false);
    private void OnDrawGizmosSelected() => DrawOutline(true);

    private void DrawOutline(bool selected)
    {
        BoxCollider2D box = boxCollider != null ? boxCollider : GetComponent<BoxCollider2D>();
        if (box == null) return;
        Vector2 min = box.offset - box.size * 0.5f, max = box.offset + box.size * 0.5f;
        Vector3 bl = transform.TransformPoint(new Vector3(min.x, min.y, 0f));
        Vector3 br = transform.TransformPoint(new Vector3(max.x, min.y, 0f));
        Vector3 tl = transform.TransformPoint(new Vector3(min.x, max.y, 0f));
        Vector3 tr = transform.TransformPoint(new Vector3(max.x, max.y, 0f));
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.identity;
        if (!Application.isPlaying)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, selected ? 0.22f : 0.12f);
            Gizmos.DrawCube((bl + tr) * 0.5f, new Vector3(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y), 0.01f));
        }
        Gizmos.color = new Color(0.35f, 0.75f, 1f, selected ? 1f : 0.7f);
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(bl, tl);
        Gizmos.DrawLine(br, tr);
        Gizmos.color = new Color(0.75f, 0.95f, 1f, 1f); // the surface
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tl + Vector3.down * 0.03f, tr + Vector3.down * 0.03f);
        Gizmos.matrix = old;
    }

    // force: world units per step pushed into the surface (negative = down). radius: world units.
    public void Splash(float worldX, float force, float radius = 0.35f)
    {
        if (!EnsureSetup()) return;

        float center = NodeFloat(worldX);
        float radiusNodes = Mathf.Max(1f, radius * surfaceNodesPerUnit);
        int from = Mathf.Max(0, Mathf.FloorToInt(center - radiusNodes));
        int to = Mathf.Min(nodeCount - 1, Mathf.CeilToInt(center + radiusNodes));

        for (int i = from; i <= to; i++)
        {
            float d = (i - center) / radiusNodes;
            float falloff = Mathf.Max(0f, 1f - d * d);
            falloff *= falloff;
            velocities[i] = Mathf.Clamp(velocities[i] + force * falloff, -maxNodeVelocity, maxNodeVelocity);
        }
    }

    // =========================================================================
    // BODIES ON / IN THE WATER
    // Scans the strip around the surface every step (Rowdy surfs on top of the solid water tiles,
    // so he's often just above the mesh, never inside a trigger).
    // =========================================================================

    private void PushFromBodies()
    {
        Bounds water = boxCollider.bounds;
        Vector2 center = new Vector2(water.center.x, (water.min.y + water.max.y + maxWaveHeight + contactMargin) * 0.5f);
        Vector2 size = new Vector2(water.size.x, water.size.y + maxWaveHeight + contactMargin);

        overlapResults.Clear();
        Physics2D.OverlapBox(center, size, 0f, solidFilter, overlapResults);

        touchingNow.Clear();
        foreach (Collider2D col in overlapResults)
        {
            Rigidbody2D rb = col.attachedRigidbody;
            if (rb == null || rb.bodyType == RigidbodyType2D.Static || !touchingNow.Add(rb)) continue;

            PushFromBody(rb, col.bounds);
        }

        touching.Clear();
        touching.UnionWith(touchingNow);
    }

    private void PushFromBody(Rigidbody2D rb, Bounds body)
    {
        float x = body.center.x;
        float surface = SurfaceY(x);

        if (body.min.y > surface + contactMargin) { touchingNow.Remove(rb); return; } // above the water
        if (body.max.y < surface - 0.1f) return; // fully under: doesn't press the surface

        Vector2 v = rb.linearVelocity;
        float halfWidth = body.extents.x;

        // Just touched down: impact
        if (!touching.Contains(rb) && v.y < -1f)
        {
            Splash(x, -Mathf.Min(-v.y * landingForcePerSpeed, 0.2f), halfWidth + 0.35f);
        }

        // Riding / wading: the body presses a trough that travels with it, leaving a wake behind
        float speed = Mathf.Abs(v.x);
        float press = Mathf.Min(weightForce + speed * surfForcePerSpeed, 0.035f);
        Splash(x, -press, halfWidth + 0.15f);

        // Bow wave: water heaps up in front
        if (speed > 1f && bowWave > 0f)
        {
            float front = x + Mathf.Sign(v.x) * (halfWidth + 0.3f);
            Splash(front, press * bowWave, 0.3f);
        }
    }
}
