using UnityEngine;
using System.Collections;

public class Tower : MonoBehaviour
{
    [Header("Data")]
    public TowerData data;

    [Header("Runtime")]
    public int level = 1;

    public float Damage
    {
        get
        {
            if (data == null) return 0;
            if (level >= 3) return data.damage * data.upgradeDamageMultiplier3;
            if (level >= 2) return data.damage * data.upgradeDamageMultiplier;
            return data.damage;
        }
    }

    public float Range
    {
        get
        {
            if (data == null) return 0;
            if (level >= 3) return data.range * data.upgradeRangeMultiplier3;
            if (level >= 2) return data.range * data.upgradeRangeMultiplier;
            return data.range;
        }
    }

    public int TotalInvested
    {
        get
        {
            if (data == null) return 0;
            int total = data.cost;
            if (level >= 2) total += data.upgradeCost;
            if (level >= 3) total += data.upgradeCost2;
            return total;
        }
    }

    public int SellValue => data != null ? Mathf.RoundToInt(TotalInvested * data.sellRefundPercent) : 0;

    Transform target;
    float fireTimer;
    Collider[] hitBuffer = new Collider[20];

    [Header("References")]
    public Transform firePoint;

    void Update()
    {
        if (data == null) return;

        fireTimer -= Time.deltaTime;
        FindTarget();

        if (target != null)
        {
            RotateTowards(target);
            if (fireTimer <= 0f)
            {
                Fire();
                fireTimer = 1f / data.fireRate;
            }
        }
    }

    void FindTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, Range, hitBuffer);
        float closestDist = float.MaxValue;
        Transform closest = null;

        // Target the enemy closest to the base (most dangerous)
        Transform endPoint = WaveManager.Instance != null ? WaveManager.Instance.endPoint : null;

        for (int i = 0; i < count; i++)
        {
            if (!hitBuffer[i].CompareTag("Enemy")) continue;
            float dist = endPoint != null
                ? Vector3.Distance(hitBuffer[i].transform.position, endPoint.position)
                : Vector3.Distance(transform.position, hitBuffer[i].transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = hitBuffer[i].transform;
            }
        }

        target = closest;
    }

    void RotateTowards(Transform t)
    {
        Vector3 dir = t.position - transform.position;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
        }
    }

    void Fire()
    {
        if (data.projectilePrefab == null) return;

        Transform spawnPoint = firePoint != null ? firePoint : transform;
        var proj = Instantiate(data.projectilePrefab, spawnPoint.position, Quaternion.identity);
        var projectile = proj.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Initialize(target, Damage, data.isAoE, data.aoERadius, data.isSlow, data.slowFactor, data.slowDuration, data.damageType, data.fxType);
        }

        SpawnMuzzleFlash(spawnPoint.position);

        if (data.fxType == TowerFXType.Sniper && target != null)
            StartCoroutine(LaserBeam(spawnPoint.position, target.position));
    }

    void SpawnMuzzleFlash(Vector3 pos)
    {
        var go = new GameObject("MuzzleFlash");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = 0.15f;
        main.startSpeed = 3f;
        main.startSize = 0.1f;
        main.duration = 0.05f;
        main.loop = false;
        main.maxParticles = 6;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        Color flashColor;
        switch (data.fxType)
        {
            case TowerFXType.Sniper: flashColor = new Color(1f, 0.3f, 0.3f); break;
            case TowerFXType.AoE: flashColor = new Color(1f, 0.6f, 0f); break;
            case TowerFXType.Slow: flashColor = Color.cyan; break;
            default: flashColor = new Color(1f, 0.8f, 0.2f); break;
        }
        main.startColor = flashColor;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 5) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
    }

    IEnumerator LaserBeam(Vector3 from, Vector3 to)
    {
        var go = new GameObject("LaserBeam");
        var lr = go.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(1f, 0.2f, 0.1f, 1f);
        lr.endColor = new Color(1f, 0.4f, 0.2f, 0.8f);
        lr.startWidth = 0.15f;
        lr.endWidth = 0.08f;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        float duration = 0.15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration);
            lr.startColor = new Color(1f, 0.2f, 0.1f, alpha);
            lr.endColor = new Color(1f, 0.4f, 0.2f, alpha * 0.8f);
            lr.startWidth = 0.15f * alpha;
            lr.endWidth = 0.08f * alpha;
            yield return null;
        }

        Destroy(go);
    }

    public bool CanUpgrade()
    {
        return level < 3;
    }

    public int GetUpgradeCost()
    {
        if (data == null) return 0;
        if (level == 1) return data.upgradeCost;
        if (level == 2) return data.upgradeCost2;
        return 0;
    }

    public void Upgrade()
    {
        if (!CanUpgrade()) return;
        level++;
    }

    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, Range);
    }
}
