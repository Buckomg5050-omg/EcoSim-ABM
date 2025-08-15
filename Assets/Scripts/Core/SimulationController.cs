// Assets/Scripts/Core/SimulationController.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationController : MonoBehaviour
{
    [Header("Run Control")]
    [Range(0.5f, 60f)] public float ticksPerSecond = 5f;
    [Min(1)] public int numberOfAgents = 1;
    public bool startPaused = true;

    [Header("Reproduction")]
    public bool enableReproduction = true;
    [Min(0.01f)] public float reproduceThreshold = 8f;
    [Range(0.05f, 0.95f)] public float offspringEnergyFraction = 0.4f;
    [Min(1)] public int maxAgents = 200;

    [Header("Logging")]
    public bool enableCsvLogging = true;
    public bool newLogOnReset = true;
    [Min(1)] public int logFlushEvery = 60;

    [Header("Population Chart")]
    public bool showPopChart = true;
    [Range(60, 1024)] public int chartWidth = 320;
    [Range(40, 256)] public int chartHeight = 80;

    // Enum (no Header attribute on enums)
    public enum PolicyType { EpsilonGreedy, RichnessLinger, ObservationGreedy, MLAgents }

    [Header("Policy")]
    public PolicyType policy = PolicyType.EpsilonGreedy;

    [Header("Reward")]
    public float metabolismPenaltyScale = 1f; // reward -= scale * metabolismPerTick
    public float deathPenalty = 1f;           // applied once on death

    [Header("Episodes")]
    public bool enableEpisodes = true;
    [Min(1)] public int maxTicksPerEpisode = 2000;
    public bool resetWhenAllDead = true;
    public bool resetWhenMaxTicks = true;

    private int episodeIndex = 0;

    private GridManager grid;
    private EnvironmentGrid env;
    private System.Random rng;
    private readonly List<AgentBase> agents = new();
    private Coroutine loop;
    private bool paused;
    private Transform agentsRoot;

    // Readouts
    private bool showReadout = true;
    private Vector2Int mouseCell;
    private bool hasMouseCell;

    // Ticks & logging
    private int tick;
    private RunLogger logger;
    private string logPathShown;
    private bool loggingStarted;
    private bool loggingEnabled; // runtime switch; controls whether we log at all
    private bool newLogArmed; // when true, roll over to a new CSV on the next tick

    // Pop history & chart
    private readonly List<int> popHistory = new();
    private Texture2D chartTex;
    private Color32[] chartPixels;
    private bool chartDirty;
    private int lastWindowMax = 1;

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

        // Logging: instantiate holder only; don't start until first tick
        if (enableCsvLogging && logger == null)
            logger = new RunLogger();
        loggingEnabled = enableCsvLogging;  // runtime toggle follows the inspector default
        loggingStarted = false;

        // Init counters/series
        tick = 0;
        popHistory.Clear();
        // (no RecordPopulation here; we start after the first tick)

        paused = startPaused;
    }

    private void OnEnable()
    {
        loop = StartCoroutine(TickLoop());
    }

    private void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
        logger?.Close();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            paused = !paused;

        hasMouseCell = TryGetMouseCell(out mouseCell);
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
        // Reset per-step rewards
        for (int i = 0; i < agents.Count; i++)
            agents[i].ResetStepReward();

        // ML-Agents hook: request decisions before Steps
        #if MLAGENTS_PRESENT
        for (int i = 0; i < agents.Count; i++)
        {
            var adapter = agents[i].GetComponent<MLAgentsAdapter>();
            if (adapter != null) adapter.RequestDecisionTick();
        }
        #endif

        // 1) Decision/movement
        for (int i = 0; i < agents.Count; i++)
            agents[i].Step();

        // 2) Metabolism (may mark agents dead)
        for (int i = 0; i < agents.Count; i++)
            agents[i].ApplyMetabolism();

        // 2b) Metabolism penalty to reward
        for (int i = 0; i < agents.Count; i++)
            agents[i].AddReward(-metabolismPenaltyScale * agents[i].metabolismPerTick);

        // 3) Cull dead (apply one-time death penalty)
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            if (agents[i].IsDead)
            {
                agents[i].AddReward(-deathPenalty);
                var go = agents[i].gameObject;
                agents.RemoveAt(i);
                if (go != null) Destroy(go);
            }
        }

        // ML-Agents hook: sync rewards after potential deaths (but before logging)
        #if MLAGENTS_PRESENT
        for (int i = 0; i < agents.Count; i++)
        {
            var adapter = agents[i].GetComponent<MLAgentsAdapter>();
            if (adapter != null) adapter.SyncRewardFrom(agents[i]);
        }
        #endif

        // 4) Reproduction (energy split)
        if (enableReproduction && agents.Count < maxAgents)
        {
            var births = new List<(Vector2Int cell, float energy)>();

            for (int i = 0; i < agents.Count; i++)
            {
                if (agents.Count + births.Count >= maxAgents) break;

                if (agents[i].TrySplitForOffspring(reproduceThreshold, offspringEnergyFraction, out float eOff))
                {
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

        // 6) Bookkeeping (logging with optional rollover)
        if (loggingEnabled)
        {
            // If a rollover was requested and we were logging, close so a fresh one starts below
            if (newLogArmed && loggingStarted)
            {
                logger?.Close();
                loggingStarted = false;
            }

            if (!loggingStarted)
            {
                if (logger == null) logger = new RunLogger();
                logger.StartNew("run", grid.config.seed, grid.config, logFlushEvery);
                logPathShown = logger.LogPath;
                loggingStarted = true;
                newLogArmed = false; // consume the arm
            }
        }

        tick++;
        RecordPopulation(); // sample + CSV + chart mark dirty

        // 7) Episode termination checks
        if (enableEpisodes)
        {
            bool allDead = agents.Count == 0;
            bool hitMax = tick >= maxTicksPerEpisode;

            if ((resetWhenAllDead && allDead) || (resetWhenMaxTicks && hitMax))
                ResetEpisode();
        }
    }

    private void ResetEpisode()
    {
        episodeIndex++;
        ResetAgents(); // clears tick/history, (re)logging, and respawns
    }

    // ---------- Helpers ----------
    private void SetPolicyAndReset(PolicyType p)
    {
        if (policy == p) return;
        policy = p;
        ResetAgents(); // respawns with the new policy selection
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

    private bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;
        var cam = Camera.main;
        if (cam == null || grid == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
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

    private void RecordPopulation()
    {
        // history capped to chartWidth samples
        popHistory.Add(agents.Count);
        if (popHistory.Count > chartWidth) popHistory.RemoveAt(0);
        chartDirty = true;

        if (loggingEnabled && loggingStarted) logger?.LogTick(tick, agents.Count);
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

        // Tint
        var rend = go.GetComponent<Renderer>();
        var mat = new Material(rend.sharedMaterial);
        var c = ColorFromIndex(agents.Count);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", c);
        rend.material = mat;

        var a = go.AddComponent<RandomWalkerAgent>();
        if (startEnergyOverride.HasValue)
            a.startEnergy = Mathf.Min(a.maxEnergy, Mathf.Max(0f, startEnergyOverride.Value));

        // Choose and attach policy BEFORE Initialize so the agent uses it
        MonoBehaviour pol = null;
        switch (policy)
        {
            case PolicyType.EpsilonGreedy:
                pol = go.AddComponent<EpsilonGreedyEnergyPolicy>();
                break;
            case PolicyType.RichnessLinger:
                pol = go.AddComponent<RichnessLingerPolicy>();
                break;
            case PolicyType.ObservationGreedy:
                pol = go.AddComponent<GreedyObsPolicy>();
                break;
            case PolicyType.MLAgents:
            #if MLAGENTS_PRESENT
                go.AddComponent<MLAgentsAdapter>();            // ML Agent component
                pol = go.AddComponent<MLAgentsActionPolicy>(); // IActionPolicy that reads adapter's action
                break;
            #else
                Debug.LogWarning("MLAgents not present; falling back to GreedyObsPolicy.");
                pol = go.AddComponent<GreedyObsPolicy>();
                break;
            #endif
        }

        a.policyBehaviour = pol;
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

        // Reset counters & chart
        tick = 0;
        popHistory.Clear();
        chartDirty = true;

        // Logging will start on first tick after reset if enabled
        loggingStarted = false;
        if (enableCsvLogging && logger == null)
            logger = new RunLogger();
        loggingEnabled = enableCsvLogging; // keep inspector as the default after reset

        // (no StartNew and no RecordPopulation here)
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

    // ---------- Chart drawing ----------
    private void EnsureChartTexture()
    {
        if (chartTex != null && chartTex.width == chartWidth && chartTex.height == chartHeight) return;
        chartTex = new Texture2D(chartWidth, chartHeight, TextureFormat.RGBA32, false);
        chartTex.wrapMode = TextureWrapMode.Clamp;
        chartPixels = new Color32[chartWidth * chartHeight];
        chartDirty = true;
    }

    private void RebuildChartTexture()
    {
        if (!showPopChart) return;
        EnsureChartTexture();

        // Clear background (semi-transparent dark)
        int total = chartPixels.Length;
        for (int i = 0; i < total; i++) chartPixels[i] = new Color32(0, 0, 0, 160);

        if (popHistory.Count >= 1)
        {
            int count = popHistory.Count;
            int maxVal = 1;
            for (int i = 0; i < count; i++) if (popHistory[i] > maxVal) maxVal = popHistory[i];
            lastWindowMax = maxVal;

            // Plot as columns/resampled
            for (int x = 0; x < chartWidth; x++)
            {
                int idx = Mathf.RoundToInt(Mathf.Lerp(0, count - 1, (float)x / (chartWidth - 1)));
                float v = popHistory[idx];
                int yVal = Mathf.Clamp(Mathf.RoundToInt((v / maxVal) * (chartHeight - 1)), 0, chartHeight - 1);

                int px = x;
                int pyTop = (chartHeight - 1 - yVal);
                for (int y = pyTop; y < chartHeight; y++)
                {
                    int ii = y * chartWidth + px;
                    // lighter for the column, bright white at the tip
                    if (y == pyTop) chartPixels[ii] = new Color32(255, 255, 255, 255);
                    else chartPixels[ii] = new Color32(220, 220, 220, 80);
                }
            }
        }

        chartTex.SetPixels32(chartPixels);
        chartTex.Apply(false, false);
        chartDirty = false;
    }

    // ---------- Debug UI ----------
    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 860, 24),
            $"Ep {episodeIndex}  |  Ticks/sec: {ticksPerSecond:0.0}   Agents: {agents.Count}/{maxAgents}   {(paused ? "Paused" : "Running")} (Space toggles)");

        if (GUI.Button(new Rect(10, 40, 80, 24), paused ? "Resume" : "Pause"))
            paused = !paused;

        if (paused && GUI.Button(new Rect(100, 40, 80, 24), "Step"))
            TickOnce();

        if (GUI.Button(new Rect(190, 40, 80, 24), "Reset"))
            ResetAgents();

        if (GUI.Button(new Rect(280, 40, 80, 24), "Frame"))
            FrameCamera();

        // --- Policy row ---
        float bx = 370f, by = 40f, bw = 110f, bh = 24f;
        if (GUI.Button(new Rect(bx, by, bw, bh), policy == PolicyType.EpsilonGreedy ? "ε-Greedy ✓" : "ε-Greedy"))
            SetPolicyAndReset(PolicyType.EpsilonGreedy);
        bx += bw + 6f;

        if (GUI.Button(new Rect(bx, by, bw, bh), policy == PolicyType.RichnessLinger ? "Linger ✓" : "Linger"))
            SetPolicyAndReset(PolicyType.RichnessLinger);
        bx += bw + 6f;

        if (GUI.Button(new Rect(bx, by, bw, bh), policy == PolicyType.ObservationGreedy ? "ObsGreedy ✓" : "ObsGreedy"))
            SetPolicyAndReset(PolicyType.ObservationGreedy);
        bx += bw + 6f;

        if (GUI.Button(new Rect(bx, by, bw, bh), policy == PolicyType.MLAgents ? "ML-Agents ✓" : "ML-Agents"))
            SetPolicyAndReset(PolicyType.MLAgents);

        // Readouts
        if (showReadout && env != null)
        {
            if (hasMouseCell)
            {
                float e = env.GetEnergy(mouseCell);
                GUI.Label(new Rect(10, 70, 400, 22), $"Mouse cell: {mouseCell.x},{mouseCell.y}  Energy: {e:0.000}");
            }

            if (agents.Count > 0)
            {
                var a0 = agents[0];
                float ea = env.GetEnergy(a0.GridPos);
                GUI.Label(new Rect(10, 92, 400, 22), $"Agent_000 at {a0.GridPos.x},{a0.GridPos.y}  Cell E: {ea:0.000}");
                GUI.Label(new Rect(10, 114, 400, 22), $"Agent_000 body energy: {a0.Energy:0.000}");
                GUI.Label(new Rect(10, 136, 400, 22), $"Agent_000 reward: last {a0.LastReward:0.000}  cum {a0.CumulativeReward:0.000}");
            }
        }

        // Population chart + log path
        if (showPopChart)
        {
            if (chartDirty) RebuildChartTexture();
            if (chartTex != null)
            {
                GUI.Label(new Rect(10, 160, 300, 18), $"Population (window max: {lastWindowMax})");
                GUI.Box(new Rect(10, 174, chartWidth + 8, chartHeight + 8), GUIContent.none);
                GUI.DrawTexture(new Rect(14, 178, chartWidth, chartHeight), chartTex);
                if (enableCsvLogging)
                {
                    // Logging toggle button
                    string logLabel = (loggingEnabled && loggingStarted) ? "Logging: ON" :
                                    (loggingEnabled ? "Logging: armed" : "Logging: OFF");
                    if (GUI.Button(new Rect(14, 178 + chartHeight + 6, 120, 24), logLabel))
                    {
                        loggingEnabled = !loggingEnabled;
                        if (!loggingEnabled && loggingStarted)
                        {
                            logger?.Close();
                            loggingStarted = false;
                            logPathShown = null;
                        }
                    }

                    // Log file path display
                    if (!string.IsNullOrEmpty(logPathShown))
                    {
                        GUI.Label(new Rect(140, 178 + chartHeight + 11, 760, 20),
                            $"→ {System.IO.Path.GetFileName(logPathShown)}  (…/persistentDataPath/Logs)");
                    }

                    // "New Log" button (only show when logging is enabled)
                    if (loggingEnabled && GUI.Button(new Rect(140, 154 + chartHeight + 28, 100, 24), "New Log"))
                    {
                        // If logging is on, we'll close now and reopen next tick.
                        // If logging is OFF/armed, we'll just force the next file to be new.
                        newLogArmed = true;
                    }
                }
            }
        }
    }
}
