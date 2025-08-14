using UnityEngine;

[CreateAssetMenu(fileName = "GridConfig", menuName = "EcoSim/Grid Config")]
public class GridConfig : ScriptableObject
{
    [Min(2)] public int width = 32;
    [Min(2)] public int height = 32;
    [Min(0.25f)] public float cellSize = 1f;
    public int seed = 12345;
}
