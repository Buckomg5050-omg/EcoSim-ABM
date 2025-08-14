using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationController : MonoBehaviour
{
    [Range(0.5f, 60f)] public float ticksPerSecond = 5f;
    [Min(1)] public int numberOfAgents = 1;

    private GridManager grid;
    private System.Random rng;
    private readonly List<AgentBase> agents = new();

    private Coroutine loop;

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
    }

    private void OnEnable()
    {
        loop = StartCoroutine(TickLoop());
    }

    private void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
    }

    IEnumerator TickLoop()
    {
        var wait = new WaitForSeconds(1f / ticksPerSecond);
        while (true)
        {
            for (int i = 0; i < agents.Count; i++)
                agents[i].Step();

            yield return wait;
        }
    }
}
