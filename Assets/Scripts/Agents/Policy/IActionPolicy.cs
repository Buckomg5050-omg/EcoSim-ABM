using UnityEngine;

public interface IActionPolicy
{
    // Called once at agent init
    void Initialize(AgentBase agent, GridManager grid, EnvironmentGrid env, System.Random rng);

    // Decide a discrete action (0..4) given the current observation
    int DecideAction(float[] observation);
}
