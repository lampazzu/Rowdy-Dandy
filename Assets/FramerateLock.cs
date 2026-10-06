using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FramerateLock : MonoBehaviour
{
    // Start is called before the first frame update

    [SerializeField] private int FPS = 60;


    void Start()
    {
        Application.targetFrameRate = FPS;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
