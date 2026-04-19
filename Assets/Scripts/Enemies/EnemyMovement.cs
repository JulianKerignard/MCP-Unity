using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    NavMeshAgent agent;
    float baseSpeed;
    float slowTimer;
    float slowFactor;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void Initialize(float speed, Transform target)
    {
        baseSpeed = speed;
        agent.speed = speed;
        agent.SetDestination(target.position);
        slowTimer = 0f;
        slowFactor = 1f;
    }

    public void ApplySlow(float duration, float factor)
    {
        slowTimer = duration;
        slowFactor = factor;
        agent.speed = baseSpeed * factor;
    }

    void Update()
    {
        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                slowFactor = 1f;
                agent.speed = baseSpeed;
            }
        }
    }
}
