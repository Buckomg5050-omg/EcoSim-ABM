using UnityEngine;

public class EnvironmentGrid : MonoBehaviour
{
    [Header("Links")]
    public GridManager grid; // if left null, will auto-find

    [Header("Energy Settings")]
    [Min(0.01f)] public float maxEnergyPerCell = 10f;
    [Range(0f, 1f)] public float initialFill = 0.6f;   // average fullness
    [Range(0.01f, 2f)] public float noiseScale = 0.25f; // larger = more variation per cell
    [Min(1)] public int noiseOctaves = 1;
    [Range(0f, 1f)] public float noisePersistence = 0.5f;

    [Header("Regeneration (not wired to ticks yet)")]
    [Min(0f)] public float regenPerTick = 0.05f;

    [Header("Gizmo View")]
    public bool drawGizmos = true;
    [Range(0.25f, 1f)] public float gizmoCubeScale = 0.85f;    // footprint within a cell
    [Range(0.05f, 1.5f)] public float gizmoHeightScale = 0.25f; // how tall max energy looks

    private float[] energy; // length = width * height

    void Awake()
    {
        if (grid == null) grid = Object.FindFirstObjectByType<GridManager>();
        TryBuildInitial();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Refresh preview in editor when values change
        if (Application.isPlaying) return;
        if (grid == null) grid = Object.FindFirstObjectByType<GridManager>();
        TryBuildInitial();
    }
#endif

    public void RebuildNow()
    {
        // Calls the existing private method that rebuilds the field & gizmos
        TryBuildInitial();
    }

    private void TryBuildInitial()
    {
        if (grid == null || grid.config == null) return;
        BuildInitialEnergy();
    }

    private void BuildInitialEnergy()
    {
        int w = grid.config.width;
        int h = grid.config.height;
        if (w <= 0 || h <= 0) return;

        energy = new float[w * h];

        // Perlin noise can't be seeded directly; offset the sample coords by a seed-derived offset
        int seed = grid.config.seed + 1000; // offset so it's independent of other RNG uses
        float offX = (seed * 0.12345f) % 10000f;
        float offY = (seed * 0.54321f) % 10000f;

        float scale = Mathf.Max(0.0001f, noiseScale);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float n = 0f;
                float amp = 1f;
                float freq = 1f;
                float norm = 0f;

                for (int o = 0; o < Mathf.Max(1, noiseOctaves); o++)
                {
                    float nx = (x + offX) * scale * freq;
                    float ny = (y + offY) * scale * freq;
                    n += Mathf.PerlinNoise(nx, ny) * amp;
                    norm += amp;
                    amp *= Mathf.Clamp01(noisePersistence);
                    freq *= 2f;
                }

                n = (norm > 0f) ? (n / norm) : 0.5f; // normalize 0..1
                float filled = Mathf.Clamp01(initialFill) * n; // bias by desired average fill
                energy[y * w + x] = filled * maxEnergyPerCell;
            }
        }
    }

    private int Index(Vector2Int c) => c.y * grid.config.width + c.x;

    public bool InBounds(Vector2Int c) => grid != null && grid.InBounds(c);

    public float GetEnergy(Vector2Int c)
    {
        if (!InBounds(c) || energy == null) return 0f;
        return energy[Index(c)];
    }

    public float Harvest(Vector2Int c, float amount)
    {
        if (!InBounds(c) || energy == null) return 0f;
        int i = Index(c);
        float take = Mathf.Clamp(amount, 0f, energy[i]);
        energy[i] -= take;
        return take;
    }

    public float Deposit(Vector2Int cell, float amount)
    {
        if (amount <= 0f) return 0f;
        if (!InBounds(cell) || energy == null) return 0f;

        int i = Index(cell);
        float cur = energy[i];
        float max = Mathf.Max(0.0001f, maxEnergyPerCell);
        float add = Mathf.Min(amount, max - cur);
        
        if (add <= 0f) return 0f;
        
        // Apply the deposit
        energy[i] = cur + add;
        return add;
    }

    public void RegenTick(float amountPerCell)
    {
        if (grid == null || grid.config == null || energy == null) return;
        int len = energy.Length;
        float cap = maxEnergyPerCell;
        float amt = Mathf.Max(0f, amountPerCell);
        for (int i = 0; i < len; i++)
            energy[i] = Mathf.Min(cap, energy[i] + amt);
    }

    public void TickRegen() => RegenTick(regenPerTick);

    private void OnDrawGizmos()
    {
        if (!drawGizmos || grid == null || grid.config == null) return;

        // Ensure there is something to draw in edit mode
        if (!Application.isPlaying && (energy == null || energy.Length != grid.config.width * grid.config.height))
            BuildInitialEnergy();

        if (energy == null) return;

        int w = grid.config.width;
        int h = grid.config.height;

        float cs = grid.config.cellSize * Mathf.Clamp01(gizmoCubeScale);
        float maxH = grid.config.cellSize * Mathf.Max(0.01f, gizmoHeightScale);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                float frac = Mathf.InverseLerp(0f, maxEnergyPerCell, energy[idx]);
                // dark gray -> green ramp
                Color c = Color.Lerp(new Color(0.2f, 0.2f, 0.2f, 0.8f), Color.green, frac);
                Gizmos.color = c;

                Vector3 basePos = grid.GridToWorld(new Vector2Int(x, y));
                float hNow = Mathf.Max(0.02f, frac * maxH);
                Vector3 size = new Vector3(cs, hNow, cs);
                Vector3 center = basePos + new Vector3(0f, hNow * 0.5f, 0f);

                Gizmos.DrawCube(center, size);
            }
        }
    }
}
