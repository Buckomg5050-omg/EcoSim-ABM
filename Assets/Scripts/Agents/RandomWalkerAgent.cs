using UnityEngine;

public class RandomWalkerAgent : AgentBase
{
    [Header("Foraging")]
    [Min(0f)] public float harvestPerStep = 0.4f;   // was 0.1

    static readonly Vector2Int[] dirs = new[]
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left, Vector2Int.zero
    };

    public override void Step()
    {
        // Nibble energy at current cell
        HarvestHere(harvestPerStep);

        var d = dirs[rng.Next(dirs.Length)];
        var target = GridPos + d;

        // 90% chance to move if inside bounds, else stay
        if (rng.NextDouble() < 0.9 && grid.InBounds(target))
            MoveTo(target);
    }
}
