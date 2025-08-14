using UnityEngine;

public class GreedyObsPolicy : MonoBehaviour, IActionPolicy
{
    public void Initialize(AgentBase agent, GridManager grid, EnvironmentGrid env, System.Random rng) { }

    public int DecideAction(float[] obs)
    {
        // obs[0..4] are energies for [cur,up,right,down,left]; pick argmax
        int bestIdx = 0;
        float best = float.NegativeInfinity;
        for (int i = 0; i < 5 && i < obs.Length; i++)
        {
            if (obs[i] > best) { best = obs[i]; bestIdx = i; }
        }
        return bestIdx; // 0..4
    }
}
