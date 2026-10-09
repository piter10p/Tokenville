# Design

## Context

See proposal.md for motivation. `World.Observe` already filters bushes and agents by `PerceptionRadius` with `DistanceSquaredTo <= radius²` on exact positions and returns `[]` for `RecentEvents`. Every event goes through one private `World.Emit`, which knows the subject's position at that moment. `StartActions` is the only place a pending decision is consumed. `AgentDied` is emitted after the agent is removed from `_agents`. `Simulation.Run` takes a `Func<World, Agent, AgentDecision>` and `TextWriter`s, so tests drive it with strings; `JsonFormat.Options` serializes `WorldEvent` compactly with `EntityId` and `Position` converters that round-trip, nulls omitted, enums by name, unmapped members rejected. `Program.cs` parses four flags by hand with exit codes 2 (usage), 1 (config), 0. `Observation`'s lists compare by reference as record members; existing tests compare the lists themselves. The test project references Brains and Runner and uses the internal `World.FromLayout` for hand-placed worlds. The `scripted-brain-and-runner` change is merged but not archived, so `event-log` and `simulation-runner` are not yet in `openspec/specs/`; `openspec validate` reports that archive would refuse this change's MODIFIED deltas until they are.

## Goals / Non-Goals

**Goals:**
- Fill `RecentEvents` with no change to any contract type's shape, no new Core allocations per step beyond the buffer appends, and all mutation inside `Step`.
- One log file that is a complete, ordered trace and that a replay regenerates byte for byte.
- Replay and dump reachable from tests through `Simulation` without files or the console.

**Non-Goals:**
- Porting `ScriptedBrain` to `Observation` or changing the loop's delegate type (see proposal).
- A config header in the log, log versioning, or a decision-line schema beyond what replay needs.
- Bounding or windowing the per-agent buffer.
- Making `Program.cs` unit-testable; flag handling is verified by hand as in Phase 1.

## Decisions

**Per-agent buffer on `Agent`, filled in `Emit`, cleared in `StartActions`.** `Agent` gets `internal List<WorldEvent> RecentEvents`. `World.Emit` builds the event, appends it to `_events`, then loops `_agents.Values` (ascending id, living only) and appends to each agent whose current position is within the perception radius of the event's position, using the same squared-distance comparison `Observe` uses. The observer's position is whatever it is at that instant within the step; this is what "perceived at emission time" means and it is deterministic because phases and id order are fixed. `StartActions` calls `RecentEvents.Clear()` for each pending entry whose agent is alive, before validation, so both the valid `ActionStarted` and the invalid `ActionFailed` land after the clear. `Observe` returns `self.RecentEvents.ToArray()`, a snapshot, so a held observation never changes when the world steps. The dying agent is removed before `AgentDied` is emitted, so it never sees its own death and the buffer dies with it; survivors in range see it. Alternatives: a world-wide event history plus a per-agent "last decision tick", filtered at `Observe` time by the observer's position then — rejected because the history is unbounded, the position at emission time is lost, and the perception rule would silently change meaning; clearing in `Submit` — rejected because it mutates outside `Step` and a replaced decision would clear twice. Cost: one O(agents) loop per event, with six agents and tens of events per tick.

**`RecentEvents` keeps `WorldEvent` with sub-tile positions.** Stated in the proposal. The one-line consequence for Phase 3 is noted in the `Observation` doc comment.

**Decision line is a runner-side record serialized with the shared options.** `internal sealed record DecisionLine(long Tick, string Kind, EntityId Subject, AgentDecision Decision)` in the runner, `Kind` always `"Decision"`, written as `{"Tick":5,"Kind":"Decision","Subject":"agent-1","Decision":{...}}`. Declaration order gives the property order; `WhenWritingNull` drops the absent `X`, `Y`, `Ticks`, `Reason`; the `EntityId` converter writes `agent-1`. `Kind` is a string rather than an enum so the reader can branch on it without a union type: `WorldEvent.Kind` is an `EventKind` and has no `Decision` member, so an event line never reads as a decision and a decision line fails `WorldEvent` deserialization (`"Decision"` is not an `EventKind`). `Simulation.Run` writes the line right after `Submit`, inside the awaiting-agents loop, so decisions for tick T sit before tick T's events in ascending id order with no sorting. Alternative: a separate decisions file — rejected in the proposal. Alternative: a shared log-line enum in Core — rejected, Core knows nothing about logs.

**Replay reads the whole log first, into a dictionary keyed by `(tick, agentId)`.** `Simulation.ReadDecisions(TextReader) -> IReadOnlyDictionary<(long Tick, EntityId Agent), AgentDecision>` parses each line with `JsonDocument`, reads `Kind`, deserializes `DecisionLine` when it is `Decision`, otherwise deserializes `WorldEvent` purely to validate the line, and throws `FormatException` with the 1-based line number on any failure. Eager reading is what makes "malformed log fails before the world steps" true and keeps the decide delegate trivial. The replay delegate is `(w, a) => map.TryGetValue((w.Tick, a.Id), out var d) ? d : throw new InvalidOperationException($"no decision recorded for {a.Id} at tick {w.Tick}")`. `world.Tick` at decision time equals the `Tick` the line was written with, because `Run` writes before `Step` increments. Replaying writes decision lines again through the same path, which is why the whole file reproduces. A log of 2000 ticks is a few thousand lines; the dictionary is small. Alternative: stream the log lazily in lockstep — more code, and a mismatch would surface mid-run instead of up front.

**`Simulation.Run` is unchanged in signature; replay and dump are composed in `Program.cs`.** Dump runs `Simulation.Run(world, decide, log, maxTicks: tick, every: 0, view: null)`, then checks `world.Tick == tick` (else "run ended at tick N" to stderr, exit 1), then `world.Observe(id)` inside a `try` for the `ArgumentException` a dead agent throws (message to stderr, exit 1), then writes `JsonSerializer.Serialize(observation, JsonFormat.Options)` plus a newline to stdout. Compact, not indented: one line, pipeable to `jq`, and the spec scenarios quote `"Tick":0` with no space. `--max-ticks` is ignored under dump; `tick` is the limit. The log is still written because the same loop produces it and suppressing it would be more code. Alternative: a `Simulation.RunUntil` or a per-tick callback — more surface for one flag.

**`Observation` serialization needs no new converters.** `WorldEvent` already serializes through the shared options, `ActionKind?` goes through the string enum converter, and `IReadOnlyList<T>` serializes as arrays. A test asserts the dump's first property is `Tick` and that `RecentEvents` holds objects with `Kind` by name.

**Flags.** Hand parsing continues. `--replay <path>` sets a path; `--dump-observation <agentId> <tick>` consumes two values, `EntityId.TryParse` and invariant `int.TryParse`, usage error on either failing. Reading the replay log sits in the same `try` as config loading and maps `IOException` and `FormatException` to exit 1. A missing decision during the run is an `InvalidOperationException` caught around `Simulation.Run` and reported with exit 1. Usage string lists all six flags.

**Doc comments.** `World.Config`, `Tick`, `Agents`, `Bushes`, `EntityKind`, `IEntity` and the new `DecisionLine` get `<summary>` lines. `Observation.RecentEvents` gets a `<param>` that states the rule and the sub-tile positions.

**Tests live where their subject lives.** Buffer rules in `ObservationTests` on hand-placed worlds; decision-line format, round trip, ordering and the determinism test in `SimulationTests`; replay reading, reproduction, mismatch and malformed-log tests in a new `ReplayTests` driving `Simulation.Run` and `Simulation.ReadDecisions` with `StringWriter`/`StringReader`. Flag behaviour by hand.

## Risks / Trade-offs

- [Buffers grow during long waits near busy agents, up to roughly 50 ticks × nearby events] → bounded by `Wait`'s 50-tick cap and the radius; the LLM brain's prompt builder decides what to show. Revisit with a window if Phase 3 prompts bloat.
- [Decision lines roughly double the log's line count] → a 2000-tick default run stays well under a megabyte; the determinism test is unaffected.
- [`Observation` record equality is by reference for its lists] → unchanged from Phase 1; tests compare the lists. Noted so nobody adds a whole-record `Assert.Equal` with a non-empty buffer.
- [Replaying against a mismatched config fails only when the first missing lookup happens, which could be several ticks in] → the error names the agent and tick; a config header line in the log would catch it at tick 0 and is a one-line addition when it matters.
- [`openspec archive` of this change fails until `scripted-brain-and-runner` is archived] → first task.
- [`--dump-observation` still writes `events.jsonl` into the working directory] → stated in the spec; pass `--log` to redirect.
