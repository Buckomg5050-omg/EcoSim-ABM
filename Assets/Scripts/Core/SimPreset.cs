using UnityEngine;

[CreateAssetMenu(menuName = "EcoSim/Sim Preset", fileName = "SimPreset")]
public class SimPreset : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Preset";

    [Header("Grid Seed")]
    public int seed = 12345;

    [Header("Environment")]
    public float maxEnergyPerCell = 1f;
    [Range(0f, 1f)] public float initialFill = 0.5f;
    public float noiseScale = 10f;
    [Min(1)] public int noiseOctaves = 1;
    [Range(0f, 1f)] public float noisePersistence = 0.5f;
    public float regenPerTick = 0.01f;

    [Header("Simulation")]
    [Min(1)] public int numberOfAgents = 1;
    [Range(0.5f, 60f)] public float ticksPerSecond = 5f;

    [Header("Reproduction")]
    public bool enableReproduction = true;
    [Min(0.01f)] public float reproduceThreshold = 8f;
    [Range(0.05f, 0.95f)] public float offspringEnergyFraction = 0.4f;
    [Min(1)] public int maxAgents = 200;

    [Header("Policy")]
    public SimulationController.PolicyType policy = SimulationController.PolicyType.EpsilonGreedy;
}
