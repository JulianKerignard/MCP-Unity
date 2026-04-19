using UnityEngine;

public class ShockwaveEffect : MonoBehaviour
{
    float maxRadius;
    float duration = 0.35f;
    float elapsed;
    LineRenderer lr;

    public void Initialize(float radius)
    {
        maxRadius = radius;

        lr = gameObject.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(1f, 0.5f, 0f, 0.9f);
        lr.endColor = new Color(1f, 0.5f, 0f, 0.9f);
        lr.startWidth = 0.25f;
        lr.endWidth = 0.25f;
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 48;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float currentRadius = maxRadius * t;
        float alpha = 1f - t;
        float width = 0.25f * alpha;

        lr.startColor = new Color(1f, 0.5f, 0f, alpha);
        lr.endColor = new Color(1f, 0.5f, 0f, alpha);
        lr.startWidth = width;
        lr.endWidth = width;

        DrawCircle(currentRadius);
    }

    void DrawCircle(float radius)
    {
        Vector3 center = transform.position;
        for (int i = 0; i < lr.positionCount; i++)
        {
            float angle = (float)i / lr.positionCount * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            lr.SetPosition(i, center + new Vector3(x, 0f, z));
        }
    }
}
