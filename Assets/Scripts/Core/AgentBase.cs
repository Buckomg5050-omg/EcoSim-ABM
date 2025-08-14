using System.Collections.Generic;
using UnityEngine;

public abstract class AgentBase : MonoBehaviour
{
    protected GridManager grid;
    protected System.Random rng;
    protected EnvironmentGrid env;

    public Vector2Int GridPos { get; private set; }

    // env is optional; if null we'll find one in the scene
    public virtual void Initialize(GridManager grid, System.Random rng, Vector2Int? start = null, EnvironmentGrid env = null)
    {
        this.grid = grid;
        this.rng = rng;
        this.env = env ?? Object.FindFirstObjectByType<EnvironmentGrid>();

        GridPos = start ?? grid.RandomCell(rng);
        transform.position = grid.GridToWorld(GridPos);
        name = string.IsNullOrEmpty(name) ? GetType().Name : name;
    }

    public abstract void Step();

    protected void MoveTo(Vector2Int newPos)
    {
        if (!grid.InBounds(newPos)) return;
        GridPos = newPos;
        transform.position = grid.GridToWorld(GridPos);
    }

    // --- Simple environment helpers ---
    protected float SenseEnergy(Vector2Int pos) => env ? env.GetEnergy(pos) : 0f;
    protected float HarvestHere(float amount) => env ? env.Harvest(GridPos, amount) : 0f;

    // --- NEW: neighborhood helper (cardinal + optional center) ---
    protected IEnumerable<Vector2Int> CardinalNeighborhood(bool includeSelf = true)
    {
        if (includeSelf) yield return GridPos;

        var dirs = new[]
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            var p = GridPos + dirs[i];
            if (grid.InBounds(p)) yield return p;
        }
    }
}
