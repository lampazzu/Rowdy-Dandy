using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SoundEffect : MonoBehaviour
{
    // Serialized UnityEvent
    [SerializeField] public UnityEvent onWolfAttack;

    private void OnEnable()
    {
        // Invoking the UnityEvent when the script is enabled
        onWolfAttack.Invoke();
    }
}