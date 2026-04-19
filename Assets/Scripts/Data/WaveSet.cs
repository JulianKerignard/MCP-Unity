using UnityEngine;

[CreateAssetMenu(fileName = "New WaveSet", menuName = "TD/Wave Set")]
public class WaveSet : ScriptableObject
{
    public string setName;
    public string description;
    public WaveData[] waves;
}
