using UnityEngine;
using System.Collections;

public class Projectile : MonoBehaviour
{
    float damage;
    float speed = 20f;
    bool isAoE;
    float aoERadius;
    bool isSlow;
    float slowFactor;
    float slowDuration;
    DamageType damageType;
    TowerFXType fxType;
    Transform target;
    Vector3 lastTargetPos;

    public void Initialize(Transform target, float damage, bool isAoE, float aoERadius, bool isSlow, float slowFactor, float slowDuration, DamageType damageType = DamageType.Physical, TowerFXType fxType = TowerFXType.Basic)
    {
        this.target = target;
        this.damage = damage;
        this.isAoE = isAoE;
        this.aoERadius = aoERadius;
        this.isSlow = isSlow;
        this.slowFactor = slowFactor;
        this.slowDuration = slowDuration;
        this.damageType = damageType;
        this.fxType = fxType;

        if (target != null)
            lastTargetPos = target.position;

        SetupTrail();

        if (fxType == TowerFXType.Sniper)
            speed = 40f;
    }

    void SetupTrail()
    {
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.numCornerVertices = 2;
        trail.numCapVertices = 2;

        Color trailColor;
        switch (fxType)
        {
            case TowerFXType.Sniper:
                trailColor = new Color(1f, 0.2f, 0.1f);
                trail.time = 0.1f;
                trail.startWidth = 0.06f;
                trail.endWidth = 0f;
                break;
            case TowerFXType.AoE:
                trailColor = new Color(1f, 0.5f, 0f);
                trail.time = 0.25f;
                trail.startWidth = 0.18f;
                trail.endWidth = 0.02f;
                break;
            case TowerFXType.Slow:
                trailColor = new Color(0.3f, 0.8f, 1f);
                trail.time = 0.3f;
                trail.startWidth = 0.1f;
                trail.endWidth = 0f;
                break;
            default:
                trailColor = new Color(1f, 0.8f, 0.2f);
                trail.time = 0.2f;
                trail.startWidth = 0.12f;
                trail.endWidth = 0f;
                break;
        }

        trail.startColor = trailColor;
        trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
    }

    void Update()
    {
        Vector3 destination = target != null ? target.position : lastTargetPos;

        if (target != null)
            lastTargetPos = target.position;

        Vector3 dir = destination - transform.position;
        float dist = speed * Time.deltaTime;

        if (dir.magnitude <= dist)
        {
            HitTarget();
            return;
        }

        transform.position += dir.normalized * dist;
        transform.LookAt(destination);
    }

    void HitTarget()
    {
        SpawnImpactEffect();

        if (isAoE)
        {
            SpawnShockwave(transform.position, aoERadius);
            Collider[] hits = Physics.OverlapSphere(transform.position, aoERadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    ApplyDamage(hit.GetComponent<Enemy>());
                }
            }
        }
        else if (target != null)
        {
            ApplyDamage(target.GetComponent<Enemy>());
        }

        Destroy(gameObject);
    }

    void ApplyDamage(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.TakeDamage(damage, damageType);
        if (isSlow)
        {
            enemy.ApplySlow(slowDuration, slowFactor);
            SpawnFrostEffect(enemy.transform.position);
        }
    }

    void SpawnImpactEffect()
    {
        var go = new GameObject("Impact");
        go.transform.position = transform.position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = 0.3f;
        main.startSpeed = 5f;
        main.startSize = 0.15f;
        main.duration = 0.1f;
        main.loop = false;
        main.maxParticles = 12;
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        int burstCount;
        switch (fxType)
        {
            case TowerFXType.Sniper:
                main.startColor = new Color(1f, 0.3f, 0.1f);
                main.startSpeed = 8f;
                main.startSize = 0.1f;
                burstCount = 12;
                break;
            case TowerFXType.AoE:
                main.startColor = new Color(1f, 0.5f, 0f);
                main.startSpeed = 4f;
                main.startSize = 0.2f;
                main.startLifetime = 0.5f;
                burstCount = 16;
                break;
            case TowerFXType.Slow:
                main.startColor = new Color(0.4f, 0.85f, 1f);
                main.startSpeed = 3f;
                main.startSize = 0.12f;
                main.gravityModifier = 0.3f;
                burstCount = 10;
                break;
            default:
                main.startColor = new Color(1f, 0.8f, 0.2f);
                burstCount = 8;
                break;
        }

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
    }

    void SpawnShockwave(Vector3 center, float radius)
    {
        var go = new GameObject("Shockwave");
        go.transform.position = center + Vector3.up * 0.15f;
        var runner = go.AddComponent<ShockwaveEffect>();
        runner.Initialize(radius);
    }

    void SpawnFrostEffect(Vector3 pos)
    {
        var go = new GameObject("FrostHit");
        go.transform.position = pos + Vector3.up * 0.5f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = 0.6f;
        main.startSpeed = 1.5f;
        main.startSize = 0.2f;
        main.duration = 0.1f;
        main.loop = false;
        main.maxParticles = 6;
        main.startColor = new Color(0.5f, 0.9f, 1f, 0.8f);
        main.gravityModifier = -0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 4) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();
    }
}
