using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    public float moveSpeed = 2f;   // Speed at which the text moves up
    public float lifetime = 1f;    // Time before text disappears
    public TextMeshProUGUI damageText;

    public void SetText(int damage)
    {
        damageText.text = damage.ToString();
        Destroy(gameObject, lifetime); // Destroy after time expires
    }

    void Update()
    {
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;
        damageText.alpha -= Time.deltaTime / lifetime; // Gradually fade out
    }
}
