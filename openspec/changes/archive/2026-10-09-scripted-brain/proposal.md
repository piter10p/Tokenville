# Proposal

## Why

After `actions-and-step`, the engine runs but nothing decides: there is no way for a brain to see the world and no brain to see it. The last Phase 1 engine item, `ScriptedBrain`, is specified against the plan's `IAgentBrain` contract, which takes an `Observation` that does not exist yet, so the brain cannot be built without a first, read-only slice of the engine–brain contract. This change adds that slice and the brain, and so the first end-to-end loop: world → observation → decision → action → world.

## What Changes

- Add the observation side of the engine–brain contract from the PoC plan: `Observation`, `AgentSelf`, `VisibleBush`, `VisibleAgent` and `World.Observe(agentId)`. Brains see tile coordinates and whole-tile distances only, never sub-tile units. Bushes and agents are filtered to `PerceptionRadius`.
- Pull `Observe` forward from the Phase 2 checklist, but only its perception half: `RecentEvents` is present in the record and always empty. Per-agent event history since the last decision stays in Phase 2, which will modify the `agent-observation` requirement that pins this.
- Add `IAgentBrain` to `Tokenville.Brains` exactly as the plan writes it, and `ScriptedBrain`: eat from the nearest visible bush with berries when hunger ≥ 30, walk to it when it is not in reach, otherwise move to a random in-bounds, bush-free tile drawn from a per-agent seeded RNG. A move blocked by a bush is answered by one random sidestep, because the previous change's calibration showed a straight-line retry starves agents beside empty bushes.
- Make the test project reference `Tokenville.Brains` so brain tests live beside the engine tests. The Phase 1 acceptance run (6 scripted agents alive at tick 2,000 with 8 bushes, at least one dead with 3) is added as a test driven through `Observe` and `DecideAsync`.

The scope decision (minimal `Observe` now rather than a brain that reads `World` directly) was made in exploration: the plan freezes the contract after Phase 2 and says swapping brains must not touch Core, so a brain written against `World` would be rewritten one phase later.

Not in this change: `RecentEvents` content, the runner loop, JSONL logging, the ASCII map, `--dump-observation`, any LLM code. The runner is untouched. No config defaults change.

## Capabilities

### New Capabilities
- `agent-observation`: what an agent is told about the world when asked for a decision: itself, visible bushes and agents in tile units, its last action result, world size, and the (for now empty) recent events.
- `scripted-brain`: the deterministic policy a scripted agent follows given an observation, including its random wander and its sidestep after a blocked move.

### Modified Capabilities
<!-- none: agent-actions, tick-pipeline and world-events requirements are unchanged; Observe is read-only and adds no behavior to Step -->

## Impact

- `src/Tokenville.Core`: new public contract records `Observation`, `AgentSelf`, `VisibleBush`, `VisibleAgent`; `World` gains `Observe(EntityId)`. Read-only; no change to `Step`, events or actions. No package dependencies.
- `src/Tokenville.Brains`: first code in the project: `IAgentBrain` and `ScriptedBrain`. Depends only on Core.
- `src/Tokenville.Core.Tests`: new project reference to `Tokenville.Brains`; new tests for observation contents and filtering, each brain rule, brain determinism, and the 2,000-tick acceptance run.
- Contract types are introduced with the plan's names and fields; the one addition the plan leaves open, the type of "current action" in observations, is `ActionKind?` so no sub-tile data leaks to brains.
