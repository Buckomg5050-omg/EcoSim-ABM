using UnityEngine;

public abstract class AgentBase : MonoBehaviour
{
    protected GridManager grid;
    protected System.Random rng;

    public Vector2Int GridPos { get; private set; }

    public virtual void Initialize(GridManager grid, System.Random rng, Vector2Int? start = null)
    {
        this.grid = grid;
        this.rng = rng;

        // Use deterministic random start unless a position is provided
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
}
