# Proposal

## Why

After `world-structure`, a `World` can be created but never changes: agents cannot act, hunger never rises, nothing is observable over time. The remaining Phase 1 items (scripted brain, runner loop) and the whole of Phase 2 (observations, determinism and replay tests) need a world that advances one tick at a time and reports what happened. This change makes the engine run.

## What Changes

- Add the decision input side of the engine–brain contract from the PoC plan: `ActionKind`, `AgentDecision`, `World.Submit(agentId, decision)` and `World.AgentsAwaitingDecision`. `Submit` never throws; a bad decision fails at the next step with a readable reason.
- Add the three actions `MoveTo`, `Eat`, `Wait` with the plan's preconditions, durations and effects, each ending as `Completed`, `Failed(reason)` or `Interrupted`, recorded on the agent as its last action result.
- Add `World.Step()` running the six-phase tick pipeline in the plan's order with ascending-id iteration: start, progress, hunger and death, regrowth, alerts and interrupts, tick increment.
- Add the event model: one `WorldEvent` record covering the nine event kinds from the plan, each carrying the tick, the subject id and an exact sub-tile position. `Step()` returns the append-only list of events emitted during that tick, in emission order.
- Add an internal hand-placement factory so tests can build tiny worlds with chosen positions, berries and hunger, plus `InternalsVisibleTo` for the test project.
- Deviate from one sentence of the plan: `MoveTo(targetId)` toward a bush shortens its final step so the agent stops within `Reach` without entering the bush tile. The plan asserts this happens by itself; with Euclidean steps it does not (see design.md).

This bundles three Phase 1 checklist items into one PR, as `world-structure` did, because none has testable behavior without the other two. Stated here and in the PR description.

Not in this change: `Observation`, `World.Observe`, perception filtering, per-agent recent events, `ScriptedBrain`, the runner loop, JSONL logging, the ASCII map. The runner is untouched.

## Capabilities

### New Capabilities
- `agent-actions`: how decisions enter the world and how `MoveTo`, `Eat` and `Wait` start, progress, complete, fail and are interrupted.
- `tick-pipeline`: what one `Step()` does, in which order, including hunger growth, death, bush regrowth, hunger alerts and which agents await a decision afterwards.
- `world-events`: the event kinds the engine emits, what each carries, and how a tick's events are returned.

### Modified Capabilities
<!-- none: world-setup's requirements are unchanged; replacing the CurrentAction placeholder type is an implementation detail, and world-setup is not yet in openspec/specs (world-structure is complete but not archived) -->

## Impact

- `src/Tokenville.Core`: new public types `ActionKind`, `AgentDecision`, `ActionOutcome`, `ActionResult`, `WorldEvent`, `EventKind`; new internal `AgentAction`; `World` gains `Submit`, `AgentsAwaitingDecision`, `Step` and the hand-placement factory; `Agent.CurrentAction` changes from `object?` to the action type and gains `LastAction`. `Position` gains `Speed` and `Reach` constants. No package dependencies.
- `src/Tokenville.Core.Tests`: new test classes for each action, each pipeline phase, the last-berry tie, event ordering and the 2,000-tick survival calibration driven by a minimal in-test scripted policy.
- Contract types `ActionKind` and `AgentDecision` are introduced exactly as written in the plan; no config defaults change.
