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

        // Start in the center unless a position is provided
        int cx = Mathf.FloorToInt(grid.config.width * 0.5f);
        int cy = Mathf.FloorToInt(grid.config.height * 0.5f);
        GridPos = start ?? new Vector2Int(cx, cy);

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
