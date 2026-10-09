# Proposal

## Why

The engine runs but nothing drives it: `Tokenville.Brains` is an empty project and the runner only echoes its config. The two remaining Phase 1 items, `ScriptedBrain` and the runner loop, turn the engine into a simulation that can be watched and logged, and the Phase 1 acceptance criteria (six scripted agents alive at tick 2,000 with 8 bushes, at least one death with 3) cannot be checked until both exist.

## What Changes

- Add `ScriptedBrain` to `Tokenville.Brains` with the plan's policy: eat from the nearest bush with berries when hunger is 30 or more, otherwise move to a random in-bounds, bush-free tile drawn from a per-agent seeded RNG. It also answers a `blocked by bush-k` failure with a one-tile sidestep, the rule the `actions-and-step` smoke test showed is needed to avoid starving beside an empty bush.
- Deviate from the plan's brain input, stated here and in the PR: the brain decides from `World` and `Agent` directly, not from `Observation`. `Observation`, `World.Observe` and `IAgentBrain` arrived on master in parallel (PR #4) and are kept here, with their `agent-observation` spec carried in this change; the brain written against them was superseded by this one. Until the brain is ported to `Observation` it is omniscient: "nearest visible bush" is "nearest bush". The port is a follow-up; the policy itself does not change.
- `IAgentBrain` exists but has no implementation yet. The runner reaches the scripted brain through a plain delegate; the interface comes into use when the brain is ported.
- Rewrite the runner: lockstep loop (collect agents awaiting a decision, ask each brain, submit, step), a JSON Lines event log with one line per event, a plain-text ASCII map with a stats panel printed every N ticks and once at the end, exit when every agent is dead or the tick limit is reached, and four command-line flags (`--config`, `--max-ticks`, `--every`, `--log`).
- Leave `Spectre.Console` referenced but unused. A 2,000-tick scripted run finishes in milliseconds, so an in-place live view would be a blur; scrolling snapshots are readable and pipeable. The live view earns its place in Phase 3 when ticks take seconds.
- Keep one test project: `Tokenville.Core.Tests` gains project references to `Tokenville.Brains` and `Tokenville.Runner`, as the plan's scaffolding lists only this one test project.

This bundles two Phase 1 checklist items into one PR, as the two earlier changes did, because a runner without a brain has nothing to log and a brain without a runner cannot meet the phase's acceptance criteria. Stated here and in the PR description.

Not in this change: the brain's port to `Observation`, recent events, the decision log, replay mode, the `--dump-observation` flag, asynchronous brains, brain timeouts and the `Wait(5)` fallback, any Spectre rendering. `Tokenville.Core` is untouched.

## Capabilities

### New Capabilities
- `agent-observation`: what an agent is told about the world when asked for a decision (merged in from master's PR #4, unchanged).
- `scripted-brain`: the decisions a scripted brain makes for one agent from world state, including the sidestep after a blocked move and per-agent deterministic randomness.
- `event-log`: the JSON Lines file the runner writes, its line format and ordering, and its byte-for-byte reproducibility.
- `simulation-runner`: the lockstep loop, the stop conditions, the ASCII map and stats panel, the snapshot cadence and the command line.

### Modified Capabilities
<!-- none: no requirement of world-setup, world-config, agent-actions, tick-pipeline or world-events changes -->

## Impact

- `src/Tokenville.Brains`: new `ScriptedBrain` class. No package dependencies.
- `src/Tokenville.Runner`: `Program.cs` becomes flag parsing plus wiring; new `Simulation` (loop, event log, death tracking), `ConsoleView` (map and panel text) and JSON options with an `EntityId` converter. No new packages.
- `src/Tokenville.Core.Tests`: project references to Brains and Runner; new tests for the brain policy, the log format, the loop's stop conditions, the rendering, and the two Phase 1 acceptance runs (8 bushes all alive at 2,000; 3 bushes at least one death).
- `src/Tokenville.Core`: `Observation` records and `World.Observe` from PR #4, merged unchanged. No config defaults change.
