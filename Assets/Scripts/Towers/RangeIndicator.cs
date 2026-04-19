using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RangeIndicator : MonoBehaviour
{
    static RangeIndicator _instance;
    LineRenderer lr;
    const int segments = 64;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = segments;
        lr.startWidth = 0.15f;
        lr.endWidth = 0.15f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // Use a simple unlit material
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(1f, 1f, 0.3f, 0.8f);
        lr.endColor = new Color(1f, 1f, 0.3f, 0.8f);

        gameObject.SetActive(false);
        _instance = this;
    }

    public static void Show(Vector3 center, float radius)
    {
        if (_instance == null) Create();
        _instance.gameObject.SetActive(true);
        _instance.DrawCircle(center, radius);
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance.gameObject.SetActive(false);
    }

    void DrawCircle(Vector3 center, float radius)
    {
        float y = center.y + 0.1f;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            lr.SetPosition(i, new Vector3(x, y, z));
        }
    }

    static void Create()
    {
        var go = new GameObject("RangeIndicator");
        go.AddComponent<LineRenderer>();
        go.AddComponent<RangeIndicator>();
        DontDestroyOnLoad(go);
    }
}
