// Assets/Scripts/Agents/Policy/MLAgentsAdapter.cs
#if MLAGENTS_PRESENT
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies; // <-- added

[RequireComponent(typeof(BehaviorParameters))]
public class MLAgentsAdapter : Agent
{
    public AgentBase body;
    public EnvironmentGrid env;

    private int lastAction;
    private bool hasAction;

    public override void Initialize()
    {
        if (!body) body = GetComponent<AgentBase>();
        if (!env)  env  = Object.FindFirstObjectByType<EnvironmentGrid>();
        // BehaviorParameters component on this GameObject will define action/obs sizes.
        // We don't force them here to avoid version-specific API churn.
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!body) return;
        float[] obs = AgentIO.BuildObservation(body, env);
        for (int i = 0; i < obs.Length; i++) sensor.AddObservation(obs[i]); // size = 6
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Discrete branch 0 has 5 actions: 0..4
        lastAction = Mathf.Clamp(actions.DiscreteActions[0], 0, AgentIO.ActionSize - 1);
        hasAction = true;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Arrow keys for quick manual testing; default = Stay
        var da = actionsOut.DiscreteActions;
        da[0] = 0;
        if      (Input.GetKey(KeyCode.UpArrow))    da[0] = 1;
        else if (Input.GetKey(KeyCode.RightArrow)) da[0] = 2;
        else if (Input.GetKey(KeyCode.DownArrow))  da[0] = 3;
        else if (Input.GetKey(KeyCode.LeftArrow))  da[0] = 4;
    }

    // Called by our policy to read the most recent decision (consumes it)
    public int ConsumeLastActionOrDefault(int fallback)
    {
        if (hasAction) { hasAction = false; return lastAction; }
        return fallback;
    }

    // Called once per sim tick to sync reward computed by our sim
    public void SyncRewardFrom(AgentBase agent)
    {
        AddReward(agent.LastReward);
    }

    // Called once per sim tick *before* agent.Step() to trigger a decision request
    public void RequestDecisionTick()
    {
        RequestDecision();
    }
}
#endif
