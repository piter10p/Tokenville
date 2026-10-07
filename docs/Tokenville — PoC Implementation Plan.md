# Tokenville — PoC Implementation Plan

Oct 6, 2026 · @Piotr

## Purpose and scope

The PoC proves one loop end to end: world → observation → decision → action → world, first with scripted brains, then with a small local LLM. It is a headless, deterministic, tick-based simulation of agents who must eat from berry bushes to survive.

This plan is written for coding agents implementing the PoC. Treat the specs below as binding; anything not listed is out of scope. When a detail is unspecified, choose the simplest option that keeps the engine deterministic, and note the choice in the PR description.

Success criteria:

- Six scripted agents run 2,000 ticks headless; with 8 bushes they survive, with 3 bushes some starve.
- The same seed and config always produce a byte-identical event log.
- The `Observation` of any agent at any tick can be dumped as JSON.
- Swapping the scripted brain for an Ollama-backed brain requires no change to `Tokenville.Core`.

## Architecture and solution layout

The solution has four .NET 10 projects with `Tokenville.Core` at the bottom: a pure, dependency-free engine that everything else references.

&#91;embedded content: solution layout · 4 projects, 1 external service\]

Arrows are project references or calls. Only `Tokenville.Brains` talks to the model; the engine never sees an LLM, a clock or a thread.

| Concern | Choice |
| --- | --- |
| Runtime | .NET 10, C#, nullable enabled |
| Engine | plain class library, no packages |
| Runner | console app, `Spectre.Console` for the live ASCII view |
| Config and logs | `System.Text.Json`, config from JSON, logs as JSONL |
| Tests | xUnit |
| LLM access (phase 3) | `Microsoft.Extensions.AI` + `OllamaSharp` behind `IChatClient` |

Scaffolding:

```bash
mkdir Tokenville && cd Tokenville
dotnet new sln -n Tokenville
dotnet new gitignore
dotnet new classlib -n Tokenville.Core   -o src/Tokenville.Core   -f net10.0
dotnet new classlib -n Tokenville.Brains -o src/Tokenville.Brains -f net10.0
dotnet new console  -n Tokenville.Runner -o src/Tokenville.Runner -f net10.0
dotnet new xunit    -n Tokenville.Core.Tests -o tests/Tokenville.Core.Tests -f net10.0
dotnet sln add src/Tokenville.Core src/Tokenville.Brains src/Tokenville.Runner tests/Tokenville.Core.Tests
dotnet add src/Tokenville.Brains reference src/Tokenville.Core
dotnet add src/Tokenville.Runner reference src/Tokenville.Core src/Tokenville.Brains
dotnet add tests/Tokenville.Core.Tests reference src/Tokenville.Core
dotnet add src/Tokenville.Runner package Spectre.Console
```

## World specification

The world is an empty 32×32 grid of tiles with two layers, Minecraft-style: berry bushes are blocks welded to the grid, agents are entities that move freely over it. There is one need: hunger. There is no inventory, health, speech or terrain.

### Map

- Square grid of tiles, default 32×32, tile coordinates `(x, y)` with `(0, 0)` top-left.
- Positions are fixed-point integers in sub-tile units, `TileSize` = 256 units per tile. Tile `(x, y)` spans units `[x·256, (x+1)·256)` on each axis; its center is `(x·256 + 128, y·256 + 128)`. Tile index is `units >> 8`, sub-tile offset is `units & 255`. Never `float` or `double`.
- Blocks (bushes) occupy a whole tile and their position is the tile center. Entities (agents, later animals) have sub-tile positions and move in any direction.
- Distance is Euclidean: `isqrt(dx² + dy²)` with an integer square root, squared terms in `long`. Range checks compare squared distances and never take a root. Distances shown to brains are whole tiles (`units / TileSize`).
- Agents do not collide with each other. A bush blocks its own tile: an agent never ends a tick inside one.
- Bushes and agents are placed at setup using the seeded RNG, never two bushes on one tile; agents spawn at the center of a bush-free tile.

### Entities

| Entity | Fields | Notes |
| --- | --- | --- |
| Agent | `Id` (`agent-1`), `Name`, `Position` (sub-tile units), `Hunger` (int 0–100, 0 = full), `CurrentAction` | moves `Speed` = 256 units (one tile) per tick; removed from the world when hunger reaches 100 |
| BerryBush | `Id` (`bush-1`), `Position` (its tile center), `Berries` (int 0–max), regrow counter | occupies one tile; used from within `Reach` = 256 units (one tile) of its center |

Ids are stable, readable strings because they appear in LLM prompts. Agent names come from a fixed list indexed by agent number.

### Actions

| Action | Duration | Preconditions | Effect on completion |
| --- | --- | --- | --- |
| `MoveTo(targetId)` or `MoveTo(x, y)` | until arrival, `Speed` units per tick | target exists; tile `(x, y)` is in bounds and not a bush | agent ends within `Reach` of the entity, or at the center of tile `(x, y)` |
| `Eat(bushId)` | `EatTicks` (5) | agent within `Reach` of the bush; bush has ≥ 1 berry at start | bush −1 berry, agent hunger −`BerryNutrition` (min 0) |
| `Wait(ticks)` | n (1–50) | none | nothing |

`MoveTo` moves in a straight line. Each tick, with `d = isqrt(dx² + dy²)` from the agent to the target point: if `d ≤ Speed` the agent arrives exactly; otherwise it moves by `(dx·Speed / d, dy·Speed / d)` using integer division and recomputes from its new position next tick, so rounding never accumulates. `MoveTo(x, y)` targets the tile center. `MoveTo(targetId)` targets the entity's position and completes as soon as the agent is within `Reach`; with `Reach` = one tile that always happens before the agent would enter a bush tile. A step that would end inside a bush tile fails with `Failed("blocked by bush-k")` and the agent stays where it was; there is no pathfinding, and a step that crosses a bush tile's corner without ending inside it is allowed. Preconditions are checked when the action starts; `Eat` re-checks the bush when it completes. Every action ends as `Completed`, `Failed(reason)` or `Interrupted`. Failures are normal outcomes reported to the agent, never exceptions.

### Rules

| Rule | Default |
| --- | --- |
| Hunger growth | +1 every `HungerTicksPerPoint` (2) ticks |
| Death | hunger reaches 100 |
| Berry nutrition | −25 hunger |
| Bush capacity | `BushMaxBerries` (5), bushes start full |
| Bush regrowth | +1 berry every `BushRegrowTicks` (40) while below max |
| Perception radius | `PerceptionRadius` (5) tiles, Euclidean |
| Tile size | `TileSize` = 256 units, constant |
| Movement speed | `Speed` = 256 units (one tile) per tick, constant |
| Reach | `Reach` = 256 units (one tile), constant |
| Hunger alert thresholds | 50 and 80 |

Calibration: an agent needs one berry per \~50 ticks and a bush yields one per 40, so one bush sustains about 1.25 agents in steady state. With 6 agents, 8 bushes should be survivable and 3 should not. Use this to sanity-check the engine. Travel is Euclidean, so trips are a little shorter than on a 4-directional grid; the calibration still holds.

`TileSize`, `Speed` and `Reach` are constants in `Tokenville.Core`, not config: nothing in the PoC varies them. Per-entity speed arrives with animals, after the PoC.

### Tick pipeline

`World.Step()` runs these phases in this order, iterating agents in ascending id order:

1. Start actions from decisions delivered to idle agents; invalid ones fail immediately.
2. Progress running actions; complete or fail those that finish. On a tie for the last berry, the lower id wins and the other gets `Failed("bush is empty")`.
3. Increase hunger; remove agents at 100 and emit `AgentDied`.
4. Regrow bushes.
5. Emit events, then mark agents needing a decision: idle, action ended, or a hunger threshold crossed (interrupts any current action except `Eat`).
6. Increment `Tick`.

### Events

`AgentMoved`, `ActionStarted`, `ActionCompleted`, `ActionFailed`, `ActionInterrupted`, `AgentAte`, `BushRegrew`, `HungerThresholdCrossed`, `AgentDied`. Each event carries `Tick`, the subject id and, where relevant, a position in sub-tile units (the log is exact engine state; tile rounding happens only in `Observation`).

### Config

```csharp
public sealed record WorldConfig(
    int Width = 32, int Height = 32, int Seed = 42,
    int AgentCount = 6, int BushCount = 8,
    int HungerTicksPerPoint = 2, int BerryNutrition = 25,
    int EatTicks = 5, int BushMaxBerries = 5, int BushRegrowTicks = 40,
    int PerceptionRadius = 5, int[]? HungerAlertThresholds = null); // default [50, 80]
```

Config is loaded from JSON by the runner; the engine receives it as an immutable record.

## Engine–brain contract

The engine never calls a brain. It exposes observations and accepts decisions; the runner shuttles between them. This boundary is the only place an LLM plugs in, so it is frozen after phase 2.

```csharp
// Tokenville.Core
public sealed record Observation(
    long Tick,
    AgentSelf Self,                         // id, name, tile position, hunger, current action
    IReadOnlyList<VisibleBush> Bushes,      // id, tile position, berries, distance in tiles
    IReadOnlyList<VisibleAgent> Agents,     // id, name, tile position, distance in tiles, current action kind
    IReadOnlyList<WorldEvent> RecentEvents, // perceived since this agent's last decision
    ActionResult? LastAction,               // outcome + reason of the previous action
    int WorldWidth, int WorldHeight);

public enum ActionKind { MoveTo, Eat, Wait }

public sealed record AgentDecision(
    ActionKind Action, string? TargetId, int? X, int? Y, int? Ticks, string? Reason);

// World API used by the runner
IReadOnlyList<EntityId> AgentsAwaitingDecision { get; }
Observation Observe(EntityId agentId);
void Submit(EntityId agentId, AgentDecision decision);
void Step();

// Tokenville.Brains
public interface IAgentBrain
{
    Task<AgentDecision> DecideAsync(Observation observation, CancellationToken ct);
}
```

Rules for the boundary:

- `Observation` is pure data and serializes to JSON. Converting it to prompt text is the LLM brain's job, not the engine's.
- Brains never see sub-tile units. Positions in `Observation` and `AgentDecision` are tile coordinates (`units / TileSize`), distances are whole tiles, and `MoveTo(x, y)` means the center of that tile. The engine converts both ways, so the engine can change its position resolution without touching the contract.
- An agent sees bushes and agents within `PerceptionRadius`, and only events whose position is within that radius.
- `Submit` never throws on a bad decision; the action fails at the next `Step` with a readable reason.
- `Reason` is logged but has no effect on the world.

Scheduling for the PoC is strict lockstep. The runner loop is: collect `AgentsAwaitingDecision`, observe each, await all brains, submit every decision, call `Step()`. The world never advances while a decision is pending. A brain that times out or returns garbage is replaced by `Wait(5)` and logged.

## Implementation phases

Four phases, each merged only when its acceptance criteria pass. Phases 1 and 2 involve no LLM.

### Phase 0 — Scaffolding

- [ ] Create the solution: `Tokenville.Core`, `Tokenville.Brains`, `Tokenville.Runner`, `Tokenville.Core.Tests` on `net10.0`, references as in the architecture section.
- [ ] Enable nullable reference types and treat warnings as errors in every project.
- [ ] Add `Spectre.Console` to `Tokenville.Runner`.

Accepted when `dotnet build` and `dotnet test` succeed on a clean clone.

### Phase 1 — Engine and scripted brains

- [ ] Value types: `EntityId`, fixed-point `Position` (sub-tile units, tile conversion), integer square root and Euclidean distance.
- [ ] `WorldConfig` and JSON loading in the runner.
- [ ] `World` setup: seeded placement of agents and bushes.
- [ ] Actions `MoveTo`, `Eat`, `Wait` with start, progress, complete, fail and interrupt.
- [ ] `World.Step()` implementing the tick pipeline exactly as specified.
- [ ] Event model and an append-only event list per tick.
- [ ] `ScriptedBrain`: eat from the nearest visible bush with berries when hunger ≥ 30, otherwise move to a random in-bounds, bush-free tile, using a per-agent seeded RNG.
- [ ] Runner: lockstep loop, JSONL event log, ASCII map with a stats panel every N ticks, exit on all-dead or max ticks.

Accepted when:

- Unit tests cover each rule in the world specification, including the last-berry tie.
- With default config (6 agents, 8 bushes), all agents are alive at tick 2,000.
- With 3 bushes, at least one agent dies before tick 2,000.

### Phase 2 — Contract and observations

- [ ] `World.Observe` with perception filtering and per-agent `RecentEvents` since the last decision.
- [ ] `--dump-observation <agentId> <tick>` runner flag printing the observation as JSON.
- [ ] Determinism test: two runs with the same seed produce byte-identical JSONL logs.
- [ ] Decision log: every submitted `AgentDecision` written to the JSONL log.
- [ ] Replay mode: re-run a world from config plus a recorded decision log, without brains, and assert the same event log.

Accepted when the determinism and replay tests pass and the contract types carry XML doc comments.

### Phase 3 — LLM brain

- [ ] Add `Microsoft.Extensions.AI` and `OllamaSharp` to `Tokenville.Brains`.
- [ ] `LlmBrain : IAgentBrain` taking an `IChatClient`; model name and endpoint from config.
- [ ] Prompt builder: static system prompt (rules, action list) first, then a compact text rendering of the observation.
- [ ] Structured output: a JSON schema with an `action` enum plus `targetId`, `x`, `y`, `ticks`, `reason`; parse and validate against the observation.
- [ ] Fallback to `Wait(5)` on timeout, parse failure or invalid target; count fallbacks as a metric.
- [ ] Runner flag to choose the brain per agent (scripted, llm), so mixed populations can be compared.

Accepted when 6 LLM agents run 500 ticks against a local Ollama model with a fallback rate under 10%, and the log shows every prompt, raw response and parsed decision.

## Engineering rules for agents

Determinism is the top priority: any change that breaks the determinism test is rejected, whatever it adds.

### Determinism in `Tokenville.Core`

- One seeded `Random` owned by the world; never `Random.Shared` or an unseeded `new Random()`.
- Iterate entities in ascending id order (`SortedDictionary` or explicit sort); never rely on `Dictionary` or `HashSet` order.
- Integer math only; no `float` or `double`. Positions are fixed-point integers (`TileSize` units per tile), squared distances use `long`, and square roots use an integer `isqrt`, never `Math.Sqrt`.
- No wall-clock time (`DateTime`, `Stopwatch`) in the engine; time is `World.Tick`.
- No `async`, threads, I/O or logging frameworks in `Tokenville.Core`. The engine returns events; the runner writes them.
- JSON output uses fixed property order and invariant culture.

### Code

- `Tokenville.Core` has no package dependencies.
- Prefer immutable records for value types, events, observations and decisions; mutable state lives only in `World` and its entities.
- World state changes only inside `World.Step()`; `Observe` is read-only.
- No speculative abstraction: no ECS, plugin systems or generic rule engines.
- Public types in the contract carry XML doc comments.

### Tests

- Every rule in the world specification has at least one unit test that builds a tiny hand-placed world.
- The determinism test (two runs, same seed, identical logs) runs in CI and on every PR.
- Tests never depend on an LLM; `LlmBrain` is tested with a fake `IChatClient` returning canned responses.

### Working style

- One phase task per PR, with tests in the same PR.
- When the spec is ambiguous, pick the simplest deterministic option and state it in the PR description.
- Do not change config defaults or the contract without flagging it explicitly.

## Out of scope and future extensions

Do not implement any of these in the PoC: inventory, health, speech, obstacles or terrain, pathfinding around bushes, agent–agent collision, per-entity speeds, multiple resource types, crafting, building, combat, day/night, agent memory or reflection, asynchronous (non-lockstep) scheduling, graphical visualization and cloud LLM providers.

Likely next steps after the PoC, roughly in this order: thirst with water tiles (a second need in a different place), animals as entities with their own speed, speech between nearby agents, inventory with giving, per-agent memory in the LLM brain, then asynchronous scheduling where the world keeps ticking while agents think. Keep the design open to these, but do not build for them now.
