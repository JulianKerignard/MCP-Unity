using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 25f;

    [Header("Zoom")]
    public float zoomSpeed = 15f;
    public float zoomSmoothing = 8f;

    [Header("Bounds")]
    public float minX = -20f;
    public float maxX = 70f;
    public float minZ = -30f;
    public float maxZ = 70f;
    public float minY = 5f;
    public float maxY = 90f;

    float targetZoom;

    void Start()
    {
        targetZoom = transform.position.y;
    }

    void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        Vector3 move = Vector3.zero;

        // ZQSD movement
        if (Keyboard.current.zKey.isPressed || Keyboard.current.wKey.isPressed)
            move.z += 1f;
        if (Keyboard.current.sKey.isPressed)
            move.z -= 1f;
        if (Keyboard.current.qKey.isPressed || Keyboard.current.aKey.isPressed)
            move.x -= 1f;
        if (Keyboard.current.dKey.isPressed)
            move.x += 1f;

        if (move != Vector3.zero)
            transform.position += move.normalized * moveSpeed * Time.deltaTime;

        // Scroll zoom (smooth)
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll != 0f)
            targetZoom -= scroll * zoomSpeed * 0.01f;
        targetZoom = Mathf.Clamp(targetZoom, minY, maxY);

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetZoom, Time.deltaTime * zoomSmoothing);
        transform.position = pos;

        // Clamp position
        Vector3 clamped = transform.position;
        clamped.x = Mathf.Clamp(clamped.x, minX, maxX);
        clamped.z = Mathf.Clamp(clamped.z, minZ, maxZ);
        clamped.y = Mathf.Clamp(clamped.y, minY, maxY);
        transform.position = clamped;
    }
}
