using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// CHECKPOINT MENU: {INTERACT} on a checkpoint while Rowdy carries legendary rats. (No rats = it rests straight away,
// like before.) The game pauses while it's open.
//   BEAUTY SLEEP      - rest (CheckpointRest: reload here, full health, morning)
//   DROP A RAT (xN)   - RatBait: a lost cat smells it and comes running back
//   NEVER MIND
public class CheckpointMenu : MonoBehaviour
{
    private static CheckpointMenu instance;
    public static bool IsOpen { get; private set; }
    private static int closedFrame = -10;
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedFrame + 2;

    private RectTransform root;
    private PixelText title, info, hints;
    private readonly PixelText[] options = new PixelText[3];
    private readonly Image[] plates = new Image[3];
    private int selected, heldDir, hintVersion = -1;
    private float navTimer, openedAt;
    private System.Action rest;
    private Vector3 baitAt;
    private PetFollower lurable;

    private static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

    public static void Open(System.Action rest, Vector3 baitAt)
    {
        if (IsOpen || PauseMenu.IsPaused) return;
        if (instance == null)
        {
            var go = new GameObject("CheckpointMenu (auto)");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CheckpointMenu>();
            SceneManager.sceneLoaded += (s, m) => { if (instance != null && IsOpen) instance.Close(); };
        }
        instance.rest = rest;
        instance.baitAt = baitAt;
        instance.DoOpen();
    }

    private void DoOpen()
    {
        if (root == null) Build();
        IsOpen = true;
        openedAt = Time.unscaledTime;
        lurable = CatRoster.Lurable();
        selected = lurable != null ? 1 : 0;
        root.gameObject.SetActive(true);
        PauseMenu.SetExternalPause(true);
        UISound.Play(UISound.Cue.Open);
        Refresh();
    }

    private void Close()
    {
        IsOpen = false;
        closedFrame = Time.frameCount;
        if (root != null) root.gameObject.SetActive(false);
        PauseMenu.SetExternalPause(false);
    }

    private void Build()
    {
        root = OverlayUI.MakeRect("Checkpoint Menu", OverlayUI.Root);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(820f, 470f);
        root.anchoredPosition = new Vector2(0f, 40f);
        Image dim = OverlayUI.MakeImage("Dim", root, new Color(0.03f, 0.01f, 0.05f, 0.55f), OverlayUI.WhiteSprite);
        dim.rectTransform.anchorMin = dim.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dim.rectTransform.sizeDelta = new Vector2(4000f, 3000f);
        Image panel = OverlayUI.MakePanel("Panel", root);
        panel.rectTransform.anchorMin = Vector2.zero; panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;

        title = Text("CHECKPOINT", 6, Gold, 175f);
        info = Text("", 3, new Color(1f, 0.85f, 0.95f), 118f);
        for (int i = 0; i < 3; i++)
        {
            plates[i] = OverlayUI.MakeImage("Row " + i, root, Color.clear, OverlayUI.WhiteSprite);
            plates[i].rectTransform.anchorMin = plates[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            plates[i].rectTransform.sizeDelta = new Vector2(700f, 62f);
            plates[i].rectTransform.anchoredPosition = new Vector2(0f, 40f - i * 74f);
            options[i] = Text("", 4, Color.white, 40f - i * 74f);
        }
        hints = Text("", 2, new Color(1f, 1f, 1f, 0.7f), -195f);
    }

    private PixelText Text(string s, int scale, Color color, float y)
    {
        PixelText t = PixelText.Create(root, s, scale, color, 0.5f);
        t.Rect.anchorMin = t.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        t.Rect.anchoredPosition = new Vector2(0f, y);
        return t;
    }

    private bool RatUsable => CatRoster.Rats > 0 && lurable != null;

    private void Refresh()
    {
        hintVersion = GameInput.DeviceVersion;
        info.SetText(CatRoster.Rats + " LEGENDARY RAT" + (CatRoster.Rats == 1 ? "" : "S") + "     " +
                     (lurable != null ? lurable.CatName + " IS OUT THERE SOMEWHERE" : "NO LOST CATS AROUND"));
        options[0].SetText("BEAUTY SLEEP");
        options[1].SetText("DROP A RAT  (X" + CatRoster.Rats + ")");
        options[2].SetText("NEVER MIND");
        for (int i = 0; i < 3; i++)
        {
            bool on = i == selected;
            bool usable = i != 1 || RatUsable;
            options[i].Color = !usable ? new Color(0.55f, 0.5f, 0.6f) : on ? (i == 1 ? Gold : Color.white) : new Color(0.85f, 0.75f, 0.9f);
            plates[i].color = on ? new Color(1f, 0.25f, 0.75f, 0.28f) : Color.clear;
        }
        hints.SetText(GameInput.Format("{MOVE} CHOOSE     {OK} CONFIRM     {BACK} CLOSE"));
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (hintVersion != GameInput.DeviceVersion) Refresh();
        // a little pulse on the selected row
        float pulse = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f);
        for (int i = 0; i < 3; i++) options[i].Rect.localScale = Vector3.one * (i == selected ? pulse : 1f);
        if (Time.unscaledTime - openedAt < 0.2f) return;

        if (GameInput.Down(GameInput.Act.Back) || GameInput.Down(GameInput.Act.Pause)) { UISound.Play(UISound.Cue.Back); Close(); return; }
        int dir = GameInput.MoveY < 0f ? 1 : GameInput.MoveY > 0f ? -1 : 0;
        if (Repeat(dir))
        {
            selected = (selected + dir + 3) % 3;
            UISound.Play(UISound.Cue.Move);
            Refresh();
        }
        if (GameInput.Down(GameInput.Act.Submit) || GameInput.Down(GameInput.Act.Interact) || GameInput.Down(GameInput.Act.Jump))
        {
            switch (selected)
            {
                case 0:
                    UISound.Play(UISound.Cue.Confirm);
                    Close();
                    rest?.Invoke();
                    break;
                case 1:
                    if (!RatUsable) { UISound.Play(UISound.Cue.Locked); return; }
                    UISound.Play(UISound.Cue.Confirm);
                    Close();
                    if (CatRoster.SpendRat(out PetFollower cat)) RatBait.Drop(baitAt, cat);
                    break;
                default:
                    UISound.Play(UISound.Cue.Back);
                    Close();
                    break;
            }
        }
    }

    private bool Repeat(int dir)
    {
        if (dir == 0) { heldDir = 0; return false; }
        if (dir != heldDir) { heldDir = dir; navTimer = 0.32f; return true; }
        navTimer -= Time.unscaledDeltaTime;
        if (navTimer <= 0f) { navTimer = 0.13f; return true; }
        return false;
    }
}

// A legendary rat dropped as bait by a checkpoint: it squeaks and wiggles on the ground, and the lured cat comes
// dashing in from off-screen, gobbles it up and joins Rowdy (or waits for a swap if the party is full).
public class RatBait : MonoBehaviour
{
    private static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
    private Sprite[] frames;
    private SpriteRenderer sr;

    public static void Drop(Vector3 at, PetFollower cat)
    {
        ItemArt art = ItemArt.Get;
        if (SolidGround.Ray((Vector2)at + Vector2.up * 1.5f, Vector2.down, 6f, out RaycastHit2D hit)) at = hit.point;
        var go = new GameObject("Rat Bait");
        go.transform.position = at + new Vector3(0.9f, 0.05f, 0f);
        var bait = go.AddComponent<RatBait>();
        bait.sr = go.AddComponent<SpriteRenderer>();
        bait.sr.sortingOrder = 64;
        if (ItemArt.Lit != null) bait.sr.sharedMaterial = ItemArt.Lit;
        if (art != null && art.flyingRat != null) bait.frames = ItemArt.Frames(art.flyingRat, 10, 2, new Vector2(0.5f, 0.15f), 64f);
        if (bait.frames != null) bait.sr.sprite = bait.frames[0];
        bait.StartCoroutine(bait.Run(cat));

        FXSound.Play("RatGrab", 0.8f, 0.85f);
        PulseRing.Spawn(go.transform.position, Gold, 1.6f, 0.5f);
        FXParticle.Burst(go.transform.position + Vector3.up * 0.2f, Gold, 18, 1.5f, 4f, 3f, 0.8f);
        IconPopup.Show(go.transform.position + Vector3.up * 0.8f, bait.frames != null ? bait.frames[0] : null, "RAT BAIT!", Gold, 1.1f, 2f);
    }

    private IEnumerator Run(PetFollower cat)
    {
        // squeak squeak: the smell spreads (rings), the cat comes
        float t = 0f;
        while (t < 1.4f)
        {
            t += Time.deltaTime;
            Wiggle(t);
            if (Mathf.Repeat(t, 0.45f) < Time.deltaTime) PulseRing.Spawn(transform.position + Vector3.up * 0.15f, new Color(Gold.r, Gold.g, Gold.b, 0.5f), 2.5f + t * 2f, 0.6f, 61);
            yield return null;
        }
        if (cat == null || cat.IsCollected) { Vanish(); yield break; }

        // the cat dashes in from the side Rowdy isn't facing... from off-screen anyway
        int side = Random.value < 0.5f ? -1 : 1;
        Vector3 end = transform.position + new Vector3(side * 0.4f, 0.2f, 0f);
        Vector3 start = end + new Vector3(side * 9f, 2.5f, 0f);
        cat.Relocate(start);
        SpriteRenderer catSr = cat.GetComponent<SpriteRenderer>();
        IconPopup.Show(BoonRunner.Rowdy != null ? BoonRunner.RowdyCenter + Vector3.up * 1f : end + Vector3.up, cat.Portrait, cat.CatName + " SMELLS RAT!", new Color(1f, 0.75f, 0.9f), 1f, 2f);
        float after = 0f;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.7f)
        {
            if (cat == null) { Vanish(); yield break; }
            Vector3 p = Vector3.Lerp(start, end, 1f - (1f - k) * (1f - k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
            cat.Relocate(p);
            if (catSr != null) catSr.flipX = side > 0;
            after -= Time.deltaTime;
            if (after <= 0f && catSr != null) { after = 0.04f; CatFX.Afterimage(catSr, new Color(1f, 0.8f, 0.95f, 0.55f), 0.2f); }
            Wiggle(t += Time.deltaTime);
            yield return null;
        }
        if (cat == null) { Vanish(); yield break; }
        cat.Relocate(end);

        // CHOMP
        FXSound.Play("RatGrab", 0.9f, 1.3f);
        ScreenShake.Impulse(0.25f);
        PulseRing.Spawn(transform.position, Gold, 1.8f, 0.45f);
        FXParticle.Burst(transform.position + Vector3.up * 0.2f, Gold, 26, 2f, 5f, 4f, 0.9f);
        IconPopup.Show(end + Vector3.up * 0.8f, cat.Portrait, cat.CatName + " IS BACK!", new Color(1f, 0.85f, 0.4f), 1.2f, 2.4f);
        cat.SpeakLine();
        if (CatRoster.HasRoom && BoonRunner.Rowdy != null) cat.Collect(BoonRunner.Rowdy);
        Vanish();
    }

    private void Wiggle(float t)
    {
        if (frames == null || sr == null) return;
        sr.sprite = frames[new[] { 3, 4, 5, 4 }[(int)(t * 12f) % 4]];
        sr.flipX = Mathf.Repeat(t, 0.8f) < 0.4f;
    }

    private void Vanish() => Destroy(gameObject);
}
