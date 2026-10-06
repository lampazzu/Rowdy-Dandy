using UnityEngine;

public class DetachAndPlayParticles : MonoBehaviour
{
    private ParticleSystem _particleSystem;

    void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>();
    }

    public void PlayAndDetach()
    {
        if (_particleSystem != null)
        {
            // Play the particle system and detach it from the GameObject
            _particleSystem.Play();
            _particleSystem.transform.parent = null; // Detach from parent
            Destroy(_particleSystem.gameObject, _particleSystem.main.duration + _particleSystem.main.startLifetime.constantMax); // Destroy after particles finish
        }
    }
}