using UnityEngine;

public class AssignSimulationSpace : MonoBehaviour
{
    private void Start()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps != null && ps.main.simulationSpace == ParticleSystemSimulationSpace.Custom)
        {
            var main = ps.main;
            main.customSimulationSpace = transform.parent; // Assign parent as simulation space
        }
    }
}
