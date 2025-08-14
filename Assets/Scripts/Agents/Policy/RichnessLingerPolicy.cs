using System.Collections.Generic;
using UnityEngine;

public class RichnessLingerPolicy : MonoBehaviour, IAgentPolicy
{
    [Header("Behavior")]
    [Range(0f, 1f)] public float lingerThresholdFrac = 0.6f; // stay if current cell >= 60% of max cell energy
    [Range(0f, 1f)] public float exploreProb = 0.10f;        // chance to pick a random option anyway

    private GridManager grid;
    private EnvironmentGrid env;
    private System.Random rng;

    public void Initialize(AgentBase agent, GridManager grid, EnvironmentGrid env, System.Random rng)
    {
        this.grid = grid;
        this.env = env;
        this.rng  = rng;
    }

    public Vector2Int Decide(Vector2Int currentCell, List<Vector2Int> options)
    {
        if (options == null || options.Count == 0) return currentCell;

        // Occasional exploration
        if (rng.NextDouble() < exploreProb)
            return options[rng.Next(options.Count)];

        // Linger if current cell is sufficiently rich
        float currentE = env ? env.GetEnergy(currentCell) : 0f;
        float maxCellE = (env != null) ? Mathf.Max(0.0001f, env.maxEnergyPerCell) : 1f;
        if (currentE >= lingerThresholdFrac * maxCellE)
            return currentCell;

        // Otherwise, choose the best-energy option (ties broken randomly)
        float best = float.NegativeInfinity;
        List<Vector2Int> bests = new();
        for (int i = 0; i < options.Count; i++)
        {
            float e = env ? env.GetEnergy(options[i]) : 0f;
            if (e > best + 1e-6f)
            {
                best = e;
                bests.Clear();
                bests.Add(options[i]);
            }
            else if (Mathf.Abs(e - best) <= 1e-6f)
            {
                bests.Add(options[i]);
            }
        }
        return bests.Count > 0 ? bests[rng.Next(bests.Count)] : currentCell;
    }
}
