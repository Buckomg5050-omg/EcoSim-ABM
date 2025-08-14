using System.Collections.Generic;
using UnityEngine;

public class RandomWalkerAgent : AgentBase
{
    [Header("Foraging")]
    [Min(0f)] public float harvestPerStep = 0.4f;

    [Header("Policy")]
    public MonoBehaviour policyBehaviour; // IAgentPolicy or IActionPolicy
    private IAgentPolicy cellPolicy;
    private IActionPolicy actionPolicy;

    public override void Initialize(GridManager grid, System.Random rng, Vector2Int? start = null, EnvironmentGrid env = null)
    {
        base.Initialize(grid, rng, start, env);

        // If none assigned, attach default (epsilon-greedy cell policy)
        if (policyBehaviour == null)
            policyBehaviour = gameObject.AddComponent<EpsilonGreedyEnergyPolicy>();

        // Try both interfaces
        cellPolicy = policyBehaviour as IAgentPolicy;
        actionPolicy = policyBehaviour as IActionPolicy;

        if (cellPolicy != null) cellPolicy.Initialize(this, grid, this.env, this.rng);
        if (actionPolicy != null) actionPolicy.Initialize(this, grid, this.env, this.rng);

        if (cellPolicy == null && actionPolicy == null)
            Debug.LogError("Assigned policyBehaviour implements neither IAgentPolicy nor IActionPolicy.");
    }

    public override void Step()
    {
        // Eat at current cell → adds to body energy
        float gained = HarvestHere(harvestPerStep);
        AddReward(gained); // positive reward for eating
        GainEnergy(gained);

        // Candidate cell list for cell-based policies
        List<Vector2Int> options = null;
        if (cellPolicy != null)
        {
            options = new List<Vector2Int>(5);
            options.Add(GridPos);
            var dirs = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            for (int i = 0; i < dirs.Length; i++)
            {
                var p = GridPos + dirs[i];
                if (grid.InBounds(p)) options.Add(p);
            }
        }

        Vector2Int chosen = GridPos;

        if (actionPolicy != null)
        {
            var obs = AgentIO.BuildObservation(this, env);
            int action = Mathf.Clamp(actionPolicy.DecideAction(obs), 0, AgentIO.ActionSize - 1);
            chosen = AgentIO.ActionToCell(this, action);
        }
        else if (cellPolicy != null)
        {
            chosen = cellPolicy.Decide(GridPos, options);
        }
        else
        {
            // safety fallback: random stay/move
            int a = this.rng.Next(AgentIO.ActionSize);
            chosen = AgentIO.ActionToCell(this, a);
        }

        if (chosen != GridPos)
            MoveTo(chosen);
    }
}
