using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Vanish : MonoBehaviour
{
    private void OnEnable()
    {
        if (System.IO.File.Exists("Temp/rd_vanishlog.txt")) Debug.LogError("VANISH " + name + " root " + transform.root.name + " | " + System.Environment.StackTrace.Replace("\n", " <- "));
        Destroy(gameObject);
    }
}