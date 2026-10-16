using UnityEngine;

// One station of the tutorial level: a sign, a zone around it, the cards it shows when Rowdy walks in and the hint
// that floats over the sign while he's there. Optional: a practice enemy that comes back after every kill.
public class TutorialStation : MonoBehaviour
{
    [Tooltip("Name of this station (only for you, in the Hierarchy)")]
    public string title = "STATION";
    [Tooltip("Floats over the sign while Rowdy is in the zone. Button tokens work: {JUMP} {ATTACK} {SURF} {INTERACT} {MAP}...")]
    public string hint = "";
    [Tooltip("The tutorial cards shown (in this order) when Rowdy walks into the zone")]
    public Tutorials.Topic[] topics = new Tutorials.Topic[0];
    [Tooltip("The zone, left and right of this object (world units)")]
    public float zoneLeft = 5f, zoneRight = 5f;
    [Tooltip("Optional practice enemy (a prefab name in Resources/Enemies Prefab). It comes back a few seconds after each kill.")]
    public string practiceEnemy = "";
    public Transform enemySpot;
    public SpriteRenderer sign;

    private float hintAt, respawnAt;
    private GameObject enemy;
    private EnemyHealth enemyHealth;
    private bool was;

    private void Start()
    {
        if (sign != null && sign.sprite == null) sign.sprite = TutorialBuilder.SignSprite; // a baked sign lost its drawn art
    }

    public void Tick(Vector3 rowdy)
    {
        float x = transform.position.x;
        bool inZone = rowdy.x >= x - zoneLeft && rowdy.x <= x + zoneRight && Mathf.Abs(rowdy.y - transform.position.y) < 12f;
        if (inZone && !was)
        {
            foreach (Tutorials.Topic t in topics) Tutorials.Show(t, null, 0.25f);
            hintAt = 0f;
        }
        was = inZone;
        if (!inZone) return;
        if (!string.IsNullOrEmpty(hint) && Time.time >= hintAt && !Tutorials.IsOpen)
        {
            hintAt = Time.time + 3.2f;
            Vector3 at = (sign != null ? sign.bounds.max.y : transform.position.y + 1.6f) * Vector3.up + new Vector3(x, 0.5f, 0f);
            IconPopup.Show(at, null, GameInput.Format(hint), new Color(0.45f, 1f, 0.85f), 0.75f, 2.8f);
        }
        if (!string.IsNullOrEmpty(practiceEnemy)) PracticeEnemy();
    }

    private void PracticeEnemy()
    {
        bool gone = enemy == null || enemyHealth == null || enemyHealth.enemydead || !enemy.activeInHierarchy;
        if (!gone) return;
        if (enemy != null && enemyHealth != null && enemyHealth.enemydead && respawnAt <= 0f) respawnAt = Time.time + 3f;
        if (respawnAt > 0f && Time.time < respawnAt) return;
        respawnAt = 0f;
        GameObject prefab = Resources.Load<GameObject>("Enemies Prefab/" + practiceEnemy);
        if (prefab == null) return;
        Vector3 at = enemySpot != null ? enemySpot.position : transform.position + Vector3.right * 6f;
        enemy = Instantiate(prefab, at + Vector3.up * 0.4f, Quaternion.identity);
        enemy.name = prefab.name;
        enemyHealth = enemy.GetComponentInChildren<EnemyHealth>(true);
        foreach (EnemyMovement m in enemy.GetComponentsInChildren<EnemyMovement>(true)) { m.ArenaAggro(); if (BoonRunner.Rowdy != null) m.FaceTowards(BoonRunner.RowdyCenter.x); }
        PulseRing.Spawn(at + Vector3.up * 0.5f, new Color(0.45f, 1f, 0.85f, 0.9f), 1.2f, 0.35f);
        FXParticle.Burst(at + Vector3.up * 0.5f, new Color(0.45f, 1f, 0.85f), 12, 1f, 3f, 4f, 0.5f);
        FXSound.Play("Teleport", 0.35f, 1.2f);
    }
}

