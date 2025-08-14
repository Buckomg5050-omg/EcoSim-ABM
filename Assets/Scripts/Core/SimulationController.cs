using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationController : MonoBehaviour
{
    [Header("Run Control")]
    [Range(0.5f, 60f)] public float ticksPerSecond = 5f;
    [Min(1)] public int numberOfAgents = 1;
    public bool startPaused = false;

    private GridManager grid;
    private System.Random rng;
    private EnvironmentGrid env;
    private readonly List<AgentBase> agents = new();
    private Coroutine loop;
    private bool paused;
    private Transform agentsRoot;

    private bool showReadout = true;
    private Vector2Int mouseCell;
    private bool hasMouseCell;

    private void Awake()
    {
        grid = Object.FindFirstObjectByType<GridManager>();
        if (grid == null || grid.config == null)
        {
            Debug.LogError("SimulationController: add a GridManager in the scene and assign a GridConfig.");
            enabled = false;
            return;
        }

        env = Object.FindFirstObjectByType<EnvironmentGrid>();

        rng = new System.Random(grid.config.seed);
        SpawnAgents();

        paused = startPaused;
    }

    private void OnEnable()
    {
        loop = StartCoroutine(TickLoop());
    }

    private void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
    }

    private void Update()
    {
        // Space toggles pause
        if (Input.GetKeyDown(KeyCode.Space))
            paused = !paused;

        hasMouseCell = TryGetMouseCell(out mouseCell);
    }

    private bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;
        var cam = Camera.main;
        if (cam == null || grid == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        // Ground plane at y=0
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            var c = grid.WorldToCell(hit);
            if (grid.InBounds(c))
            {
                cell = c;
                return true;
            }
        }
        return false;
    }

    IEnumerator TickLoop()
    {
        while (true)
        {
            if (!paused)
            {
                TickOnce();
                yield return new WaitForSeconds(1f / Mathf.Max(0.5f, ticksPerSecond));
            }
            else
            {
                yield return null;
            }
        }
    }

    private void TickOnce()
    {
        // Decision/movement
        for (int i = 0; i < agents.Count; i++)
            agents[i].Step();

        // Metabolism (may mark agents dead)
        for (int i = 0; i < agents.Count; i++)
            agents[i].ApplyMetabolism();

        // Cull dead agents (destroy GO and remove from list)
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            if (agents[i].IsDead)
            {
                var go = agents[i].gameObject;
                agents.RemoveAt(i);
                if (go != null) Destroy(go);
            }
        }

        // Environment regrows a bit each tick, capped internally
        env?.TickRegen();
    }

    // ---------- Runtime controls ----------

    private void SpawnAgents()
    {
        if (agentsRoot != null) Destroy(agentsRoot.gameObject);
        agentsRoot = new GameObject("Agents").transform;
        agents.Clear();

        for (int i = 0; i < numberOfAgents; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Agent_{i:D3}";
            go.transform.SetParent(agentsRoot, worldPositionStays: false);
            go.transform.localScale = Vector3.one * (0.6f * grid.config.cellSize);

            // Assign deterministic tint by index
            var rend = go.GetComponent<Renderer>();
            var mat = new Material(rend.sharedMaterial);
            var c = ColorFromIndex(i);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            rend.material = mat;

            var a = go.AddComponent<RandomWalkerAgent>();
            a.Initialize(grid, rng);
            agents.Add(a);
        }
    }

    private void DestroyAgents()
    {
        if (agentsRoot != null)
            Destroy(agentsRoot.gameObject);
        agents.Clear();
    }

    public void ResetAgents()
    {
        // Recreate RNG from current seed so spawns are deterministic for that seed
        rng = new System.Random(grid.config.seed);
        DestroyAgents();
        SpawnAgents();
    }

    // Compute a pleasant camera pose that frames the grid
    private void FrameCamera()
    {
        var cam = Camera.main;
        if (cam == null || grid == null) return;

        float sizeX = grid.config.width * grid.config.cellSize;
        float sizeZ = grid.config.height * grid.config.cellSize;
        Vector3 center = new Vector3(
            sizeX * 0.5f - 0.5f * grid.config.cellSize,
            0f,
            sizeZ * 0.5f - 0.5f * grid.config.cellSize
        );

        float maxSize = Mathf.Max(sizeX, sizeZ);
        float dist = maxSize * 1.2f;   // padding out from center
        float height = maxSize * 0.75f;

        Vector3 offset = new Vector3(-dist, height, -dist); // look from a diagonal
        cam.transform.position = center + offset;
        cam.transform.LookAt(center);
    }

    // Golden-ratio hue spacing for stable, distinct colors
    private static Color ColorFromIndex(int i)
    {
        float hue = Mathf.Repeat((float)(i * 0.61803398875f), 1f);
        float sat = 0.65f;
        float val = 0.9f;
        var c = Color.HSVToRGB(hue, sat, val);
        c.a = 1f;
        return c;
    }

    // ---------- Debug UI ----------
   private void OnGUI()
{
    GUI.Label(new Rect(10, 10, 540, 24),
        $"Ticks/sec: {ticksPerSecond:0.0}   Agents: {agents.Count}   {(paused ? "Paused" : "Running")} (Space toggles)");

    if (GUI.Button(new Rect(10, 40, 80, 24), paused ? "Resume" : "Pause"))
        paused = !paused;

    if (paused && GUI.Button(new Rect(100, 40, 80, 24), "Step"))
        TickOnce();

    if (GUI.Button(new Rect(190, 40, 80, 24), "Reset"))
        ResetAgents();

    if (GUI.Button(new Rect(280, 40, 80, 24), "Frame"))
        FrameCamera();

    // --- NEW: Readouts ---
    if (showReadout && env != null)
    {
        if (hasMouseCell)
        {
            float e = env.GetEnergy(mouseCell);
            GUI.Label(new Rect(10, 70, 400, 22),
                $"Mouse cell: {mouseCell.x},{mouseCell.y}  Energy: {e:0.000}");
        }

        if (agents.Count > 0)
        {
            var a0 = agents[0];
            float ea = env.GetEnergy(a0.GridPos);
            GUI.Label(new Rect(10, 92, 400, 22),
                $"Agent_000 at {a0.GridPos.x},{a0.GridPos.y}  Cell E: {ea:0.000}");

            // NEW: internal energy
            GUI.Label(new Rect(10, 114, 400, 22),
                $"Agent_000 body energy: {a0.Energy:0.000}");
        }
    }
}

}
