# Design

## Context

See proposal.md for motivation. `World` holds `Config`, `Tick`, two `SortedDictionary` stores of mutable `Agent` and `BerryBush`, and one seeded `Random`. `Agent.CurrentAction` is an `object?` placeholder. `Position` has `TileSize`, tile conversion, `DistanceSquaredTo` (long) and `DistanceTo` (floor of an integer square root). The binding constraints are the plan's engineering rules: ascending id iteration, integer math only, no I/O or packages in Core, world state changes only inside `Step()`, immutable records for events and decisions. `openspec/specs/` is empty because `world-structure` is complete but not archived; this change adds only new capabilities and leaves `world-setup`'s requirements untouched.

## Goals / Non-Goals

**Goals:**
- Fix the per-tick event order and the exact integer movement rule, since Phase 2's byte-identical determinism test pins both.
- Introduce the contract types the plan names (`ActionKind`, `AgentDecision`) exactly as written, so Phase 2 only adds `Observation`.
- Keep every rule in the world specification testable on a hand-placed world without a brain or runner.

**Non-Goals:**
- `Observation`, `Observe`, perception filtering and per-agent recent events (Phase 2).
- An all-time event log on `World`. Phase 2 decides between a full log with per-agent cursors and per-agent buffers, since "events perceived since the last decision" also has to fix where the agent was when each event happened.
- A `Reason`/decision log; the runner's JSONL decision log is a Phase 2 item.
- Runner changes of any kind.

## Decisions

**Data-only action records, one per action, with the logic in `World`.** `public abstract record AgentAction(ActionKind Kind, EntityId? TargetId)` with `MoveToAction(Position Target, EntityId? TargetId)`, `EatAction(EntityId Bush, int TicksRemaining)` and `WaitAction(int TicksRemaining)`, all in one file. `World` creates them when a decision validates and `Step()` pattern-matches on type; countdowns are `with` expressions that replace `Agent.CurrentAction`. Each action carries only its own state (no nullable fields that mean nothing for `Wait`), tests and the later `Observation` pattern-match on type, and the records stay immutable as the plan asks. The records are public because `Agent.CurrentAction` is public; they carry no behavior. `Agent.LastAction` is a new `ActionResult?`.
Alternatives: one flat class with `Kind`, nullable `TargetId`, `Target` and `TicksRemaining` plus an enum switch is fewer types but every action drags fields it ignores and tile-vs-entity moves are told apart by a null check; full per-action classes with their own `Progress(World)` methods spread the phase logic over three files while the tick order still lives in `World`, and the plan's "world state changes only inside `Step()`" reads more naturally with one mutation site.

**One flat `WorldEvent` record, not nine event types.** `public sealed record WorldEvent(long Tick, EventKind Kind, EntityId Subject, Position Position, EntityId? Target = null, ActionKind? Action = null, int? Value = null, string? Reason = null)`. `Value` is hunger after eating for `AgentAte`, berries after regrowing for `BushRegrew` and the threshold for `HungerThresholdCrossed`; the meaning is documented on the record. It serializes to JSONL with `System.Text.Json` in fixed declaration order with no polymorphism configuration, and Core stays free of serialization attributes. Record equality makes the Phase 2 determinism test a plain sequence comparison.
Alternative: `abstract record WorldEvent` plus nine sealed derived records pattern-matches better and avoids the overloaded `Value`, but needs `[JsonPolymorphic]`/`[JsonDerivedType]` on the base (serialization concerns inside Core) and nine files. Revisit if Phase 3's prompt rendering finds the flat shape awkward; the runner's log format is the only thing that would change.

**`Step()` returns the tick's events; `World` keeps no history.** `_events` is a `List<WorldEvent>` cleared at the start of `Step()`, appended to by every phase, and returned as a fresh `IReadOnlyList<WorldEvent>` copy (`ToArray()`), so a caller's reference survives later steps. The runner writes each returned list to JSONL. Per-agent history is Phase 2's problem, as stated in non-goals.

**`Speed` and `Reach` are `const int` on `Position`, both 256.** The plan says they are constants in Core, not config. `Position` already owns `TileSize`; three movement constants together beat a new `WorldRules` type with nothing else in it.

**`Submit` stores into `SortedDictionary<EntityId, AgentDecision> _pending`; last write wins; ids that are not living agents are dropped on submit.** Phase 1 of `Step()` iterates `_pending` (ascending by construction), starts or fails each for agents with `CurrentAction == null`, then clears it. A decision for a busy agent cannot happen through the runner's lockstep loop (busy agents are never listed as awaiting), so it is simply dropped with the rest, no event. The plan says `Submit` never throws; dropping is the only non-throwing option for an unknown id.

**`AgentsAwaitingDecision` is computed, not stored.** `_agents.Values.Where(a => a.CurrentAction is null).Select(a => a.Id).ToList()`. The world changes only inside `Step()`, so computing on read equals marking at phase 5. Returned as `IReadOnlyList<EntityId>` per the plan's signature.

**Actions progress on the tick they start.** Phase 2 iterates all agents with a `CurrentAction`, including those started in phase 1 of the same step. This makes `Wait(n)` last exactly `n` steps and `Eat` exactly `EatTicks`, with no "started but did nothing" tick. The alternative (skip newly started actions) adds a flag and makes every action one tick longer than its name says.

**MoveTo target is resolved and snapshotted at start.** `AgentAction.Target` is the entity's position (or the tile center) when the action starts; `TargetId` is kept only for the event. The plan says `MoveTo` "targets the entity's position"; tracking a moving agent would need a dead-target failure path and re-resolution each tick for a case the PoC never exercises (the scripted brain only targets bushes and tiles).

**MoveTo(entity) shortens its final step: `step = min(Speed, d - Reach + 3)`.** Each tick for an entity target: if `DistanceSquaredTo(Target) <= Reach * Reach`, complete without moving. Otherwise `d = DistanceTo(Target)` and the step length is `min(Speed, d - Reach + 3)`; the move vector is `(dx * step / d, dy * step / d)` with truncating division. For tile targets the step is `min(Speed, d)` and `d <= Speed` snaps exactly to the center, as the plan says.
Why: the plan claims that with `Reach` = one tile the agent is always within reach before it would enter the bush tile. That holds only on axis-aligned approaches. Diagonally, an agent at 300 units is out of reach and a 256-unit step lands it 44 units from the center, inside the tile, where the blocked rule fails it; a scripted brain would then retry forever beside food. Shortening the final step to land just inside `Reach` fixes it. The `+3` absorbs the rounding that would otherwise strand the agent: `DistanceTo` floors (< 1 unit) and each truncated component loses < 1 unit (< 1.42 together), so a step of exactly `d - Reach` can land at `Reach + 1`, where the next step length is 1 and truncates to a zero move forever. With `+3` the final position is in `[Reach - 3, Reach - 0.58)`, always within reach and always at least 253 units from the center, far outside the tile's 181-unit corner radius. Documented in code as a named constant with this reasoning.
Alternatives: target the nearest free 4-neighbour tile center instead (exact arithmetic, no margin, but a new "all neighbours are bushes" failure and ~10 more lines); clamp at the tile edge by binary search on the step (most physical, most code). The sweep test in the spec passes for all three, so switching later is cheap.

**Blocked check is on the end position only.** After computing the new position, `TileX`/`TileY` are compared against every bush's tile (8 bushes, linear scan, no index). If a bush is there, emit `ActionFailed("blocked by bush-k")`, clear the action and do not move. Corner crossings are allowed by construction. This runs for tile targets and for entity targets alike; for an entity target it can only trigger on a bush other than the target, because the final-step rule keeps the agent out of the target's tile.

**Hunger rises when `(Tick + 1) % HungerTicksPerPoint == 0`.** A global schedule rather than a per-agent counter: all agents share one clock, and this way the first point lands after a full `HungerTicksPerPoint` steps rather than on step one. A never-eating agent dies on tick 199 with defaults, matching the plan's "one berry per ~50 ticks" calibration.

**Threshold crossings are detected in phase 3, acted on in phase 5.** Phase 3 records, per agent, the thresholds `T` with `before < T && after >= T` (thresholds from `Config.HungerAlertThresholds ?? [50, 80]`, resolved once in the constructor). Phase 5 emits `HungerThresholdCrossed` and, if the agent has a non-`Eat` action, `ActionInterrupted` right after, then clears the action. Upward only: a drop below a threshold by eating does not alert, so an agent that eats back under 80 and climbs again alerts again. Agents that die in phase 3 are not in the list.

**An agent eating through a threshold is not marked for a decision.** The plan exempts `Eat` from interruption but is silent on whether the agent is still "needing a decision". Marking it would make the runner submit a decision to a busy agent, which phase 1 has no rule for. The event is emitted; the agent is asked when the eat ends, at most `EatTicks` ticks later.

**Death ends the action silently.** Phase 3 removes the agent from `_agents` and `_pending`, emits `AgentDied` at its last position, and emits nothing for the action it was running. The plan lists only `AgentDied`; an extra `ActionInterrupted` for a dead subject would be noise in the log and in Phase 2's recent-events.

**Regrow counter runs only while below max.** Phase 4 per bush: if `Berries < Max` then `RegrowCounter++`, and when it reaches `BushRegrowTicks` set `Berries++`, `RegrowCounter = 0`, emit `BushRegrew`. A full bush's counter is left at 0 (it was reset on the regrowth that filled it, or never ran). A berry picked from a full bush on tick `t` reappears on tick `t + 39` (40 counted steps including the pick step).

**Eat completion re-checks only berries.** Neither party moves during `Eat`, so reach cannot change. The last-berry tie resolves itself: phase 2 iterates ascending, the first agent decrements the berry, the second finds 0 and fails with `bush is empty`.

**Hand-placed worlds via an internal static factory.** `internal static World FromLayout(WorldConfig config, IEnumerable<(Position Position, int Hunger)> agents, IEnumerable<(Position Position, int Berries)> bushes)` assigns ids in list order and skips the RNG placement. `InternalsVisibleTo("Tokenville.Core.Tests")` in `Tokenville.Core.csproj`. The seeded constructor is unchanged; both route to the same private initializer. Public surface stays as the plan specifies.

**Validation reasons are short lowercase strings naming the offending thing**: `blocked by bush-3`, `bush is empty`, `unknown target 'bush-42'`, `tile (32, 5) is out of bounds`, `tile (4, 4) holds bush-1`, `bush-1 is out of reach`, `ticks must be between 1 and 50`, `MoveTo needs a target id or coordinates`, `Eat needs a bush id`. They are the strings the LLM reads in Phase 3; spelled out here so tests and prompt text agree.

**Calibration smoke test with an in-test policy.** One test drives a default world for 2,000 ticks with a 15-line policy (eat the nearest in-reach bush with berries when hunger >= 30, else move toward the nearest bush with berries, else wait) and asserts all six agents survive with 8 bushes. It is not the Phase 1 acceptance test (that is `ScriptedBrain`'s PR) but it is the cheapest check that no agent gets stuck or blocked in a real run; the MoveTo rounding bug above is exactly the kind of thing it catches.

## Risks / Trade-offs

- [The `+3` margin is an analytical argument, not a proof over all integer inputs] → the sweep test exercises every start tile within 4 tiles of a bush on a 9x9 grid, plus a 32x32 random-walk test that asserts no agent ever ends inside a bush tile over 2,000 ticks.
- [Flat `WorldEvent` with an overloaded `Value` field] → documented on the record; the runner and Phase 3 prompt builder switch on `Kind` anyway. Changing to a hierarchy later touches Core, tests and the log format but no contract type.
- [Event order within a tick is now frozen by the determinism test] → the spec's "Order within a step" scenario pins it on purpose; any reorder is a deliberate spec change.
- [`Step()` copies the event list each tick] → ~10 events per tick; negligible. Avoids the aliasing bug where a runner holds a list that the next step clears.
- [Deviation from the plan's MoveTo sentence] → stated in proposal, design and PR description; the observable rule ("completes within Reach, never ends inside a bush tile") is what the plan actually wants.
- [An agent standing beside an empty bush whose next target lies on the far side is blocked every tick, because the straight line to it ends inside the empty bush's tile] → by design (no pathfinding); the engine reports `blocked by bush-k` and the brain must step aside. Found by the calibration smoke test: without a sidestep, agent-5 starved next to bush-4 with 8 bushes. The smoke policy sidesteps one tile after a blocked move; `ScriptedBrain` must do the same or its "nearest bush with berries" rule will starve agents the same way.
- [In-test policy duplicates part of `ScriptedBrain`] → accepted; it is deliberately simpler (no RNG wander) and lives only in tests.
