using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
public class DynamicWater2D : MonoBehaviour
{
    [Header("Mesh Setup")]
    [SerializeField] private int edgeCount = 60; // Number of surface points
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int orderInLayer = 0;

    [Header("Wave Physics")]
    [Range(0.001f, 0.1f)] public float stiffness = 0.025f;
    [Range(0.01f, 0.2f)] public float damping = 0.05f;
    [Range(0.01f, 0.2f)] public float spread = 0.06f;

    [Header("Wave Limits")]
    [SerializeField] private float maxWaveHeight = 0.35f;
    [SerializeField] private float maxVelocity = 2.0f;
    [SerializeField] private float splashForceMultiplier = 0.08f;

    [Header("Continuous Ripples")]
    [SerializeField] private float swimmingRippleForce = 0.03f;
    [SerializeField] private float rippleInterval = 0.12f;

    private struct WaterSpring
    {
        public float height;
        public float targetHeight;
        public float velocity;
    }

    private WaterSpring[] springs;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private BoxCollider2D boxCollider;
    private Mesh waterMesh;

    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uvs;

    // Reused every FixedUpdate to avoid allocating new arrays each physics step
    private float[] leftDeltas;
    private float[] rightDeltas;

    private float timer;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();

        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = orderInLayer;

        SetupWater();
    }

    public void SetupWater()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        edgeCount = Mathf.Max(10, edgeCount);
        springs = new WaterSpring[edgeCount];
        leftDeltas = new float[edgeCount];
        rightDeltas = new float[edgeCount];

        // Use local bounds matching the BoxCollider2D
        float topY = boxCollider.offset.y + (boxCollider.size.y / 2f);

        for (int i = 0; i < edgeCount; i++)
        {
            springs[i].height = topY;
            springs[i].targetHeight = topY;
            springs[i].velocity = 0f;
        }

        BuildMesh();
    }

    private void BuildMesh()
    {
        waterMesh = new Mesh();
        waterMesh.name = "Dynamic Water Mesh";

        int vertexCount = edgeCount * 2;
        vertices = new Vector3[vertexCount];
        uvs = new Vector2[vertexCount];
        triangles = new int[(edgeCount - 1) * 6];

        UpdateMeshGeometry();

        int triIndex = 0;
        for (int i = 0; i < edgeCount - 1; i++)
        {
            int topL = i * 2;
            int botL = topL + 1;
            int topR = topL + 2;
            int botR = topL + 3;

            triangles[triIndex++] = topL;
            triangles[triIndex++] = topR;
            triangles[triIndex++] = botL;

            triangles[triIndex++] = botL;
            triangles[triIndex++] = topR;
            triangles[triIndex++] = botR;
        }

        waterMesh.vertices = vertices;
        waterMesh.uv = uvs;
        waterMesh.triangles = triangles;

        meshFilter.mesh = waterMesh;
    }

    private void UpdateMeshGeometry()
    {
        float leftX = boxCollider.offset.x - (boxCollider.size.x / 2f);
        float rightX = boxCollider.offset.x + (boxCollider.size.x / 2f);
        float bottomY = boxCollider.offset.y - (boxCollider.size.y / 2f);

        for (int i = 0; i < edgeCount; i++)
        {
            float t = (float)i / (edgeCount - 1);
            float xPos = Mathf.Lerp(leftX, rightX, t);

            // Top surface vertex
            vertices[i * 2] = new Vector3(xPos, springs[i].height, 0f);
            uvs[i * 2] = new Vector2(t, 1f);

            // Bottom base vertex
            vertices[i * 2 + 1] = new Vector3(xPos, bottomY, 0f);
            uvs[i * 2 + 1] = new Vector2(t, 0f);
        }

        if (waterMesh != null)
        {
            waterMesh.vertices = vertices;
            waterMesh.uv = uvs;
            waterMesh.RecalculateBounds();
        }
    }

    // Rebuilds the water if its runtime data was lost (e.g. scripts recompiled during Play mode)
    // or edgeCount was changed in the Inspector while playing.
    private bool EnsureSetup()
    {
        if (springs == null || springs.Length != edgeCount || vertices == null || leftDeltas == null)
        {
            if (GetComponent<BoxCollider2D>() == null) return false;
            SetupWater();
        }
        return true;
    }

    private void FixedUpdate()
    {
        if (!EnsureSetup()) return;

        // 1. Update spring physics
        for (int i = 0; i < edgeCount; i++)
        {
            float x = springs[i].height - springs[i].targetHeight;
            float acceleration = -stiffness * x - damping * springs[i].velocity;

            springs[i].velocity += acceleration;
            springs[i].height += springs[i].velocity;

            // HARD CLAMP: Prevents vertices from stretching into giant walls or exploding
            springs[i].height = Mathf.Clamp(springs[i].height, springs[i].targetHeight - maxWaveHeight, springs[i].targetHeight + maxWaveHeight);
            springs[i].velocity = Mathf.Clamp(springs[i].velocity, -maxVelocity, maxVelocity);
        }

        // 2. Propagate waves across neighbor springs
        System.Array.Clear(leftDeltas, 0, edgeCount);
        System.Array.Clear(rightDeltas, 0, edgeCount);

        for (int i = 0; i < edgeCount; i++)
        {
            if (i > 0)
            {
                leftDeltas[i] = spread * (springs[i].height - springs[i - 1].height);
                springs[i - 1].velocity += leftDeltas[i];
            }
            if (i < edgeCount - 1)
            {
                rightDeltas[i] = spread * (springs[i].height - springs[i + 1].height);
                springs[i + 1].velocity += rightDeltas[i];
            }
        }

        for (int i = 0; i < edgeCount; i++)
        {
            if (i > 0) springs[i - 1].height += leftDeltas[i];
            if (i < edgeCount - 1) springs[i + 1].height += rightDeltas[i];
        }

        UpdateMeshGeometry();
    }

    public void Splash(float worldXPos, float force)
    {
        if (!EnsureSetup()) return;

        float localX = transform.InverseTransformPoint(new Vector3(worldXPos, 0, 0)).x;
        float leftX = boxCollider.offset.x - (boxCollider.size.x / 2f);
        float width = boxCollider.size.x;

        float normalizedX = Mathf.Clamp01((localX - leftX) / width);
        int index = Mathf.RoundToInt(normalizedX * (edgeCount - 1));

        if (index >= 0 && index < edgeCount)
        {
            springs[index].velocity = Mathf.Clamp(force, -maxVelocity, maxVelocity);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Rigidbody2D rb = collision.attachedRigidbody;
        if (rb != null)
        {
            // Clamp downward impact force so fast falling doesn't spike waves
            float force = Mathf.Clamp(rb.linearVelocity.y * splashForceMultiplier, -maxVelocity, 0f);
            Splash(collision.bounds.center.x, force);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        Rigidbody2D rb = collision.attachedRigidbody;
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            timer += Time.deltaTime;
            if (timer >= rippleInterval)
            {
                timer = 0f;
                // Generate soft, continuous swimming ripples
                float ripple = -swimmingRippleForce * (rb.linearVelocity.magnitude / 5f);
                Splash(collision.bounds.center.x, Mathf.Max(ripple, -0.15f));
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Rigidbody2D rb = collision.attachedRigidbody;
        if (rb != null)
        {
            // CRITICAL FIX: Leaving water applies a tiny downward wake instead of pulling mesh UP
            Splash(collision.bounds.center.x, -0.05f);
        }
    }
}