// Assets/Scripts/Core/SimulationController.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationController : MonoBehaviour
{
    [Header("Run Control")]
    [Range(0.5f, 60f)] public float ticksPerSecond = 5f;
    [Min(1)] public int numberOfAgents = 1;
    public bool startPaused = false;

    [Header("Reproduction")]
    public bool enableReproduction = true;
    [Min(0.01f)] public float reproduceThreshold = 8f;
    [Range(0.05f, 0.95f)] public float offspringEnergyFraction = 0.4f;
    [Min(1)] public int maxAgents = 200;

    private GridManager grid;
    private EnvironmentGrid env;
    private System.Random rng;
    private readonly List<AgentBase> agents = new();
    private Coroutine loop;
    private bool paused;
    private Transform agentsRoot;

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
        if (Input.GetKeyDown(KeyCode.Space))
            paused = !paused;
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
        // 1) Decision/movement
        for (int i = 0; i < agents.Count; i++)
            agents[i].Step();

        // 2) Metabolism (may mark agents dead)
        for (int i = 0; i < agents.Count; i++)
            agents[i].ApplyMetabolism();

        // 3) Cull dead
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            if (agents[i].IsDead)
            {
                var go = agents[i].gameObject;
                agents.RemoveAt(i);
                if (go != null) Destroy(go);
            }
        }

        // 4) Reproduction (energy split)
        if (enableReproduction && agents.Count < maxAgents)
        {
            var births = new List<(Vector2Int cell, float energy)>();

            for (int i = 0; i < agents.Count; i++)
            {
                if (agents.Count + births.Count >= maxAgents) break;

                if (agents[i].TrySplitForOffspring(reproduceThreshold, offspringEnergyFraction, out float eOff))
                {
                    // pick a good nearby cell for the baby (greedy toward higher energy)
                    var parentPos = agents[i].GridPos;
                    Vector2Int birthCell = ChooseBirthCell(parentPos);
                    births.Add((birthCell, eOff));
                }
            }

            for (int b = 0; b < births.Count; b++)
                SpawnOneAgent(births[b].cell, births[b].energy);
        }

        // 5) Environment regrows a bit each tick
        env?.TickRegen();
    }

    private Vector2Int ChooseBirthCell(Vector2Int center)
    {
        Vector2Int[] options = new[]
        {
            center,
            center + Vector2Int.up,
            center + Vector2Int.right,
            center + Vector2Int.down,
            center + Vector2Int.left
        };

        Vector2Int chosen = center;
        float best = float.NegativeInfinity;
        for (int i = 0; i < options.Length; i++)
        {
            var c = options[i];
            if (!grid.InBounds(c)) continue;

            float e = env ? env.GetEnergy(c) : 0f;
            if (e > best)
            {
                best = e;
                chosen = c;
            }
        }
        return chosen;
    }

    // ---------- Spawning ----------
    private void SpawnAgents()
    {
        if (agentsRoot != null) Destroy(agentsRoot.gameObject);
        agentsRoot = new GameObject("Agents").transform;
        agents.Clear();

        for (int i = 0; i < numberOfAgents; i++)
            SpawnOneAgent(startCell: null, startEnergyOverride: null);
    }

    private void SpawnOneAgent(Vector2Int? startCell, float? startEnergyOverride)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"Agent_{agents.Count:D3}";
        go.transform.SetParent(agentsRoot, worldPositionStays: false);
        go.transform.localScale = Vector3.one * (0.6f * grid.config.cellSize);

        // Deterministic tint by index
        var rend = go.GetComponent<Renderer>();
        var mat = new Material(rend.sharedMaterial);
        var c = ColorFromIndex(agents.Count);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        rend.material = mat;

        var a = go.AddComponent<RandomWalkerAgent>();
        if (startEnergyOverride.HasValue) a.startEnergy = Mathf.Min(a.maxEnergy, Mathf.Max(0f, startEnergyOverride.Value));

        // Use provided cell or seeded random
        a.Initialize(grid, rng, startCell, env);
        agents.Add(a);
    }

    public void ResetAgents()
    {
        // Recreate RNG from current seed so spawns are deterministic for that seed
        rng = new System.Random(grid.config.seed);
        if (agentsRoot != null) Destroy(agentsRoot.gameObject);
        agents.Clear();
        agentsRoot = null;
        SpawnAgents();
    }

    // ---------- Camera helper ----------
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
        float dist = maxSize * 1.2f;
        float height = maxSize * 0.75f;

        Vector3 offset = new Vector3(-dist, height, -dist);
        cam.transform.position = center + offset;
        cam.transform.LookAt(center);
    }

    // Golden-ratio hue spacing
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
        GUI.Label(new Rect(10, 10, 680, 24),
            $"Ticks/sec: {ticksPerSecond:0.0}   Agents: {agents.Count}/{maxAgents}   {(paused ? "Paused" : "Running")} (Space toggles)");

        if (GUI.Button(new Rect(10, 40, 80, 24), paused ? "Resume" : "Pause"))
            paused = !paused;

        if (paused && GUI.Button(new Rect(100, 40, 80, 24), "Step"))
            TickOnce();

        if (GUI.Button(new Rect(190, 40, 80, 24), "Reset"))
            ResetAgents();

        if (GUI.Button(new Rect(280, 40, 80, 24), "Frame"))
            FrameCamera();

        // Readouts (if env present)
        if (env != null && agents.Count > 0)
        {
            var a0 = agents[0];
            float ea = env.GetEnergy(a0.GridPos);
            GUI.Label(new Rect(10, 92, 400, 22), $"Agent_000 at {a0.GridPos.x},{a0.GridPos.y}  Cell E: {ea:0.000}");
            GUI.Label(new Rect(10, 114, 400, 22), $"Agent_000 body energy: {a0.Energy:0.000}");
        }
    }
}
