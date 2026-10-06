using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Vanish : MonoBehaviour
{
    private void OnEnable()
    {
        Destroy(gameObject);
    }
}