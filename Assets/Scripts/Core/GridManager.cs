using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Assign your GridConfig asset here")]
    public GridConfig config;

    private GameObject floor;

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("GridManager: assign a GridConfig in the Inspector.");
            enabled = false;
            return;
        }

        BuildFloor();
    }

    // Convert grid cell -> world position (cell anchored on floor)
    public Vector3 GridToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x * config.cellSize, 0f, cell.y * config.cellSize);
    }

    // Check if a cell is inside the grid bounds
    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < config.width && cell.y < config.height;
    }

    public Vector2Int RandomCell(System.Random rng)
    {
        return new Vector2Int(
            rng.Next(0, config.width),
            rng.Next(0, config.height)
        );
    }

    // Draw the grid lines in the Scene/Game view when Gizmos are on
    private void OnDrawGizmos()
    {
        if (config == null) return;

        Gizmos.color = Color.gray;
        float w = config.width * config.cellSize;
        float h = config.height * config.cellSize;

        // verticals
        for (int x = 0; x <= config.width; x++)
        {
            float xPos = x * config.cellSize;
            Gizmos.DrawLine(new Vector3(xPos, 0, 0), new Vector3(xPos, 0, h));
        }
        // horizontals
        for (int y = 0; y <= config.height; y++)
        {
            float yPos = y * config.cellSize;
            Gizmos.DrawLine(new Vector3(0, 0, yPos), new Vector3(w, 0, yPos));
        }
    }

    private void BuildFloor()
    {
        // Clean up any previous floor created at runtime
        if (floor != null) Destroy(floor);

        var sizeX = config.width * config.cellSize;
        var sizeZ = config.height * config.cellSize;

        floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(transform, false);

        // Unity Plane is 10x10 units at scale 1
        floor.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);
        floor.transform.position = new Vector3(
            sizeX * 0.5f - 0.5f * config.cellSize,
            0f,
            sizeZ * 0.5f - 0.5f * config.cellSize
        );
    }
    public Vector2Int WorldToCell(Vector3 worldPos)
{
    int x = Mathf.FloorToInt(worldPos.x / config.cellSize);
    int y = Mathf.FloorToInt(worldPos.z / config.cellSize);
    return new Vector2Int(x, y);
}

}
