using System.Collections.Generic;
using UnityEngine;

public class RandomWalkerAgent : AgentBase
{
    [Header("Foraging")]
    [Min(0f)] public float harvestPerStep = 0.4f;

    [Header("Policy")]
    public MonoBehaviour policyBehaviour; // should implement IAgentPolicy
    private IAgentPolicy policy;

    public override void Initialize(GridManager grid, System.Random rng, Vector2Int? start = null, EnvironmentGrid env = null)
    {
        base.Initialize(grid, rng, start, env);

        // If none assigned in Inspector, attach a default one
        if (policyBehaviour == null)
            policyBehaviour = gameObject.AddComponent<EpsilonGreedyEnergyPolicy>();

        policy = policyBehaviour as IAgentPolicy;
        if (policy == null)
        {
            Debug.LogError("Assigned policyBehaviour does not implement IAgentPolicy.");
        }
        else
        {
            policy.Initialize(this, grid, this.env, this.rng);
        }
    }

    public override void Step()
    {
        // Eat from current cell → convert to body energy
        float gained = HarvestHere(harvestPerStep);
        GainEnergy(gained);

        // Candidate moves: stay + 4-cardinal
        List<Vector2Int> options = new();
        options.Add(GridPos);
        var dirs = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        for (int i = 0; i < dirs.Length; i++)
        {
            var p = GridPos + dirs[i];
            if (grid.InBounds(p)) options.Add(p);
        }

        Vector2Int chosen =
            (policy != null) ? policy.Decide(GridPos, options) : options[rng.Next(options.Count)];

        if (chosen != GridPos)
            MoveTo(chosen);
    }
}
