using System.Collections.Generic;
using UnityEngine;

public class RandomWalkerAgent : AgentBase
{
    [Header("Foraging")]
    [Min(0f)] public float harvestPerStep = 0.4f;

    [Header("Decision Policy")]
    [Range(0f, 1f)] public float exploitProb = 0.85f; // chance to pick best-energy neighbor; else explore randomly

    public override void Step()
    {
        // Consume some energy at current cell (makes dents visible)
        HarvestHere(harvestPerStep);

        // Build candidate moves: stay + 4-cardinal neighbors
        List<Vector2Int> options = new();
        foreach (var p in CardinalNeighborhood(includeSelf: true))
            options.Add(p);

        Vector2Int chosen = GridPos;

        // With probability exploitProb, choose the neighbor with max energy; otherwise pick random
        if (rng.NextDouble() < exploitProb)
        {
            float best = float.NegativeInfinity;
            for (int i = 0; i < options.Count; i++)
            {
                float e = SenseEnergy(options[i]);
                if (e > best)
                {
                    best = e;
                    chosen = options[i];
                }
            }
        }
        else
        {
            chosen = options[rng.Next(options.Count)];
        }

        // Move (no-op if chosen == current cell)
        if (chosen != GridPos)
            MoveTo(chosen);
    }
}
