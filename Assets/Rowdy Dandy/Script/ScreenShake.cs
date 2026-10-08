using Cinemachine;
using UnityEngine;

// One-line camera shake from code: ScreenShake.Impulse(0.5f). Uses a runtime Cinemachine impulse source (the
// scene's virtual camera already listens to impulses) and follows Settings > Screen Shake.
public static class ScreenShake
{
    private static CinemachineImpulseSource source;

    public static void Impulse(float force)
    {
        if (GameSettings.ScreenShake <= 0f || force <= 0f) return;
        if (source == null)
        {
            var go = new GameObject("ScreenShake (auto)");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<CinemachineImpulseSource>();
            source.m_ImpulseDefinition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            source.m_ImpulseDefinition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            source.m_ImpulseDefinition.m_ImpulseDuration = 0.18f;
            source.m_DefaultVelocity = new Vector3(0.25f, 0.35f, 0f);
        }
        Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(0.4f, 1f), 0f).normalized * 0.4f;
        source.GenerateImpulseWithVelocity(v * force * GameSettings.ScreenShake);
    }
}
