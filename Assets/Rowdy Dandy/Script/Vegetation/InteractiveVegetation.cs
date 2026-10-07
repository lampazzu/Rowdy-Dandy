using System.Collections.Generic;
using UnityEngine;

// Makes a vegetation sprite sway in the wind and react to Rowdy / enemies:
//  - Grass / bushes / flowers: bend away when walked through at ground level, spring back with a wobble,
//    throw a few leaves when rushed through, landed in or slashed, and shed petals under Rowdy's feet as he runs
//  - Hanging vines: sway from the top, swing when brushed
//  - Trees: breeze, shake and drop leaves from the canopy when bumped at the trunk or slashed
// Interaction uses the sprite's VISIBLE pixels (measured by the setup tool), not its whole rectangle,
// so empty space around a sprite never reacts.
// Needs the "Rowdy Dandy/2D/Sprite-Lit-Vegetation" material (Tools > Rowdy Dandy > Scene Organizer sets this up).
[RequireComponent(typeof(SpriteRenderer))]
public class InteractiveVegetation : MonoBehaviour
{
    public enum Mode { Grass, Hanging, Tree }

    [SerializeField] private Mode mode = Mode.Grass;
    [Tooltip("How much this plant moves in the wind.")]
    [SerializeField] private float windScale = 1f;

    [Header("Interaction")]
    [Tooltip("Max bend when pushed, as a fraction of the plant's height.")]
    [SerializeField] private float pushStrength = 0.35f;
    [SerializeField] private float stiffness = 45f;
    [SerializeField] private float damping = 5f;

    [Header("Leaves")]
    [SerializeField] private bool rustle = true;
    [SerializeField] private Color leafColorA = new Color(0.9f, 0.3f, 0.5f, 1f);
    [SerializeField] private Color leafColorB = new Color(0.6f, 0.15f, 0.35f, 1f);
    [Tooltip("Horizontal speed needed to throw leaves.")]
    [SerializeField] private float rustleSpeed = 3.5f;

    [Header("Visible Area (set by the setup tool)")]
    [Tooltip("Visible pixels of the sprite, in the sprite's local space. Zero = use the whole sprite.")]
    [SerializeField] private Vector2 visibleMin;
    [SerializeField] private Vector2 visibleMax;

    [Tooltip("The material this sprite had before; used by the setup tool's Remove button.")]
    [SerializeField] private Material originalMaterial;
    public Material OriginalMaterial { get => originalMaterial; set => originalMaterial = value; }

    private static readonly int BaseYId = Shader.PropertyToID("_VegBaseY");
    private static readonly int HeightId = Shader.PropertyToID("_VegHeight");
    private static readonly int BendId = Shader.PropertyToID("_VegBend");
    private static readonly int HangingId = Shader.PropertyToID("_VegHanging");
    private static readonly int WindScaleId = Shader.PropertyToID("_VegWindScale");
    private static readonly int PhaseId = Shader.PropertyToID("_VegPhase");

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock block;
    private float baseY, height, minX, maxX;
    private float bend, bendVelocity, writtenBend;
    private float rustleCooldown, footstepCooldown;
    private readonly HashSet<VegetationInteractor> inside = new HashSet<VegetationInteractor>();

    public Mode PlantMode => mode;
    public bool IsVisible => spriteRenderer != null && spriteRenderer.isVisible;
    public Color LeafColorA => leafColorA;
    public Color LeafColorB => leafColorB;
    public float BaseY => baseY;
    public float Height => height;
    public float MinX => minX;
    public float MaxX => maxX;

    // Used by the editor setup tool
    public void Configure(Mode newMode, float newWindScale, float newPushStrength, Color colorA, Color colorB, bool newRustle, Vector2 localVisibleMin, Vector2 localVisibleMax)
    {
        mode = newMode;
        windScale = newWindScale;
        pushStrength = newPushStrength;
        leafColorA = colorA;
        leafColorB = colorB;
        rustle = newRustle;
        visibleMin = localVisibleMin;
        visibleMax = localVisibleMax;
    }

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (block == null) block = new MaterialPropertyBlock();

        // Plants don't move, so measure once
        Bounds visible = GetVisibleWorldBounds();
        baseY = visible.min.y;
        height = Mathf.Max(0.01f, visible.size.y);
        minX = visible.min.x;
        maxX = visible.max.x;

        bend = bendVelocity = 0f;
        spriteRenderer.GetPropertyBlock(block);
        block.SetFloat(BaseYId, baseY);
        block.SetFloat(HeightId, height);
        block.SetFloat(HangingId, mode == Mode.Hanging ? 1f : 0f);
        block.SetFloat(WindScaleId, windScale);
        block.SetFloat(PhaseId, Random.value * Mathf.PI * 2f);
        block.SetFloat(BendId, 0f);
        spriteRenderer.SetPropertyBlock(block);
        writtenBend = 0f;

        VegetationSystem system = VegetationSystem.Instance;
        if (system != null) system.Register(this);
    }

    private void OnDisable()
    {
        inside.Clear();
        if (VegetationSystem.HasInstance) VegetationSystem.Instance.Unregister(this);
    }

    // World-space box around the visible pixels (falls back to the whole sprite)
    private Bounds GetVisibleWorldBounds()
    {
        if (visibleMax.x <= visibleMin.x || visibleMax.y <= visibleMin.y) return spriteRenderer.bounds;

        Vector2 min = visibleMin, max = visibleMax;
        if (spriteRenderer.flipX) { float a = -max.x; max.x = -min.x; min.x = a; }
        if (spriteRenderer.flipY) { float a = -max.y; max.y = -min.y; min.y = a; }

        var b = new Bounds(transform.TransformPoint(min), Vector3.zero);
        b.Encapsulate(transform.TransformPoint(new Vector3(max.x, min.y)));
        b.Encapsulate(transform.TransformPoint(new Vector3(min.x, max.y)));
        b.Encapsulate(transform.TransformPoint(max));
        return b;
    }

    // Called every frame by VegetationSystem
    public void Tick(float dt, List<VegetationInteractor> interactors, VegetationSystem system)
    {
        bool settled = Mathf.Abs(bend) < 0.0005f && Mathf.Abs(bendVelocity) < 0.0005f;
        if (settled && inside.Count == 0 && !spriteRenderer.isVisible) return;

        if (rustleCooldown > 0f) rustleCooldown -= dt;
        if (footstepCooldown > 0f) footstepCooldown -= dt;

        float target = 0f;
        float centerX = (minX + maxX) * 0.5f;
        float halfWidth = (maxX - minX) * 0.5f;

        for (int i = 0; i < interactors.Count; i++)
        {
            VegetationInteractor body = interactors[i];
            if (!body.IsActive || !Touches(body, centerX, halfWidth))
            {
                inside.Remove(body);
                continue;
            }

            Bounds b = body.BodyBounds;
            Vector2 velocity = body.Velocity;
            bool justEntered = inside.Add(body);
            float dx = b.center.x - centerX;

            if (body.IsSlash)
            {
                if (justEntered) Slashed(system, dx, halfWidth);
                continue;
            }

            float reach = halfWidth + b.extents.x;
            float proximity = 1f - Mathf.Clamp01(Mathf.Abs(dx) / reach);
            float treeFactor = mode == Mode.Tree ? 0.35f : 1f;

            // Lean with the direction of travel, and away from a body standing in the plant
            float push = Mathf.Clamp(velocity.x * 0.12f, -1f, 1f) + (-dx / reach) * 0.6f;
            float contribution = push * pushStrength * treeFactor * body.Strength * Mathf.Lerp(0.5f, 1f, proximity);
            if (Mathf.Abs(contribution) > Mathf.Abs(target)) target = contribution;

            float speed = Mathf.Abs(velocity.x);
            bool landed = justEntered && velocity.y < -4f;
            if (landed)
            {
                bendVelocity += (dx > 0f ? -1f : 1f) * Mathf.Min(-velocity.y, 12f) * 0.08f * pushStrength * treeFactor;
            }

            if (!rustle) continue;

            if (mode == Mode.Tree)
            {
                // Bumping the trunk at speed shakes a few leaves loose from the canopy
                if (justEntered && (speed > rustleSpeed || landed) && rustleCooldown <= 0f)
                {
                    rustleCooldown = 1.2f;
                    bendVelocity += Mathf.Sign(velocity.x == 0f ? 1f : velocity.x) * 0.15f;
                    system.DropCanopyLeaves(this, Random.Range(2, 5));
                }
                continue;
            }

            // Rushing through / landing in grass throws leaves from the foliage
            if (rustleCooldown <= 0f && (landed || (justEntered && speed > rustleSpeed) || speed > rustleSpeed * 1.6f))
            {
                rustleCooldown = landed || justEntered ? 0.2f : 0.45f;
                float intensity = Mathf.Clamp01(Mathf.Max(speed / 9f, -velocity.y / 12f));
                float x = Mathf.Clamp(b.center.x, minX, maxX);
                system.Rustle(new Vector2(x, baseY + height * 0.6f), leafColorA, leafColorB, intensity, Mathf.Sign(velocity.x), Mathf.Min(halfWidth, 0.4f));
            }

            // Running feet kick up bits of the plant they step on
            if (mode == Mode.Grass && footstepCooldown <= 0f && speed > 1.5f && Mathf.Abs(b.min.y - baseY) < 0.35f)
            {
                footstepCooldown = 0.28f;
                float footX = Mathf.Clamp(b.center.x - Mathf.Sign(velocity.x) * b.extents.x * 0.5f, minX, maxX);
                system.Footstep(new Vector2(footX, baseY + Mathf.Min(height * 0.25f, 0.15f)), leafColorA, leafColorB, -Mathf.Sign(velocity.x));
            }
        }

        // Spring toward the target bend, with a wobble on release
        float springStiffness = mode == Mode.Tree ? stiffness * 0.6f : stiffness;
        bendVelocity += (target - bend) * springStiffness * dt;
        bendVelocity *= Mathf.Exp(-damping * dt);
        bend = Mathf.Clamp(bend + bendVelocity * dt, -0.8f, 0.8f);

        if (Mathf.Abs(bend - writtenBend) > 0.0005f || (bend == 0f && writtenBend != 0f))
        {
            writtenBend = Mathf.Abs(bend) < 0.0005f && Mathf.Abs(bendVelocity) < 0.0005f ? 0f : bend;
            spriteRenderer.GetPropertyBlock(block);
            block.SetFloat(BendId, writtenBend);
            spriteRenderer.SetPropertyBlock(block);
        }
    }

    // Is this body actually in the plant (not just inside its rectangle, and not high above / far below it)?
    private bool Touches(VegetationInteractor body, float centerX, float halfWidth)
    {
        Bounds b = body.BodyBounds;
        if (Mathf.Abs(b.center.x - centerX) >= halfWidth + b.extents.x) return false;

        // Ground level of whoever is touching (for a slash: the one swinging it)
        float feetY = body.IsSlash && body.Owner != null ? body.Owner.BodyBounds.min.y : b.min.y;

        switch (mode)
        {
            case Mode.Hanging:
                // The body has to reach up into the visible vine
                return b.max.y > baseY && b.min.y < baseY + height;

            case Mode.Tree:
                // Standing at the trunk: feet near the tree's base, close to its middle
                if (Mathf.Abs(feetY - baseY) > 0.5f) return false;
                if (body.IsSlash) return b.min.y < baseY + height;
                return Mathf.Abs(b.center.x - centerX) < halfWidth * 0.3f + b.extents.x;

            default:
                // Standing in the plant: feet between just below its base and halfway up it
                if (feetY < baseY - 0.35f || feetY > baseY + height * 0.5f) return false;
                return b.min.y < baseY + height;
        }
    }

    private void Slashed(VegetationSystem system, float dx, float halfWidth)
    {
        float direction = dx > 0f ? -1f : 1f; // away from the swing
        float treeFactor = mode == Mode.Tree ? 0.35f : 1f;
        bendVelocity += direction * 3f * pushStrength * treeFactor;
        if (!rustle) return;

        if (mode == Mode.Tree)
        {
            system.DropCanopyLeaves(this, Random.Range(3, 7));
        }
        else
        {
            system.Rustle(new Vector2((minX + maxX) * 0.5f, baseY + height * 0.6f), leafColorA, leafColorB, 1f, direction, Mathf.Min(halfWidth, 0.5f));
        }
    }
}
