using System.Collections.Generic;
using UnityEngine;

public class EpsilonGreedyEnergyPolicy : MonoBehaviour, IAgentPolicy
{
    [Header("Policy")]
    [Range(0f, 1f)] public float exploitProb = 0.85f;

    private GridManager grid;
    private EnvironmentGrid env;
    private System.Random rng;

    public void Initialize(AgentBase agent, GridManager grid, EnvironmentGrid env, System.Random rng)
    {
        this.grid = grid;
        this.env = env;
        this.rng = rng;
    }

    public Vector2Int Decide(Vector2Int currentCell, List<Vector2Int> options)
    {
        if (options == null || options.Count == 0)
            return currentCell;

        // Explore randomly with (1 - exploitProb)
        if (rng.NextDouble() >= exploitProb)
            return options[rng.Next(options.Count)];

        // Otherwise choose the option with max environment energy
        float best = float.NegativeInfinity;
        Vector2Int chosen = currentCell;

        for (int i = 0; i < options.Count; i++)
        {
            var c = options[i];
            float e = env ? env.GetEnergy(c) : 0f;
            if (e > best)
            {
                best = e;
                chosen = c;
            }
        }

        return chosen;
    }
}
