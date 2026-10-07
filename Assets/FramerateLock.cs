using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FramerateLock : MonoBehaviour
{
    // Start is called before the first frame update

    [SerializeField] private int FPS = 60;


    void Start()
    {
        // The player's choice (Pause > Settings > Frame Limit) wins once they've picked one
        Application.targetFrameRate = PlayerPrefs.HasKey("RD_Settings_FrameLimit") ? GameSettings.FrameLimit : FPS;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
