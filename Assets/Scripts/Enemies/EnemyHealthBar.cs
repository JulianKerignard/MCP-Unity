using UnityEngine;

public class EnemyHealthBar : MonoBehaviour
{
    Transform cam;
    Transform fillBar;
    Enemy enemy;
    float maxHP;

    static Material bgMat;
    static Material fillMat;

    public void Initialize(Enemy enemy, float maxHP)
    {
        this.enemy = enemy;
        this.maxHP = maxHP;
        cam = Camera.main.transform;

        // Create background (dark)
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "HealthBG";
        bg.transform.SetParent(transform, false);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(1.2f, 0.15f, 1f);
        Destroy(bg.GetComponent<Collider>());

        if (bgMat == null)
        {
            bgMat = new Material(Shader.Find("Sprites/Default"));
            bgMat.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        }
        bg.GetComponent<Renderer>().material = bgMat;

        // Create fill (green → red)
        var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "HealthFill";
        fill.transform.SetParent(transform, false);
        fill.transform.localPosition = Vector3.zero;
        fill.transform.localScale = new Vector3(1.1f, 0.1f, 1f);
        Destroy(fill.GetComponent<Collider>());

        if (fillMat == null)
        {
            fillMat = new Material(Shader.Find("Sprites/Default"));
            fillMat.color = Color.green;
        }
        fill.GetComponent<Renderer>().material = new Material(fillMat);

        fillBar = fill.transform;
    }

    void LateUpdate()
    {
        if (enemy == null || cam == null) return;

        // Position above enemy
        transform.position = enemy.transform.position + Vector3.up * 2.2f;

        // Face camera
        transform.forward = cam.forward;

        // Update fill
        float ratio = Mathf.Clamp01(enemy.currentHP / maxHP);

        if (fillBar != null)
        {
            // Scale X based on ratio, offset to align left
            fillBar.localScale = new Vector3(1.1f * ratio, 0.1f, 1f);
            fillBar.localPosition = new Vector3(-1.1f * (1f - ratio) * 0.5f, 0f, -0.01f);

            // Color: green → yellow → red
            Color c = ratio > 0.5f
                ? Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f)
                : Color.Lerp(Color.red, Color.yellow, ratio * 2f);
            fillBar.GetComponent<Renderer>().material.color = c;
        }
    }
}
