using System.Collections.Generic;
using UnityEngine;

// The Purple Reign's lake tiles, animated together (frames cut from the lake sheet on wake-up: works baked too)
public class PoolWater : MonoBehaviour
{
    public Texture2D sheet;
    [SerializeField] private List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    [SerializeField] private List<int> offsets = new List<int>();
    private Sprite[] frames;

    public void Add(SpriteRenderer sr, Sprite[] f, int offset) { renderers.Add(sr); offsets.Add(offset); frames = f; }

    private void Update()
    {
        if (frames == null && sheet != null) frames = ItemArt.Frames(sheet, 6, 1, new Vector2(0f, 1f), 64f);
        if (frames == null || frames.Length == 0) return;
        int f = (int)(Time.time * 6f);
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i] != null) renderers[i].sprite = frames[(f + offsets[i]) % frames.Length];
    }
}
