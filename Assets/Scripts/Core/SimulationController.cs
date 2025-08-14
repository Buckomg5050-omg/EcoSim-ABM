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
    private readonly List<AgentBase> agents = new();
    private Coroutine loop;
    private bool paused;

    private void Awake()
    {
        grid = FindObjectOfType<GridManager>();
        if (grid == null || grid.config == null)
        {
            Debug.LogError("SimulationController: add a GridManager in the scene and assign a GridConfig.");
            enabled = false;
            return;
        }

        rng = new System.Random(grid.config.seed);

        // Spawn simple capsule agents
        for (int i = 0; i < numberOfAgents; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Agent_{i:D3}";
            go.transform.localScale = Vector3.one * (0.6f * grid.config.cellSize);

            var a = go.AddComponent<RandomWalkerAgent>();
            a.Initialize(grid, rng);
            agents.Add(a);
        }

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
    }

    IEnumerator TickLoop()
    {
        while (true)
        {
            if (!paused)
            {
                TickOnce();
                // recompute wait each loop so runtime changes to ticksPerSecond apply
                yield return new WaitForSeconds(1f / Mathf.Max(0.5f, ticksPerSecond));
            }
            else
            {
                // while paused, just yield until unpaused
                yield return null;
            }
        }
    }

    private void TickOnce()
    {
        for (int i = 0; i < agents.Count; i++)
            agents[i].Step();
    }

    // Super-simple debug UI
    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 360, 24),
            $"Ticks/sec: {ticksPerSecond:0.0}   Agents: {agents.Count}   {(paused ? "Paused" : "Running")} (Space to toggle)");

        if (GUI.Button(new Rect(10, 40, 80, 24), paused ? "Resume" : "Pause"))
            paused = !paused;

        if (paused && GUI.Button(new Rect(100, 40, 80, 24), "Step"))
            TickOnce();
    }
}
