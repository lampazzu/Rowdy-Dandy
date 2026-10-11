using UnityEngine;

// Loops sprite frames on a renderer (palms swaying)
// The frames are cut from the sheet when it wakes up, so it also works baked into the scene.
public class LoopFrames : MonoBehaviour
{
    [SerializeField] private Texture2D sheet;
    [SerializeField] private int count = 1;
    [SerializeField] private Vector2 pivot = new Vector2(0.5f, 0f);
    [SerializeField] private float fps = 10f, offset;
    private Sprite[] frames;
    private SpriteRenderer sr;

    public void Set(Texture2D sheet, int count, Vector2 pivot, float framesPerSecond, float timeOffset)
    {
        this.sheet = sheet; this.count = count; this.pivot = pivot; fps = framesPerSecond; offset = timeOffset;
        frames = null;
    }

    private void Update()
    {
        if (frames == null && sheet != null) { frames = ItemArt.Frames(sheet, count, 1, pivot, 64f); sr = GetComponent<SpriteRenderer>(); }
        if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[(int)((Time.time + offset) * fps) % frames.Length];
    }
}
