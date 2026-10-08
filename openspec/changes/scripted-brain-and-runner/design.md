# Design

## Context

See proposal.md for motivation. `World` exposes `Config`, `Tick`, `Agents` and `Bushes` (ascending id), `AgentsAwaitingDecision`, `Submit` and `Step()` returning that tick's events as a fresh list. `Agent.LastAction` holds the previous outcome and reason. `World.FromLayout` is internal with `InternalsVisibleTo("Tokenville.Core.Tests")`. `Tokenville.Brains` holds `IAgentBrain` (from PR #4, no implementation after the merge); `Observation` and `World.Observe` exist in Core. `Program.cs` reads a config path from `args[0]` with case-insensitive, unmapped-member-rejecting JSON options. `Spectre.Console` is referenced by the runner and unused. There is one test project. The binding constraints are the plan's engineering rules: determinism first, no I/O in Core, JSON in fixed property order and invariant culture, one phase task per PR with ambiguities resolved toward the simplest deterministic option.

## Goals / Non-Goals

**Goals:**
- A run from `dotnet run` that produces a readable log and a watchable console trace with zero new dependencies.
- The loop and log writer callable from tests with a `TextWriter`, so Phase 2's determinism and replay tests need no console or file system.
- A brain that meets the Phase 1 acceptance runs and whose policy ports to `Observation` unchanged.

**Non-Goals:**
- Anything in Phase 2 or 3: observations, decision log, replay, async brains, timeouts, prompt text.
- A log envelope or type discriminator for future line kinds. Phase 2 adds decision lines and chooses the envelope then; no golden file exists yet, so the format is free to change.
- Spectre rendering. See proposal.

## Decisions

**The brain reads `World` and `Agent`, not `Observation`.** (Decided before PR #4 merged; kept after the merge at the user's call, with the port to `Observation` as a follow-up.) `public sealed class ScriptedBrain` with `AgentDecision Decide(World world, Agent agent)`. The alternative of pulling `Observation` and `Observe` forward would put Phase 2's first item, including the per-agent recent-events design, into a Phase 1 PR; a runner-only PR with an inline policy could not meet the phase's acceptance criteria. The cost is that the brain is omniscient until Phase 2 and that its signature changes then. The policy reads only positions, hunger, berries and the last action, all of which `Observation` carries in tile units, so the port is mechanical. The runner looks the `Agent` up from the awaiting id with a LINQ `Single` over `World.Agents`; six agents, no index needed.

**No `IAgentBrain`, no `Task`.** The plan's interface is `Task<AgentDecision> DecideAsync(Observation, ct)`. Declaring it now with a different parameter type means editing a frozen contract type later; declaring it with `Observation` is impossible. One implementation needs no interface. The loop takes `Func<World, Agent, AgentDecision>`; `Program.cs` closes over a per-agent `Dictionary<EntityId, ScriptedBrain>` built once from `World.Agents` at tick 0 (agents are never added later). Synchronous because the only brain is synchronous; with sync brains lockstep is trivially satisfied. Phase 3 introduces the real interface and `Task.WhenAll`.

**Policy details, in priority order.** (1) If `LastAction` is `Failed` with a reason starting `blocked by`: `MoveTo` the first in-bounds, bush-free tile of below, above, right, left; else `Wait(1)`. Fixed order rather than a random neighbour so the sidestep is reproducible without touching the RNG. (2) If `Hunger >= 30` and any bush has berries: scan `world.Bushes` (ascending id) keeping the strictly nearest by squared distance, so ties fall to the lower id; `Eat` if within `Reach`, else `MoveTo(bushId)`. (3) Otherwise `MoveTo(x, y)` with `x = rng.Next(Width)`, `y = rng.Next(Height)`, redrawn while the tile holds a bush, mirroring `World.DrawBushFreeTileCenter`. The brain is memoryless: after a sidestep it re-evaluates from scratch and draws a new wander target if still not hungry. The hunger threshold 30 is a constant in the brain, as the plan gives it; it is not config. `Reason` is one word per rule: `sidestep`, `eat`, `hungry`, `wander`, `boxed-in`.

**Per-agent seed is `unchecked(seed * 1000 + agentNumber)`.** Explicit arithmetic because `HashCode.Combine` is randomized per process and would break determinism silently. `new Random(int)` uses the same stable seeded algorithm `World` relies on. The formula is a constant of the brain and documented there.

**`Simulation` is a public static class in the runner project.** `Run(World world, Func<World, Agent, AgentDecision> decide, TextWriter log, int maxTicks, int every, TextWriter? view)` loops: ask, submit, step, write events, maybe render; returns when `world.Agents.Count == 0` or `world.Tick >= maxTicks`. It keeps a `SortedDictionary<EntityId, (string Name, long Tick)>` of deaths filled from `AgentDied` events and a name snapshot taken at start, because `World.Agents` forgets the dead. Public rather than internal so the test project needs no second `InternalsVisibleTo`. Alternative: put the loop in `Tokenville.Brains` to avoid referencing an exe from tests; wrong home for it, and referencing an exe project from a test project works in the .NET SDK.

**One test project, referencing Brains and Runner.** The plan's scaffolding lists only `Tokenville.Core.Tests` and says `LlmBrain` is tested with a fake client, so it already intends brain tests to live there. Brain and view tests use the internal `FromLayout` for hand-placed worlds, which is only visible to this project. A separate `Tokenville.Runner.Tests` would need its own `InternalsVisibleTo` on Core for nothing. Revisit if the project splits by phase 3.

**JSON options live in the runner as one shared `JsonSerializerOptions`.** Case-insensitive reading with unmapped members rejected (as today, for config), plus `JsonStringEnumConverter`, a converter writing `EntityId` as its `ToString()` and reading it back through `TryParse`, and `DefaultIgnoreCondition = WhenWritingNull`. Property names stay PascalCase in declaration order; `System.Text.Json` serializes record positional properties in declaration order with no extra setup, and numbers are culture-invariant by design. `Position` gets a converter too, writing only `{"X":..,"Y":..}`: without one the serializer also emits its derived `TileX`/`TileY` properties, found during implementation. The converter round-trips so Phase 2's replay can read decision lines with the same options. Core gains no attributes.

**Log writing.** `log.Write(JsonSerializer.Serialize(e, options)); log.Write('\n');` per event, straight after `Step()`. `Program.cs` opens the file with `new StreamWriter(path, append: false, new UTF8Encoding(false))`, which also gives no byte order mark, and disposes it after the run. No per-tick flush; a crash loses the buffered tail, which no scenario cares about.

**Console view is plain text built with a `StringBuilder`.** `ConsoleView.Render(World world, IReadOnlyDictionary<EntityId, (string Name, long Tick)> deaths, int startingAgents)` returns the map, a blank line, the header and the per-agent lines. Map glyphs: `.`, berry digit capped at `9`, first letter of the agent's name; agents are painted in ascending id order and the first painter wins, which gives "lowest id shows". The twelve names start with A through L, so letters are unique for default populations. Action text: `MoveTo bush-3`, `MoveTo (4, 5)`, `Eat bush-3 (2)` with ticks remaining, `Wait 3`, `idle`. The runner writes the rendered string to `Console.Out`; tests render to a string.

**Flags are parsed by hand.** Four flags, a `for` loop over `args`, `int.TryParse` with invariant culture. `System.CommandLine` is a new dependency for four flags; Phase 2 and 3 add about three more, still fine by hand. The positional `args[0]` config path from the stub is dropped; nothing uses it. Exit codes: 2 for usage, 1 for config, 0 otherwise, so a CI script can tell the three apart.

**Snapshot condition is `finished || (every > 0 && Tick % every == 0)`**, evaluated once per step after logging, so the end-of-run snapshot is never printed twice. The summary line follows the final snapshot.

## Risks / Trade-offs

- [The wander brain is a weaker forager than the smoke test's greedy policy, so the 8-bush acceptance could fail] → the acceptance run is a test in this PR. Hunger alerts at 50 and 80 interrupt wandering, after which rule (2) heads for food, and 8 bushes sustain about 10 agents; the margin is wide. If it fails, the first knob is the hunger threshold, which the plan fixes at 30, so a failure would be reported rather than tuned away.
- [Blocked-then-sidestep can oscillate when the sidestep does not clear the straight line] → the same rule passed 2,000 ticks in the smoke test; the acceptance run catches a regression. No pathfinding by plan.
- [Omniscient brain until Phase 2 changes behavior when perception lands] → stated in proposal and PR; the Phase 1 acceptance runs are re-run as part of the Phase 2 port.
- [Test project references an executable project] → supported by the SDK; if the `Program` entry point or Spectre assets cause friction, fall back to `internal` plus `InternalsVisibleTo` on the runner, which is a two-line change.
- [Log format will change in Phase 2 when decision lines arrive] → no golden file is stored; the determinism scenario compares two live runs, so the change is free.
- [Consecutive per-agent seeds give correlated `Random` streams] → acceptable for a scripted PoC brain; the streams only pick wander targets. Seeds within one world are `seed * 1000 + 1 ..  + n`, distinct by construction.
