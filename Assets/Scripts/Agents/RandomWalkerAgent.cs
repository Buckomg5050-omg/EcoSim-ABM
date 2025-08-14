using UnityEngine;

public class RandomWalkerAgent : AgentBase
{
    static readonly Vector2Int[] dirs = new[]
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left, Vector2Int.zero
    };

    public override void Step()
    {
        // Nibble some energy at current cell (visible in gizmos as a small dip)
        HarvestHere(0.1f);

        var d = dirs[rng.Next(dirs.Length)];
        var target = GridPos + d;

        // 90% chance to move if inside bounds, else stay
        if (rng.NextDouble() < 0.9 && grid.InBounds(target))
            MoveTo(target);
    }
}
