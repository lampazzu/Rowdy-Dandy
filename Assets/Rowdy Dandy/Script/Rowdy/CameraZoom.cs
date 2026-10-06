using UnityEngine;
using Cinemachine;

public class CameraZoom : MonoBehaviour
{
    public CinemachineVirtualCamera cinemachineCam;
    public float normalZoom = 5f;
    public float surfingZoom = 7f;
    public float zoomSpeed = 2f;

    private bool isSurfing;
    private float targetZoom;

    void Start()
    {
        targetZoom = normalZoom;
    }

    void Update()
    {
        // Smooth transition to target zoom
        cinemachineCam.m_Lens.OrthographicSize = Mathf.Lerp(
            cinemachineCam.m_Lens.OrthographicSize,
            targetZoom,
            Time.deltaTime * zoomSpeed
        );
    }

    public void SetSurfing(bool surfing)
    {
        isSurfing = surfing;
        targetZoom = isSurfing ? surfingZoom : normalZoom;
    }
}
