# Proposal

## Why

`Tokenville.Core` is an empty class library after Phase 0. Every later Phase 1 task (actions, tick pipeline, events, scripted brain, runner loop) needs a world to act on: stable entity ids, fixed-point positions over the tile grid, a config record, and a seeded initial placement of agents and bushes. This change lays that foundation so the rest of Phase 1 can be built one task at a time on top of it.

## What Changes

- Add the value types from the spec's Phase 1 list: `EntityId` (kind + number, prints as `agent-1` / `bush-1`, orders numerically), `Position` in fixed-point sub-tile units (256 per tile, with tile-center and tile-index conversion) and Euclidean distance via an integer square root, and an `IEntity` view (id + position) implemented by both entity kinds.
- Add `WorldConfig` exactly as written in the PoC plan, with the plan's defaults baked into the record.
- Add JSON config loading to the runner: an optional config file path argument, missing fields fall back to the record defaults, unknown fields are rejected.
- Add `World` construction: seeded placement of `BushCount` bushes (no two on one tile, starting full) and `AgentCount` agents (spawned at the center of a bush-free tile, hunger 0, names from a fixed list), every position a tile center, both in ascending id order, plus validation that the config is placeable.
- Add unit tests for ordering, distance, config loading and placement invariants.

This change deliberately bundles three Phase 1 checklist items into one PR, an exception to the "one phase task per PR" rule. Value types and config have no observable behavior until `World` consumes them, so splitting them would produce PRs with nothing to test. The exception is stated here and will be stated in the PR description.

Not in this change: actions, `World.Step()`, events, observation, brains, the runner loop or ASCII map. The runner only gains config loading.

## Capabilities

### New Capabilities
- `world-config`: the simulation configuration record, its defaults, and how the runner loads it from JSON.
- `world-setup`: entity identity and position semantics, and the seeded initial placement of agents and bushes when a world is created.

### Modified Capabilities
<!-- none: there are no existing specs -->

## Impact

- `src/Tokenville.Core`: new public types `EntityId`, `EntityKind`, `Position`, `IEntity`, `WorldConfig`, `Agent`, `BerryBush`, `World`. No package dependencies added.
- `src/Tokenville.Runner/Program.cs`: replaces the scaffold `Hello, World!` with config loading. No new packages (`System.Text.Json` is in the shared framework).
- `src/Tokenville.Core.Tests`: new test classes; the Phase 0 smoke test is removed once real tests exist.
- No changes to the engine–brain contract types; `Observation` and `AgentDecision` are not introduced yet.
