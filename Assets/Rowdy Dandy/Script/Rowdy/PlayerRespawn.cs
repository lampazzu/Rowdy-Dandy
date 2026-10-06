using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    private void Awake()
    {
        // Check if a respawn point has been set
        if (PlayerPrefs.HasKey("RespawnX") && PlayerPrefs.HasKey("RespawnY"))
        {
            float respawnX = PlayerPrefs.GetFloat("RespawnX");
            float respawnY = PlayerPrefs.GetFloat("RespawnY");

            // Set player position to the respawn point
            transform.position = new Vector2(respawnX, respawnY);
        }
    }
}