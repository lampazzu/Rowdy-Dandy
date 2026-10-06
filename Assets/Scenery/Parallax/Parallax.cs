using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Parallax : MonoBehaviour
{
    public GameObject cameraPlayer; // Reference to the camera or player
    private float length, startPos;
    public float speedParallax; // Speed for regular parallax effect
    public bool isMovingAlone; // Toggle for independent movement
    public float speedMovingAlone; // Speed for independent movement when isMovingAlone is true

    void Start()
    {
        startPos = transform.position.x;
        length = GetComponent<SpriteRenderer>().bounds.size.x;
    }

    void FixedUpdate()
    {
        if (isMovingAlone)
        {
            // Independent movement: increment based only on time
            float dist = (Time.time * speedMovingAlone) % length;
            transform.position = new Vector3(startPos + dist, transform.position.y, transform.position.z);

            // Reset position based on camera to keep in view
            if (transform.position.x < cameraPlayer.transform.position.x - length)
            {
                startPos += length;
            }
            else if (transform.position.x > cameraPlayer.transform.position.x + length)
            {
                startPos -= length;
            }
        }
        else
        {
            // Standard parallax movement based on cameraPlayer's position
            float temp = (cameraPlayer.transform.position.x * (1 - speedParallax));
            float dist = (cameraPlayer.transform.position.x * speedParallax);

            transform.position = new Vector3(startPos + dist, transform.position.y, transform.position.z);

            // Adjust startPos to maintain looping with camera movement
            if (temp > startPos + length)
            {
                startPos += length;
            }
            else if (temp < startPos - length)
            {
                startPos -= length;
            }
        }
    }
}