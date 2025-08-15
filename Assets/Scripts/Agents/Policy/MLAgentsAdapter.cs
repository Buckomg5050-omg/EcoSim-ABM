// Assets/Scripts/Agents/Policy/MLAgentsAdapter.cs
#if MLAGENTS_PRESENT
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies; // for BehaviorParameters & BrainParameters

[RequireComponent(typeof(BehaviorParameters))]
public class MLAgentsAdapter : Agent
{
    public AgentBase body;
    public EnvironmentGrid env;

    private int lastAction;
    private bool hasAction;

    // --- NEW: Set BehaviorParameters before base Agent initializes ---
    protected override void Awake()
    {
        var bp = GetComponent<BehaviorParameters>();
        var brain = bp.BrainParameters;

        // Observation: 6 floats from AgentIO
        brain.VectorObservationSize = AgentIO.ObservationSize; // = 6

        // Actions: 1 discrete branch with 5 actions (Stay/Up/Right/Down/Left)
        brain.ActionSpec = ActionSpec.MakeDiscrete(AgentIO.ActionSize); // = 5

        // (Optional) ensure single stack
        brain.NumStackedVectorObservations = 1;

        // Default a name if empty (used by trainer YAML)
        if (string.IsNullOrEmpty(bp.BehaviorName))
            bp.BehaviorName = "EcoAgent";

        // Now let base Agent do its setup
        base.Awake();
    }

    public override void Initialize()
    {
        if (!body) body = GetComponent<AgentBase>();
        if (!env)  env  = Object.FindFirstObjectByType<EnvironmentGrid>();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!body) return;
        float[] obs = AgentIO.BuildObservation(body, env); // size 6
        for (int i = 0; i < obs.Length; i++) sensor.AddObservation(obs[i]);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Discrete branch 0: 0..4
        lastAction = Mathf.Clamp(actions.DiscreteActions[0], 0, AgentIO.ActionSize - 1);
        hasAction = true;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Arrow-key heuristic (default Stay)
        var da = actionsOut.DiscreteActions;
        da[0] = 0;
        if      (Input.GetKey(KeyCode.UpArrow))    da[0] = 1;
        else if (Input.GetKey(KeyCode.RightArrow)) da[0] = 2;
        else if (Input.GetKey(KeyCode.DownArrow))  da[0] = 3;
        else if (Input.GetKey(KeyCode.LeftArrow))  da[0] = 4;
    }

    // Read the most recent action (consumes it)
    public int ConsumeLastActionOrDefault(int fallback)
    {
        if (hasAction) { hasAction = false; return lastAction; }
        return fallback;
    }

    // Sync our sim reward into the Agent each tick
    public void SyncRewardFrom(AgentBase agent)
    {
        AddReward(agent.LastReward);
    }

    // Ask ML-Agents for a decision once per sim tick
    public void RequestDecisionTick()
    {
        RequestDecision();
    }
}
#endif
