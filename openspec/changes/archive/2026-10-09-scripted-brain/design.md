# Design

## Context

See proposal.md for motivation. `World` exposes `Agents`, `Bushes`, `AgentsAwaitingDecision`, `Submit` and `Step`; it holds `Config` (with `PerceptionRadius`, `Width`, `Height`, `Seed`), `SortedDictionary` stores for agents and bushes, and one seeded `Random`. `Agent` has `Position` (sub-tile), `Hunger`, `CurrentAction` (`AgentAction?`, whose `MoveToAction.Target` is a sub-tile `Position`) and `LastAction` (`ActionResult?`). `Position` has `TileX`/`TileY`, `DistanceSquaredTo` (long) and `DistanceTo` (floored integer root). `Tokenville.Brains` is an empty class library referencing Core. `Tokenville.Core.Tests` references Core only, and holds a 2,000-tick calibration test driven by an omniscient in-test policy that reads `World` directly. Binding constraints are the plan's: contract types verbatim, brains never see sub-tile units, `Observe` is read-only, no I/O or packages in Core, determinism above all.

## Goals / Non-Goals

**Goals:**
- Introduce the observation records with the plan's names and fields so Phase 2 only fills `RecentEvents` and Phase 3 only renders them to text.
- A scripted brain that is a pure function of (observation, own RNG), testable with hand-built observations and no world.
- Pass the Phase 1 acceptance run through the real contract path, not through `World` internals.

**Non-Goals:**
- Per-agent event history (`RecentEvents` content); a decision log; the runner; any LLM code.
- Pathfinding or any smarter wander than the plan's one line. The sidestep is the minimum that keeps agents from starving beside empty bushes.
- An `InReach` flag or any other field the plan does not list. The brain works around the tile-distance loss instead (see below); revisit when the LLM prompt is designed in Phase 3.

## Decisions

**Observation records live in Core, four flat records, verbatim names.** `Observation(long Tick, AgentSelf Self, IReadOnlyList<VisibleBush> Bushes, IReadOnlyList<VisibleAgent> Agents, IReadOnlyList<WorldEvent> RecentEvents, ActionResult? LastAction, int WorldWidth, int WorldHeight)`, `AgentSelf(EntityId Id, string Name, int X, int Y, int Hunger, ActionKind? CurrentAction)`, `VisibleBush(EntityId Id, int X, int Y, int Berries, int Distance)`, `VisibleAgent(EntityId Id, string Name, int X, int Y, int Distance, ActionKind? CurrentAction)`, all in one file with XML docs. Ids stay `EntityId` as in `WorldEvent`; brains call `ToString()` to fill `AgentDecision.TargetId`, and JSON rendering of ids is the runner's problem in both cases. "Current action" is `ActionKind?` for self and others because `AgentAction` carries a sub-tile target. `X`/`Y` are `TileX`/`TileY`.
Alternative: string ids in the observation to mirror `AgentDecision.TargetId`. Rejected: two id types for one thing inside Core; the asymmetry with `WorldEvent` would be odder than the asymmetry with the decision, which takes a string because an LLM writes it.

**`Observe(EntityId)` is computed on call, read-only, and throws `ArgumentException` for a non-living agent.** Filter is `DistanceSquaredTo <= (PerceptionRadius * TileSize)^2` on exact positions, no root, inclusive. Iteration over the sorted stores gives ascending id order for free; the observing agent is skipped. Distance shown is `DistanceTo(other) / TileSize` (floor). `RecentEvents` is `Array.Empty<WorldEvent>()`. Throwing is the plan's own choice for programmer errors (config validation throws); the runner only observes ids it just got from `AgentsAwaitingDecision`.
Alternative: return null for unknown ids. Rejected: hides a runner bug.

**Reach is lossy in tiles, so the brain tries `Eat` at distance 1 and walks only after an "out of reach" failure.** Engine reach is 256 units; tile distance 1 covers 256 to 511 units, in reach only at exactly 256 (an axis-adjacent tile center). Distance 0 is always in reach. The brain decides `Eat` when the nearest food is at distance 0 or 1; if the engine answers `bush-k is out of reach`, the next observation's `LastAction` says so and the brain decides `MoveTo(bushId)`, whose final-step shortening ends within reach (253 to 255 units, distance 0), so the following `Eat` succeeds. Cost: one wasted tick on a diagonal approach. Correct on every input, no contract change.
Rejected first attempt: eat only at distance 0, walk otherwise. It deadlocks: an agent exactly one tile from a bush is already in reach, `MoveTo` completes without moving, the bush still shows distance 1, and the brain repeats `MoveTo` until it starves (agent-2 at tile (22, 16) beside bush-4, dead at tick 199 on the default seed).
Rejected alternative: round tile distances up in `Observe`, so distance 1 means exactly "within reach". Semantically cleanest, but it deviates from the plan's `units / TileSize` and on the default seed one agent still starved with 8 bushes (a different random trajectory), so it does not buy the acceptance run either.
Deferred alternative: add `bool InReach` to `VisibleBush`. More honest for an LLM but a contract addition; weigh it in Phase 3 against prompt text.

**`IAgentBrain` verbatim; `ScriptedBrain(int seed)` is one instance per agent, synchronous inside, returns `Task.FromResult`.** The brain holds a `Random(seed)`. Seed convention for callers (tests now, runner later): `config.Seed + agent.Id.Number`. `Random(int)` with a fixed seed is deterministic for a given runtime; the engine already relies on the same guarantee. Never `HashCode.Combine` or `Random.Shared`.
Alternative: one brain for all agents with a per-agent RNG dictionary keyed by `Self.Id`. Same determinism, but the plan's Phase 3 flag "choose the brain per agent" already implies per-agent instances, and the dictionary is state the runner can hold more naturally.

**Decision order: sidestep, then eat or approach, then wander.**
1. If `LastAction` is `Failed` and `Reason` starts with `blocked`: collect the four edge neighbours of `Self` in the fixed order (x, y+1), (x, y-1), (x+1, y), (x-1, y), keep those in bounds and not equal to any visible bush tile, pick index `rng.Next(count)`; if none, `Wait(1)`. One tick, then the normal rules resume; the straight line to the target has usually changed enough to miss the blocking tile. The calibration test proved a deterministic first-free-neighbour sidestep suffices; a random one costs nothing extra and fits the brain's theme.
2. If `Hunger >= 30`: among visible bushes with `Berries > 0`, take the first by `(Distance, Id)`; `Eat(id)` if `Distance <= 1` and `LastAction` is not a failure ending in `out of reach`, else `MoveTo(id)`. The list is already id-ascending, so a stable `OrderBy(Distance)` gives the tie rule.
3. Otherwise wander: `x = rng.Next(WorldWidth)`, then `y = rng.Next(WorldHeight)`; redraw both while a visible bush sits on (x, y). Unseen bushes are handled by the engine: the move fails at start with `tile holds bush-k` and the brain redraws next tick. The redraw loop terminates whenever a visible-bush-free tile exists, which `WorldConfig` validation guarantees whenever agents exist; worst case is bounded by `PerceptionRadius` (at most ~81 visible tiles out of 1,024 by default). Draw order x then y is fixed because byte-identical logs depend on it.
The rules apply to hungry agents that see no food too: they wander rather than wait, since waiting beside nothing is certain death.

**Tests go in `Tokenville.Core.Tests` with a project reference to Brains.** Brain unit tests build `Observation` records by hand with a small helper; no world needed. The acceptance and determinism runs use `World.Observe` and `DecideAsync(...).GetAwaiter().GetResult()` (the task is already completed). The existing omniscient calibration test stays: it isolates the engine from perception, so if the radius-5 brain ever fails the survival run, the two tests together say which side broke.
Alternative: a `Tokenville.Brains.Tests` project. Rejected for the PoC: one more csproj for one class.

## Risks / Trade-offs

- [Six radius-5 agents may not survive 2,000 ticks on 8 bushes, unlike the omniscient policy] → the acceptance test is the only proof; rough odds (a random position sees a bush about half the time, and a wander sweeps far more area) say it passes. If it fails, the first knob is wandering to a random tile within `PerceptionRadius` while hungry so food is re-evaluated sooner; that is a spec change to `scripted-brain`, not a config change.
- [One wasted tick when a bush at tile distance 1 is diagonally out of reach] → accepted; a hungry agent has 140 ticks of margin from 30 to 100.
- [Wander redraw loop is unbounded in theory] → bounded in practice by the visible-tile count; `ponytail:` comment names it. Degenerate configs with almost every tile a bush already fail `WorldConfig` validation when agents exist.
- [Blocked sidestep can itself be blocked by an unseen or newly relevant bush] → the next observation reports `blocked` again and the brain sidesteps again from its unchanged position; random choice among neighbours prevents a fixed loop. Covered by the "no agent ends inside a bush tile" assertion over 2,000 ticks.
- [`RecentEvents` pinned as empty in a spec] → deliberate; Phase 2 writes a MODIFIED requirement. Better than a field that is silently absent and then breaks the frozen record shape.
- [`Observe` is O(bushes + agents) per call, six calls per tick] → trivial at PoC scale; no spatial index.
