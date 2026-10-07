# Tasks

## 1. Value types (Tokenville.Core)

- [ ] 1.1 Add `EntityKind` enum and `EntityId` readonly record struct with `ToString`, `TryParse` and `IComparable<EntityId>` (kind, then number); verify with tests for rendering `agent-1`, round-tripping `bush-12`, rejecting `tree-1` / `agent-` / `agent-x` / `agent-0` without throwing, and sorting `agent-10, agent-2, agent-1` into numeric order
- [ ] 1.2 Add `Position` readonly record struct with `DistanceTo` (Manhattan, integer); verify with tests that `(1,2)->(4,6)` is 7, the reverse is 7, and `(3,3)->(3,3)` is 0
- [ ] 1.3 Add `IEntity` interface exposing `EntityId Id` and `Position Position`; verify by compiling `Agent` and `BerryBush` against it in task 3.1

## 2. Configuration

- [ ] 2.1 Add `WorldConfig` record to Core exactly as written in the PoC plan; verify with tests that a parameterless instance has every default from the plan and that `new WorldConfig(BushCount: 3)` changes only `BushCount`
- [ ] 2.2 Add JSON config loading to the runner (`args[0]` optional path, case-insensitive names, unmapped members disallowed) replacing the `Hello, World!` scaffold; verify by running the runner with no args, with a file containing `{"bushCount": 3}`, and with `{"BushCont": 3}` (must fail naming `BushCont`), and with `{"HungerAlertThresholds": [40,70,90]}`, printing the loaded config each time

## 3. Entities and world setup

- [ ] 3.1 Add `Agent` (id, name, position, hunger, current action placeholder) and `BerryBush` (id, position, berries, regrow counter) as mutable classes implementing `IEntity`, plus the static agent names list; verify the solution builds with warnings as errors
- [ ] 3.2 Add `World` with `Config`, `Tick`, two `SortedDictionary` stores exposed as ascending `Agents` and `Bushes`, config validation (grid size, negative counts, `BushCount <= Width*Height`, free tile for agents) and seeded placement (bushes then agents, `x` then `y` draws, redraw on bush tile); verify with tests: 6/8 default yields ids `agent-1..6` and `bush-1..8`; 4x4 with 16 bushes fills every tile exactly once; 4x4 with 15 bushes and 6 agents puts every agent on the one free tile; same config twice gives identical positions; seeds 1 and 2 differ; 3x3 with 10 bushes fails naming `BushCount`; 2x2 with 4 bushes and 1 agent fails naming `AgentCount`
- [ ] 3.3 Add initial-state tests: default world has every bush at 5 berries, every agent at hunger 0 with no action, tick 0, 12 agents enumerate in order `agent-1..agent-12`, and `agent-1` has the same name under seeds 1 and 2

## 4. Wrap-up

- [ ] 4.1 Remove the Phase 0 `SmokeTests.cs`; verify `dotnet build` and `dotnet test` pass from a clean `src/` with zero warnings
- [ ] 4.2 Draft the PR description stating the bundling exception, the chosen defaults (initial hunger 0, bushes-then-agents draw order, agents never start on bushes, `IEntity` added ahead of its first caller) and that no config defaults or contract types changed
