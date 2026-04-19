using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Distance")]
    public float distance = 5f;
    public float minDistance = 2f;
    public float maxDistance = 12f;
    public float scrollSpeed = 2f;

    [Header("Rotation")]
    public float mouseSensitivity = 3f;
    public float minPitch = -20f;
    public float maxPitch = 60f;

    [Header("Offset")]
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Smoothing")]
    public float positionSmoothing = 10f;

    float yaw;
    float pitch = 15f;

    void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void LateUpdate()
    {
        if (target == null || Mouse.current == null) return;

        // Mouse rotation (only when cursor is locked)
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            yaw += mouseDelta.x * mouseSensitivity * 0.1f;
            pitch -= mouseDelta.y * mouseSensitivity * 0.1f;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // Scroll zoom
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll != 0f)
        {
            distance -= scroll * scrollSpeed * 0.01f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        // Calculate desired position
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPos = target.position + targetOffset;
        Vector3 desiredPos = targetPos - rotation * Vector3.forward * distance;

        // Smooth follow
        transform.position = Vector3.Lerp(transform.position, desiredPos, positionSmoothing * Time.deltaTime);
        transform.LookAt(targetPos);
    }
}
