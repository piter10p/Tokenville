# Spec Delta

## ADDED Requirements

### Requirement: Recent events since the last decision
An observation's recent events SHALL be exactly the events emitted since the observing agent's most recent decision was consumed by a step (or since the world was created, if it has had none) whose position was within `PerceptionRadius` tiles (Euclidean, inclusive) of the observing agent's exact position at the moment the event was emitted, in emission order. The agent's own events SHALL be included. An event kept for an agent SHALL be the same value the step returned.

#### Scenario: Own move is seen
- **WHEN** an agent submits a `MoveTo` to a free tile two tiles away and the world steps once
- **THEN** its recent events are `ActionStarted` then `AgentMoved`, both for that agent

#### Scenario: Far events are not seen
- **WHEN** with `PerceptionRadius` 5 agent-1 at tile (0, 0) and agent-2 at tile (8, 8) each start a `Wait`
- **THEN** agent-1's recent events hold agent-2's `ActionStarted` nowhere, and agent-1's own `ActionStarted` once

#### Scenario: Near events are seen
- **WHEN** with `PerceptionRadius` 5 agent-1 at tile (0, 0) waits while agent-2 at tile (3, 3) finishes eating from a bush at tile (3, 4)
- **THEN** agent-1's recent events include agent-2's `AgentAte` with that bush as target

#### Scenario: Filtered at emission time
- **WHEN** with `PerceptionRadius` 2 agent-1 walks from tile (0, 0) toward tile (6, 0) over several steps while agent-2 stands at tile (6, 0) and completes a `Wait` on the first step
- **THEN** agent-2's `ActionCompleted` is absent from agent-1's recent events, even once agent-1 is within two tiles of agent-2

#### Scenario: Events accumulate across a long action
- **WHEN** an agent submits `Wait` for 3 ticks and the world steps three times
- **THEN** its recent events are `ActionStarted` then `ActionCompleted`, with nothing dropped in between

#### Scenario: Cleared when the next decision is consumed
- **WHEN** an agent whose `Wait` completed last step submits another `Wait` and the world steps once
- **THEN** its recent events hold only this step's `ActionStarted`, not the previous `ActionCompleted`

#### Scenario: A failed decision is reported in the next observation
- **WHEN** an agent submits `Eat` for an unknown bush id and the world steps once
- **THEN** its recent events hold exactly one `ActionFailed` for that agent

#### Scenario: Interruption is reported
- **WHEN** an agent walking a long path crosses a hunger alert threshold during a step
- **THEN** its recent events end with `HungerThresholdCrossed` then `ActionInterrupted`

## REMOVED Requirements

### Requirement: Recent events are not yet reported
**Reason**: Per-agent event history now exists; the placeholder rule that the list is always empty is replaced by "Recent events since the last decision".
**Migration**: Brains that ignored the list keep working; brains may now read it.
