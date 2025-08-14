// Assets/Scripts/Core/AgentBase.cs
using System.Collections.Generic;
using UnityEngine;

public abstract class AgentBase : MonoBehaviour
{
    protected GridManager grid;
    protected System.Random rng;
    protected EnvironmentGrid env;

    [Header("Energy")]
    [Min(0.01f)] public float maxEnergy = 10f;
    [Min(0f)] public float startEnergy = 5f;
    [Min(0f)] public float metabolismPerTick = 0.2f;

    private float bodyEnergy;
    public float Energy => bodyEnergy;
    public bool IsDead { get; private set; }

    public Vector2Int GridPos { get; private set; }

    // NEW: public getter to expose the grid
    public GridManager Grid => grid;

    // env is optional; if null we'll find one in the scene
    public virtual void Initialize(GridManager grid, System.Random rng, Vector2Int? start = null, EnvironmentGrid env = null)
    {
        this.grid = grid;
        this.rng = rng;
        this.env = env ?? Object.FindFirstObjectByType<EnvironmentGrid>();

        GridPos = start ?? grid.RandomCell(rng);
        transform.position = grid.GridToWorld(GridPos);
        name = string.IsNullOrEmpty(name) ? GetType().Name : name;

        bodyEnergy = Mathf.Clamp(startEnergy, 0f, maxEnergy);
        IsDead = false;
    }

    public abstract void Step();

    protected void MoveTo(Vector2Int newPos)
    {
        if (!grid.InBounds(newPos)) return;
        GridPos = newPos;
        transform.position = grid.GridToWorld(GridPos);
    }

    // --- Environment helpers ---
    protected float SenseEnergy(Vector2Int pos) => env ? env.GetEnergy(pos) : 0f;
    protected float HarvestHere(float amount) => env ? env.Harvest(GridPos, amount) : 0f;

    // --- Energy helpers ---
    protected void GainEnergy(float amount)
    {
        if (IsDead) return;
        bodyEnergy = Mathf.Min(maxEnergy, bodyEnergy + Mathf.Max(0f, amount));
    }

    public void ApplyMetabolism()
    {
        if (IsDead) return;
        bodyEnergy -= metabolismPerTick;
        if (bodyEnergy <= 0f)
        {
            bodyEnergy = 0f;
            IsDead = true;
        }
    }

    /// <summary>
    /// If Energy >= threshold, split off a fraction into offspringEnergy and keep the rest.
    /// Returns true if a split happened.
    /// </summary>
    public bool TrySplitForOffspring(float threshold, float fraction, out float offspringEnergy)
    {
        offspringEnergy = 0f;
        if (IsDead || Energy < threshold) return false;

        fraction = Mathf.Clamp01(fraction);
        if (fraction <= 0f) return false;

        float give = bodyEnergy * fraction;
        bodyEnergy -= give;
        offspringEnergy = give;

        // clamp parent back within bounds just in case
        bodyEnergy = Mathf.Clamp(bodyEnergy, 0f, maxEnergy);
        return offspringEnergy > 0f;
    }

    // Neighborhood helper (cardinal + optional center)
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
