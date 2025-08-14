#if MLAGENTS_PRESENT
using UnityEngine;

// Bridges MLAgentsAdapter's chosen action to our IActionPolicy interface
public class MLAgentsActionPolicy : MonoBehaviour, IActionPolicy
{
    private MLAgentsAdapter adapter;

    public void Initialize(AgentBase agent, GridManager grid, EnvironmentGrid env, System.Random rng)
    {
        adapter = GetComponent<MLAgentsAdapter>();
        if (adapter == null)
            Debug.LogError("MLAgentsActionPolicy requires MLAgentsAdapter on the same GameObject.");
    }

    public int DecideAction(float[] observation)
    {
        // Fallback: greedy on obs if no adapter (shouldn't happen)
        int fallback = 0;
        float best = float.NegativeInfinity;
        for (int i = 0; i < 5 && i < observation.Length; i++)
            if (observation[i] > best) { best = observation[i]; fallback = i; }

        return adapter ? adapter.ConsumeLastActionOrDefault(fallback) : fallback;
    }
}
#endif
