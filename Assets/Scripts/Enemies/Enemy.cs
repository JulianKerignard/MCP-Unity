using UnityEngine;
using System;
using System.Collections;

public class Enemy : MonoBehaviour
{
    [Header("Data")]
    public EnemyData data;

    [Header("Runtime")]
    public float currentHP;
    public float maxHPActual;
    public bool hasShield;

    public static event Action<Enemy> OnEnemyDied;
    public static event Action<Enemy> OnEnemyReachedEnd;

    EnemyMovement movement;
    EnemyHealthBar healthBar;
    bool isDead;
    float abilityTimer;
    Transform spawnTarget;
    GameObject shieldVisual;

    void Awake()
    {
        movement = GetComponent<EnemyMovement>();
    }

    public void Initialize(EnemyData enemyData, Transform target, float healthMultiplier = 1f)
    {
        data = enemyData;
        maxHPActual = data.maxHP * healthMultiplier;
        currentHP = maxHPActual;
        isDead = false;
        spawnTarget = target;

        if (data.bossAbility == BossAbility.Shield)
            hasShield = true;

        if (movement != null)
            movement.Initialize(data.speed, target);

        // Apply color from data
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.material.color = data.enemyColor;

        // Create shield visual if needed
        if (hasShield)
            CreateShieldVisual();

        // Create health bar as child
        var barGO = new GameObject("HealthBar");
        barGO.transform.SetParent(transform);
        healthBar = barGO.AddComponent<EnemyHealthBar>();
        healthBar.Initialize(this, currentHP);
    }

    void Update()
    {
        if (isDead) return;

        // Check distance to end point
        if (spawnTarget != null && Vector3.Distance(transform.position, spawnTarget.position) < 2f)
        {
            ReachEnd();
            return;
        }

        if (data == null || data.bossAbility == BossAbility.None) return;

        abilityTimer -= Time.deltaTime;
        if (abilityTimer > 0f) return;

        switch (data.bossAbility)
        {
            case BossAbility.Regen:
                float regenAmount = data.abilityValue > 0 ? data.abilityValue : maxHPActual * 0.02f;
                currentHP = Mathf.Min(currentHP + regenAmount, maxHPActual);
                abilityTimer = 1f;
                break;
            case BossAbility.Shield:
                if (!hasShield && currentHP < maxHPActual * 0.3f)
                {
                    hasShield = true;
                    CreateShieldVisual();
                    abilityTimer = 8f;
                }
                else
                    abilityTimer = 1f;
                break;
            case BossAbility.Summon:
                SpawnMinion();
                abilityTimer = data.abilityValue > 0 ? data.abilityValue : 5f;
                break;
        }
    }

    void SpawnMinion()
    {
        if (WaveManager.Instance == null) return;
        var minionData = WaveManager.Instance.GetBasicEnemyData();
        if (minionData == null || minionData.prefab == null) return;

        var go = Instantiate(minionData.prefab, transform.position + Vector3.right * 1.5f, Quaternion.identity);
        var minion = go.GetComponent<Enemy>();
        if (minion != null && spawnTarget != null)
        {
            minion.Initialize(minionData, spawnTarget, 0.5f);
            WaveManager.Instance.RegisterEnemy(minion);
        }
    }

    void CreateShieldVisual()
    {
        if (shieldVisual != null) return;
        shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shieldVisual.name = "ShieldBubble";
        shieldVisual.transform.SetParent(transform);
        shieldVisual.transform.localPosition = Vector3.up * 0.5f;
        shieldVisual.transform.localScale = Vector3.one * 2.5f;
        Destroy(shieldVisual.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0.2f, 0.5f, 1f, 0.25f);
        shieldVisual.GetComponent<Renderer>().material = mat;
    }

    void DestroyShieldVisual()
    {
        if (shieldVisual != null)
        {
            Destroy(shieldVisual);
            shieldVisual = null;
        }
    }

    public void TakeDamage(float amount, DamageType damageType = DamageType.Physical)
    {
        if (isDead) return;

        // Shield blocks one hit
        if (hasShield)
        {
            hasShield = false;
            DestroyShieldVisual();
            StartCoroutine(FlashHit());
            return;
        }

        // Armor reduces physical damage
        if (damageType == DamageType.Physical && data != null && data.armor > 0)
            amount = Mathf.Max(amount - data.armor, amount * 0.2f);

        currentHP -= amount;

        // Flash white on hit
        StartCoroutine(FlashHit());

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    public void ApplySlow(float duration, float factor)
    {
        if (isDead || movement == null) return;
        movement.ApplySlow(duration, factor);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        OnEnemyDied?.Invoke(this);

        // Hide health bar (destroyed with parent)
        if (healthBar != null) healthBar.gameObject.SetActive(false);

        // Disable movement and collider immediately
        if (movement != null) movement.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        StartCoroutine(DeathEffect());
    }

    IEnumerator FlashHit()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        // Store original colors
        var origColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            origColors[i] = renderers[i].material.color;

        // Flash white
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = Color.white;

        yield return new WaitForSeconds(0.05f);

        // Restore (if not dead/destroyed)
        if (!isDead)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].material.color = origColors[i];
        }
    }

    IEnumerator DeathEffect()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        // Shrink + sink + fade
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Shrink
            transform.localScale = startScale * (1f - t);

            // Sink
            transform.position += Vector3.down * Time.deltaTime * 2f;

            // Spin
            transform.Rotate(0f, 720f * Time.deltaTime, 0f);

            yield return null;
        }

        Destroy(gameObject);
    }

    void ReachEnd()
    {
        if (isDead) return;
        isDead = true;
        OnEnemyReachedEnd?.Invoke(this);
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        if (other.CompareTag("EndPoint"))
            ReachEnd();
    }
}
