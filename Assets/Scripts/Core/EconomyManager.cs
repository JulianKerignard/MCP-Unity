using UnityEngine;
using System;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    public event Action<int> OnGoldChanged;

    [SerializeField] int currentGold;

    public int Gold => currentGold;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Initialize(int startingGold)
    {
        currentGold = startingGold;
        OnGoldChanged?.Invoke(currentGold);
    }

    public bool CanAfford(int cost)
    {
        return currentGold >= cost;
    }

    public void Spend(int amount)
    {
        if (!CanAfford(amount)) return;
        currentGold -= amount;
        OnGoldChanged?.Invoke(currentGold);
    }

    public void Earn(int amount)
    {
        currentGold += amount;
        OnGoldChanged?.Invoke(currentGold);
    }
}
