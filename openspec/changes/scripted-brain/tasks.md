# Tasks

## 1. Observation contract in Core

- [ ] 1.1 Add `Observation`, `AgentSelf`, `VisibleBush`, `VisibleAgent` records to Core in one file with the plan's field names (ids as `EntityId`, tile `X`/`Y`, `ActionKind?` for current actions) and XML doc comments; verify the solution builds with warnings as errors and that a hand-built `Observation` serializes with `JsonSerializer` to properties in declaration order starting with `Tick`
- [ ] 1.2 Add `World.Observe(EntityId)`: throws `ArgumentException` naming the id for a non-living agent, fills self (tile position, hunger, action kind), `LastAction`, `WorldWidth`/`WorldHeight`, and an empty `RecentEvents`; verify with tests for the "Fresh agent", "Last action is reported", "Observing is read-only", "Dead agent", "Sub-tile position rounds to its tile" and "Always empty for now" scenarios
- [ ] 1.3 Implement perception filtering and distances: include bushes and other agents with `DistanceSquaredTo <= (PerceptionRadius * TileSize)^2`, ascending id, self excluded, distance `DistanceTo / TileSize`; verify with tests for "Inside and outside the radius", "Self is excluded", "Empty bush is visible", "Distance in whole tiles" (bush at (3, 4) from (0, 0) shows 5) and "Adjacent diagonal bush shows distance 1"

## 2. Brains project

- [ ] 2.1 Add `IAgentBrain` to `Tokenville.Brains` exactly as written in the plan (`Task<AgentDecision> DecideAsync(Observation, CancellationToken)`) with XML docs; verify the Brains project builds
- [ ] 2.2 Add `ScriptedBrain(int seed)` holding one `Random(seed)` and returning `Task.FromResult`, implementing the three rules in design order: blocked sidestep (fixed neighbour order, in-bounds, not a visible bush tile, uniform pick, `Wait(1)` when boxed in), eat/approach (hunger >= 30, bushes with berries, order by distance then id, `Eat` at distance 0 else `MoveTo(id)`), wander (x then y uniform draws, redraw while on a visible bush tile; `ponytail:` comment on the loop's ceiling); verify the project builds with no warnings

## 3. Brain tests

- [ ] 3.1 Add a project reference from `Tokenville.Core.Tests` to `Tokenville.Brains` and a small test helper that builds `Observation` records by hand (self at a tile with a hunger, visible bushes by tile and berries, optional last action, world size); verify `dotnet test` still runs the existing suites
- [ ] 3.2 Test the eat rule: "Eat in reach", "Walk to food", "Nearest wins, ties by id", "Empty bush is skipped"; verify all four pass
- [ ] 3.3 Test the wander rule: "Not hungry" (in-bounds, not on a visible bush, action is `MoveTo` with coordinates and no target id), "Hungry but blind", "Visible bushes are avoided" (2x2 world, three bushes, fourth tile chosen); verify all three pass
- [ ] 3.4 Test the sidestep rule: "Blocked while hungry" (one of the four neighbours even at hunger 80), "Blocked neighbours are excluded", "Boxed in" yields `Wait(1)`; verify all three pass
- [ ] 3.5 Test brain determinism: "Same seed, same decisions" over 100 observations and "Different seeds diverge" over 20 wander observations; verify both pass

## 4. Acceptance

- [ ] 4.1 Add the Phase 1 acceptance test driving a default world in lockstep (collect awaiting ids, `Observe` each, `DecideAsync` with one `ScriptedBrain(config.Seed + agentNumber)` per agent, `Submit`, `Step`) for 2,000 steps, asserting six agents alive with 8 bushes, fewer than six with 3, and that no agent ever ends a step inside a bush tile; verify both theory cases pass, and if the 8-bush case fails, report the tick and agent before changing anything
- [ ] 4.2 Add the brain-driven determinism test: two default worlds with identically seeded brains run 500 steps and return equal event lists every step; verify it passes

## 5. Wrap-up

- [ ] 5.1 Run `dotnet build` and `dotnet test` from `src/` with zero warnings; verify all tests pass, including the unchanged omniscient calibration test
- [ ] 5.2 Draft the PR description stating: `Observe` pulled forward from Phase 2 with `RecentEvents` empty, `ActionKind?` as the observation's current-action type, ids kept as `EntityId`, eat only at tile distance 0 and why, the random sidestep rule, the x-then-y wander draw order, the `config.Seed + agentNumber` brain seed convention, the test project now referencing Brains, and that no config defaults or existing contract types changed
