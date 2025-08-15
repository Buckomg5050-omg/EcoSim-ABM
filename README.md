# EcoSim-ABM

Agent-Based Modeling (ABM) sandbox in Unity for simulating ecosystem dynamics, visualizing them in 3D, and optionally training policies with Unity ML-Agents. The project emphasizes **determinism** (seeded RNG), **observability** (HUD, chart, logs, snapshots), and **policy modularity** (heuristic vs ML).

> Built and iterated step-by-step with meticulous commits. Designed to be easy to pause, inspect, and resume later.

---

## Table of contents

* [Features](#features)
* [Quick start](#quick-start)
* [Controls & HUD](#controls--hud)
* [Project structure](#project-structure)
* [Core concepts](#core-concepts)
* [Configuration](#configuration)

  * [Grid & Environment](#grid--environment)
  * [Agents](#agents)
  * [Reproduction](#reproduction)
  * [Presets](#presets)
  * [Policies](#policies)
* [Observability](#observability)

  * [Population chart](#population-chart)
  * [CSV logging](#csv-logging)
  * [Snapshot → JSON](#snapshot--json)
  * [Shocks (Drought @ Mouse)](#shocks-drought--mouse)
* [ML-Agents (optional)](#ml-agents-optional)
* [Determinism & seeds](#determinism--seeds)
* [Builds](#builds)
* [Version control](#version-control)
* [Troubleshooting](#troubleshooting)
* [Roadmap](#roadmap)
* [License](#license)

---

## Features

* **Grid world** with resource field (noise-based), regeneration, and gizmo coloring.
* **Agents** with internal energy, metabolism, foraging, death, and reproduction.
* **Deterministic spawns** and behavior via **seeded RNG**.
* **Policy plug-in system**

  * Heuristics: ε-Greedy, Richness-Linger, Greedy via observation vector
  * ML: thin adapter for Unity ML-Agents (compile-time gated)
* **Observability:** pause/step, on-screen readouts, **bottom-left population chart**, CSV logs, JSON snapshots.
* **Runtime controls:** frame camera, reset agents, **policy switcher**, **logging toggle**, **New Log**, **Snapshot**, **Drought @Mouse**.
* **Config presets** (ScriptableObjects) you can apply at runtime.

---

## Quick start

1. **Open in Unity** (use the Unity version you created the project with; URP enabled).
2. Open scene: `Assets/Scenes/Main.unity`.
3. Select **Simulation** (has `SimulationController`). By default **Start Paused** = ✅.
4. Press **Play** → simulate via:

   * **Resume / Pause** or hit **Space**
   * **Step** (single tick)
   * **Frame** (camera framing)
   * Choose a **Policy** from the row (auto-reset applies it)
   * **Bottom-left**: chart + logging controls + snapshot + drought button
5. (Optional) Create & apply **Presets**: `Assets/Resources/Presets` → select in HUD and **Apply**.

---

## Controls & HUD

* **Space** → Pause/Resume
* **Step** (button) → advance exactly one tick when paused
* **Reset** → destroy & respawn agents (deterministic given current seed)
* **Frame** → position camera to view the entire grid
* **Policy buttons** → ε-Greedy, Linger, ObsGreedy, ML-Agents (auto-Reset)
* **Readouts (top-left):**

  * Tick rate, agent count, pause state
  * Mouse cell & energy, Agent\_000 cell energy & body energy, last/cumulative reward
* **Bottom-left**:

  * **Population chart** (sparkline bars)
  * **Logging**: OFF → armed → ON (toggle)
  * **New Log**: close current file and start fresh on next tick
  * **Snapshot**: export run config/status to JSON
  * **Drought @Mouse**: deplete local area around mouse
* **Start paused by default** so you can select agents/params first.

---

## Project structure

```
Assets/
  Scenes/Main.unity
  Scripts/
    Agents/
      AgentIO.cs                         # observation vector (6) & 5-way action mapper
      RandomWalkerAgent.cs               # agent body; calls policy each tick
      Policy/
        IAgentPolicy.cs                  # cell-decider interface
        IActionPolicy.cs                 # action-decider interface (0..4)
        EpsilonGreedyEnergyPolicy.cs
        RichnessLingerPolicy.cs
        GreedyObsPolicy.cs               # greedy via observations
        MLAgentsAdapter.cs               # (conditional) wraps Unity Agent
        MLAgentsActionPolicy.cs          # (conditional) IActionPolicy via adapter
    Core/
      GridConfig.cs
      GridManager.cs                     # world↔cell + gizmos + random cell
      EnvironmentGrid.cs                 # energy field, regen, Harvest(), RebuildNow()
      SimulationController.cs            # the conductor (loop, UI, logging, presets, shocks)
      RunLogger.cs
      SimPreset.cs                       # ScriptableObject for presets
  Resources/
    Presets/                             # SimPreset assets live here
ML/
  EcoAgentPPO.yaml                       # minimal PPO config (optional)
```

---

## Core concepts

* **EnvironmentGrid**: 2D scalar field with energy per cell.
* **Agents**:

  * Each tick: `harvest → move (policy) → metabolism → death if ≤0 → reproduction if high energy`.
  * Repro splits energy with offspring, birth cell chosen greedily among (stay + 4 neighbors).
* **Policies**:

  * **Cell-based** (`IAgentPolicy`): returns a target cell from {stay, up, right, down, left}.
  * **Action-based** (`IActionPolicy`): returns discrete action 0..4; `AgentIO` maps to a cell.
  * Observation vector (size 6): `[E(cur), E(up), E(right), E(down), E(left), bodyEnergy]` (normalized).

---

## Configuration

### Grid & Environment

* `GridConfig`: `width`, `height`, `cellSize`, `seed`.
* `EnvironmentGrid`:

  * `maxEnergyPerCell`, `initialFill`
  * noise: `noiseScale`, `noiseOctaves`, `noisePersistence`
  * `regenPerTick` (uniform per tick)
  * Call `RebuildNow()` to rebuild field (used by presets apply).

### Agents

* `RandomWalkerAgent`:

  * `harvestPerStep` (how much to attempt from the cell each tick)
  * Energy model on `AgentBase`: `maxEnergy`, `startEnergy`, `metabolismPerTick`.
* Rewards: positive on harvest, negative each tick (metabolism penalty), death penalty once.

### Reproduction

* `enableReproduction`, `reproduceThreshold`, `offspringEnergyFraction`, `maxAgents`.

### Presets

* Assets → **Create → EcoSim → Sim Preset**
* Save under `Assets/Resources/Presets`.
* Apply at runtime via bottom-left HUD (preset row) → **Apply**.

Preset fields include seed, env params, sim params, reproduction, and desired policy.

### Policies

* **ε-Greedy**: move to highest-energy neighbor (with epsilon exploration).
* **Richness-Linger**: stay on rich tiles; otherwise move to better options.
* **ObsGreedy**: greedy via standardized observation vector.
* **ML-Agents** (optional): action chosen by trained model (or arrow-key heuristic).

---

## Observability

### Population chart

* Bottom-left sparkline (windowed).
* Auto-scales to current max; anchored bottom-left (responsive).

### CSV logging

* **OFF by default**; toggle at runtime.
* **Armed** when enabled while paused; turns **ON** at first tick.
* **New Log** closes current file and starts a new one on the next tick.
* Path: `Application.persistentDataPath/Logs/*.csv`

  * Windows: `C:\Users\<you>\AppData\LocalLow\<Company>\<Product>\Logs\...`

Columns: `time,tick,agents`.

### Snapshot → JSON

* Button: **Snapshot** (bottom-left row).
* Saves config + episode/tick + counts + policy/behavior to
  `Application.persistentDataPath/Exports/snapshot_*.json`.

### Shocks (Drought @ Mouse)

* Depletes a **disc** of cells centered at the mouse cell to a fraction of their current energy.
* Config: `shockRadius` (cells), `droughtToFrac` (keep fraction, e.g., 0.25).

---

## ML-Agents (optional)

* Install package `com.unity.ml-agents`.
* Add scripting define **`MLAGENTS_PRESENT`** (Project Settings → Player).
* Set **SimulationController → Policy = MLAgents** and **Reset**.
* `MLAgentsAdapter` will **auto-configure BehaviorParameters** in `Awake()`:

  * **Vector Observation Size = 6**
  * **Discrete actions** (1 branch) **= 5** (Stay/Up/Right/Down/Left)
  * BehaviorName defaults to `EcoAgent` (used by YAML).
* Train (shell):

  ```bash
  mlagents-learn ML/EcoAgentPPO.yaml --run-id eco-001
  ```

  Then press **Play**.
* Not training? Set Behavior Type = **Heuristic Only** (arrow keys) to silence port warnings.

---

## Determinism & seeds

* The sim uses `GridConfig.seed` to seed RNG for **spawn** and policy randomness.
* **Reset** recreates RNG from the current seed; same seed → same spawns & behavior (given same params).
* Changing seed + **Reset** gives different but reproducible runs.

---

## Builds

* Typical Unity Player builds supported (URP project).
* For headless/fast training, you could later add a "no-render" mode (not yet included).

---

## Version control

* GitHub remote: `https://github.com/Buckomg5050-omg/EcoSim-ABM`
* Default branch: **main**
* Git LFS is initialized; large binary assets will be tracked.
* **Ignored generated files**: ML-Agents Timers at `Assets/ML-Agents/Timers/`.

Common commands (Windows CMD):

```bat
git add -A
git commit -m "message"
git push
```

---

## Troubleshooting

**"More observations (6) than vector size (1)"**

* Adapter now sets obs/action sizes in `Awake()`. If you still see it, ensure `MLAGENTS_PRESENT` is defined and that you aren't overriding BehaviorParameters manually on a prefab.

**"Couldn't connect to trainer; will perform inference"**

* Normal unless `mlagents-learn` is running. Use Behavior Type = **Heuristic Only** to silence.

**Ambiguous `Object` reference**

* Use `UnityEngine.Object.FindFirstObjectByType<T>()` (fully qualify `UnityEngine.Object`).

**Logging starts immediately**

* We intentionally **delay** CSV creation until the **first tick**; logging defaults **OFF**.

**Preset apply rebuild error**

* Use `env.RebuildNow()` (public wrapper), not the private method.

**Unity .meta churn**

* Make sure new folders/assets (especially under `Resources/Presets`) are committed with their `.meta` files.

---

## Roadmap

* Predator–prey interactions (second species).
* Headless mode / batch seeds.
* Replay capture & deterministic replays.
* Diffusion/weather overlays.
* Tiny in-game charts for mean energy, births/deaths.
* ECS/Jobs/Burst for scale (if needed).

---

## License

Choose a license and place it as `LICENSE` at repo root (MIT/Apache-2.0/BSD-3-Clause recommended for research/experiments).

---

### Commit this file

```bat
:: create/update README
echo.>README.md & notepad README.md
:: paste the content, save, close

git add README.md
git commit -m "docs: add comprehensive README with setup, usage, ML, presets, logging"
git push
```
