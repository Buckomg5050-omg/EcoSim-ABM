// Assets/Scripts/Core/AgentIO.cs
using UnityEngine;

public static class AgentIO
{
    public const int ObservationSize = 6; // [Ecur,Eup,Eright,Edown,Eleft,body]
    public const int ActionSize = 5;      // 0=Stay,1=Up,2=Right,3=Down,4=Left

    // Build normalized observation vector for the given agent
    public static float[] BuildObservation(AgentBase agent, EnvironmentGrid env)
    {
        var obs = new float[ObservationSize];

        float eMax = (env != null) ? Mathf.Max(0.0001f, env.maxEnergyPerCell) : 1f;
        float aMax = Mathf.Max(0.0001f, agent.maxEnergy);

        Vector2Int c = agent.GridPos;
        Vector2Int up = c + Vector2Int.up;
        Vector2Int ri = c + Vector2Int.right;
        Vector2Int dn = c + Vector2Int.down;
        Vector2Int lf = c + Vector2Int.left;

        obs[0] = env != null && agent.Grid.InBounds(c)  ? Mathf.Clamp01(env.GetEnergy(c)  / eMax) : 0f;
        obs[1] = env != null && agent.Grid.InBounds(up) ? Mathf.Clamp01(env.GetEnergy(up) / eMax) : 0f;
        obs[2] = env != null && agent.Grid.InBounds(ri) ? Mathf.Clamp01(env.GetEnergy(ri) / eMax) : 0f;
        obs[3] = env != null && agent.Grid.InBounds(dn) ? Mathf.Clamp01(env.GetEnergy(dn) / eMax) : 0f;
        obs[4] = env != null && agent.Grid.InBounds(lf) ? Mathf.Clamp01(env.GetEnergy(lf) / eMax) : 0f;

        obs[5] = Mathf.Clamp01(agent.Energy / aMax);

        return obs;
    }

    // Map discrete action index to target cell, clamped to grid bounds
    public static Vector2Int ActionToCell(AgentBase agent, int action)
    {
        Vector2Int c = agent.GridPos;
        Vector2Int t = c;
        switch (action)
        {
            case 1: t = c + Vector2Int.up; break;
            case 2: t = c + Vector2Int.right; break;
            case 3: t = c + Vector2Int.down; break;
            case 4: t = c + Vector2Int.left; break;
                // 0 = Stay -> current cell
        }
        return agent.Grid.InBounds(t) ? t : c;
    }
}
