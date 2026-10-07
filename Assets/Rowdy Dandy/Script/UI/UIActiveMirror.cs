using UnityEngine;

// Shows/hides some objects together with another one.
// Used on the durability row: WeaponManager hides the durability fill for the unbreakable Rod,
// and this hides the rest of that bar (background, frame, label) along with it.
public class UIActiveMirror : MonoBehaviour
{
    [SerializeField] private GameObject source;
    [SerializeField] private GameObject[] targets;

    private void LateUpdate()
    {
        if (source == null || targets == null) return;

        bool visible = source.activeSelf;
        foreach (GameObject target in targets)
        {
            if (target != null && target != gameObject && target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }
    }
}
