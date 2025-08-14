using System.Collections.Generic;
using UnityEngine;

public interface IAgentPolicy
{
    // Called once when the agent is initialized
    void Initialize(AgentBase agent, GridManager grid, EnvironmentGrid env, System.Random rng);

    // Decide where to move given the current cell and candidate options (current + neighbors)
    Vector2Int Decide(Vector2Int currentCell, List<Vector2Int> options);
}
