# Proposal

## Why

The engine–brain contract freezes after Phase 2 and is the only place the Phase 3 LLM brain plugs in, but today `Observation.RecentEvents` is hard-wired empty, nothing can show what a brain would be handed at a given tick, and a run cannot be reproduced without the brains that drove it. Phase 2 closes those three gaps so the contract can be frozen with evidence: a replayed decision log reproduces the event log byte for byte.

## What Changes

- Fill `Observation.RecentEvents`: every event emitted since the agent's last decision was consumed whose position lay within `PerceptionRadius` of the agent at the moment of emission, including the agent's own events. Perception filtering of bushes and agents already exists and is unchanged.
- **BREAKING** (log format): the runner's JSON Lines log gains one decision line per submitted decision, written before the events of the tick it was submitted for. The log no longer holds "nothing but events". No golden file exists; the determinism test compares live runs.
- `--replay <path>` runner flag: drive a world from `--config` plus the decisions recorded in a log, without constructing brains. A tick/agent pair with no recorded decision is a hard error, since the world is deterministic given config and decisions and a miss means the config does not match the recording.
- `--dump-observation <agentId> <tick>` runner flag: run silently to that tick and print the agent's observation as JSON to standard output. Composes with `--replay`.
- Keep the existing determinism test (two scripted runs, byte-identical logs) green with decision lines included, and add the replay test (scripted log replayed equals the original).
- Sweep XML doc comments over the remaining undocumented public members of the contract surface (`World.Config`, `Tick`, `Agents`, `Bushes`, `EntityKind`, `IEntity`); the contract records already carry them.

Stated choices for ambiguities, to be repeated in the PR description:

- `RecentEvents` keeps the plan's literal type, `IReadOnlyList<WorldEvent>`, so event positions stay in sub-tile units even though the rest of the observation is in tiles. The plan lists the contract that way; a tile-unit event type would change a frozen contract. The Phase 3 prompt builder converts.
- The buffer clears when a decision for the agent is consumed at the start of a step, valid or not, so a failed decision's `ActionFailed` is in the next observation. No cap on the buffer.
- One log file, not two. The plan says "the JSONL log", and replaying a log then produces the whole file again byte for byte, which is a stronger assertion than comparing events alone.
- The ScriptedBrain stays on `World`/`Agent` and the loop keeps its `Func<World, Agent, AgentDecision>`. Porting the brain to `Observation` changes its behaviour (it stops being omniscient) and puts the Phase 1 acceptance run at risk; that is its own change, right after this one.

Not in this change: the ScriptedBrain port to `Observation`, `IAgentBrain` coming into use, async brains, a config header line in the log, a cap or window on `RecentEvents`, CI workflow, Spectre rendering.

## Capabilities

### New Capabilities
- `run-replay`: re-running a world from its configuration and a recorded decision log, without brains, so that any run can be reproduced and compared.

### Modified Capabilities
- `agent-observation`: the "Recent events are not yet reported" requirement is replaced by the per-agent, perception-filtered, since-last-decision rule.
- `event-log`: the file holds decision lines as well as event lines, in a defined order and format; the identical-runs requirement now covers both line kinds.
- `simulation-runner`: the lockstep loop writes each submitted decision to the log; the command line gains `--replay` and `--dump-observation`, with the observation dump defined as a new requirement.

Note: `event-log` and `simulation-runner` were introduced by the `scripted-brain-and-runner` change, which is merged and complete but not yet archived, so their main specs do not exist yet. The deltas here are written against the requirement texts in that change's delta specs, which archive copies unchanged. Archiving `scripted-brain-and-runner` is the first task.

## Impact

- `src/Tokenville.Core`: `Agent` gains a per-agent event buffer; `World.Emit` appends to buffers within radius; `World.StartActions` clears the buffer of each agent whose decision is consumed; `World.Observe` returns the buffer. Doc comments on the public members listed above. No config defaults change; no contract type changes shape.
- `src/Tokenville.Runner`: `Simulation.Run` writes a decision line per submission; a decision-line record and a log reader for replay; `Program.cs` gains two flags, the replay decide delegate and the observation dump path. No new packages.
- `src/Tokenville.Core.Tests`: tests for every new scenario in the three modified specs and the new one; the replay test; the determinism test unchanged in intent.
- `openspec/`: `scripted-brain-and-runner` archived first so its `event-log` and `simulation-runner` specs land in `openspec/specs/`.
